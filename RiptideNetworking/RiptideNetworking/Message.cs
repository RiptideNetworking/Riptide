using System;
using System.Collections;
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

        /// <summary>How many bytes a <see langword="bool"/> is represented by.</summary>
        public const byte boolLength = sizeof(bool);
        /// <summary>How many bytes a <see langword="short"/> (or <see langword="ushort"/>) is represented by.</summary>
        public const byte shortLength = sizeof(short);
        /// <summary>How many bytes an <see langword="int"/> (or <see langword="uint"/>) is represented by.</summary>
        public const byte intLength = sizeof(int);
        /// <summary>How many bytes a <see langword="long"/> (or <see langword="ulong"/>) is represented by.</summary>
        public const byte longLength = sizeof(long);
        /// <summary>How many bytes a <see langword="float"/> is represented by.</summary>
        public const byte floatLength = sizeof(float);
        /// <summary>How many bytes a <see langword="double"/> is represented by.</summary>
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
                Console.WriteLine($"[ERROR] Can't fully handle {data.Length} bytes because it exceeds the maximum of {message.Bytes.Length}, message will contain incomplete data!"); // TODO: Might need to be rethinked
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
            byte[] sequenceIdBytes = StandardizeEndianness(BitConverter.GetBytes(seqId));
#else
            byte[] sequenceIdBytes = BitConverter.GetBytes(seqId);
#endif
            Bytes[1] = sequenceIdBytes[0];
            Bytes[2] = sequenceIdBytes[1];
        }

        /// <summary>Resets the internal write position so the message be reused. Header type and send mode remain unchanged, but message contents can be rewritten.</summary>
        internal void Reuse()
        {
            writePos = (ushort)(SendMode == MessageSendMode.reliable ? 3 : 1);
            readPos = 0;
        }

#if BIG_ENDIAN
        /// <summary>Standardizes byte order across big and little endian systems by reversing the given bytes on big endian systems.</summary>
        /// <param name="value">The bytes whose order to standardize.</param>
        internal static byte[] StandardizeEndianness(byte[] value)
        {
            Array.Reverse(value);
            return value;
        }
        /// <summary>Standardizes byte order across big and little endian systems by reversing byteAmount bytes at the message's current read position on big endian systems.</summary>
        /// <param name="byteAmount">The number of bytes whose order to standardize, starting at the message's current read position.</param>
        internal void StandardizeEndianness(int byteAmount)
        {
            Array.Reverse(Bytes, readPos, byteAmount);
        }
#endif
#endregion

#region Add & Retrieve Data
#region Byte
        /// <summary>Adds a single <see langword="byte"/> to the message.</summary>
        /// <param name="value">The <see langword="byte"/> to add.</param>
        /// <returns>The Message instance that the <see langword="byte"/> was added to.</returns>
        public Message Add(byte value)
        {
            if (UnwrittenLength < 1)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'byte'!");

            Bytes[writePos++] = value;
            return this;
        }

        /// <summary>Retrieves a <see langword="byte"/> from the message.</summary>
        /// <returns>The <see langword="byte"/> that was retrieved.</returns>
        public byte GetByte()
        {
            if (UnreadLength < 1)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }
            
            // If there are enough unread bytes
            byte value = Bytes[readPos]; // Get the byte at readPos' position
            readPos += 1;
            return value;
        }

        /// <summary>Adds a <see langword="byte"/> array to the message.</summary>
        /// <param name="value">The <see langword="byte"/> array to add.</param>
        /// <returns>The Message instance that the <see langword="byte"/> array was added to.</returns>
        public Message Add(byte[] value)
        {
            if (UnwrittenLength < value.Length)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'byte[]'!");

            Array.Copy(value, 0, Bytes, writePos, value.Length);
            writePos += (ushort)value.Length;
            return this;
        }

        /// <summary>Retrieves a <see langword="byte"/> array from the message.</summary>
        /// <param name="length">The length of the <see langword="byte"/> array.</param>
        /// <returns>The <see langword="byte"/> array that was retrieved.</returns>
        public byte[] GetByteArray(int length)
        {
            byte[] value = new byte[length];

            if (UnreadLength < length)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = UnreadLength;
            }

            // If there are enough unread bytes
            Array.Copy(Bytes, readPos, value, 0, length); // Copy the bytes at readPos' position to the array that will be returned
            readPos += (ushort)length;
            return value;
        }
#endregion

#region Bool
        /// <summary>Adds a <see langword="bool"/> to the message.</summary>
        /// <param name="value">The <see langword="bool"/> to add.</param>
        /// <returns>The Message instance that the <see langword="bool"/> was added to.</returns>
        public Message Add(bool value)
        {
            if (UnwrittenLength < boolLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool'!");

            Bytes[writePos++] = BitConverter.GetBytes(value)[0];
            return this;
        }

        /// <summary>Retrieves a <see langword="bool"/> from the message.</summary>
        /// <returns>The <see langword="bool"/> that was retrieved.</returns>
        public bool GetBool()
        {
            if (UnreadLength < boolLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool', returning false!"); // TODO: Might need to be rethinked
                return false;
            }
            
            // If there are enough unread bytes
            bool value = BitConverter.ToBoolean(Bytes, readPos); // Convert the bytes at readPos' position to a bool
            readPos += boolLength;
            return value;
        }

        /// <summary>Adds a <see langword="bool"/> array to the message.</summary>
        /// <param name="array">The <see langword="bool"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="bool"/> array was added to.</returns>
        public Message Add(bool[] array, bool includeLength = true)
        {
            ushort byteLength = (ushort)(array.Length / 8 + (array.Length % 8 == 0 ? 0 : 1));
            if (UnwrittenLength < byteLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool[]'!");

            if (includeLength)
                Add((ushort)array.Length);

            BitArray bits = new BitArray(array);
            bits.CopyTo(Bytes, writePos);
            writePos += byteLength;
            return this;
        }

        /// <summary>Retrieves a <see langword="bool"/> array from the message.</summary>
        /// <returns>The <see langword="bool"/> array that was retrieved.</returns>
        public bool[] GetBoolArray()
        {
            return GetBoolArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="bool"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="bool"/> array that was retrieved.</returns>
        public bool[] GetBoolArray(ushort length)
        {
            ushort byteLength = (ushort)(length / 8 + (length % 8 == 0 ? 0 : 1));
            if (UnreadLength < byteLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / shortLength);
            }

            BitArray bits = new BitArray(GetByteArray(byteLength));
            bool[] array = new bool[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = bits.Get(i);

            return array;
        }
#endregion

#region Short
        /// <summary>Adds a <see langword="short"/> to the message.</summary>
        /// <param name="value">The <see langword="short"/> to add.</param>
        /// <returns>The Message instance that the <see langword="short"/> was added to.</returns>
        public Message Add(short value)
        {
            if (UnwrittenLength < shortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            return this;
        }

        /// <summary>Retrieves a <see langword="short"/> from the message.</summary>
        /// <returns>The <see langword="short"/> that was retrieved.</returns>
        public short GetShort()
        {
            if (UnreadLength < shortLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'short', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(shortLength);
#endif
            short value = BitConverter.ToInt16(Bytes, readPos); // Convert the bytes at readPos' position to a short
            readPos += shortLength;
            return value;
        }

        /// <summary>Adds a <see langword="short"/> array to the message.</summary>
        /// <param name="array">The <see langword="short"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="short"/> array was added to.</returns>
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

        /// <summary>Retrieves a <see langword="short"/> array from the message.</summary>
        /// <returns>The <see langword="short"/> array that was retrieved.</returns>
        public short[] GetShortArray()
        {
            return GetShortArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="short"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="short"/> array that was retrieved.</returns>
        public short[] GetShortArray(ushort length)
        {
            short[] array = new short[length];

            if (UnreadLength < length * shortLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'short[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / shortLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetShort();

            return array;
        }
#endregion

#region UShort
        /// <summary>Adds a <see langword="ushort"/> to the message.</summary>
        /// <param name="value">The <see langword="ushort"/> to add.</param>
        /// <returns>The Message instance that the <see langword="ushort"/> was added to.</returns>
        public Message Add(ushort value)
        {
            if (UnwrittenLength < shortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ushort'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            return this;
        }

        /// <summary>Retrieves a <see langword="ushort"/> from the message.</summary>
        /// <returns>The <see langword="ushort"/> that was retrieved.</returns>
        public ushort GetUShort()
        {
            if (UnreadLength < shortLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(shortLength);
#endif
            ushort value = BitConverter.ToUInt16(Bytes, readPos); // Convert the bytes at readPos' position to a ushort
            readPos += shortLength;
            return value;
        }

        /// <summary>Retrieves a <see langword="ushort"/> from the message without moving the read position, allowing the same bytes to be read again.</summary>
        internal ushort PeekUShort()
        {
            if (UnreadLength < shortLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to peek type 'ushort', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }
            
            // If there are enough unread bytes
            byte[] bytesToConvert = new byte[shortLength];
            Array.Copy(Bytes, readPos, bytesToConvert, 0, shortLength);
#if BIG_ENDIAN
            return BitConverter.ToUInt16(StandardizeEndianness(bytesToConvert), 0); // Convert the bytes to a ushort
#else
            return BitConverter.ToUInt16(bytesToConvert, 0); // Convert the bytes to a ushort
#endif
        }

        /// <summary>Adds a <see langword="ushort"/> array to the message.</summary>
        /// <param name="array">The <see langword="ushort"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="ushort"/> array was added to.</returns>
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

        /// <summary>Retrieves a <see langword="ushort"/> array from the message.</summary>
        /// <returns>The <see langword="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShortArray()
        {
            return GetUShortArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="ushort"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShortArray(ushort length)
        {
            ushort[] array = new ushort[length];

            if (UnreadLength < length * shortLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / shortLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetUShort();
            
            return array;
        }
#endregion

#region Int
        /// <summary>Adds an <see langword="int"/> to the message.</summary>
        /// <param name="value">The <see langword="int"/> to add.</param>
        /// <returns>The Message instance that the <see langword="int"/> was added to.</returns>
        public Message Add(int value)
        {
            if (UnwrittenLength < intLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            Bytes[writePos++] = valueBytes[2];
            Bytes[writePos++] = valueBytes[3];
            return this;
        }

        /// <summary>Retrieves an <see langword="int"/> from the message.</summary>
        /// <returns>The <see langword="int"/> that was retrieved.</returns>
        public int GetInt()
        {
            if (UnreadLength < intLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'int', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(intLength);
#endif
            int value = BitConverter.ToInt32(Bytes, readPos); // Convert the bytes at readPos' position to an int
            readPos += intLength;
            return value;
        }

        /// <summary>Adds an <see langword="int"/> array message.</summary>
        /// <param name="array">The <see langword="int"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="int"/> array was added to.</returns>
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

        /// <summary>Retrieves an <see langword="int"/> array from the message.</summary>
        /// <returns>The <see langword="int"/> array that was retrieved.</returns>
        public int[] GetIntArray()
        {
            return GetIntArray(GetUShort());
        }
        /// <summary>Retrieves an <see langword="int"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="int"/> array that was retrieved.</returns>
        public int[] GetIntArray(ushort length)
        {
            int[] array = new int[length];

            if (UnreadLength < length * intLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'int[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / intLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetInt();

            return array;
        }
#endregion

#region UInt
        /// <summary>Adds a <see langword="uint"/> to the message.</summary>
        /// <param name="value">The <see langword="uint"/> to add.</param>
        /// <returns>The Message instance that the <see langword="uint"/> was added to.</returns>
        public Message Add(uint value)
        {
            if (UnwrittenLength < intLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'uint'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            Bytes[writePos++] = valueBytes[2];
            Bytes[writePos++] = valueBytes[3];
            return this;
        }

        /// <summary>Retrieves a <see langword="uint"/> from the message.</summary>
        /// <returns>The <see langword="uint"/> that was retrieved.</returns>
        public uint GetUInt()
        {
            if (UnreadLength < intLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(intLength);
#endif
            uint value = BitConverter.ToUInt32(Bytes, readPos); // Convert the bytes at readPos' position to an uint
            readPos += intLength;
            return value;
        }

        /// <summary>Adds a <see langword="uint"/> array to the message.</summary>
        /// <param name="array">The <see langword="uint"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="uint"/> array was added to.</returns>
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

        /// <summary>Retrieves a <see langword="uint"/> array from the message.</summary>
        /// <returns>The <see langword="uint"/> array that was retrieved.</returns>
        public uint[] GetUIntArray()
        {
            return GetUIntArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="uint"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="uint"/> array that was retrieved.</returns>
        public uint[] GetUIntArray(ushort length)
        {
            uint[] array = new uint[length];

            if (UnreadLength < length * intLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / intLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetUInt();

            return array;
        }
#endregion

#region Long
        /// <summary>Adds a <see langword="long"/> to the message.</summary>
        /// <param name="value">The <see langword="long"/> to add.</param>
        /// <returns>The Message instance that the <see langword="long"/> was added to.</returns>
        public Message Add(long value)
        {
            if (UnwrittenLength < longLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            Bytes[writePos++] = valueBytes[2];
            Bytes[writePos++] = valueBytes[3];
            Bytes[writePos++] = valueBytes[4];
            Bytes[writePos++] = valueBytes[5];
            Bytes[writePos++] = valueBytes[6];
            Bytes[writePos++] = valueBytes[7];
            return this;
        }

        /// <summary>Retrieves a <see langword="long"/> from the message.</summary>
        /// <returns>The <see langword="long"/> that was retrieved.</returns>
        public long GetLong()
        {
            if (UnreadLength < longLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'long', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(longLength);
#endif
            long value = BitConverter.ToInt64(Bytes, readPos); // Convert the bytes at readPos' position to a long;
            readPos += longLength;
            return value;
        }

        /// <summary>Adds a <see langword="long"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="long"/> array was added to.</returns>
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

        /// <summary>Retrieves a <see langword="long"/> array from the message.</summary>
        /// <returns>The <see langword="long"/> array that was retrieved.</returns>
        public long[] GetLongArray()
        {
            return GetLongArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="long"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="long"/> array that was retrieved.</returns>
        public long[] GetLongArray(ushort length)
        {
            long[] array = new long[length];

            if (UnreadLength < length * longLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'long[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / longLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetLong();

            return array;
        }
#endregion

#region ULong
        /// <summary>Adds a <see langword="ulong"/> to the message.</summary>
        /// <param name="value">The <see langword="ulong"/> to add.</param>
        /// <returns>The Message instance that the <see langword="ulong"/> was added to.</returns>
        public Message Add(ulong value)
        {
            if (UnwrittenLength < longLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ulong'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            Bytes[writePos++] = valueBytes[2];
            Bytes[writePos++] = valueBytes[3];
            Bytes[writePos++] = valueBytes[4];
            Bytes[writePos++] = valueBytes[5];
            Bytes[writePos++] = valueBytes[6];
            Bytes[writePos++] = valueBytes[7];
            return this;
        }

        /// <summary>Retrieves a <see langword="ulong"/> from the message.</summary>
        /// <returns>The <see langword="ulong"/> that was retrieved.</returns>
        public ulong GetULong()
        {
            if (UnreadLength < longLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(longLength);
#endif
            ulong value = BitConverter.ToUInt64(Bytes, readPos); // Convert the bytes at readPos' position to a ulong
            readPos += longLength;
            return value;
        }

        /// <summary>Adds a <see langword="ulong"/> array to the message.</summary>
        /// <param name="array">The <see langword="ulong"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="ulong"/> array was added to.</returns>
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

        /// <summary>Retrieves a <see langword="ulong"/> array from the message.</summary>
        /// <returns>The <see langword="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongArray()
        {
            return GetULongArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="ulong"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongArray(ushort length)
        {
            ulong[] array = new ulong[length];

            if (UnreadLength < length * longLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / longLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetULong();

            return array;
        }
#endregion

#region Float
        /// <summary>Adds a <see langword="float"/> to the message.</summary>
        /// <param name="value">The <see langword="float"/> to add.</param>
        /// <returns>The Message instance that the <see langword="float"/> was added to.</returns>
        public Message Add(float value)
        {
            if (UnwrittenLength < floatLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'float'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            Bytes[writePos++] = valueBytes[2];
            Bytes[writePos++] = valueBytes[3];
            return this;
        }

        /// <summary>Retrieves a <see langword="float"/> from the message.</summary>
        /// <returns>The <see langword="float"/> that was retrieved.</returns>
        public float GetFloat()
        {
            if (UnreadLength < floatLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'float', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(floatLength);
#endif
            float value = BitConverter.ToSingle(Bytes, readPos); // Convert the bytes at readPos' position to a float
            readPos += floatLength;
            return value;
        }

        /// <summary>Adds a <see langword="float"/> array to the message.</summary>
        /// <param name="array">The <see langword="float"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="float"/> array was added to.</returns>
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

        /// <summary>Retrieves a <see langword="float"/> array from the message.</summary>
        /// <returns>The <see langword="float"/> array that was retrieved.</returns>
        public float[] GetFloatArray()
        {
            return GetFloatArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="float"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="float"/> array that was retrieved.</returns>
        public float[] GetFloatArray(ushort length)
        {
            float[] array = new float[length];

            if (UnreadLength < length * floatLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'float[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / floatLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetFloat();

            return array;
        }
#endregion

#region Double
        /// <summary>Adds a <see langword="double"/> to the message.</summary>
        /// <param name="value">The <see langword="double"/> to add.</param>
        /// <returns>The Message instance that the <see langword="double"/> was added to.</returns>
        public Message Add(double value)
        {
            if (UnwrittenLength < doubleLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'double'!");

#if BIG_ENDIAN
            byte[] valueBytes = StandardizeEndianness(BitConverter.GetBytes(value));
#else
            byte[] valueBytes = BitConverter.GetBytes(value);
#endif
            Bytes[writePos++] = valueBytes[0];
            Bytes[writePos++] = valueBytes[1];
            Bytes[writePos++] = valueBytes[2];
            Bytes[writePos++] = valueBytes[3];
            Bytes[writePos++] = valueBytes[4];
            Bytes[writePos++] = valueBytes[5];
            Bytes[writePos++] = valueBytes[6];
            Bytes[writePos++] = valueBytes[7];
            return this;
        }

        /// <summary>Retrieves a <see langword="double"/> from the message.</summary>
        /// <returns>The <see langword="double"/> that was retrieved.</returns>
        public double GetDouble()
        {
            if (UnreadLength < doubleLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'double', returning 0!"); // TODO: Might need to be rethinked
                return 0;
            }

            // If there are enough unread bytes
#if BIG_ENDIAN
            StandardizeEndianness(doubleLength);
#endif
            double value = BitConverter.ToDouble(Bytes, readPos); // Convert the bytes at readPos' position to a double
            readPos += doubleLength;
            return value;
        }

        /// <summary>Adds a <see langword="double"/> array to the message.</summary>
        /// <param name="array">The <see langword="double"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="double"/> array was added to.</returns>
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

        /// <summary>Retrieves a<see langword="double"/> array from the message.</summary>
        /// <returns>The <see langword="double"/> array that was retrieved.</returns>
        public double[] GetDoubleArray()
        {
            return GetDoubleArray(GetUShort());
        }
        /// <summary>Retrieves a<see langword="double"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="double"/> array that was retrieved.</returns>
        public double[] GetDoubleArray(ushort length)
        {
            double[] array = new double[length];

            if (UnreadLength < length * doubleLength)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'double[]', array will contain default elements!"); // TODO: Might need to be rethinked
                length = (ushort)(UnreadLength / doubleLength);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetDouble();

            return array;
        }
#endregion

#region String
        /// <summary>Adds a <see langword="string"/> to the message.</summary>
        /// <param name="value">The <see langword="string"/> to add.</param>
        /// <returns>The Message instance that the <see langword="string"/> was added to.</returns>
        public Message Add(string value)
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            Add((ushort)stringBytes.Length); // Add the length of the string (in bytes) to the message

            if (UnwrittenLength < stringBytes.Length)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'string'!");

            Add(stringBytes); // Add the string itself
            return this;
        }

        /// <summary>Retrieves a <see langword="string"/> from the message.</summary>
        /// <returns>The <see langword="string"/> that was retrieved.</returns>
        public string GetString()
        {
            ushort length = GetUShort(); // Get the length of the string (in bytes, NOT characters)
            if (UnreadLength < length)
            {
                Console.WriteLine($"[ERROR] Message contains insufficient unread bytes ({UnreadLength}) to read type 'string', result will be truncated!"); // TODO: Might need to be rethinked
                length = (ushort)UnreadLength;
            }
            
            string value = Encoding.UTF8.GetString(Bytes, readPos, length); // Convert the bytes at readPos' position to a string
            readPos += length;
            return value;
        }

        /// <summary>Adds a <see langword="string"/> array to the message.</summary>
        /// <param name="array">The <see langword="string"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="string"/> array was added to.</returns>
        public Message Add(string[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see langword="string"/> array from the message.</summary>
        /// <returns>The <see langword="string"/> array that was retrieved.</returns>
        public string[] GetStringArray()
        {
            return GetStringArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="string"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="string"/> array that was retrieved.</returns>
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
}
