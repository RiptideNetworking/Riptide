// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

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
        Unreliable = MessageHeader.Unreliable,
        /// <summary>Reliable send mode.</summary>
        Reliable = MessageHeader.Reliable,
    }

    /// <summary>Provides functionality for converting data to bytes and vice versa.</summary>
    public class Message
    {
        /// <summary>The maximum number of bytes required for a message's header.</summary>
        /// <remarks>1 byte for the actual header, 2 bytes for the sequence ID (only for reliable messages), 2 bytes for the message ID. Messages sent unreliably will use 2 bytes less than this value for the header.</remarks>
        public const int MaxHeaderSize = 5;
        /// <summary>The maximum number of bytes that a message can contain, including the <see cref="MaxHeaderSize"/>.</summary>
        public static int MaxSize { get; private set; } = MaxHeaderSize + 1225;
        /// <summary>The maximum number of bytes of payload data that a message can contain. This value represents how many bytes can be added to a message <i>on top of</i> the <see cref="MaxHeaderSize"/>.</summary>
        public static int MaxPayloadSize
        {
            get => MaxSize - MaxHeaderSize;
            set
            {
                if (Peer.ActiveCount > 0)
                    RiptideLogger.Log(LogType.Error, $"Changing the max message size is not allowed while a {nameof(Server)} or {nameof(Client)} is running!");
                else
                {
                    if (value < 0)
                    {
                        RiptideLogger.Log(LogType.Error, $"The max payload size cannot be negative! Setting it to 0 instead of the given value ({value}).");
                        MaxSize = MaxHeaderSize;
                    }
                    else
                        MaxSize = MaxHeaderSize + value;

                    TrimPool(); // When ActiveSocketCount is 0, this clears the pool
                }
            }
        }

        /// <summary>How many messages to add to the pool for each <see cref="Server"/> or <see cref="Client"/> instance that is started.</summary>
        /// <remarks>Changes will not affect <see cref="Server"/> and <see cref="Client"/> instances which are already running until they are restarted.</remarks>
        public static byte InstancesPerPeer { get; set; } = 4;
        /// <summary>A pool of reusable message instances.</summary>
        private static readonly List<Message> pool = new List<Message>(InstancesPerPeer * 2);

        /// <summary>The message's send mode.</summary>
        public MessageSendMode SendMode { get; private set; }
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
            if (Peer.ActiveCount == 0)
            {
                // No Servers or Clients are running, empty the list and reset the capacity
                pool.Clear();
                pool.Capacity = InstancesPerPeer * 2; // x2 so there's some buffer room for extra Message instances in the event that more are needed
            }
            else
            {
                // Reset the pool capacity and number of Message instances in the pool to what is appropriate for how many Servers & Clients are active
                int idealInstanceAmount = Peer.ActiveCount * InstancesPerPeer;
                if (pool.Count > idealInstanceAmount)
                {
                    pool.RemoveRange(Peer.ActiveCount * InstancesPerPeer, pool.Count - idealInstanceAmount);
                    pool.Capacity = idealInstanceAmount * 2;
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
        /// <returns>A message instance ready to be used for sending.</returns>
        public static Message Create(MessageSendMode sendMode, ushort id)
        {
            return RetrieveFromPool().PrepareForUse((MessageHeader)sendMode).AddUShort(id);
        }
        /// <inheritdoc cref="Create(MessageSendMode, ushort)"/>
        /// <remarks>NOTE: <paramref name="id"/> will be cast to a <see cref="ushort"/>. You should ensure that its value never exceeds that of <see cref="ushort.MaxValue"/>, otherwise you'll encounter unexpected behaviour when handling messages.</remarks>
        public static Message Create(MessageSendMode sendMode, Enum id)
        {
            return Create(sendMode, (ushort)(object)id);
        }
        /// <summary>Gets a message instance that can be used for sending.</summary>
        /// <param name="header">The message's header type.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        internal static Message Create(MessageHeader header)
        {
            return RetrieveFromPool().PrepareForUse(header);
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

        /// <summary>Returns the message instance to the internal pool so it can be reused.</summary>
        public void Release()
        {
            if (pool.Count < pool.Capacity)
            {
                // Pool exists and there's room
                if (!pool.Contains(this))
                    pool.Add(this); // Only add it if it's not already in the list, otherwise this method being called twice in a row for whatever reason could cause *serious* issues
            }
        }
        #endregion

        #region Functions
        /// <summary>Prepares the message to be used.</summary>
        /// <returns>The message, ready to be used.</returns>
        private Message PrepareForUse()
        {
            readPos = 0;
            writePos = 0;
            return this;
        }
        /// <summary>Prepares the message to be used for sending.</summary>
        /// <param name="header">The header of the message.</param>
        /// <returns>The message, ready to be used for sending.</returns>
        private Message PrepareForUse(MessageHeader header)
        {
            SetHeader(header);
            return this;
        }
        /// <summary>Prepares the message to be used for handling.</summary>
        /// <param name="header">The header of the message.</param>
        /// <param name="contentLength">The number of bytes that this message contains and which can be retrieved.</param>
        /// <returns>The message, ready to be used for handling.</returns>
        internal Message PrepareForUse(MessageHeader header, ushort contentLength)
        {
            SetHeader(header);
            writePos = contentLength;
            return this;
        }

        /// <summary>Sets the message's header byte to the given <paramref name="header"/> and determines the appropriate <see cref="MessageSendMode"/> and read/write positions.</summary>
        /// <param name="header">The header to use for this message.</param>
        internal void SetHeader(MessageHeader header)
        {
            Bytes[0] = (byte)header;
            if (header >= MessageHeader.Reliable)
            {
                readPos = 3;
                writePos = 3;
                SendMode = MessageSendMode.Reliable;
            }
            else
            {
                readPos = 1;
                writePos = 1;
                SendMode = MessageSendMode.Unreliable;
            }
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

        /// <summary>Retrieves a single <see cref="byte"/> from the message.</summary>
        /// <returns>The <see cref="byte"/> that was retrieved.</returns>
        public byte GetByte()
        {
            if (UnreadLength < 1)
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(ByteName));
                return 0;
            }
            
            return Bytes[readPos++]; // Get the byte at readPos' position
        }

        /// <summary>Retrieves a single <see cref="sbyte"/> from the message.</summary>
        /// <returns>The <see cref="sbyte"/> that was retrieved.</returns>
        public sbyte GetSByte()
        {
            if (UnreadLength < 1)
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(SByteName));
                return 0;
            }

            return (sbyte)Bytes[readPos++]; // Get the sbyte at readPos' position
        }

        /// <summary>Adds a <see cref="byte"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
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

        /// <summary>Adds an <see cref="sbyte"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
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
        /// <returns>The array that was retrieved.</returns>
        public byte[] GetBytes() => GetBytes(GetArrayLength());
        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
        /// <param name="amount">The amount of bytes to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public byte[] GetBytes(int amount)
        {
            byte[] array = new byte[amount];
            ReadBytes(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="byte"/> array with bytes retrieved from the message.</summary>
        /// <param name="amount">The amount of bytes to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetBytes(int amount, byte[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, ByteName));

            ReadBytes(amount, intoArray, startIndex);
        }

        /// <summary>Retrieves an <see cref="sbyte"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public sbyte[] GetSBytes() => GetSBytes(GetArrayLength());
        /// <summary>Retrieves an <see cref="sbyte"/> array from the message.</summary>
        /// <param name="amount">The amount of sbytes to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public sbyte[] GetSBytes(int amount)
        {
            sbyte[] array = new sbyte[amount];
            ReadSBytes(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="sbyte"/> array with bytes retrieved from the message.</summary>
        /// <param name="amount">The amount of sbytes to retrieve.</param>
        /// <param name="intArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="intArray"/>.</param>
        public void GetSBytes(int amount, sbyte[] intArray, int startIndex = 0)
        {
            if (startIndex + amount > intArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intArray.Length, startIndex, SByteName));

            ReadSBytes(amount, intArray, startIndex);
        }

        /// <summary>Reads a number of bytes from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of bytes to read.</param>
        /// <param name="intoArray">The array to write the bytes into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadBytes(int amount, byte[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount)
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, ByteName));
                amount = UnreadLength;
            }

            Array.Copy(Bytes, readPos, intoArray, startIndex, amount); // Copy the bytes at readPos' position to the array that will be returned
            readPos += (ushort)amount;
        }

        /// <summary>Reads a number of sbytes from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of sbytes to read.</param>
        /// <param name="intoArray">The array to write the sbytes into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadSBytes(int amount, sbyte[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount)
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, SByteName));
                amount = UnreadLength;
            }

            for (int i = 0; i < amount; i++)
                intoArray[startIndex + i] = (sbyte)Bytes[readPos++];
        }
        #endregion

        #region Bool
        /// <summary>Adds a <see cref="bool"/> to the message.</summary>
        /// <param name="value">The <see cref="bool"/> to add.</param>
        /// <returns>The message that the <see cref="bool"/> was added to.</returns>
        public Message AddBool(bool value)
        {
            if (UnwrittenLength < sizeof(bool))
                throw new InsufficientCapacityException(this, BoolName, sizeof(bool));

            Bytes[writePos++] = (byte)(value ? 1 : 0);
            return this;
        }

        /// <summary>Retrieves a <see cref="bool"/> from the message.</summary>
        /// <returns>The <see cref="bool"/> that was retrieved.</returns>
        public bool GetBool()
        {
            if (UnreadLength < sizeof(bool))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(BoolName, "false"));
                return false;
            }
            
            return Bytes[readPos++] == 1; // Convert the byte at readPos' position to a bool
        }

        /// <summary>Adds a <see cref="bool"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddBools(bool[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            ushort byteLength = (ushort)(array.Length / 8 + (array.Length % 8 == 0 ? 0 : 1));
            if (UnwrittenLength < byteLength)
                throw new InsufficientCapacityException(this, array.Length, BoolName, sizeof(bool), byteLength);

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
        /// <returns>The array that was retrieved.</returns>
        public bool[] GetBools() => GetBools(GetArrayLength());
        /// <summary>Retrieves a <see cref="bool"/> array from the message.</summary>
        /// <param name="amount">The amount of bools to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public bool[] GetBools(int amount)
        {
            bool[] array = new bool[amount];

            int byteAmount = amount / 8 + (amount % 8 == 0 ? 0 : 1);
            if (UnreadLength < byteAmount)
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(array.Length, BoolName));
                byteAmount = UnreadLength;
            }

            ReadBools(byteAmount, array);
            return array;
        }
        /// <summary>Populates a <see cref="bool"/> array with bools retrieved from the message.</summary>
        /// <param name="amount">The amount of bools to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetBools(int amount, bool[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, BoolName));

            int byteAmount = amount / 8 + (amount % 8 == 0 ? 0 : 1);
            if (UnreadLength < byteAmount)
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, BoolName));

            ReadBools(byteAmount, intoArray, startIndex);
        }

        /// <summary>Reads a number of bools from the message and writes them into the given array.</summary>
        /// <param name="byteAmount">The number of bytes the bools are being stored in.</param>
        /// <param name="intoArray">The array to write the bools into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadBools(int byteAmount, bool[] intoArray, int startIndex = 0)
        {
            // Read 8 bools from each byte
            bool isLengthMultipleOf8 = intoArray.Length % 8 == 0;
            for (int i = 0; i < byteAmount; i++)
            {
                int bitsToRead = 8;
                if ((i + 1) == byteAmount && !isLengthMultipleOf8)
                    bitsToRead = intoArray.Length % 8;

                for (int bit = 0; bit < bitsToRead; bit++)
                    intoArray[startIndex + (i * 8 + bit)] = (Bytes[readPos + i] >> bit & 1) == 1;
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
            if (UnwrittenLength < sizeof(short))
                throw new InsufficientCapacityException(this, ShortName, sizeof(short));

            Converter.FromShort(value, Bytes, writePos);
            writePos += sizeof(short);
            return this;
        }

        /// <summary>Adds a <see cref="ushort"/> to the message.</summary>
        /// <param name="value">The <see cref="ushort"/> to add.</param>
        /// <returns>The message that the <see cref="ushort"/> was added to.</returns>
        public Message AddUShort(ushort value)
        {
            if (UnwrittenLength < sizeof(ushort))
                throw new InsufficientCapacityException(this, UShortName, sizeof(ushort));

            Converter.FromUShort(value, Bytes, writePos);
            writePos += sizeof(ushort);
            return this;
        }

        /// <summary>Retrieves a <see cref="short"/> from the message.</summary>
        /// <returns>The <see cref="short"/> that was retrieved.</returns>
        public short GetShort()
        {
            if (UnreadLength < sizeof(short))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(ShortName));
                return 0;
            }

            short value = Converter.ToShort(Bytes, readPos);
            readPos += sizeof(short);
            return value;
        }

        /// <summary>Retrieves a <see cref="ushort"/> from the message.</summary>
        /// <returns>The <see cref="ushort"/> that was retrieved.</returns>
        public ushort GetUShort()
        {
            if (UnreadLength < sizeof(ushort))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(UShortName));
                return 0;
            }

            ushort value = Converter.ToUShort(Bytes, readPos);
            readPos += sizeof(ushort);
            return value;
        }
        
        /// <summary>Adds a <see cref="short"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddShorts(short[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(short))
                throw new InsufficientCapacityException(this, array.Length, ShortName, sizeof(short));

            for (int i = 0; i < array.Length; i++)
                AddShort(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="ushort"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddUShorts(ushort[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(ushort))
                throw new InsufficientCapacityException(this, array.Length, UShortName, sizeof(ushort));

            for (int i = 0; i < array.Length; i++)
                AddUShort(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public short[] GetShorts() => GetShorts(GetArrayLength());
        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <param name="amount">The amount of shorts to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public short[] GetShorts(int amount)
        {
            short[] array = new short[amount];
            ReadShorts(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="short"/> array with shorts retrieved from the message.</summary>
        /// <param name="amount">The amount of shorts to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetShorts(int amount, short[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, ShortName));

            ReadShorts(amount, intoArray, startIndex);
        }

        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public ushort[] GetUShorts() => GetUShorts(GetArrayLength());
        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <param name="amount">The amount of ushorts to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public ushort[] GetUShorts(int amount)
        {
            ushort[] array = new ushort[amount];
            ReadUShorts(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="ushort"/> array with ushorts retrieved from the message.</summary>
        /// <param name="amount">The amount of ushorts to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetUShorts(int amount, ushort[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, UShortName));

            ReadUShorts(amount, intoArray, startIndex);
        }

        /// <summary>Reads a number of shorts from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of shorts to read.</param>
        /// <param name="intoArray">The array to write the shorts into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadShorts(int amount, short[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(short))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, ShortName));
                amount = UnreadLength / sizeof(short);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToShort(Bytes, readPos);
                readPos += sizeof(short);
            }
        }

        /// <summary>Reads a number of ushorts from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ushorts to read.</param>
        /// <param name="intoArray">The array to write the ushorts into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadUShorts(int amount, ushort[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(ushort))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, UShortName));
                amount = UnreadLength / sizeof(short);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToUShort(Bytes, readPos);
                readPos += sizeof(ushort);
            }
        }
        #endregion

        #region Int & UInt
        /// <summary>Adds an <see cref="int"/> to the message.</summary>
        /// <param name="value">The <see cref="int"/> to add.</param>
        /// <returns>The message that the <see cref="int"/> was added to.</returns>
        public Message AddInt(int value)
        {
            if (UnwrittenLength < sizeof(int))
                throw new InsufficientCapacityException(this, IntName, sizeof(int));

            Converter.FromInt(value, Bytes, writePos);
            writePos += sizeof(int);
            return this;
        }

        /// <summary>Adds a <see cref="uint"/> to the message.</summary>
        /// <param name="value">The <see cref="uint"/> to add.</param>
        /// <returns>The message that the <see cref="uint"/> was added to.</returns>
        public Message AddUInt(uint value)
        {
            if (UnwrittenLength < sizeof(uint))
                throw new InsufficientCapacityException(this, UIntName, sizeof(uint));

            Converter.FromUInt(value, Bytes, writePos);
            writePos += sizeof(uint);
            return this;
        }

        /// <summary>Retrieves an <see cref="int"/> from the message.</summary>
        /// <returns>The <see cref="int"/> that was retrieved.</returns>
        public int GetInt()
        {
            if (UnreadLength < sizeof(int))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(IntName));
                return 0;
            }

            int value = Converter.ToInt(Bytes, readPos);
            readPos += sizeof(int);
            return value;
        }

        /// <summary>Retrieves a <see cref="uint"/> from the message.</summary>
        /// <returns>The <see cref="uint"/> that was retrieved.</returns>
        public uint GetUInt()
        {
            if (UnreadLength < sizeof(uint))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(UIntName));
                return 0;
            }

            uint value = Converter.ToUInt(Bytes, readPos);
            readPos += sizeof(uint);
            return value;
        }

        /// <summary>Adds an <see cref="int"/> array message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddInts(int[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(int))
                throw new InsufficientCapacityException(this, array.Length, IntName, sizeof(int));

            for (int i = 0; i < array.Length; i++)
                AddInt(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="uint"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddUInts(uint[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(uint))
                throw new InsufficientCapacityException(this, array.Length, UIntName, sizeof(uint));

            for (int i = 0; i < array.Length; i++)
                AddUInt(array[i]);

            return this;
        }

        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public int[] GetInts() => GetInts(GetArrayLength());
        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <param name="amount">The amount of ints to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public int[] GetInts(int amount)
        {
            int[] array = new int[amount];
            ReadInts(amount, array);
            return array;
        }
        /// <summary>Populates an <see cref="int"/> array with ints retrieved from the message.</summary>
        /// <param name="amount">The amount of ints to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetInts(int amount, int[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, IntName));

            ReadInts(amount, intoArray, startIndex);
        }

        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public uint[] GetUInts() => GetUInts(GetArrayLength());
        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <param name="amount">The amount of uints to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public uint[] GetUInts(int amount)
        {
            uint[] array = new uint[amount];
            ReadUInts(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="uint"/> array with uints retrieved from the message.</summary>
        /// <param name="amount">The amount of uints to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetUInts(int amount, uint[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, UIntName));

            ReadUInts(amount, intoArray, startIndex);
        }

        /// <summary>Reads a number of ints from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ints to read.</param>
        /// <param name="intoArray">The array to write the ints into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadInts(int amount, int[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(int))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, IntName));
                amount = UnreadLength / sizeof(int);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToInt(Bytes, readPos);
                readPos += sizeof(int);
            }
        }

        /// <summary>Reads a number of uints from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of uints to read.</param>
        /// <param name="intoArray">The array to write the uints into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadUInts(int amount, uint[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(uint))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, UIntName));
                amount = UnreadLength / sizeof(uint);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToUInt(Bytes, readPos);
                readPos += sizeof(uint);
            }
        }
        #endregion

        #region Long & ULong
        /// <summary>Adds a <see cref="long"/> to the message.</summary>
        /// <param name="value">The <see cref="long"/> to add.</param>
        /// <returns>The message that the <see cref="long"/> was added to.</returns>
        public Message AddLong(long value)
        {
            if (UnwrittenLength < sizeof(long))
                throw new InsufficientCapacityException(this, LongName, sizeof(long));

            Converter.FromLong(value, Bytes, writePos);
            writePos += sizeof(long);
            return this;
        }

        /// <summary>Adds a <see cref="ulong"/> to the message.</summary>
        /// <param name="value">The <see cref="ulong"/> to add.</param>
        /// <returns>The message that the <see cref="ulong"/> was added to.</returns>
        public Message AddULong(ulong value)
        {
            if (UnwrittenLength < sizeof(ulong))
                throw new InsufficientCapacityException(this, ULongName, sizeof(ulong));

            Converter.FromULong(value, Bytes, writePos);
            writePos += sizeof(ulong);
            return this;
        }

        /// <summary>Retrieves a <see cref="long"/> from the message.</summary>
        /// <returns>The <see cref="long"/> that was retrieved.</returns>
        public long GetLong()
        {
            if (UnreadLength < sizeof(long))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(LongName));
                return 0;
            }

            long value = Converter.ToLong(Bytes, readPos);
            readPos += sizeof(long);
            return value;
        }

        /// <summary>Retrieves a <see cref="ulong"/> from the message.</summary>
        /// <returns>The <see cref="ulong"/> that was retrieved.</returns>
        public ulong GetULong()
        {
            if (UnreadLength < sizeof(ulong))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(ULongName));
                return 0;
            }

            ulong value = Converter.ToULong(Bytes, readPos);
            readPos += sizeof(ulong);
            return value;
        }

        /// <summary>Adds a <see cref="long"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddLongs(long[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(long))
                throw new InsufficientCapacityException(this, array.Length, LongName, sizeof(long));

            for (int i = 0; i < array.Length; i++)
                AddLong(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="ulong"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddULongs(ulong[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(ulong))
                throw new InsufficientCapacityException(this, array.Length, ULongName, sizeof(ulong));

            for (int i = 0; i < array.Length; i++)
                AddULong(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public long[] GetLongs() => GetLongs(GetArrayLength());
        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <param name="amount">The amount of longs to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public long[] GetLongs(int amount)
        {
            long[] array = new long[amount];
            ReadLongs(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="long"/> array with longs retrieved from the message.</summary>
        /// <param name="amount">The amount of longs to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetLongs(int amount, long[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, LongName));

            ReadLongs(amount, intoArray, startIndex);
        }

        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public ulong[] GetULongs() => GetULongs(GetArrayLength());
        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <param name="amount">The amount of ulongs to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public ulong[] GetULongs(int amount)
        {
            ulong[] array = new ulong[amount];
            ReadULongs(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="ulong"/> array with ulongs retrieved from the message.</summary>
        /// <param name="amount">The amount of ulongs to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetULongs(int amount, ulong[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, ULongName));

            ReadULongs(amount, intoArray, startIndex);
        }

        /// <summary>Reads a number of longs from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of longs to read.</param>
        /// <param name="intoArray">The array to write the longs into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadLongs(int amount, long[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(long))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, LongName));
                amount = UnreadLength / sizeof(long);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToLong(Bytes, readPos);
                readPos += sizeof(long);
            }
        }

        /// <summary>Reads a number of ulongs from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ulongs to read.</param>
        /// <param name="intoArray">The array to write the ulongs into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadULongs(int amount, ulong[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(ulong))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, ULongName));
                amount = UnreadLength / sizeof(ulong);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToULong(Bytes, readPos);
                readPos += sizeof(ulong);
            }
        }
        #endregion

        #region Float
        /// <summary>Adds a <see cref="float"/> to the message.</summary>
        /// <param name="value">The <see cref="float"/> to add.</param>
        /// <returns>The message that the <see cref="float"/> was added to.</returns>
        public Message AddFloat(float value)
        {
            if (UnwrittenLength < sizeof(float))
                throw new InsufficientCapacityException(this, FloatName, sizeof(float));

            Converter.FromFloat(value, Bytes, writePos);
            writePos += sizeof(float);
            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> from the message.</summary>
        /// <returns>The <see cref="float"/> that was retrieved.</returns>
        public float GetFloat()
        {
            if (UnreadLength < sizeof(float))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(FloatName));
                return 0;
            }

            float value = Converter.ToFloat(Bytes, readPos);
            readPos += sizeof(float);
            return value;
        }

        /// <summary>Adds a <see cref="float"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddFloats(float[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(float))
                throw new InsufficientCapacityException(this, array.Length, FloatName, sizeof(float));

            for (int i = 0; i < array.Length; i++)
                AddFloat(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public float[] GetFloats() => GetFloats(GetArrayLength());
        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <param name="amount">The amount of floats to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public float[] GetFloats(int amount)
        {
            float[] array = new float[amount];
            ReadFloats(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="float"/> array with floats retrieved from the message.</summary>
        /// <param name="amount">The amount of floats to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetFloats(int amount, float[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, FloatName));

            ReadFloats(amount, intoArray, startIndex);
        }

        /// <summary>Reads a number of floats from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of floats to read.</param>
        /// <param name="intoArray">The array to write the floats into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadFloats(int amount, float[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(float))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, FloatName));
                amount = UnreadLength / sizeof(float);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToFloat(Bytes, readPos);
                readPos += sizeof(float);
            }
        }
        #endregion

        #region Double
        /// <summary>Adds a <see cref="double"/> to the message.</summary>
        /// <param name="value">The <see cref="double"/> to add.</param>
        /// <returns>The message that the <see cref="double"/> was added to.</returns>
        public Message AddDouble(double value)
        {
            if (UnwrittenLength < sizeof(double))
                throw new InsufficientCapacityException(this, DoubleName, sizeof(double));

            Converter.FromDouble(value, Bytes, writePos);
            writePos += sizeof(double);
            return this;
        }

        /// <summary>Retrieves a <see cref="double"/> from the message.</summary>
        /// <returns>The <see cref="double"/> that was retrieved.</returns>
        public double GetDouble()
        {
            if (UnreadLength < sizeof(double))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(DoubleName));
                return 0;
            }

            double value = Converter.ToDouble(Bytes, readPos);
            readPos += sizeof(double);
            return value;
        }

        /// <summary>Adds a <see cref="double"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddDoubles(double[] array, bool includeLength = true)
        {
            if (includeLength)
                AddArrayLength(array.Length);

            if (UnwrittenLength < array.Length * sizeof(double))
                throw new InsufficientCapacityException(this, array.Length, DoubleName, sizeof(double));

            for (int i = 0; i < array.Length; i++)
                AddDouble(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="double"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public double[] GetDoubles() => GetDoubles(GetArrayLength());
        /// <summary>Retrieves a <see cref="double"/> array from the message.</summary>
        /// <param name="amount">The amount of doubles to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public double[] GetDoubles(int amount)
        {
            double[] array = new double[amount];
            ReadDoubles(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="double"/> array with doubles retrieved from the message.</summary>
        /// <param name="amount">The amount of doubles to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetDoubles(int amount, double[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, DoubleName));

            ReadDoubles(amount, intoArray, startIndex);
        }

        /// <summary>Reads a number of doubles from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of doubles to read.</param>
        /// <param name="intoArray">The array to write the doubles into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadDoubles(int amount, double[] intoArray, int startIndex = 0)
        {
            if (UnreadLength < amount * sizeof(double))
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(intoArray.Length, DoubleName));
                amount = UnreadLength / sizeof(double);
            }

            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = Converter.ToDouble(Bytes, readPos);
                readPos += sizeof(double);
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
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(StringName, "shortened string"));
                length = (ushort)UnreadLength;
            }
            
            string value = Encoding.UTF8.GetString(Bytes, readPos, length); // Convert the bytes at readPos' position to a string
            readPos += length;
            return value;
        }

        /// <summary>Adds a <see cref="string"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
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
        /// <returns>The array that was retrieved.</returns>
        public string[] GetStrings() => GetStrings(GetArrayLength());
        /// <summary>Retrieves a <see cref="string"/> array from the message.</summary>
        /// <param name="amount">The amount of strings to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public string[] GetStrings(int amount)
        {
            string[] array = new string[amount];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetString();

            return array;
        }
        /// <summary>Populates a <see cref="string"/> array with strings retrieved from the message.</summary>
        /// <param name="amount">The amount of strings to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetStrings(int amount, string[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, StringName));

            for (int i = 0; i < amount; i++)
                intoArray[startIndex + i] = GetString();
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
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(ArrayLengthName));
                return 0;
            }

            if ((Bytes[readPos] & 0b_1000_0000) == 0)
                return GetByte();
            
            if (UnreadLength < 2)
            {
                RiptideLogger.Log(LogType.Error, NotEnoughBytesError(ArrayLengthName));
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
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetSerializables<T>(int amount, T[] intoArray, int startIndex = 0) where T : IMessageSerializable, new()
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, typeof(T).Name));

            ReadSerializables(amount, intoArray, startIndex);
        }

        /// <summary>Reads a number of serializables from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of serializables to read.</param>
        /// <param name="intArray">The array to write the serializables into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="intArray"/>.</param>
        private void ReadSerializables<T>(int amount, T[] intArray, int startIndex = 0) where T : IMessageSerializable, new()
        {
            for (int i = 0; i < amount; i++)
            {
                intArray[startIndex + i] = GetSerializable<T>();
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
        private const string SByteName       = "sbyte";
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
