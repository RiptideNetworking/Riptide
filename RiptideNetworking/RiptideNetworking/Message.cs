using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    /// <summary>The send mode for a message.</summary>
    public enum MessageSendMode : byte
    {
        /// <summary>Unreliable send mode.</summary>
        unreliable = HeaderType.unreliable,
        /// <summary>Reliable send mode.</summary>
        reliable = HeaderType.reliable,
    }

    internal enum HeaderType : byte
    {
        unreliable,
        ack,
        ackExtra,
        connect,
        heartbeat,
        disconnect,
        reliable,
        welcome,
        clientConnected,
        clientDisconnected,
    }

    /// <summary>Represents a packet.</summary>
    public class Message
    {
        /// <summary>How many bytes a bool is represented by.</summary>
        public const byte boolLength = sizeof(bool);
        /// <summary>How many bytes a short is represented by.</summary>
        public const byte shortLength = sizeof(short);
        /// <summary>How many bytes an int is represented by.</summary>
        public const byte intLength = sizeof(int);
        /// <summary>How many bytes a long is represented by.</summary>
        public const byte longLength = sizeof(long);
        /// <summary>How many bytes a float is represented by.</summary>
        public const byte floatLength = sizeof(float);
        /// <summary>How many bytes a double is represented by.</summary>
        public const byte doubleLength = sizeof(double);

        /// <summary>The length in bytes of the message's contents.</summary>
        public int Length { get => bytes.Count; }

        /// <summary>The length in bytes of the unread data contained in the message.</summary>
        public int UnreadLength { get => Length - readPos; }

        internal MessageSendMode SendMode { get; private set; }

        private List<byte> bytes;
        private byte[] readableBytes;
        private ushort readPos = 0;

        /// <summary>Creates a new empty message (without an ID).</summary>
        /// <param name="headerType">The header type for the message.</param>
        /// <param name="messageLength">The length in bytes of the message's contents.</param>
        internal Message(HeaderType headerType, ushort messageLength = 0)
        {
            SendMode = headerType >= HeaderType.reliable ? MessageSendMode.reliable : MessageSendMode.unreliable;
            bytes = new List<byte>(messageLength + 3) // +3 for message header
            {
                (byte)headerType
            };

            if (SendMode == MessageSendMode.reliable)
                Add((ushort)0); // Add 2 bytes to the list to overwrite later with the sequence ID
        }

        /// <summary>Creates a new message with a given ID.</summary>
        /// <param name="sendMode">The mode in which the message should be sent.</param>
        /// <param name="id">The message ID.</param>
        /// <param name="messageLength">The length in bytes of the message's contents.</param>
        public Message(MessageSendMode sendMode, ushort id, ushort messageLength = 0)
        {
            SendMode = sendMode;
            bytes = new List<byte>(messageLength + 5) // +5 for message header
            {
                (byte)sendMode
            };

            if (SendMode == MessageSendMode.reliable)
                Add((ushort)0); // Add 2 bytes to the list to overwrite later with the sequence ID
            Add(id);
        }

        /// <summary>Creates a message from which data can be read. Used for receiving.</summary>
        /// <param name="data">The bytes to add to the message.</param>
        internal Message(byte[] data)
        {
            bytes = new List<byte>(data.Length);

            SetBytes(data);
        }

        #region Functions
        /// <summary>Sets the message's content and prepares it to be read.</summary>
        /// <param name="data">The bytes to add to the message.</param>
        internal void SetBytes(byte[] data)
        {
            Add(data);
            readableBytes = bytes.ToArray();
        }

        /// <summary>Sets the bytes reserved for the sequence ID (should only be called on reliable messages).</summary>
        /// <param name="seqId">The sequence ID to insert.</param>
        internal void SetSequenceIdBytes(ushort seqId)
        {
            byte[] sequenceIdBytes = StandardizeEndianness(BitConverter.GetBytes(seqId));
            bytes[1] = sequenceIdBytes[0];
            bytes[2] = sequenceIdBytes[1];
        }

        /// <summary>Inserts the given bytes into the message at the given position.</summary>
        /// <param name="bytes">The bytes to insert.</param>
        /// <param name="position">The position at which to insert the bytes into the message.</param>
        internal void InsertBytes(byte[] bytes, int position = 0)
        {
            this.bytes.InsertRange(position, bytes);
        }

        /// <summary>Inserts the given byte at the start of the message.</summary>
        /// <param name="singleByte">The byte to insert.</param>
        private void InsertByte(byte singleByte)
        {
            bytes.Insert(0, singleByte);
        }

        /// <summary>Gets the message's content in array form.</summary>
        internal byte[] ToArray()
        {
            readableBytes = bytes.ToArray();
            return readableBytes;
        }

        /// <summary>Standardizes byte order across big and little endian systems by reversing the given bytes on big endian systems.</summary>
        /// <param name="value">The bytes whose order to standardize.</param>
        internal static byte[] StandardizeEndianness(byte[] value)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(value);
            return value;
        }
        /// <summary>Standardizes byte order across big and little endian systems by reversing byteAmount bytes at the message's current read position on big endian systems.</summary>
        /// <param name="byteAmount">The number of bytes whose order to standardize, starting at the message's current read position.</param>
        internal void StandardizeEndianness(int byteAmount)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(readableBytes, readPos, byteAmount);
        }
        #endregion

        #region Write & Read Data
        #region Byte
        /// <summary>Adds a byte to the message.</summary>
        /// <param name="value">The byte to add.</param>
        public void Add(byte value)
        {
            bytes.Add(value);
        }

        /// <summary>Adds an array of bytes to the message.</summary>
        /// <param name="value">The byte array to add.</param>
        public void Add(byte[] value)
        {
            bytes.AddRange(value);
        }

        /// <summary>Reads a byte from the message.</summary>
        public byte GetByte()
        {
            if (bytes.Count > readPos)
            {
                // If there are unread bytes
                byte value = readableBytes[readPos]; // Get the byte at readPos' position
                readPos += 1;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'byte'!");
        }

        /// <summary>Reads an array of bytes from the message.</summary>
        /// <param name="length">The length of the byte array.</param>
        public byte[] GetBytes(int length)
        {
            if (bytes.Count > readPos)
            {
                // If there are unread bytes
                byte[] value = bytes.GetRange(readPos, length).ToArray(); // Get the bytes at readPos' position with a range of length
                readPos += (ushort)length;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'byte[]'!");
            }
        }
        #endregion

        #region Bool
        /// <summary>Adds a bool to the message.</summary>
        /// <param name="value">The bool to add.</param>
        public void Add(bool value)
        {
            bytes.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a bool from the message.</summary>
        public bool GetBool()
        {
            if (bytes.Count >= readPos + boolLength)
            {
                // If there are unread bytes
                bool value = BitConverter.ToBoolean(readableBytes, readPos); // Convert the bytes at readPos' position to a bool
                readPos += boolLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'bool'!");
        }

        /// <summary>Adds an array of bools to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(bool[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of bools from the message.</summary>
        public bool[] GetBoolArray()
        {
            return GetBoolArray(GetUShort());
        }
        /// <summary>Reads an array of bools from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public bool[] GetBoolArray(ushort length)
        {
            bool[] array = new bool[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetBool();

            return array;
        }
        #endregion

        #region Short
        /// <summary>Adds a short to the message.</summary>
        /// <param name="value">The short to add.</param>
        public void Add(short value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads a short from the message.</summary>
        public short GetShort()
        {
            if (bytes.Count >= readPos + shortLength)
            {
                // If there are unread bytes
                StandardizeEndianness(shortLength);
                short value = BitConverter.ToInt16(readableBytes, readPos); // Convert the bytes at readPos' position to a short
                readPos += shortLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'short'!");
        }

        /// <summary>Adds an array of shorts to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(short[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of shorts from the message.</summary>
        public short[] GetShortArray()
        {
            return GetShortArray(GetUShort());
        }
        /// <summary>Reads an array of shorts from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public short[] GetShortArray(ushort length)
        {
            short[] array = new short[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetShort();

            return array;
        }
        #endregion

        #region UShort
        /// <summary>Adds a ushort to the message.</summary>
        /// <param name="value">The ushort to add.</param>
        public void Add(ushort value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads a ushort from the message.</summary>
        public ushort GetUShort()
        {
            if (bytes.Count >= readPos + shortLength)
            {
                // If there are unread bytes
                StandardizeEndianness(shortLength);
                ushort value = BitConverter.ToUInt16(readableBytes, readPos); // Convert the bytes at readPos' position to a ushort
                readPos += shortLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'ushort'!");
        }

        /// <summary>Reads a ushort from the message without moving the read position, allowing the same bytes to be read again.</summary>
        internal ushort PeekUShort()
        {
            if (bytes.Count > readPos)
            {
                // If there are unread bytes
                byte[] bytesToConvert = new byte[shortLength];
                Array.Copy(readableBytes, readPos, bytesToConvert, 0, shortLength);
                return BitConverter.ToUInt16(StandardizeEndianness(bytesToConvert), 0); // Convert the bytes at readPos' position to a ushort
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'ushort'!");
        }

        /// <summary>Adds an array of ushorts to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(ushort[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of ushorts from the message.</summary>
        public ushort[] GetUShortArray()
        {
            return GetUShortArray(GetUShort());
        }
        /// <summary>Reads an array of ushorts from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public ushort[] GetUShortArray(ushort length)
        {
            ushort[] array = new ushort[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetUShort();
            
            return array;
        }
        #endregion

        #region Int
        /// <summary>Adds an int to the message.</summary>
        /// <param name="value">The int to add.</param>
        public void Add(int value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads an int from the message.</summary>
        public int GetInt()
        {
            if (bytes.Count >= readPos + intLength)
            {
                // If there are unread bytes
                StandardizeEndianness(intLength);
                int value = BitConverter.ToInt32(readableBytes, readPos); // Convert the bytes at readPos' position to an int
                readPos += intLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'int'!");
        }

        /// <summary>Adds an array of ints to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(int[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of ints from the message.</summary>
        public int[] GetIntArray()
        {
            return GetIntArray(GetUShort());
        }
        /// <summary>Reads an array of ints from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public int[] GetIntArray(ushort length)
        {
            int[] array = new int[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetInt();

            return array;
        }
        #endregion

        #region UInt
        /// <summary>Adds a uint to the message.</summary>
        /// <param name="value">The uint to add.</param>
        public void Add(uint value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads a uint from the message.</summary>
        public uint GetUInt()
        {
            if (bytes.Count >= readPos + intLength)
            {
                // If there are unread bytes
                StandardizeEndianness(intLength);
                uint value = BitConverter.ToUInt32(readableBytes, readPos); // Convert the bytes at readPos' position to an uint
                readPos += intLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'uint'!");
        }

        /// <summary>Adds an array of uints to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(uint[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of uints from the message.</summary>
        public uint[] GetUIntArray()
        {
            return GetUIntArray(GetUShort());
        }
        /// <summary>Reads an array of uints from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public uint[] GetUIntArray(ushort length)
        {
            uint[] array = new uint[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetUInt();

            return array;
        }
        #endregion

        #region Long
        /// <summary>Adds a long to the message.</summary>
        /// <param name="value">The long to add.</param>
        public void Add(long value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads a long from the message.</summary>
        public long GetLong()
        {
            if (bytes.Count >= readPos + longLength)
            {
                // If there are unread bytes
                StandardizeEndianness(longLength);
                long value = BitConverter.ToInt64(readableBytes, readPos); // Convert the bytes at readPos' position to a long
                readPos += longLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'long'!");
        }

        /// <summary>Adds an array of longs to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(long[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of longs from the message.</summary>
        public long[] GetLongArray()
        {
            return GetLongArray(GetUShort());
        }
        /// <summary>Reads an array of longs from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public long[] GetLongArray(ushort length)
        {
            long[] array = new long[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetLong();

            return array;
        }
        #endregion

        #region ULong
        /// <summary>Adds a ulong to the message.</summary>
        /// <param name="value">The ulong to add.</param>
        public void Add(ulong value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads a ulong from the message.</summary>
        public ulong GetULong()
        {
            if (bytes.Count >= readPos + longLength)
            {
                // If there are unread bytes
                StandardizeEndianness(longLength);
                ulong value = BitConverter.ToUInt64(readableBytes, readPos); // Convert the bytes at readPos' position to a ulong
                readPos += longLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'ulong'!");
        }

        /// <summary>Adds an array of ulongs to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(ulong[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of ulongs from the message.</summary>
        public ulong[] GetULongArray()
        {
            return GetULongArray(GetUShort());
        }
        /// <summary>Reads an array of ulongs from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public ulong[] GetULongArray(ushort length)
        {
            ulong[] array = new ulong[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetULong();

            return array;
        }
        #endregion

        #region Float
        /// <summary>Adds a float to the message.</summary>
        /// <param name="value">The float to add.</param>
        public void Add(float value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads a float from the message.</summary>
        public float GetFloat()
        {
            if (bytes.Count >= readPos + floatLength)
            {
                // If there are unread bytes
                StandardizeEndianness(floatLength);
                float value = BitConverter.ToSingle(readableBytes, readPos); // Convert the bytes at readPos' position to a float
                readPos += floatLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'float'!");
        }

        /// <summary>Adds an array of floats to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(float[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of floats from the message.</summary>
        public float[] GetFloatArray()
        {
            return GetFloatArray(GetUShort());
        }
        /// <summary>Reads an array of floats from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public float[] GetFloatArray(ushort length)
        {
            float[] array = new float[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetFloat();

            return array;
        }
        #endregion

        #region Double
        /// <summary>Adds a double to the message.</summary>
        /// <param name="value">The double to add.</param>
        public void Add(double value)
        {
            bytes.AddRange(StandardizeEndianness(BitConverter.GetBytes(value)));
        }

        /// <summary>Reads a double from the message.</summary>
        public double GetDouble()
        {
            if (bytes.Count >= readPos + doubleLength)
            {
                // If there are unread bytes
                StandardizeEndianness(doubleLength);
                double value = BitConverter.ToDouble(readableBytes, readPos); // Convert the bytes at readPos' position to a double
                readPos += doubleLength;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'double'!");
        }

        /// <summary>Adds an array of doubles to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(double[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of doubles from the message.</summary>
        public double[] GetDoubleArray()
        {
            return GetDoubleArray(GetUShort());
        }
        /// <summary>Reads an array of doubles from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public double[] GetDoubleArray(ushort length)
        {
            double[] array = new double[length];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = GetDouble();
            }
            return array;
        }
        #endregion

        #region String
        /// <summary>Adds a string to the message.</summary>
        /// <param name="value">The string to add.</param>
        public void Add(string value)
        {
            Add((ushort)value.Length); // Add the length of the string to the message
            Add(Encoding.UTF8.GetBytes(value)); // Add the string itself
        }

        /// <summary>Reads a string from the message.</summary>
        public string GetString()
        {
            ushort length = GetUShort(); // Get the length of the string
            if (bytes.Count >= readPos + length)
            {
                string value = Encoding.UTF8.GetString(readableBytes, readPos, length); // Convert the bytes at readPos' position to a string
                readPos += length;
                return value;
            }
            else
                throw new Exception("Message contains insufficient bytes to read type 'string'!");
        }

        /// <summary>Adds an array of strings to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(string[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of strings from the message.</summary>
        public string[] GetStringArray()
        {
            return GetStringArray(GetUShort());
        }
        /// <summary>Reads an array of strings from the message.</summary>
        /// <param name="length">The length of the array.</param>
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
