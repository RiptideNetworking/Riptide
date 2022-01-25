
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Transports;
using RiptideNetworking.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    /// <summary>The send mode of a <see cref="Message"/>.</summary>
    public enum MessageSendMode : byte
    {
        /// <summary>Unreliable send mode.</summary>
        unreliable = HeaderType.unreliable,
        /// <summary>Reliable send mode.</summary>
        reliable = HeaderType.reliable,
    }

    /// <summary>Provides functionality for converting data to bytes and vice versa.</summary>
    public class Message
    {
        /// <summary>The maximum amount of bytes that a message can contain. Includes a 1 byte header.</summary>
        public const int MaxMessageSize = 1250;

        /// <summary>How many messages to add to the pool for each <see cref="Server"/> or <see cref="Client"/> instance that is started.</summary>
        /// <remarks>Changes will not affect <see cref="Server"/> and <see cref="Client"/> instances which are already running until they are restarted.</remarks>
        public static byte InstancesPerSocket { get; set; } = 4;
        /// <summary>A pool of reusable message instances.</summary>
        private static readonly List<Message> pool = new List<Message>();

        /// <summary>The message's send mode.</summary>
        public MessageSendMode SendMode { get; private set; }
        /// <summary>How often to try sending the message before giving up.</summary>
        /// <remarks>The default RUDP transport only uses this when sending messages with their <see cref="SendMode"/> set to <see cref="MessageSendMode.reliable"/>. Other transports may ignore this property entirely.</remarks>
        public int MaxSendAttempts { get; set; }
        /// <summary>The message's data.</summary>
        public byte[] Bytes { get; private set; }
        /// <summary>The length in bytes of the unread data contained in the message.</summary>
        public int UnreadLength => writePos - readPos;
        /// <summary>The length in bytes of the data that has been written to the message.</summary>
        public int WrittenLength => writePos;
        /// <summary>How many more bytes can be written into the packet.</summary>
        internal int UnwrittenLength => Bytes.Length - writePos;

        /// <summary>The position in the byte array that the next bytes will be written to.</summary>
        private ushort writePos = 0;
        /// <summary>The position in the byte array that the next bytes will be read from.</summary>
        private ushort readPos = 0;

        /// <summary>Initializes a reusable <see cref="Message"/> instance.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        private Message(int maxSize = MaxMessageSize) => Bytes = new byte[maxSize];

        #region Pooling
        /// <summary>Increases the amount of messages in the pool. For use when a new <see cref="Server"/> or <see cref="Client"/> is started.</summary>
        internal static void IncreasePoolCount()
        {
            lock (pool)
            {
                pool.Capacity += InstancesPerSocket * 2; // x2 so there's room for extra Message instance in the event that more are needed

                for (int i = 0; i < InstancesPerSocket; i++)
                    pool.Add(new Message());
            }
        }

        /// <summary>Decreases the amount of messages in the pool. For use when a <see cref="Server"/> or <see cref="Client"/> is stopped.</summary>
        internal static void DecreasePoolCount()
        {
            lock (pool)
            {
                if (pool.Count < InstancesPerSocket)
                    return;

                for (int i = 0; i < InstancesPerSocket; i++)
                    pool.RemoveAt(0);
            }
        }

        /// <summary>Gets a usable message instance.</summary>
        /// <returns>A message instance ready to be used.</returns>
        public static Message Create()
        {
            return RetrieveFromPool().PrepareForUse();
        }
        /// <summary>Gets a message instance that can be used for sending.</summary>
        /// <param name="sendMode">The mode in which the message should be sent.</param>
        /// <param name="id">The message ID.</param>
        /// <param name="maxSendAttempts">How often to try sending the message before giving up.</param>
        /// <param name="shouldAutoRelay">Whether or not <see cref="Server"/> instances should automatically relay this message to all other clients. This has no effect when <see cref="Server.AllowAutoMessageRelay"/> is set to <see langword="false"/> and does not affect how clients handle messages.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        public static Message Create(MessageSendMode sendMode, ushort id, int maxSendAttempts = 15, bool shouldAutoRelay = false)
        {
            return RetrieveFromPool().PrepareForUse(shouldAutoRelay ? (HeaderType)sendMode + 1 : (HeaderType)sendMode, maxSendAttempts).Add(id);
        }
        /// <inheritdoc cref="Create(MessageSendMode, ushort, int, bool)"/>
        /// <remarks>NOTE: <paramref name="id"/> will be cast to a <see cref="ushort"/>. You should ensure that its value never exceeds that of <see cref="ushort.MaxValue"/>, otherwise you'll encounter unexpected behaviour when handling messages.</remarks>
        public static Message Create(MessageSendMode sendMode, Enum id, int maxSendAttempts = 15, bool shouldAutoRelay = false)
        {
            return Create(sendMode, (ushort)(object)id, maxSendAttempts, shouldAutoRelay);
        }
        /// <summary>Gets a message instance that can be used for sending.</summary>
        /// <param name="messageHeader">The message's header type.</param>
        /// <param name="maxSendAttempts">How often to try sending the message before giving up.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        internal static Message Create(HeaderType messageHeader, int maxSendAttempts = 15)
        {
            return RetrieveFromPool().PrepareForUse(messageHeader, maxSendAttempts);
        }

        /// <summary>Gets a message instance directly from the pool without doing any extra setup.</summary>
        /// <remarks>As this message instance is returned straight from the pool, it will contain all previous data and settings. Using this instance without preparing it properly will likely result in unexpected behaviour.</remarks>
        /// <returns>A message instance.</returns>
        internal static Message CreateRaw()
        {
            return RetrieveFromPool();
        }

        /// <summary>Retrieves a message instance from the pool. If none is available, a new instance is created.</summary>
        /// <returns>A message instance ready to be used for sending or handling.</returns>
        private static Message RetrieveFromPool()
        {
            lock (pool)
            {
                Message message;
                if (pool.Count > 0)
                {
                    message = pool[0];
                    pool.RemoveAt(0);
                }
                else
                    message = new Message();

                return message;
            }
        }

        /// <summary>Returns the message instance to the internal pool so it can be reused.</summary>
        public void Release()
        {
            lock (pool)
            {
                if (pool.Count < pool.Capacity)
                {
                    // Pool exists and there's room
                    if (!pool.Contains(this))
                        pool.Add(this); // Only add it if it's not already in the list, otherwise this method being called twice in a row for whatever reason could cause *serious* issues
                }
            }
        }
        #endregion

        #region Functions
        /// <summary>Prepares the message to be used.</summary>
        /// <returns>The message, ready to be used.</returns>
        private Message PrepareForUse()
        {
            SetReadWritePos(0, 0);
            return this;
        }
        /// <summary>Prepares the message to be used for sending.</summary>
        /// <param name="messageHeader">The header of the message.</param>
        /// <param name="maxSendAttempts">How often to try sending the message before giving up.</param>
        /// <returns>The message, ready to be used for sending.</returns>
        private Message PrepareForUse(HeaderType messageHeader, int maxSendAttempts)
        {
            MaxSendAttempts = maxSendAttempts;
            SetReadWritePos(0, 1);
            SetHeader(messageHeader);
            return this;
        }
        /// <summary>Prepares the message to be used for handling.</summary>
        /// <param name="messageHeader">The header of the message.</param>
        /// <param name="contentLength">The number of bytes that this message contains and which can be retrieved.</param>
        /// <returns>The message, ready to be used for handling.</returns>
        internal Message PrepareForUse(HeaderType messageHeader, ushort contentLength)
        {
            SetReadWritePos(1, contentLength);
            SetHeader(messageHeader);
            return this;
        }

        /// <summary>Sets the message's read and write position.</summary>
        /// <param name="newReadPos">The new read position.</param>
        /// <param name="newWritePos">The new write position.</param>
        private void SetReadWritePos(ushort newReadPos, ushort newWritePos)
        {
            readPos = newReadPos;
            writePos = newWritePos;
        }

        /// <summary>Sets the message's header byte to the given <paramref name="messageHeader"/> and determines the appropriate <see cref="MessageSendMode"/>.</summary>
        /// <param name="messageHeader">The header to use for this message.</param>
        internal void SetHeader(HeaderType messageHeader)
        {
            Bytes[0] = (byte)messageHeader;
            SendMode = messageHeader >= HeaderType.reliable ? MessageSendMode.reliable : MessageSendMode.unreliable;
        }
        #endregion

        #region Add & Retrieve Data
        #region Byte
        /// <inheritdoc cref="Add(byte)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(byte)"/> and simply provides an alternative type-explicit way to add a <see cref="byte"/> to the message.</remarks>
        public Message AddByte(byte value) => Add(value);

        /// <summary>Adds a single <see cref="byte"/> to the message.</summary>
        /// <param name="value">The <see cref="byte"/> to add.</param>
        /// <returns>The message that the <see cref="byte"/> was added to.</returns>
        public Message Add(byte value)
        {
            if (UnwrittenLength < 1)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'byte'!");

            Bytes[writePos++] = value;
            return this;
        }

        /// <summary>Retrieves a <see cref="byte"/> from the message.</summary>
        /// <returns>The <see cref="byte"/> that was retrieved.</returns>
        public byte GetByte()
        {
            if (UnreadLength < 1)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte', returning 0!");
                return 0;
            }
            
            return Bytes[readPos++]; // Get the byte at readPos' position
        }

        /// <inheritdoc cref="Add(byte[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(byte[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="byte"/> array to the message.</remarks>
        public Message AddBytes(byte[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="byte"/> array to the message.</summary>
        /// <param name="array">The <see cref="byte"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="byte"/> array was added to.</returns>
        public Message Add(byte[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetBytes to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'byte[]'!");

            Array.Copy(array, 0, Bytes, writePos, array.Length);
            writePos += (ushort)array.Length;
            return this;
        }

        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="byte"/> array that was retrieved.</returns>
        public byte[] GetBytes(bool isBigArray = false)
        {
            if (isBigArray)
                return GetBytes(GetUShort());
            else
                return GetBytes(GetByte());
        }
        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
        /// <param name="amount">The amount of bytes to retrieve.</param>
        /// <returns>The <see cref="byte"/> array that was retrieved.</returns>
        public byte[] GetBytes(int amount)
        {
            byte[] array = new byte[amount];
            ReadBytes(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="byte"/> array with bytes retrieved from the message.</summary>
        /// <param name="amount">The amount of bytes to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetBytes(int amount, byte[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} bytes, starting at index {startIndex}!");

            ReadBytes(amount, array, startIndex);
        }

        /// <summary>Reads a number of bytes from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of bytes to read.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadBytes(int amount, byte[] array, int startIndex = 0)
        {
            if (UnreadLength < amount)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte[]', array will contain default elements!");
                amount = UnreadLength;
            }

            Array.Copy(Bytes, readPos, array, startIndex, amount); // Copy the bytes at readPos' position to the array that will be returned
            readPos += (ushort)amount;
        }
        #endregion

        #region Bool
        /// <inheritdoc cref="Add(bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(bool)"/> and simply provides an alternative type-explicit way to add a <see cref="bool"/> to the message.</remarks>
        public Message AddBool(bool value) => Add(value);

        /// <summary>Adds a <see cref="bool"/> to the message.</summary>
        /// <param name="value">The <see cref="bool"/> to add.</param>
        /// <returns>The message that the <see cref="bool"/> was added to.</returns>
        public Message Add(bool value)
        {
            if (UnwrittenLength < RiptideConverter.BoolLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool'!");

            Bytes[writePos++] = (byte)(value ? 1 : 0);
            return this;
        }

        /// <summary>Retrieves a <see cref="bool"/> from the message.</summary>
        /// <returns>The <see cref="bool"/> that was retrieved.</returns>
        public bool GetBool()
        {
            if (UnreadLength < RiptideConverter.BoolLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool', returning false!");
                return false;
            }
            
            return Bytes[readPos++] == 1; // Convert the byte at readPos' position to a bool
        }

        /// <inheritdoc cref="Add(bool[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(bool[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="bool"/> array to the message.</remarks>
        public Message AddBools(bool[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="bool"/> array to the message.</summary>
        /// <param name="array">The <see cref="bool"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="bool"/> array was added to.</returns>
        public Message Add(bool[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetBools to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            ushort byteLength = (ushort)(array.Length / 8 + (array.Length % 8 == 0 ? 0 : 1));
            if (UnwrittenLength < byteLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool[]'!");

            // Pack 8 bools into each byte
            bool isLengthMultipleOf8 = array.Length % 8 == 0;
            for (int i = 0; i < byteLength; i++)
            {
                byte nextByte = 0;
                int bitsToWrite = 8;
                if ((i + 1) == byteLength && !isLengthMultipleOf8)
                    bitsToWrite = array.Length % 8;

                for (int bit = 0; bit < bitsToWrite; bit++)
                    nextByte |= (byte)((array[i * 8 + bit] ? 1 : 0) << bit);

                Bytes[writePos + i] = nextByte;
            }

            writePos += byteLength;
            return this;
        }

        /// <summary>Retrieves a <see cref="bool"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="bool"/> array that was retrieved.</returns>
        public bool[] GetBools(bool isBigArray = false)
        {
            if (isBigArray)
                return GetBools(GetUShort());
            else
                return GetBools(GetByte());
        }
        /// <summary>Retrieves a <see cref="bool"/> array from the message.</summary>
        /// <param name="amount">The amount of bools to retrieve.</param>
        /// <returns>The <see cref="bool"/> array that was retrieved.</returns>
        public bool[] GetBools(int amount)
        {
            bool[] array = new bool[amount];

            int byteAmount = amount / 8 + (amount % 8 == 0 ? 0 : 1);
            if (UnreadLength < byteAmount)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool[]', array will contain default elements!");
                byteAmount = UnreadLength;
            }

            ReadBools(byteAmount, array);
            return array;
        }
        /// <summary>Populates a <see cref="bool"/> array with bools retrieved from the message.</summary>
        /// <param name="amount">The amount of bools to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetBools(int amount, bool[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} bools, starting at index {startIndex}!");

            int byteAmount = amount / 8 + (amount % 8 == 0 ? 0 : 1);
            if (UnreadLength < byteAmount)
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool[]', array will contain default elements!");

            ReadBools(byteAmount, array, startIndex);
        }

        /// <summary>Reads a number of bools from the message and writes them into the given array.</summary>
        /// <param name="byteAmount">The number of bytes the bools are being stored in.</param>
        /// <param name="array">The array to write the bools into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadBools(int byteAmount, bool[] array, int startIndex = 0)
        {
            // Read 8 bools from each byte
            bool isLengthMultipleOf8 = array.Length % 8 == 0;
            for (int i = 0; i < byteAmount; i++)
            {
                int bitsToRead = 8;
                if ((i + 1) == byteAmount && !isLengthMultipleOf8)
                    bitsToRead = array.Length % 8;

                for (int bit = 0; bit < bitsToRead; bit++)
                    array[startIndex + (i * 8 + bit)] = (Bytes[readPos + i] >> bit & 1) == 1;
            }

            readPos += (ushort)byteAmount;
        }
        #endregion

        #region Short & UShort
        /// <inheritdoc cref="Add(short)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(short)"/> and simply provides an alternative type-explicit way to add a <see cref="short"/> to the message.</remarks>
        public Message AddShort(short value) => Add(value);

        /// <summary>Adds a <see cref="short"/> to the message.</summary>
        /// <param name="value">The <see cref="short"/> to add.</param>
        /// <returns>The message that the <see cref="short"/> was added to.</returns>
        public Message Add(short value)
        {
            if (UnwrittenLength < RiptideConverter.ShortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short'!");

            RiptideConverter.FromShort(value, Bytes, writePos);
            writePos += RiptideConverter.ShortLength;
            return this;
        }

        /// <inheritdoc cref="Add(ushort)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(ushort)"/> and simply provides an alternative type-explicit way to add a <see cref="ushort"/> to the message.</remarks>
        public Message AddUShort(ushort value) => Add(value);

        /// <summary>Adds a <see cref="ushort"/> to the message.</summary>
        /// <param name="value">The <see cref="ushort"/> to add.</param>
        /// <returns>The message that the <see cref="ushort"/> was added to.</returns>
        public Message Add(ushort value)
        {
            if (UnwrittenLength < RiptideConverter.UShortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ushort'!");

            RiptideConverter.FromUShort(value, Bytes, writePos);
            writePos += RiptideConverter.UShortLength;
            return this;
        }

        /// <summary>Retrieves a <see cref="short"/> from the message.</summary>
        /// <returns>The <see cref="short"/> that was retrieved.</returns>
        public short GetShort()
        {
            if (UnreadLength < RiptideConverter.ShortLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'short', returning 0!");
                return 0;
            }

            short value = RiptideConverter.ToShort(Bytes, readPos);
            readPos += RiptideConverter.ShortLength;
            return value;
        }

        /// <summary>Retrieves a <see cref="ushort"/> from the message.</summary>
        /// <returns>The <see cref="ushort"/> that was retrieved.</returns>
        public ushort GetUShort()
        {
            if (UnreadLength < RiptideConverter.UShortLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort', returning 0!");
                return 0;
            }

            ushort value = RiptideConverter.ToUShort(Bytes, readPos);
            readPos += RiptideConverter.UShortLength;
            return value;
        }
        
        /// <summary>Retrieves a <see cref="ushort"/> from the message without moving the read position, allowing the same bytes to be read again.</summary>
        internal ushort PeekUShort()
        {
            if (UnreadLength < RiptideConverter.UShortLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to peek type 'ushort', returning 0!");
                return 0;
            }

            return RiptideConverter.ToUShort(Bytes, readPos);
        }

        /// <inheritdoc cref="Add(short[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(short[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="short"/> array to the message.</remarks>
        public Message AddShorts(short[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="short"/> array to the message.</summary>
        /// <param name="array">The <see cref="short"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="short"/> array was added to.</returns>
        public Message Add(short[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetShorts to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.ShortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <inheritdoc cref="Add(ushort[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(ushort[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="ushort"/> array to the message.</remarks>
        public Message AddUShorts(ushort[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="ushort"/> array to the message.</summary>
        /// <param name="array">The <see cref="ushort"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="ushort"/> array was added to.</returns>
        public Message Add(ushort[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetUShorts to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.UShortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ushort[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="short"/> array that was retrieved.</returns>
        public short[] GetShorts(bool isBigArray = false)
        {
            if (isBigArray)
                return GetShorts(GetUShort());
            else
                return GetShorts(GetByte());
        }
        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <param name="amount">The amount of shorts to retrieve.</param>
        /// <returns>The <see cref="short"/> array that was retrieved.</returns>
        public short[] GetShorts(int amount)
        {
            short[] array = new short[amount];
            ReadShorts(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="short"/> array with shorts retrieved from the message.</summary>
        /// <param name="amount">The amount of shorts to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetShorts(int amount, short[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} shorts, starting at index {startIndex}!");

            ReadShorts(amount, array, startIndex);
        }

        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShorts(bool isBigArray = false)
        {
            if (isBigArray)
                return GetUShorts(GetUShort());
            else
                return GetUShorts(GetByte());
        }
        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <param name="amount">The amount of ushorts to retrieve.</param>
        /// <returns>The <see cref="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShorts(int amount)
        {
            ushort[] array = new ushort[amount];
            ReadUShorts(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="ushort"/> array with ushorts retrieved from the message.</summary>
        /// <param name="amount">The amount of ushorts to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetUShorts(int amount, ushort[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} ushorts, starting at index {startIndex}!");

            ReadUShorts(amount, array, startIndex);
        }

        /// <summary>Reads a number of shorts from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of shorts to read.</param>
        /// <param name="array">The array to write the shorts into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadShorts(int amount, short[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.ShortLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'short[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.ShortLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToShort(Bytes, readPos);
                readPos += RiptideConverter.ShortLength;
            }
        }

        /// <summary>Reads a number of ushorts from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ushorts to read.</param>
        /// <param name="array">The array to write the ushorts into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadUShorts(int amount, ushort[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.UShortLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.ShortLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToUShort(Bytes, readPos);
                readPos += RiptideConverter.UShortLength;
            }
        }
        #endregion

        #region Int & UInt
        /// <inheritdoc cref="Add(int)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(int)"/> and simply provides an alternative type-explicit way to add a <see cref="int"/> to the message.</remarks>
        public Message AddInt(int value) => Add(value);

        /// <summary>Adds an <see cref="int"/> to the message.</summary>
        /// <param name="value">The <see cref="int"/> to add.</param>
        /// <returns>The message that the <see cref="int"/> was added to.</returns>
        public Message Add(int value)
        {
            if (UnwrittenLength < RiptideConverter.IntLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int'!");

            RiptideConverter.FromInt(value, Bytes, writePos);
            writePos += RiptideConverter.IntLength;
            return this;
        }

        /// <inheritdoc cref="Add(uint)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(uint)"/> and simply provides an alternative type-explicit way to add a <see cref="uint"/> to the message.</remarks>
        public Message AddUInt(uint value) => Add(value);

        /// <summary>Adds a <see cref="uint"/> to the message.</summary>
        /// <param name="value">The <see cref="uint"/> to add.</param>
        /// <returns>The message that the <see cref="uint"/> was added to.</returns>
        public Message Add(uint value)
        {
            if (UnwrittenLength < RiptideConverter.UIntLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'uint'!");

            RiptideConverter.FromUInt(value, Bytes, writePos);
            writePos += RiptideConverter.UIntLength;
            return this;
        }

        /// <summary>Retrieves an <see cref="int"/> from the message.</summary>
        /// <returns>The <see cref="int"/> that was retrieved.</returns>
        public int GetInt()
        {
            if (UnreadLength < RiptideConverter.IntLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'int', returning 0!");
                return 0;
            }

            int value = RiptideConverter.ToInt(Bytes, readPos);
            readPos += RiptideConverter.IntLength;
            return value;
        }

        /// <summary>Retrieves a <see cref="uint"/> from the message.</summary>
        /// <returns>The <see cref="uint"/> that was retrieved.</returns>
        public uint GetUInt()
        {
            if (UnreadLength < RiptideConverter.UIntLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint', returning 0!");
                return 0;
            }

            uint value = RiptideConverter.ToUInt(Bytes, readPos);
            readPos += RiptideConverter.UIntLength;
            return value;
        }

        /// <inheritdoc cref="Add(int[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(int[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="int"/> array to the message.</remarks>
        public Message AddInts(int[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds an <see cref="int"/> array message.</summary>
        /// <param name="array">The <see cref="int"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="int"/> array was added to.</returns>
        public Message Add(int[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetInts to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.IntLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <inheritdoc cref="Add(uint[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(uint[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="uint"/> array to the message.</remarks>
        public Message AddUInts(uint[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="uint"/> array to the message.</summary>
        /// <param name="array">The <see cref="uint"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="uint"/> array was added to.</returns>
        public Message Add(uint[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetUInts to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.UIntLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'uint[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="int"/> array that was retrieved.</returns>
        public int[] GetInts(bool isBigArray = false)
        {
            if (isBigArray)
                return GetInts(GetUShort());
            else
                return GetInts(GetByte());
        }
        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <param name="amount">The amount of ints to retrieve.</param>
        /// <returns>The <see cref="int"/> array that was retrieved.</returns>
        public int[] GetInts(int amount)
        {
            int[] array = new int[amount];
            ReadInts(amount, array);
            return array;
        }
        /// <summary>Populates an <see cref="int"/> array with ints retrieved from the message.</summary>
        /// <param name="amount">The amount of ints to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetInts(int amount, int[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} ints, starting at index {startIndex}!");

            ReadInts(amount, array, startIndex);
        }

        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="uint"/> array that was retrieved.</returns>
        public uint[] GetUInts(bool isBigArray = false)
        {
            if (isBigArray)
                return GetUInts(GetUShort());
            else
                return GetUInts(GetByte());
        }
        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <param name="amount">The amount of uints to retrieve.</param>
        /// <returns>The <see cref="uint"/> array that was retrieved.</returns>
        public uint[] GetUInts(int amount)
        {
            uint[] array = new uint[amount];
            ReadUInts(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="uint"/> array with uints retrieved from the message.</summary>
        /// <param name="amount">The amount of uints to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetUInts(int amount, uint[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} uints, starting at index {startIndex}!");

            ReadUInts(amount, array, startIndex);
        }

        /// <summary>Reads a number of ints from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ints to read.</param>
        /// <param name="array">The array to write the ints into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadInts(int amount, int[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.IntLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'int[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.IntLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToInt(Bytes, readPos);
                readPos += RiptideConverter.IntLength;
            }
        }

        /// <summary>Reads a number of uints from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of uints to read.</param>
        /// <param name="array">The array to write the uints into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadUInts(int amount, uint[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.UIntLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.UIntLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToUInt(Bytes, readPos);
                readPos += RiptideConverter.UIntLength;
            }
        }
        #endregion

        #region Long & ULong
        /// <inheritdoc cref="Add(long)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(long)"/> and simply provides an alternative type-explicit way to add a <see cref="long"/> to the message.</remarks>
        public Message AddLong(long value) => Add(value);

        /// <summary>Adds a <see cref="long"/> to the message.</summary>
        /// <param name="value">The <see cref="long"/> to add.</param>
        /// <returns>The message that the <see cref="long"/> was added to.</returns>
        public Message Add(long value)
        {
            if (UnwrittenLength < RiptideConverter.LongLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long'!");

            RiptideConverter.FromLong(value, Bytes, writePos);
            writePos += RiptideConverter.LongLength;
            return this;
        }

        /// <inheritdoc cref="Add(ulong)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(ulong)"/> and simply provides an alternative type-explicit way to add a <see cref="ulong"/> to the message.</remarks>
        public Message AddULong(ulong value) => Add(value);

        /// <summary>Adds a <see cref="ulong"/> to the message.</summary>
        /// <param name="value">The <see cref="ulong"/> to add.</param>
        /// <returns>The message that the <see cref="ulong"/> was added to.</returns>
        public Message Add(ulong value)
        {
            if (UnwrittenLength < RiptideConverter.ULongLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ulong'!");

            RiptideConverter.FromULong(value, Bytes, writePos);
            writePos += RiptideConverter.ULongLength;
            return this;
        }

        /// <summary>Retrieves a <see cref="long"/> from the message.</summary>
        /// <returns>The <see cref="long"/> that was retrieved.</returns>
        public long GetLong()
        {
            if (UnreadLength < RiptideConverter.LongLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'long', returning 0!");
                return 0;
            }

            long value = RiptideConverter.ToLong(Bytes, readPos);
            readPos += RiptideConverter.LongLength;
            return value;
        }

        /// <summary>Retrieves a <see cref="ulong"/> from the message.</summary>
        /// <returns>The <see cref="ulong"/> that was retrieved.</returns>
        public ulong GetULong()
        {
            if (UnreadLength < RiptideConverter.ULongLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong', returning 0!");
                return 0;
            }

            ulong value = RiptideConverter.ToULong(Bytes, readPos);
            readPos += RiptideConverter.ULongLength;
            return value;
        }

        /// <inheritdoc cref="Add(long[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(long[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="long"/> array to the message.</remarks>
        public Message AddLongs(long[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="long"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="long"/> array was added to.</returns>
        public Message Add(long[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetLongs to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.LongLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <inheritdoc cref="Add(ulong[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(ulong[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="ulong"/> array to the message.</remarks>
        public Message AddULongs(ulong[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="ulong"/> array to the message.</summary>
        /// <param name="array">The <see cref="ulong"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="ulong"/> array was added to.</returns>
        public Message Add(ulong[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetULongs to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.ULongLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ulong[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="long"/> array that was retrieved.</returns>
        public long[] GetLongs(bool isBigArray = false)
        {
            if (isBigArray)
                return GetLongs(GetUShort());
            else
                return GetLongs(GetByte());
        }
        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <param name="amount">The amount of longs to retrieve.</param>
        /// <returns>The <see cref="long"/> array that was retrieved.</returns>
        public long[] GetLongs(int amount)
        {
            long[] array = new long[amount];
            ReadLongs(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="long"/> array with longs retrieved from the message.</summary>
        /// <param name="amount">The amount of longs to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetLongs(int amount, long[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} longs, starting at index {startIndex}!");

            ReadLongs(amount, array, startIndex);
        }

        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongs(bool isBigArray = false)
        {
            if (isBigArray)
                return GetULongs(GetUShort());
            else
                return GetULongs(GetByte());
        }
        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <param name="amount">The amount of ulongs to retrieve.</param>
        /// <returns>The <see cref="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongs(int amount)
        {
            ulong[] array = new ulong[amount];
            ReadULongs(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="ulong"/> array with ulongs retrieved from the message.</summary>
        /// <param name="amount">The amount of ulongs to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetULongs(int amount, ulong[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} ulongs, starting at index {startIndex}!");

            ReadULongs(amount, array, startIndex);
        }

        /// <summary>Reads a number of longs from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of longs to read.</param>
        /// <param name="array">The array to write the longs into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadLongs(int amount, long[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.LongLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'long[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.LongLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToLong(Bytes, readPos);
                readPos += RiptideConverter.LongLength;
            }
        }

        /// <summary>Reads a number of ulongs from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ulongs to read.</param>
        /// <param name="array">The array to write the ulongs into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadULongs(int amount, ulong[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.ULongLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.ULongLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToULong(Bytes, readPos);
                readPos += RiptideConverter.ULongLength;
            }
        }
        #endregion

        #region Float
        /// <inheritdoc cref="Add(float)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(float)"/> and simply provides an alternative type-explicit way to add a <see cref="float"/> to the message.</remarks>
        public Message AddFloat(float value) => Add(value);

        /// <summary>Adds a <see cref="float"/> to the message.</summary>
        /// <param name="value">The <see cref="float"/> to add.</param>
        /// <returns>The message that the <see cref="float"/> was added to.</returns>
        public Message Add(float value)
        {
            if (UnwrittenLength < RiptideConverter.FloatLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'float'!");

            RiptideConverter.FromFloat(value, Bytes, writePos);
            writePos += RiptideConverter.FloatLength;
            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> from the message.</summary>
        /// <returns>The <see cref="float"/> that was retrieved.</returns>
        public float GetFloat()
        {
            if (UnreadLength < RiptideConverter.FloatLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'float', returning 0!");
                return 0;
            }

            float value = RiptideConverter.ToFloat(Bytes, readPos);
            readPos += RiptideConverter.FloatLength;
            return value;
        }

        /// <inheritdoc cref="Add(float[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(float[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="float"/> array to the message.</remarks>
        public Message AddFloats(float[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="float"/> array to the message.</summary>
        /// <param name="array">The <see cref="float"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="float"/> array was added to.</returns>
        public Message Add(float[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetFloats to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.FloatLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'float[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="float"/> array that was retrieved.</returns>
        public float[] GetFloats(bool isBigArray = false)
        {
            if (isBigArray)
                return GetFloats(GetUShort());
            else
                return GetFloats(GetByte());
        }
        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <param name="amount">The amount of floats to retrieve.</param>
        /// <returns>The <see cref="float"/> array that was retrieved.</returns>
        public float[] GetFloats(int amount)
        {
            float[] array = new float[amount];
            ReadFloats(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="float"/> array with floats retrieved from the message.</summary>
        /// <param name="amount">The amount of floats to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetFloats(int amount, float[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} floats, starting at index {startIndex}!");

            ReadFloats(amount, array, startIndex);
        }

        /// <summary>Reads a number of floats from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of floats to read.</param>
        /// <param name="array">The array to write the floats into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadFloats(int amount, float[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.FloatLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'float[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.FloatLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToFloat(Bytes, readPos);
                readPos += RiptideConverter.FloatLength;
            }
        }
        #endregion

        #region Double
        /// <inheritdoc cref="Add(double)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(double)"/> and simply provides an alternative type-explicit way to add a <see cref="double"/> to the message.</remarks>
        public Message AddDouble(double value) => Add(value);

        /// <summary>Adds a <see cref="double"/> to the message.</summary>
        /// <param name="value">The <see cref="double"/> to add.</param>
        /// <returns>The message that the <see cref="double"/> was added to.</returns>
        public Message Add(double value)
        {
            if (UnwrittenLength < RiptideConverter.DoubleLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'double'!");

            RiptideConverter.FromDouble(value, Bytes, writePos);
            writePos += RiptideConverter.DoubleLength;
            return this;
        }

        /// <summary>Retrieves a <see cref="double"/> from the message.</summary>
        /// <returns>The <see cref="double"/> that was retrieved.</returns>
        public double GetDouble()
        {
            if (UnreadLength < RiptideConverter.DoubleLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'double', returning 0!");
                return 0;
            }

            double value = RiptideConverter.ToDouble(Bytes, readPos);
            readPos += RiptideConverter.DoubleLength;
            return value;
        }

        /// <inheritdoc cref="Add(double[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(double[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="double"/> array to the message.</remarks>
        public Message AddDoubles(double[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="double"/> array to the message.</summary>
        /// <param name="array">The <see cref="double"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="double"/> array was added to.</returns>
        public Message Add(double[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetDoubles to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            if (UnwrittenLength < array.Length * RiptideConverter.DoubleLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'double[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a<see cref="double"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="double"/> array that was retrieved.</returns>
        public double[] GetDoubles(bool isBigArray = false)
        {
            if (isBigArray)
                return GetDoubles(GetUShort());
            else
                return GetDoubles(GetByte());
        }
        /// <summary>Retrieves a<see cref="double"/> array from the message.</summary>
        /// <param name="amount">The amount of doubles to retrieve.</param>
        /// <returns>The <see cref="double"/> array that was retrieved.</returns>
        public double[] GetDoubles(int amount)
        {
            double[] array = new double[amount];
            ReadDoubles(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="double"/> array with doubles retrieved from the message.</summary>
        /// <param name="amount">The amount of doubles to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetDoubles(int amount, double[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} doubles, starting at index {startIndex}!");

            ReadDoubles(amount, array, startIndex);
        }

        /// <summary>Reads a number of doubles from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of doubles to read.</param>
        /// <param name="array">The array to write the doubles into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadDoubles(int amount, double[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * RiptideConverter.DoubleLength)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'double[]', array will contain default elements!");
                amount = UnreadLength / RiptideConverter.DoubleLength;
            }

            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = RiptideConverter.ToDouble(Bytes, readPos);
                readPos += RiptideConverter.DoubleLength;
            }
        }
        #endregion

        #region String
        /// <inheritdoc cref="Add(string)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(string)"/> and simply provides an alternative type-explicit way to add a <see cref="string"/> to the message.</remarks>
        public Message AddString(string value) => Add(value);

        /// <summary>Adds a <see cref="string"/> to the message.</summary>
        /// <param name="value">The <see cref="string"/> to add.</param>
        /// <returns>The message that the <see cref="string"/> was added to.</returns>
        public Message Add(string value)
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            Add((ushort)stringBytes.Length); // Add the length of the string (in bytes) to the message

            if (UnwrittenLength < stringBytes.Length)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'string'!");

            Add(stringBytes, false); // Add the string itself
            return this;
        }

        /// <summary>Retrieves a <see cref="string"/> from the message.</summary>
        /// <returns>The <see cref="string"/> that was retrieved.</returns>
        public string GetString()
        {
            ushort length = GetUShort(); // Get the length of the string (in bytes, NOT characters)
            if (UnreadLength < length)
            {
                RiptideLogger.Log(LogType.error, $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'string', result will be truncated!");
                length = (ushort)UnreadLength;
            }
            
            string value = Encoding.UTF8.GetString(Bytes, readPos, length); // Convert the bytes at readPos' position to a string
            readPos += length;
            return value;
        }

        /// <inheritdoc cref="Add(string[], bool, bool)"/>
        /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(string[], bool, bool)"/> and simply provides an alternative type-explicit way to add a <see cref="string"/> array to the message.</remarks>
        public Message AddStrings(string[] value, bool includeLength = true, bool isBigArray = false) => Add(value, includeLength, isBigArray);

        /// <summary>Adds a <see cref="string"/> array to the message.</summary>
        /// <param name="array">The <see cref="string"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The message that the <see cref="string"/> array was added to.</returns>
        public Message Add(string[] array, bool includeLength = true, bool isBigArray = false)
        {
            if (includeLength)
            {
                if (isBigArray)
                    Add((ushort)array.Length);
                else
                {
                    if (array.Length > byte.MaxValue)
                        throw new Exception($"Array is too long for the length to be stored in a single byte! Set isBigArray to true when calling Add & GetStrings to store the length in 2 bytes instead.");
                    Add((byte)array.Length);
                }
            }

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="string"/> array from the message.</summary>
        /// <param name="isBigArray">
        ///   Whether or not the array being retrieved has more than 255 elements.
        ///   <para>
        ///     Reads the length from 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Reads the length from 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The <see cref="string"/> array that was retrieved.</returns>
        public string[] GetStrings(bool isBigArray = false)
        {
            if (isBigArray)
                return GetStrings(GetUShort());
            else
                return GetStrings(GetByte());
        }
        /// <summary>Retrieves a <see cref="string"/> array from the message.</summary>
        /// <param name="amount">The amount of strings to retrieve.</param>
        /// <returns>The <see cref="string"/> array that was retrieved.</returns>
        public string[] GetStrings(int amount)
        {
            string[] array = new string[amount];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetString();

            return array;
        }
        /// <summary>Populates a <see cref="string"/> array with strings retrieved from the message.</summary>
        /// <param name="amount">The amount of string to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetStrings(int amount, string[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentOutOfRangeException($"Destination array isn't long enough to fit {amount} strings, starting at index {startIndex}!");

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = GetString();
        }
        #endregion
        #endregion
    }
}
