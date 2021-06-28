using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    /// <summary>The header type of a <see cref="Message"/>.</summary>
    internal enum HeaderType : byte
    {
        /// <summary>For unreliable user messages.</summary>
        unreliable,
        /// <summary>For unreliable internal ack messages.</summary>
        ack,
        /// <summary>For unreliable internal ack messages (when acknowledging a sequence ID other than the last received one).</summary>
        ackExtra,
        /// <summary>For unreliable internal connect messages.</summary>
        connect,
        /// <summary>For unreliable internal heartbeat messages.</summary>
        heartbeat,
        /// <summary>For unreliable internal disconnect messages.</summary>
        disconnect,
        /// <summary>For reliable user messages.</summary>
        reliable,
        /// <summary>For reliable internal welcome messages.</summary>
        welcome,
        /// <summary>For reliable internal client connected messages.</summary>
        clientConnected,
        /// <summary>For reliable internal client disconnected messages.</summary>
        clientDisconnected,
    }

    /// <summary>Represents a packet.</summary>
    public class Message
    {
        /// <summary>The message instance used for sending user messages.</summary>
        private static readonly Message send = new Message();
        /// <summary>The message instance used for sending internal messages.</summary>
        private static readonly Message sendInternal = new Message(25);
        /// <summary>The message instance used for handling user messages.</summary>
        private static readonly Message handle = new Message();
        /// <summary>The message instance used for handling internal messages.</summary>
        private static readonly Message handleInternal = new Message(25);

        /// <summary>How many bytes a <see cref="bool"/> is represented by.</summary>
        public const byte boolLength = sizeof(bool);
        /// <summary>How many bytes a <see cref="short"/> (or <see cref="ushort"/>) is represented by.</summary>
        public const byte shortLength = sizeof(short);
        /// <summary>How many bytes an <see cref="int"/> (or <see cref="uint"/>) is represented by.</summary>
        public const byte intLength = sizeof(int);
        /// <summary>How many bytes a <see cref="long"/> (or <see cref="ulong"/>) is represented by.</summary>
        public const byte longLength = sizeof(long);
        /// <summary>How many bytes a <see cref="float"/> is represented by.</summary>
        public const byte floatLength = sizeof(float);
        /// <summary>How many bytes a <see cref="double"/> is represented by.</summary>
        public const byte doubleLength = sizeof(double);

        /// <summary>The length in bytes of the data that can be read from the message.</summary>
        public int ReadableLength { get; private set; }
        /// <summary>The length in bytes of the unread data contained in the message.</summary>
        public int UnreadLength => ReadableLength - readPos;
        /// <summary>The length in bytes of the data that has been written to the message.</summary>
        public int WrittenLength => writePos;
        /// <summary>How many more bytes can be written into the packet.</summary>
        internal int UnwrittenLength => Bytes.Length - writePos;
        /// <summary>The message's send mode.</summary>
        internal MessageSendMode SendMode { get; private set; }
        /// <summary>The message's data.</summary>
        internal byte[] Bytes { get; private set; }

        /// <summary>The position in the byte array that the next bytes will be written to.</summary>
        private ushort writePos = 0;
        /// <summary>The position in the byte array that the next bytes will be read from.</summary>
        private ushort readPos = 0;

        /// <summary>Initializes a reusable Message instance.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        internal Message(ushort maxSize = 1500)
        {
            Bytes = new byte[maxSize];
        }

        /// <summary>Initializes a reusable Message instance with a pre-defined header type.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        /// <param name="headerType">The header type to initialize the message with.</param>
        internal Message(HeaderType headerType, ushort maxSize = 1500)
        {
            Bytes = new byte[maxSize];

            Add((byte)headerType);
            if (SendMode == MessageSendMode.reliable)
                writePos += shortLength;
        }

        /// <summary>Reinitializes the Message instance used for sending.</summary>
        /// <param name="sendMode">The mode in which the message should be sent.</param>
        /// <param name="id">The message ID.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        public static Message Create(MessageSendMode sendMode, ushort id)
        {
            Reinitialize(send, (HeaderType)sendMode);
            send.Add(id);
            return send;
        }

        /// <summary>Reinitializes the Message instance used for handling.</summary>
        /// <param name="headerType">The message's header type.</param>
        /// <param name="data">The bytes contained in the message.</param>
        /// <returns>A message instance ready to be used for handling.</returns>
        internal static Message Create(HeaderType headerType, byte[] data)
        {
            Reinitialize(handle, headerType, data);
            return handle;
        }

        /// <summary>Reinitializes the Message instance used for sending internal messages.</summary>
        /// <param name="headerType">The message's header type.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        internal static Message CreateInternal(HeaderType headerType)
        {
            Reinitialize(sendInternal, headerType);
            return sendInternal;
        }

        /// <summary>Reinitializes the Message instance used for handling internal messages.</summary>
        /// <param name="headerType">The message's header type.</param>
        /// <param name="data">The bytes contained in the message.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        internal static Message CreateInternal(HeaderType headerType, byte[] data)
        {
            Reinitialize(handleInternal, headerType, data);
            return handleInternal;
        }

        /// <summary>Reinitializes a message for sending.</summary>
        /// <param name="message">The message to initialize.</param>
        /// <param name="headerType">The message's header type.</param>
        private static void Reinitialize(Message message, HeaderType headerType)
        {
            message.SendMode = headerType >= HeaderType.reliable ? MessageSendMode.reliable : MessageSendMode.unreliable;
            message.writePos = 0;
            message.readPos = 0;
            message.Add((byte)headerType);
            if (message.SendMode == MessageSendMode.reliable)
                message.writePos += shortLength;
        }

        /// <summary>Reinitializes a message for handling.</summary>
        /// <param name="message">The message to initialize.</param>
        /// <param name="headerType">The message's header type.</param>
        /// <param name="data">The bytes contained in the message.</param>
        private static void Reinitialize(Message message, HeaderType headerType, byte[] data)
        {
            message.SendMode = headerType >= HeaderType.reliable ? MessageSendMode.reliable : MessageSendMode.unreliable;
            message.writePos = (ushort)data.Length;
            message.readPos = (ushort)(message.SendMode == MessageSendMode.reliable ? 3 : 1);

            if (data.Length > message.Bytes.Length)
            {
                RiptideLogger.Log("ERROR", $"Can't fully handle {data.Length} bytes because it exceeds the maximum of {message.Bytes.Length}, message will contain incomplete data!");
                Array.Copy(data, 0, message.Bytes, 0, message.Bytes.Length);
                message.ReadableLength = message.Bytes.Length;
            }
            else
            {
                Array.Copy(data, 0, message.Bytes, 0, data.Length);
                message.ReadableLength = data.Length;
            }
        }

        #region Functions
        /// <summary>Sets the bytes reserved for the sequence ID (should only be called on reliable messages).</summary>
        /// <param name="seqId">The sequence ID to insert.</param>
        internal void SetSequenceIdBytes(ushort seqId)
        {
#if BIG_ENDIAN
            Bytes[2] = (byte)seqId;
            Bytes[1] = (byte)(seqId >> 8);
#else
            Bytes[1] = (byte)seqId;
            Bytes[2] = (byte)(seqId >> 8);
#endif
        }

        /// <summary>Resets the internal write position so the message be reused. Header type and send mode remain unchanged, but message contents can be rewritten.</summary>
        internal void Reuse()
        {
            writePos = (ushort)(SendMode == MessageSendMode.reliable ? 3 : 1);
            readPos = 0;
        }
        #endregion

        #region Add & Retrieve Data
        #region Byte
        /// <summary>Adds a single <see cref="byte"/> to the message.</summary>
        /// <param name="value">The <see cref="byte"/> to add.</param>
        /// <returns>The Message instance that the <see cref="byte"/> was added to.</returns>
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
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte', returning 0!");
                return 0;
            }
            
            return Bytes[readPos++]; // Get the byte at readPos' position
        }

        /// <summary>Adds a <see cref="byte"/> array to the message.</summary>
        /// <param name="array">The <see cref="byte"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="byte"/> array was added to.</returns>
        public Message Add(byte[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'byte[]'!");

            Array.Copy(array, 0, Bytes, writePos, array.Length);
            writePos += (ushort)array.Length;
            return this;
        }

        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
        /// <returns>The <see cref="byte"/> array that was retrieved.</returns>
        public byte[] GetByteArray()
        {
            return GetByteArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
        /// <param name="length">The length of the <see cref="byte"/> array.</param>
        /// <returns>The <see cref="byte"/> array that was retrieved.</returns>
        public byte[] GetByteArray(int length)
        {
            byte[] value = new byte[length];

            if (UnreadLength < length)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte[]', array will contain default elements!");
                length = (ushort)UnreadLength;
            }

            Array.Copy(Bytes, readPos, value, 0, length); // Copy the bytes at readPos' position to the array that will be returned
            readPos += (ushort)length;
            return value;
        }
        #endregion

        #region Bool
        /// <summary>Adds a <see cref="bool"/> to the message.</summary>
        /// <param name="value">The <see cref="bool"/> to add.</param>
        /// <returns>The Message instance that the <see cref="bool"/> was added to.</returns>
        public Message Add(bool value)
        {
            if (UnwrittenLength < boolLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool'!");

            Bytes[writePos++] = (byte)(value ? 1 : 0);
            return this;
        }

        /// <summary>Retrieves a <see cref="bool"/> from the message.</summary>
        /// <returns>The <see cref="bool"/> that was retrieved.</returns>
        public bool GetBool()
        {
            if (UnreadLength < boolLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool', returning false!");
                return false;
            }
            
            return Bytes[readPos++] == 1; // Convert the byte at readPos' position to a bool
        }

        /// <summary>Adds a <see cref="bool"/> array to the message.</summary>
        /// <param name="array">The <see cref="bool"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="bool"/> array was added to.</returns>
        public Message Add(bool[] array, bool includeLength = true)
        {
            ushort byteLength = (ushort)(array.Length / 8 + (array.Length % 8 == 0 ? 0 : 1));

            if (UnwrittenLength < byteLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool[]'!");

            if (includeLength)
                Add((ushort)array.Length);

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
        public bool[] GetBoolArray()
        {
            return GetBoolArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="bool"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="bool"/> array that was retrieved.</returns>
        public bool[] GetBoolArray(ushort length)
        {
            ushort byteLength = (ushort)(length / 8 + (length % 8 == 0 ? 0 : 1));

            if (UnreadLength < byteLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool[]', array will contain default elements!");
                length = (ushort)(UnreadLength * 8);
            }

            // Read 8 bools from each byte
            bool[] array = new bool[length];
            bool isLengthMultipleOf8 = array.Length % 8 == 0;
            for (int i = 0; i < byteLength; i++)
            {
                int bitsToRead = 8;
                if ((i + 1) == byteLength && !isLengthMultipleOf8)
                    bitsToRead = array.Length % 8;

                for (int bit = 0; bit < bitsToRead; bit++)
                    array[i * 8 + bit] = (Bytes[readPos + i] >> bit & 1) == 1;
            }

            readPos += byteLength;
            return array;
        }
        #endregion

        #region Short & UShort
        /// <summary>Adds a <see cref="short"/> to the message.</summary>
        /// <param name="value">The <see cref="short"/> to add.</param>
        /// <returns>The Message instance that the <see cref="short"/> was added to.</returns>
        public Message Add(short value)
        {
            if (UnwrittenLength < shortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short'!");

            Write((ushort)value);
            return this;
        }

        /// <summary>Adds a <see cref="ushort"/> to the message.</summary>
        /// <param name="value">The <see cref="ushort"/> to add.</param>
        /// <returns>The Message instance that the <see cref="ushort"/> was added to.</returns>
        public Message Add(ushort value)
        {
            if (UnwrittenLength < shortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ushort'!");

            Write(value);
            return this;
        }

        /// <summary>Converts a given <see cref="ushort"/> to bytes and adds them to the message's contents.</summary>
        /// <param name="value">The <see cref="ushort"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(ushort value)
        {
#if BIG_ENDIAN
            Bytes[writePos + 1] = (byte)value;
            Bytes[writePos    ] = (byte)(value >> 8);
#else
            Bytes[writePos    ] = (byte)value;
            Bytes[writePos + 1] = (byte)(value >> 8);
#endif
            writePos += shortLength;
        }

        /// <summary>Retrieves a <see cref="short"/> from the message.</summary>
        /// <returns>The <see cref="short"/> that was retrieved.</returns>
        public short GetShort()
        {
            if (UnreadLength < shortLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'short', returning 0!");
                return 0;
            }

            return (short)ReadUShort(); // Convert the bytes at readPos' position to a short
        }

        /// <summary>Retrieves a <see cref="ushort"/> from the message.</summary>
        /// <returns>The <see cref="ushort"/> that was retrieved.</returns>
        public ushort GetUShort()
        {
            if (UnreadLength < shortLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort', returning 0!");
                return 0;
            }

            return ReadUShort(); // Convert the bytes at readPos' position to a ushort
        }

        /// <summary>Retrieves a <see cref="ushort"/> from the next 2 bytes, starting at the read position.</summary>
        /// <returns>The converted <see cref="ushort"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadUShort()
        {
#if BIG_ENDIAN
            ushort value = (ushort)(Bytes[readPos + 1] | (Bytes[readPos    ] << 8));
#else
            ushort value = (ushort)(Bytes[readPos    ] | (Bytes[readPos + 1] << 8));
#endif
            readPos += shortLength;
            return value;
        }

        /// <summary>Retrieves a <see cref="ushort"/> from the message without moving the read position, allowing the same bytes to be read again.</summary>
        internal ushort PeekUShort()
        {
            if (UnreadLength < shortLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to peek type 'ushort', returning 0!");
                return 0;
            }

#if BIG_ENDIAN
            return (ushort)((Bytes[readPos + 1] << 8) | Bytes[readPos]); // Convert the bytes to a ushort
#else
            return (ushort)(Bytes[readPos] | (Bytes[readPos + 1] << 8)); // Convert the bytes to a ushort
#endif
        }

        /// <summary>Adds a <see cref="short"/> array to the message.</summary>
        /// <param name="array">The <see cref="short"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="short"/> array was added to.</returns>
        public Message Add(short[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * shortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="ushort"/> array to the message.</summary>
        /// <param name="array">The <see cref="ushort"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="ushort"/> array was added to.</returns>
        public Message Add(ushort[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * shortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ushort[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <returns>The <see cref="short"/> array that was retrieved.</returns>
        public short[] GetShortArray()
        {
            return GetShortArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="short"/> array that was retrieved.</returns>
        public short[] GetShortArray(ushort length)
        {
            short[] array = new short[length];

            if (UnreadLength < length * shortLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'short[]', array will contain default elements!");
                length = (ushort)(UnreadLength / shortLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetShort();

            return array;
        }

        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <returns>The <see cref="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShortArray()
        {
            return GetUShortArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShortArray(ushort length)
        {
            ushort[] array = new ushort[length];

            if (UnreadLength < length * shortLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort[]', array will contain default elements!");
                length = (ushort)(UnreadLength / shortLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetUShort();
            
            return array;
        }
        #endregion

        #region Int & UInt
        /// <summary>Adds an <see cref="int"/> to the message.</summary>
        /// <param name="value">The <see cref="int"/> to add.</param>
        /// <returns>The Message instance that the <see cref="int"/> was added to.</returns>
        public Message Add(int value)
        {
            if (UnwrittenLength < intLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int'!");

            Write(value);
            return this;
        }

        /// <summary>Adds a <see cref="uint"/> to the message.</summary>
        /// <param name="value">The <see cref="uint"/> to add.</param>
        /// <returns>The Message instance that the <see cref="uint"/> was added to.</returns>
        public Message Add(uint value)
        {
            if (UnwrittenLength < intLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'uint'!");

            Write((int)value);
            return this;
        }

        /// <summary>Converts a given <see cref="int"/> to bytes and adds them to the message's contents.</summary>
        /// <param name="value">The <see cref="int"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(int value)
        {
#if BIG_ENDIAN
            Bytes[writePos + 3] = (byte)value;
            Bytes[writePos + 2] = (byte)(value >> 8);
            Bytes[writePos + 1] = (byte)(value >> 16);
            Bytes[writePos    ] = (byte)(value >> 24);
#else
            Bytes[writePos    ] = (byte)value;
            Bytes[writePos + 1] = (byte)(value >> 8);
            Bytes[writePos + 2] = (byte)(value >> 16);
            Bytes[writePos + 3] = (byte)(value >> 24);
#endif
            writePos += intLength;
        }

        /// <summary>Retrieves an <see cref="int"/> from the message.</summary>
        /// <returns>The <see cref="int"/> that was retrieved.</returns>
        public int GetInt()
        {
            if (UnreadLength < intLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'int', returning 0!");
                return 0;
            }

            return ReadInt(); // Convert the bytes at readPos' position to an int
        }

        /// <summary>Retrieves a <see cref="uint"/> from the message.</summary>
        /// <returns>The <see cref="uint"/> that was retrieved.</returns>
        public uint GetUInt()
        {
            if (UnreadLength < intLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint', returning 0!");
                return 0;
            }

            return (uint)ReadInt(); // Convert the bytes at readPos' position to a uint
        }

        /// <summary>Retrieves an <see cref="int"/> from the next 4 bytes, starting at the read position.</summary>
        /// <returns>The converted <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadInt()
        {
#if BIG_ENDIAN
            int value = Bytes[readPos + 3] | (Bytes[readPos + 2] << 8) | (Bytes[readPos + 1] << 16) | (Bytes[readPos    ] << 32);
#else
            int value = Bytes[readPos    ] | (Bytes[readPos + 1] << 8) | (Bytes[readPos + 2] << 16) | (Bytes[readPos + 3] << 24);
#endif
            readPos += intLength;
            return value;
        }

        /// <summary>Adds an <see cref="int"/> array message.</summary>
        /// <param name="array">The <see cref="int"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="int"/> array was added to.</returns>
        public Message Add(int[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * intLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="uint"/> array to the message.</summary>
        /// <param name="array">The <see cref="uint"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="uint"/> array was added to.</returns>
        public Message Add(uint[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * intLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'uint[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <returns>The <see cref="int"/> array that was retrieved.</returns>
        public int[] GetIntArray()
        {
            return GetIntArray(GetUShort());
        }
        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="int"/> array that was retrieved.</returns>
        public int[] GetIntArray(ushort length)
        {
            int[] array = new int[length];

            if (UnreadLength < length * intLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'int[]', array will contain default elements!");
                length = (ushort)(UnreadLength / intLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetInt();

            return array;
        }

        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <returns>The <see cref="uint"/> array that was retrieved.</returns>
        public uint[] GetUIntArray()
        {
            return GetUIntArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="uint"/> array that was retrieved.</returns>
        public uint[] GetUIntArray(ushort length)
        {
            uint[] array = new uint[length];

            if (UnreadLength < length * intLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint[]', array will contain default elements!");
                length = (ushort)(UnreadLength / intLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetUInt();

            return array;
        }
        #endregion

        #region Long & ULong
        /// <summary>Adds a <see cref="long"/> to the message.</summary>
        /// <param name="value">The <see cref="long"/> to add.</param>
        /// <returns>The Message instance that the <see cref="long"/> was added to.</returns>
        public Message Add(long value)
        {
            if (UnwrittenLength < longLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long'!");

            Write(value);
            return this;
        }

        /// <summary>Adds a <see cref="ulong"/> to the message.</summary>
        /// <param name="value">The <see cref="ulong"/> to add.</param>
        /// <returns>The Message instance that the <see cref="ulong"/> was added to.</returns>
        public Message Add(ulong value)
        {
            if (UnwrittenLength < longLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ulong'!");

            Write((long)value);
            return this;
        }

        /// <summary>Converts a given <see cref="long"/> to bytes and adds them to the message's contents.</summary>
        /// <param name="value">The <see cref="long"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(long value)
        {
#if BIG_ENDIAN
            Bytes[writePos + 7] = (byte)value;
            Bytes[writePos + 6] = (byte)(value >> 8);
            Bytes[writePos + 5] = (byte)(value >> 16);
            Bytes[writePos + 4] = (byte)(value >> 24);
            Bytes[writePos + 3] = (byte)(value >> 32);
            Bytes[writePos + 2] = (byte)(value >> 40);
            Bytes[writePos + 1] = (byte)(value >> 48);
            Bytes[writePos    ] = (byte)(value >> 56);
#else
            Bytes[writePos    ] = (byte)value;
            Bytes[writePos + 1] = (byte)(value >> 8);
            Bytes[writePos + 2] = (byte)(value >> 16);
            Bytes[writePos + 3] = (byte)(value >> 24);
            Bytes[writePos + 4] = (byte)(value >> 32);
            Bytes[writePos + 5] = (byte)(value >> 40);
            Bytes[writePos + 6] = (byte)(value >> 48);
            Bytes[writePos + 7] = (byte)(value >> 56);
#endif
            writePos += longLength;
        }

        /// <summary>Retrieves a <see cref="long"/> from the message.</summary>
        /// <returns>The <see cref="long"/> that was retrieved.</returns>
        public long GetLong()
        {
            if (UnreadLength < longLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'long', returning 0!");
                return 0;
            }

            // Convert the bytes at readPos' position to a long
#if BIG_ENDIAN
            Array.Reverse(Bytes, readPos, longLength);
#endif
            long value = BitConverter.ToInt64(Bytes, readPos);
            readPos += longLength;
            return value;
        }

        /// <summary>Retrieves a <see cref="ulong"/> from the message.</summary>
        /// <returns>The <see cref="ulong"/> that was retrieved.</returns>
        public ulong GetULong()
        {
            if (UnreadLength < longLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong', returning 0!");
                return 0;
            }
            
            // Convert the bytes at readPos' position to a ulong
#if BIG_ENDIAN
            Array.Reverse(Bytes, readPos, longLength);
#endif
            ulong value = BitConverter.ToUInt64(Bytes, readPos);
            readPos += longLength;
            return value;
        }

        /// <summary>Adds a <see cref="long"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="long"/> array was added to.</returns>
        public Message Add(long[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * longLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Adds a <see cref="ulong"/> array to the message.</summary>
        /// <param name="array">The <see cref="ulong"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="ulong"/> array was added to.</returns>
        public Message Add(ulong[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * longLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ulong[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <returns>The <see cref="long"/> array that was retrieved.</returns>
        public long[] GetLongArray()
        {
            return GetLongArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="long"/> array that was retrieved.</returns>
        public long[] GetLongArray(ushort length)
        {
            long[] array = new long[length];

            if (UnreadLength < length * longLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'long[]', array will contain default elements!");
                length = (ushort)(UnreadLength / longLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetLong();

            return array;
        }

        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <returns>The <see cref="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongArray()
        {
            return GetULongArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongArray(ushort length)
        {
            ulong[] array = new ulong[length];

            if (UnreadLength < length * longLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong[]', array will contain default elements!");
                length = (ushort)(UnreadLength / longLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetULong();

            return array;
        }
        #endregion

        #region Float
        /// <summary>Adds a <see cref="float"/> to the message.</summary>
        /// <param name="value">The <see cref="float"/> to add.</param>
        /// <returns>The Message instance that the <see cref="float"/> was added to.</returns>
        public Message Add(float value)
        {
            if (UnwrittenLength < floatLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'float'!");

            FloatConverter converter = new FloatConverter { floatValue = value };
#if BIG_ENDIAN
            Bytes[writePos + 3] = converter.byte0;
            Bytes[writePos + 2] = converter.byte1;
            Bytes[writePos + 1] = converter.byte2;
            Bytes[writePos    ] = converter.byte3;
#else
            Bytes[writePos    ] = converter.byte0;
            Bytes[writePos + 1] = converter.byte1;
            Bytes[writePos + 2] = converter.byte2;
            Bytes[writePos + 3] = converter.byte3;
#endif
            writePos += floatLength;
            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> from the message.</summary>
        /// <returns>The <see cref="float"/> that was retrieved.</returns>
        public float GetFloat()
        {
            if (UnreadLength < floatLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'float', returning 0!");
                return 0;
            }

            // Convert the bytes at readPos' position to a float
#if BIG_ENDIAN
            FloatConverter converter = new FloatConverter { byte3 = Bytes[readPos], byte2 = Bytes[readPos + 1], byte1 = Bytes[readPos + 2], byte0 = Bytes[readPos + 3] };
#else
            FloatConverter converter = new FloatConverter { byte0 = Bytes[readPos], byte1 = Bytes[readPos + 1], byte2 = Bytes[readPos + 2], byte3 = Bytes[readPos + 3] };
#endif
            readPos += floatLength;
            return converter.floatValue;
        }

        /// <summary>Adds a <see cref="float"/> array to the message.</summary>
        /// <param name="array">The <see cref="float"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="float"/> array was added to.</returns>
        public Message Add(float[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * floatLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'float[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <returns>The <see cref="float"/> array that was retrieved.</returns>
        public float[] GetFloatArray()
        {
            return GetFloatArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="float"/> array that was retrieved.</returns>
        public float[] GetFloatArray(ushort length)
        {
            float[] array = new float[length];

            if (UnreadLength < length * floatLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'float[]', array will contain default elements!");
                length = (ushort)(UnreadLength / floatLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetFloat();

            return array;
        }
        #endregion

        #region Double
        /// <summary>Adds a <see cref="double"/> to the message.</summary>
        /// <param name="value">The <see cref="double"/> to add.</param>
        /// <returns>The Message instance that the <see cref="double"/> was added to.</returns>
        public Message Add(double value)
        {
            if (UnwrittenLength < doubleLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'double'!");

            DoubleConverter converter = new DoubleConverter { doubleValue = value };
#if BIG_ENDIAN
            Bytes[writePos + 7] = converter.byte0;
            Bytes[writePos + 6] = converter.byte1;
            Bytes[writePos + 5] = converter.byte2;
            Bytes[writePos + 4] = converter.byte3;
            Bytes[writePos + 3] = converter.byte4;
            Bytes[writePos + 2] = converter.byte5;
            Bytes[writePos + 1] = converter.byte6;
            Bytes[writePos    ] = converter.byte7;
#else
            Bytes[writePos    ] = converter.byte0;
            Bytes[writePos + 1] = converter.byte1;
            Bytes[writePos + 2] = converter.byte2;
            Bytes[writePos + 3] = converter.byte3;
            Bytes[writePos + 4] = converter.byte4;
            Bytes[writePos + 5] = converter.byte5;
            Bytes[writePos + 6] = converter.byte6;
            Bytes[writePos + 7] = converter.byte7;
#endif
            writePos += doubleLength;
            return this;
        }

        /// <summary>Retrieves a <see cref="double"/> from the message.</summary>
        /// <returns>The <see cref="double"/> that was retrieved.</returns>
        public double GetDouble()
        {
            if (UnreadLength < doubleLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'double', returning 0!");
                return 0;
            }

            // Convert the bytes at readPos' position to a double
#if BIG_ENDIAN
            Array.Reverse(Bytes, readPos, doubleLength);
#endif
            double value = BitConverter.ToDouble(Bytes, readPos); 
            readPos += doubleLength;
            return value;
        }

        /// <summary>Adds a <see cref="double"/> array to the message.</summary>
        /// <param name="array">The <see cref="double"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="double"/> array was added to.</returns>
        public Message Add(double[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * doubleLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'double[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a<see cref="double"/> array from the message.</summary>
        /// <returns>The <see cref="double"/> array that was retrieved.</returns>
        public double[] GetDoubleArray()
        {
            return GetDoubleArray(GetUShort());
        }
        /// <summary>Retrieves a<see cref="double"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="double"/> array that was retrieved.</returns>
        public double[] GetDoubleArray(ushort length)
        {
            double[] array = new double[length];

            if (UnreadLength < length * doubleLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'double[]', array will contain default elements!");
                length = (ushort)(UnreadLength / doubleLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetDouble();

            return array;
        }
        #endregion

        #region String
        /// <summary>Adds a <see cref="string"/> to the message.</summary>
        /// <param name="value">The <see cref="string"/> to add.</param>
        /// <returns>The Message instance that the <see cref="string"/> was added to.</returns>
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
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'string', result will be truncated!");
                length = (ushort)UnreadLength;
            }
            
            string value = Encoding.UTF8.GetString(Bytes, readPos, length); // Convert the bytes at readPos' position to a string
            readPos += length;
            return value;
        }

        /// <summary>Adds a <see cref="string"/> array to the message.</summary>
        /// <param name="array">The <see cref="string"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see cref="string"/> array was added to.</returns>
        public Message Add(string[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="string"/> array from the message.</summary>
        /// <returns>The <see cref="string"/> array that was retrieved.</returns>
        public string[] GetStringArray()
        {
            return GetStringArray(GetUShort());
        }
        /// <summary>Retrieves a <see cref="string"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see cref="string"/> array that was retrieved.</returns>
        public string[] GetStringArray(ushort length)
        {
            string[] array = new string[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetString();

            return array;
        }
        #endregion
        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    struct FloatConverter
    {
        [FieldOffset(0)]
        public byte byte0;
        [FieldOffset(1)]
        public byte byte1;
        [FieldOffset(2)]
        public byte byte2;
        [FieldOffset(3)]
        public byte byte3;

        [FieldOffset(0)]
        public float floatValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct DoubleConverter
    {
        [FieldOffset(0)]
        public byte byte0;
        [FieldOffset(1)]
        public byte byte1;
        [FieldOffset(2)]
        public byte byte2;
        [FieldOffset(3)]
        public byte byte3;
        [FieldOffset(4)]
        public byte byte4;
        [FieldOffset(5)]
        public byte byte5;
        [FieldOffset(6)]
        public byte byte6;
        [FieldOffset(7)]
        public byte byte7;

        [FieldOffset(0)]
        public double doubleValue;
    }
}
