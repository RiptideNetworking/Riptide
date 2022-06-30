// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide
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
        /// <summary>The number of bytes required for a message's header.</summary>
        /// <remarks>
        ///     <para>1 byte for the actual header; 2 bytes for the message ID.</para>
        ///     <b>NOTE:</b> Various transports may add additional bytes when sending messages, so this value may not reflect the true size of the header that is actually sent. For example, Riptide's default RUDP transport inserts an extra 2 bytes for the message's sequence ID when sending reliable messages, but this is not (and should not be) reflected in this value.
        /// </remarks>
        public const int HeaderSize = 3;
        /// <summary>The maximum number of bytes that a message can contain, including the <see cref="HeaderSize"/>.</summary>
        public static int MaxSize { get; private set; } = HeaderSize + 1225;
        /// <summary>The maximum number of bytes of payload data that a message can contain. This value represents how many bytes can be added to a message <i>on top of</i> the <see cref="HeaderSize"/>.</summary>
        public static int MaxPayloadSize
        {
            get => MaxSize - HeaderSize;
            set
            {
                if (Common.ActiveSocketCount > 0)
                    RiptideLogger.Log(LogType.error, $"Changing the max message size is not allowed while a {nameof(Server)} or {nameof(Client)} is running!");
                else
                {
                    if (value < 0)
                    {
                        RiptideLogger.Log(LogType.error, $"The max payload size cannot be negative! Setting it to 0 instead of the given value ({value}).");
                        MaxSize = HeaderSize;
                    }
                    else
                        MaxSize = HeaderSize + value;

                    TrimPool(); // When ActiveSocketCount is 0, this clears the pool
                }
            }
        }

        /// <summary>How many messages to add to the pool for each <see cref="Server"/> or <see cref="Client"/> instance that is started.</summary>
        /// <remarks>Changes will not affect <see cref="Server"/> and <see cref="Client"/> instances which are already running until they are restarted.</remarks>
        public static byte InstancesPerSocket { get; set; } = 4;
        /// <summary>A pool of reusable message instances.</summary>
        private static readonly List<Message> pool = new List<Message>(InstancesPerSocket * 2);

        /// <summary>The message's send mode.</summary>
        public MessageSendMode SendMode { get; private set; }
        /// <summary>How often to try sending the message before giving up.</summary>
        /// <remarks>The default RUDP transport only uses this when sending messages with their <see cref="SendMode"/> set to <see cref="MessageSendMode.reliable"/>. Other transports may ignore this property entirely.</remarks>
        public int MaxSendAttempts { get; set; }
        /// <summary>The length in bytes of the unread data contained in the message.</summary>
        public int UnreadLength => writePos - readPos;
        /// <summary>The length in bytes of the data that has been written to the message.</summary>
        public int WrittenLength => writePos;
        /// <summary>How many more bytes can be written into the packet.</summary>
        internal int UnwrittenLength => Bytes.Length - writePos;
        /// <summary>The message's data.</summary>
        internal byte[] Bytes { get; private set; }

        /// <summary>The position in the byte array that the next bytes will be written to.</summary>
        private ushort writePos = 0;
        /// <summary>The position in the byte array that the next bytes will be read from.</summary>
        private ushort readPos = 0;

        /// <summary>Initializes a reusable <see cref="Message"/> instance.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        private Message(int maxSize) => Bytes = new byte[maxSize];

        #region Pooling
        /// <summary>Trims the message pool to a more appropriate size for how many <see cref="Server"/> and/or <see cref="Client"/> instances are currently running.</summary>
        public static void TrimPool()
        {
            lock (pool)
            {
                if (Common.ActiveSocketCount == 0)
                {
                    // No Servers or Clients are running, empty the list and reset the capacity
                    pool.Clear();
                    pool.Capacity = InstancesPerSocket * 2; // x2 so there's some buffer room for extra Message instances in the event that more are needed
                }
                else
                {
                    // Reset the pool capacity and number of Message instances in the pool to what is appropriate for how many Servers & Clients are active
                    int idealInstanceAmount = Common.ActiveSocketCount * InstancesPerSocket;
                    if (pool.Count > idealInstanceAmount)
                    {
                        pool.RemoveRange(Common.ActiveSocketCount * InstancesPerSocket, pool.Count - idealInstanceAmount);
                        pool.Capacity = idealInstanceAmount * 2;
                    }
                }
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
            return RetrieveFromPool().PrepareForUse(shouldAutoRelay ? (HeaderType)sendMode + 1 : (HeaderType)sendMode, maxSendAttempts).AddUShort(id);
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
                    message = new Message(MaxSize);

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
        #region Byte & SByte
        /// <summary>Adds a single <see cref="byte"/> to the message.</summary>
        /// <param name="value">The <see cref="byte"/> to add.</param>
        /// <returns>The message that the <see cref="byte"/> was added to.</returns>
        public Message AddByte(byte value)
        {
            if (UnwrittenLength < 1)
                throw new InsufficientCapacityException(this, ByteName, 1);

            Bytes[writePos++] = value;
            return this;
        }

        /// <summary>Adds a single <see cref="sbyte"/> to the message.</summary>
        /// <param name="value">The <see cref="sbyte"/> to add.</param>
        /// <returns>The message that the <see cref="sbyte"/> was added to.</returns>
        public Message AddSByte(sbyte value)
        {
            if (UnwrittenLength < 1)
                throw new InsufficientCapacityException(this, SByteName, 1);

            Bytes[writePos++] = (byte)value;
            return this;
        }

        /// <summary>Retrieves a <see cref="byte"/> from the message.</summary>
        /// <returns>The <see cref="byte"/> that was retrieved.</returns>
        public byte GetByte()
        {
            if (UnreadLength < 1)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(ByteName));
                return 0;
            }
            
            return Bytes[readPos++]; // Get the byte at readPos' position
        }

        /// <summary>Retrieves a <see cref="sbyte"/> from the message.</summary>
        /// <returns>The <see cref="sbyte"/> that was retrieved.</returns>
        public sbyte GetSByte()
        {
            if (UnreadLength < 1)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(SByteName));
                return 0;
            }

            return (sbyte)Bytes[readPos++]; // Get the sbyte at readPos' position
        }

        /// <summary>Adds a <see cref="byte"/> array to the message.</summary>
        /// <param name="array">The <see cref="byte"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="byte"/> array was added to.</returns>
        public Message AddBytes(byte[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length)
                throw new InsufficientCapacityException(this, array.Length, ByteName, 1);

            Array.Copy(array, 0, Bytes, writePos, array.Length);
            writePos += (ushort)array.Length;
            return this;
        }

        /// <summary>Adds a <see cref="sbyte"/> array to the message.</summary>
        /// <param name="array">The <see cref="sbyte"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="sbyte"/> array was added to.</returns>
        public Message AddSBytes(sbyte[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length)
                throw new InsufficientCapacityException(this, array.Length, SByteName, 1);

            for (int i = 0; i < array.Length; i++)
                Bytes[writePos++] = (byte)array[i];
            
            return this;
        }

        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
        /// <returns>The <see cref="byte"/> array that was retrieved.</returns>
        public byte[] GetBytes() => GetBytes(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, ByteName));

            ReadBytes(amount, array, startIndex);
        }

        /// <summary>Retrieves a <see cref="sbyte"/> array from the message.</summary>
        /// <returns>The <see cref="sbyte"/> array that was retrieved.</returns>
        public sbyte[] GetSBytes() => GetSBytes(GetArrayLength());
        /// <summary>Retrieves a <see cref="sbyte"/> array from the message.</summary>
        /// <param name="amount">The amount of sbytes to retrieve.</param>
        /// <returns>The <see cref="sbyte"/> array that was retrieved.</returns>
        public sbyte[] GetSBytes(int amount)
        {
            sbyte[] array = new sbyte[amount];
            ReadSBytes(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="sbyte"/> array with bytes retrieved from the message.</summary>
        /// <param name="amount">The amount of sbytes to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public void GetSBytes(int amount, sbyte[] array, int startIndex = 0)
        {
            if (startIndex + amount > array.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, SByteName));

            ReadSBytes(amount, array, startIndex);
        }

        /// <summary>Reads a number of bytes from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of bytes to read.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadBytes(int amount, byte[] array, int startIndex = 0)
        {
            if (UnreadLength < amount)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, ByteName));
                amount = UnreadLength;
            }

            Array.Copy(Bytes, readPos, array, startIndex, amount); // Copy the bytes at readPos' position to the array that will be returned
            readPos += (ushort)amount;
        }

        /// <summary>Reads a number of sbytes from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of sbytes to read.</param>
        /// <param name="array">The array to write the sbytes into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadSBytes(int amount, sbyte[] array, int startIndex = 0)
        {
            if (UnreadLength < amount)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, SByteName));
                amount = UnreadLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = (sbyte)Bytes[readPos++];
        }
        #endregion

        #region Bool
        /// <summary>Adds a <see cref="bool"/> to the message.</summary>
        /// <param name="value">The <see cref="bool"/> to add.</param>
        /// <returns>The message that the <see cref="bool"/> was added to.</returns>
        public Message AddBool(bool value)
        {
            if (UnwrittenLength < RiptideConverter.BoolLength)
                throw new InsufficientCapacityException(this, BoolName, RiptideConverter.BoolLength);

            Bytes[writePos++] = (byte)(value ? 1 : 0);
            return this;
        }

        /// <summary>Retrieves a <see cref="bool"/> from the message.</summary>
        /// <returns>The <see cref="bool"/> that was retrieved.</returns>
        public bool GetBool()
        {
            if (UnreadLength < RiptideConverter.BoolLength)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(BoolName, "false"));
                return false;
            }
            
            return Bytes[readPos++] == 1; // Convert the byte at readPos' position to a bool
        }

        /// <summary>Adds a <see cref="bool"/> array to the message.</summary>
        /// <param name="array">The <see cref="bool"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="bool"/> array was added to.</returns>
        public Message AddBools(bool[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            ushort byteLength = (ushort)(array.Length / 8 + (array.Length % 8 == 0 ? 0 : 1));
            if (UnwrittenLength < byteLength)
                throw new InsufficientCapacityException(this, array.Length, BoolName, RiptideConverter.BoolLength, byteLength);

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
        /// <returns>The <see cref="bool"/> array that was retrieved.</returns>
        public bool[] GetBools() => GetBools(GetArrayLength());
        /// <summary>Retrieves a <see cref="bool"/> array from the message.</summary>
        /// <param name="amount">The amount of bools to retrieve.</param>
        /// <returns>The <see cref="bool"/> array that was retrieved.</returns>
        public bool[] GetBools(int amount)
        {
            bool[] array = new bool[amount];

            int byteAmount = amount / 8 + (amount % 8 == 0 ? 0 : 1);
            if (UnreadLength < byteAmount)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, BoolName));
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, BoolName));

            int byteAmount = amount / 8 + (amount % 8 == 0 ? 0 : 1);
            if (UnreadLength < byteAmount)
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, BoolName));

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
        /// <summary>Adds a <see cref="short"/> to the message.</summary>
        /// <param name="value">The <see cref="short"/> to add.</param>
        /// <returns>The message that the <see cref="short"/> was added to.</returns>
        public Message AddShort(short value)
        {
            if (UnwrittenLength < RiptideConverter.ShortLength)
                throw new InsufficientCapacityException(this, ShortName, RiptideConverter.ShortLength);

            RiptideConverter.FromShort(value, Bytes, writePos);
            writePos += RiptideConverter.ShortLength;
            return this;
        }

        /// <summary>Adds a <see cref="ushort"/> to the message.</summary>
        /// <param name="value">The <see cref="ushort"/> to add.</param>
        /// <returns>The message that the <see cref="ushort"/> was added to.</returns>
        public Message AddUShort(ushort value)
        {
            if (UnwrittenLength < RiptideConverter.UShortLength)
                throw new InsufficientCapacityException(this, UShortName, RiptideConverter.UShortLength);

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(ShortName));
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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(UShortName));
                return 0;
            }

            ushort value = RiptideConverter.ToUShort(Bytes, readPos);
            readPos += RiptideConverter.UShortLength;
            return value;
        }
        
        /// <summary>Adds a <see cref="short"/> array to the message.</summary>
        /// <param name="array">The <see cref="short"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="short"/> array was added to.</returns>
        public Message AddShorts(short[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.ShortLength)
                throw new InsufficientCapacityException(this, array.Length, ShortName, RiptideConverter.ShortLength);

            for (int i = 0; i < array.Length; i++)
                AddShort(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="ushort"/> array to the message.</summary>
        /// <param name="array">The <see cref="ushort"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="ushort"/> array was added to.</returns>
        public Message AddUShorts(ushort[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.UShortLength)
                throw new InsufficientCapacityException(this, array.Length, UShortName, RiptideConverter.UShortLength);

            for (int i = 0; i < array.Length; i++)
                AddUShort(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <returns>The <see cref="short"/> array that was retrieved.</returns>
        public short[] GetShorts() => GetShorts(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, ShortName));

            ReadShorts(amount, array, startIndex);
        }

        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <returns>The <see cref="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShorts() => GetUShorts(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, UShortName));

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, ShortName));
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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, UShortName));
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
        /// <summary>Adds an <see cref="int"/> to the message.</summary>
        /// <param name="value">The <see cref="int"/> to add.</param>
        /// <returns>The message that the <see cref="int"/> was added to.</returns>
        public Message AddInt(int value)
        {
            if (UnwrittenLength < RiptideConverter.IntLength)
                throw new InsufficientCapacityException(this, IntName, RiptideConverter.IntLength);

            RiptideConverter.FromInt(value, Bytes, writePos);
            writePos += RiptideConverter.IntLength;
            return this;
        }

        /// <summary>Adds a <see cref="uint"/> to the message.</summary>
        /// <param name="value">The <see cref="uint"/> to add.</param>
        /// <returns>The message that the <see cref="uint"/> was added to.</returns>
        public Message AddUInt(uint value)
        {
            if (UnwrittenLength < RiptideConverter.UIntLength)
                throw new InsufficientCapacityException(this, UIntName, RiptideConverter.UIntLength);

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(IntName));
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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(UIntName));
                return 0;
            }

            uint value = RiptideConverter.ToUInt(Bytes, readPos);
            readPos += RiptideConverter.UIntLength;
            return value;
        }

        /// <summary>Adds an <see cref="int"/> array message.</summary>
        /// <param name="array">The <see cref="int"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="int"/> array was added to.</returns>
        public Message AddInts(int[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.IntLength)
                throw new InsufficientCapacityException(this, array.Length, IntName, RiptideConverter.IntLength);

            for (int i = 0; i < array.Length; i++)
                AddInt(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="uint"/> array to the message.</summary>
        /// <param name="array">The <see cref="uint"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="uint"/> array was added to.</returns>
        public Message AddUInts(uint[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.UIntLength)
                throw new InsufficientCapacityException(this, array.Length, UIntName, RiptideConverter.UIntLength);

            for (int i = 0; i < array.Length; i++)
                AddUInt(array[i]);

            return this;
        }

        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <returns>The <see cref="int"/> array that was retrieved.</returns>
        public int[] GetInts() => GetInts(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, IntName));

            ReadInts(amount, array, startIndex);
        }

        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <returns>The <see cref="uint"/> array that was retrieved.</returns>
        public uint[] GetUInts() => GetUInts(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, UIntName));

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, IntName));
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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, UIntName));
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
        /// <summary>Adds a <see cref="long"/> to the message.</summary>
        /// <param name="value">The <see cref="long"/> to add.</param>
        /// <returns>The message that the <see cref="long"/> was added to.</returns>
        public Message AddLong(long value)
        {
            if (UnwrittenLength < RiptideConverter.LongLength)
                throw new InsufficientCapacityException(this, LongName, RiptideConverter.LongLength);

            RiptideConverter.FromLong(value, Bytes, writePos);
            writePos += RiptideConverter.LongLength;
            return this;
        }

        /// <summary>Adds a <see cref="ulong"/> to the message.</summary>
        /// <param name="value">The <see cref="ulong"/> to add.</param>
        /// <returns>The message that the <see cref="ulong"/> was added to.</returns>
        public Message AddULong(ulong value)
        {
            if (UnwrittenLength < RiptideConverter.ULongLength)
                throw new InsufficientCapacityException(this, ULongName, RiptideConverter.ULongLength);

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(LongName));
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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(ULongName));
                return 0;
            }

            ulong value = RiptideConverter.ToULong(Bytes, readPos);
            readPos += RiptideConverter.ULongLength;
            return value;
        }

        /// <summary>Adds a <see cref="long"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="long"/> array was added to.</returns>
        public Message AddLongs(long[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.LongLength)
                throw new InsufficientCapacityException(this, array.Length, LongName, RiptideConverter.LongLength);

            for (int i = 0; i < array.Length; i++)
                AddLong(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="ulong"/> array to the message.</summary>
        /// <param name="array">The <see cref="ulong"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="ulong"/> array was added to.</returns>
        public Message AddULongs(ulong[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.ULongLength)
                throw new InsufficientCapacityException(this, array.Length, ULongName, RiptideConverter.ULongLength);

            for (int i = 0; i < array.Length; i++)
                AddULong(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <returns>The <see cref="long"/> array that was retrieved.</returns>
        public long[] GetLongs() => GetLongs(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, LongName));

            ReadLongs(amount, array, startIndex);
        }

        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <returns>The <see cref="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongs() => GetULongs(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, ULongName));

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, LongName));
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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, ULongName));
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
        /// <summary>Adds a <see cref="float"/> to the message.</summary>
        /// <param name="value">The <see cref="float"/> to add.</param>
        /// <returns>The message that the <see cref="float"/> was added to.</returns>
        public Message AddFloat(float value)
        {
            if (UnwrittenLength < RiptideConverter.FloatLength)
                throw new InsufficientCapacityException(this, FloatName, RiptideConverter.FloatLength);

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(FloatName));
                return 0;
            }

            float value = RiptideConverter.ToFloat(Bytes, readPos);
            readPos += RiptideConverter.FloatLength;
            return value;
        }

        /// <summary>Adds a <see cref="float"/> array to the message.</summary>
        /// <param name="array">The <see cref="float"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="float"/> array was added to.</returns>
        public Message AddFloats(float[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.FloatLength)
                throw new InsufficientCapacityException(this, array.Length, FloatName, RiptideConverter.FloatLength);

            for (int i = 0; i < array.Length; i++)
                AddFloat(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <returns>The <see cref="float"/> array that was retrieved.</returns>
        public float[] GetFloats() => GetFloats(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, FloatName));

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, FloatName));
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
        /// <summary>Adds a <see cref="double"/> to the message.</summary>
        /// <param name="value">The <see cref="double"/> to add.</param>
        /// <returns>The message that the <see cref="double"/> was added to.</returns>
        public Message AddDouble(double value)
        {
            if (UnwrittenLength < RiptideConverter.DoubleLength)
                throw new InsufficientCapacityException(this, DoubleName, RiptideConverter.DoubleLength);

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(DoubleName));
                return 0;
            }

            double value = RiptideConverter.ToDouble(Bytes, readPos);
            readPos += RiptideConverter.DoubleLength;
            return value;
        }

        /// <summary>Adds a <see cref="double"/> array to the message.</summary>
        /// <param name="array">The <see cref="double"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="double"/> array was added to.</returns>
        public Message AddDoubles(double[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * RiptideConverter.DoubleLength)
                throw new InsufficientCapacityException(this, array.Length, DoubleName, RiptideConverter.DoubleLength);

            for (int i = 0; i < array.Length; i++)
                AddDouble(array[i]);

            return this;
        }

        /// <summary>Retrieves a<see cref="double"/> array from the message.</summary>
        /// <returns>The <see cref="double"/> array that was retrieved.</returns>
        public double[] GetDoubles() => GetDoubles(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, DoubleName));

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
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(array.Length, DoubleName));
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
        /// <summary>Adds a <see cref="string"/> to the message.</summary>
        /// <param name="value">The <see cref="string"/> to add.</param>
        /// <returns>The message that the <see cref="string"/> was added to.</returns>
        public Message AddString(string value)
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            int requiredBytes = stringBytes.Length + (stringBytes.Length <= OneByteLengthThreshold ? 1 : 2);
            if (UnwrittenLength < requiredBytes)
                throw new InsufficientCapacityException(this, StringName, requiredBytes);

            AddBytes(stringBytes);
            return this;
        }

        /// <summary>Retrieves a <see cref="string"/> from the message.</summary>
        /// <returns>The <see cref="string"/> that was retrieved.</returns>
        public string GetString()
        {
            ushort length = GetArrayLength(); // Get the length of the string (in bytes, NOT characters)
            if (UnreadLength < length)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(StringName, "shortened string"));
                length = (ushort)UnreadLength;
            }
            
            string value = Encoding.UTF8.GetString(Bytes, readPos, length); // Convert the bytes at readPos' position to a string
            readPos += length;
            return value;
        }

        /// <summary>Adds a <see cref="string"/> array to the message.</summary>
        /// <param name="array">The <see cref="string"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the <see cref="string"/> array was added to.</returns>
        public Message AddStrings(string[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            // It'd be ideal to throw an exception here (instead of in AddString) if the entire array isn't going to fit, but since each string could
            // be (and most likely is) a different length and some characters use more than a single byte, the only way of doing that would be to loop
            // through the whole array here and convert each string to bytes ahead of time, just to get the required byte count. Then if they all fit
            // into the message, they would all be converted again when actually being written into the byte array, which is obviously inefficient.

            for (int i = 0; i < array.Length; i++)
                AddString(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="string"/> array from the message.</summary>
        /// <returns>The <see cref="string"/> array that was retrieved.</returns>
        public string[] GetStrings() => GetStrings(GetArrayLength());
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
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, array.Length, startIndex, StringName));

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = GetString();
        }
        #endregion

        #region Array Lengths
        /// <summary>The maximum number of elements an array can contain where the length still fits into a single byte.</summary>
        private const int OneByteLengthThreshold = 0b_0111_1111;
        /// <summary>The maximum number of elements an array can contain where the length still fits into two byte2.</summary>
        private const int TwoByteLengthThreshold = 0b_0111_1111_1111_1111;

        /// <summary>Adds the length of an array to the message, using either 1 or 2 bytes depending on how large the array is. Does not support arrays with more than 32,767 elements.</summary>
        /// <param name="length">The length of the array.</param>
        private void AddArrayLength(int length)
        {
            if (UnwrittenLength < 1)
                throw new InsufficientCapacityException(this, ArrayLengthName, length <= OneByteLengthThreshold ? 1 : 2);

            if (length <= OneByteLengthThreshold)
                Bytes[writePos++] = (byte)length;
            else
            {
                if (length > TwoByteLengthThreshold)
                    throw new ArgumentOutOfRangeException(nameof(length), $"Messages do not support auto-inclusion of array lengths for arrays with more than {TwoByteLengthThreshold} elements! Either send a smaller array or set the 'includeLength' paremeter to false in the Add method and manually pass the array length to the Get method.");
                
                if (UnwrittenLength < 2)
                    throw new InsufficientCapacityException(this, ArrayLengthName, 2);

                length |= 0b_1000_0000_0000_0000;
                Bytes[writePos++] = (byte)(length >> 8); // Add the byte with the big array flag bit first, using AddUShort would add it second
                Bytes[writePos++] = (byte)length;
            }
        }

        /// <summary>Retrieves the length of an array from the message, using either 1 or 2 bytes depending on how large the array is.</summary>
        /// <returns>The length of the array.</returns>
        private ushort GetArrayLength()
        {
            if (UnreadLength < 1)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(ArrayLengthName));
                return 0;
            }

            if ((Bytes[readPos] & 0b_1000_0000) == 0)
                return GetByte();
            
            if (UnreadLength < 2)
            {
                RiptideLogger.Log(LogType.error, NotEnoughBytesError(ArrayLengthName));
                return 0;
            }
            
            return (ushort)(((Bytes[readPos++] << 8) | Bytes[readPos++]) & 0b_0111_1111_1111_1111); // Read the byte with the big array flag bit first, using GetUShort would add it second
        }
        #endregion

        #region IMessageSerializable Types
        /// <summary>Adds a serializable to the message.</summary>
        /// <param name="value">The serializable to add.</param>
        /// <returns>The message that the serializable was added to.</returns>
        public Message AddSerializable<T>(T value) where T : IMessageSerializable
        {
            value.Serialize(this);
            return this;
        }

        /// <summary>Retrieves a serializable from the message.</summary>
        /// <returns>The serializable that was retrieved.</returns>
        public T GetSerializable<T>() where T : IMessageSerializable, new()
        {
            T t = new T();
            t.Deserialize(this);
            return t;
        }

        /// <summary>Adds an array of serializables to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddSerializables<T>(T[] array, bool includeLength = true) where T : IMessageSerializable
        {
            if (includeLength)
                AddArrayLength(array.Length);

            for (int i = 0; i < array.Length; i++)
                AddSerializable(array[i]);

            return this;
        }

        /// <summary>Retrieves an array of serializables from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public T[] GetSerializables<T>() where T : IMessageSerializable, new() => GetSerializables<T>(GetArrayLength());
        /// <summary>Retrieves an array of serializables from the message.</summary>
        /// <param name="amount">The amount of serializables to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public T[] GetSerializables<T>(int amount) where T : IMessageSerializable, new()
        {
            T[] array = new T[amount];
            ReadSerializables(amount, array);
            return array;
        }
        /// <summary>Populates an array of serializables retrieved from the message.</summary>
        /// <param name="amount">The amount of serializables to retrieve.</param>
        /// <param name="array">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="array"/>.</param>
        public T[] GetSerializables<T>(int amount, T[] array, int startIndex = 0) where T : IMessageSerializable, new()
        {
            ReadSerializables(amount, array, startIndex);
            return array;
        }

        /// <summary>Reads a number of serializables from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of serializables to read.</param>
        /// <param name="array">The array to write the serializables into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadSerializables<T>(int amount, T[] array, int startIndex = 0) where T : IMessageSerializable, new()
        {
            for (int i = 0; i < amount; i++)
            {
                array[startIndex + i] = GetSerializable<T>();
            }
        }
        #endregion

        #region Overload Versions
        /// <inheritdoc cref="AddByte(byte)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddByte(byte)"/>.</remarks>
        public Message Add(byte value) => AddByte(value);
        /// <inheritdoc cref="AddSByte(sbyte)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSByte(sbyte)"/>.</remarks>
        public Message Add(sbyte value) => AddSByte(value);
        /// <inheritdoc cref="AddBool(bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddBool(bool)"/>.</remarks>
        public Message Add(bool value) => AddBool(value);
        /// <inheritdoc cref="AddShort(short)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddShort(short)"/>.</remarks>
        public Message Add(short value) => AddShort(value);
        /// <inheritdoc cref="AddUShort(ushort)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUShort(ushort)"/>.</remarks>
        public Message Add(ushort value) => AddUShort(value);
        /// <inheritdoc cref="AddInt(int)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddInt(int)"/>.</remarks>
        public Message Add(int value) => AddInt(value);
        /// <inheritdoc cref="AddUInt(uint)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUInt(uint)"/>.</remarks>
        public Message Add(uint value) => AddUInt(value);
        /// <inheritdoc cref="AddLong(long)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddLong(long)"/>.</remarks>
        public Message Add(long value) => AddLong(value);
        /// <inheritdoc cref="AddULong(ulong)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddULong(ulong)"/>.</remarks>
        public Message Add(ulong value) => AddULong(value);
        /// <inheritdoc cref="AddFloat(float)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddFloat(float)"/>.</remarks>
        public Message Add(float value) => AddFloat(value);
        /// <inheritdoc cref="AddDouble(double)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddDouble(double)"/>.</remarks>
        public Message Add(double value) => AddDouble(value);
        /// <inheritdoc cref="AddString(string)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddString(string)"/>.</remarks>
        public Message Add(string value) => AddString(value);
        /// <inheritdoc cref="AddSerializable{T}(T)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSerializable{T}(T)"/>.</remarks>
        public Message Add<T>(T value) where T : IMessageSerializable => AddSerializable(value);

        /// <inheritdoc cref="AddBytes(byte[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddBytes(byte[], bool)"/>.</remarks>
        public Message Add(byte[] array, bool includeLength = true) => AddBytes(array, includeLength);
        /// <inheritdoc cref="AddSBytes(sbyte[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSBytes(sbyte[], bool)"/>.</remarks>
        public Message Add(sbyte[] array, bool includeLength = true) => AddSBytes(array, includeLength);
        /// <inheritdoc cref="AddBools(bool[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddBools(bool[], bool)"/>.</remarks>
        public Message Add(bool[] array, bool includeLength = true) => AddBools(array, includeLength);
        /// <inheritdoc cref="AddShorts(short[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddShorts(short[], bool)"/>.</remarks>
        public Message Add(short[] array, bool includeLength = true) => AddShorts(array, includeLength);
        /// <inheritdoc cref="AddUShorts(ushort[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUShorts(ushort[], bool)"/>.</remarks>
        public Message Add(ushort[] array, bool includeLength = true) => AddUShorts(array, includeLength);
        /// <inheritdoc cref="AddInts(int[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddInts(int[], bool)"/>.</remarks>
        public Message Add(int[] array, bool includeLength = true) => AddInts(array, includeLength);
        /// <inheritdoc cref="AddUInts(uint[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUInts(uint[], bool)"/>.</remarks>
        public Message Add(uint[] array, bool includeLength = true) => AddUInts(array, includeLength);
        /// <inheritdoc cref="AddLongs(long[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddLongs(long[], bool)"/>.</remarks>
        public Message Add(long[] array, bool includeLength = true) => AddLongs(array, includeLength);
        /// <inheritdoc cref="AddULongs(ulong[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddULongs(ulong[], bool)"/>.</remarks>
        public Message Add(ulong[] array, bool includeLength = true) => AddULongs(array, includeLength);
        /// <inheritdoc cref="AddFloats(float[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddFloats(float[], bool)"/>.</remarks>
        public Message Add(float[] array, bool includeLength = true) => AddFloats(array, includeLength);
        /// <inheritdoc cref="AddDoubles(double[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddDoubles(double[], bool)"/>.</remarks>
        public Message Add(double[] array, bool includeLength = true) => AddDoubles(array, includeLength);
        /// <inheritdoc cref="AddStrings(string[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddStrings(string[], bool)"/>.</remarks>
        public Message Add(string[] array, bool includeLength = true) => AddStrings(array, includeLength);
        /// <inheritdoc cref="AddSerializables{T}(T[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSerializables{T}(T[], bool)"/>.</remarks>
        public Message Add<T>(T[] array, bool includeLength = true) where T : IMessageSerializable, new() => AddSerializables(array, includeLength);
        #endregion
        #endregion

        #region Error Messaging
        /// <summary>The name of a <see cref="byte"/> value.</summary>
        private const string ByteName        = "byte";
        /// <summary>The name of a <see cref="sbyte"/> value.</summary>
        private const string SByteName        = "sbyte";
        /// <summary>The name of a <see cref="bool"/> value.</summary>
        private const string BoolName        = "bool";
        /// <summary>The name of a <see cref="short"/> value.</summary>
        private const string ShortName       = "short";
        /// <summary>The name of a <see cref="ushort"/> value.</summary>
        private const string UShortName      = "ushort";
        /// <summary>The name of an <see cref="int"/> value.</summary>
        private const string IntName         = "int";
        /// <summary>The name of a <see cref="uint"/> value.</summary>
        private const string UIntName        = "uint";
        /// <summary>The name of a <see cref="long"/> value.</summary>
        private const string LongName        = "long";
        /// <summary>The name of a <see cref="ulong"/> value.</summary>
        private const string ULongName       = "ulong";
        /// <summary>The name of a <see cref="float"/> value.</summary>
        private const string FloatName       = "float";
        /// <summary>The name of a <see cref="double"/> value.</summary>
        private const string DoubleName      = "double";
        /// <summary>The name of a <see cref="string"/> value.</summary>
        private const string StringName      = "string";
        /// <summary>The name of an array length value.</summary>
        private const string ArrayLengthName = "array length";

        /// <summary>Constructs an error message for when a message contains insufficient unread bytes to retrieve a certain value.</summary>
        /// <param name="valueName">The name of the value type for which the retrieval attempt failed.</param>
        /// <param name="defaultReturn">Text describing the value which will be returned.</param>
        /// <returns>The error message.</returns>
        private string NotEnoughBytesError(string valueName, string defaultReturn = "0")
        {
            return $"Message only contains {UnreadLength} unread {Helper.CorrectForm(UnreadLength, "byte")}, which is not enough to retrieve a value of type '{valueName}'! Returning {defaultReturn}.";
        }
        /// <summary>Constructs an error message for when a message contains insufficient unread bytes to retrieve an array of values.</summary>
        /// <param name="arrayLength">The expected length of the array.</param>
        /// <param name="valueName">The name of the value type for which the retrieval attempt failed.</param>
        /// <returns>The error message.</returns>
        private string NotEnoughBytesError(int arrayLength, string valueName)
        {
            return $"Message only contains {UnreadLength} unread {Helper.CorrectForm(UnreadLength, "byte")}, which is not enough to retrieve {arrayLength} {Helper.CorrectForm(arrayLength, valueName)}! Returned array will contain default elements.";
        }

        /// <summary>Constructs an error message for when a number of retrieved values do not fit inside the bounds of the provided array.</summary>
        /// <param name="amount">The number of values being retrieved.</param>
        /// <param name="arrayLength">The length of the provided array.</param>
        /// <param name="startIndex">The position in the array at which to begin writing values.</param>
        /// <param name="valueName">The name of the value type which is being retrieved.</param>
        /// <param name="pluralValueName">The name of the value type in plural form. If left empty, this will be set to <paramref name="valueName"/> with an <c>s</c> appended to it.</param>
        /// <returns>The error message.</returns>
        private string ArrayNotLongEnoughError(int amount, int arrayLength, int startIndex, string valueName, string pluralValueName = "")
        {
            if (string.IsNullOrEmpty(pluralValueName))
                pluralValueName = $"{valueName}s";

            return $"The amount of {pluralValueName} to retrieve ({amount}) is greater than the number of elements from the start index ({startIndex}) to the end of the given array (length: {arrayLength})!";
        }
        #endregion
    }
}
