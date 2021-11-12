using System;
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
    public enum HeaderType : byte
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

    /// <summary>Provides functionality for converting data to bytes and vice versa.</summary>
    public class Message
    {
        /// <summary>How many messages to add to the pool for each <see cref="Server"/> or <see cref="Client"/> instance that is started.</summary>
        /// <remarks>Changes will not affect <see cref="Server"/> and <see cref="Client"/> instances which are already running until they are restarted.</remarks>
        public static byte InstancesPerSocket { get; set; } = 4;
        /// <summary>A pool of reusable message instances.</summary>
        private static readonly List<Message> pool = new List<Message>();

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


        /// <summary>The message's send mode.</summary>
        public MessageSendMode SendMode { get; private set; }
        /// <summary>The message's data.</summary>
        public byte[] Bytes { get; private set; }
        /// <summary>The length in bytes of the data that can be read from the message.</summary>
        public int ReadableLength { get; private set; }
        /// <summary>The length in bytes of the unread data contained in the message.</summary>
        public int UnreadLength => ReadableLength - readPos;
        /// <summary>The length in bytes of the data that has been written to the message.</summary>
        public int WrittenLength => writePos;
        /// <summary>How many more bytes can be written into the packet.</summary>
        internal int UnwrittenLength => Bytes.Length - writePos;

        /// <summary>The position in the byte array that the next bytes will be written to.</summary>
        private ushort writePos = 0;
        /// <summary>The position in the byte array that the next bytes will be read from.</summary>
        private ushort readPos = 0;

        /// <summary>Initializes a reusable Message instance.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        internal Message(ushort maxSize = 1280)
        {
            Bytes = new byte[maxSize];
        }

        /// <summary>Initializes a reusable Message instance with a pre-defined header type.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        /// <param name="headerType">The header type to initialize the message with.</param>
        internal Message(HeaderType headerType, ushort maxSize = 1280)
        {
            Bytes = new byte[maxSize];

            Add((byte)headerType);
            if (SendMode == MessageSendMode.reliable)
                writePos += shortLength;
        }

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

        /// <summary>Gets a Message instance that can be used for sending.</summary>
        /// <param name="sendMode">The mode in which the message should be sent.</param>
        /// <param name="id">The message ID.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        public static Message Create(MessageSendMode sendMode, ushort id)
        {
            return RetrieveFromPool().Reinitialize((HeaderType)sendMode).Add(id);
        }

        /// <summary>Gets a Message instance that can be used for sending.</summary>
        /// <param name="headerType">The message's header type.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        internal static Message Create(HeaderType headerType)
        {
            return RetrieveFromPool().Reinitialize(headerType);
        }

        /// <summary>Gets a Message instance that can be used for handling.</summary>
        /// <param name="headerType">The message's header type.</param>
        /// <param name="data">The bytes contained in the message.</param>
        /// <returns>A message instance ready to be used for handling.</returns>
        public static Message Create(HeaderType headerType, byte[] data)
        {
            return RetrieveFromPool().Reinitialize(headerType, data);
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
        #endregion
        
        #region Functions
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

        /// <summary>Reinitializes a message for sending.</summary>
        /// <param name="headerType">The message's header type.</param>
        internal Message Reinitialize(HeaderType headerType)
        {
            SendMode = headerType >= HeaderType.reliable ? MessageSendMode.reliable : MessageSendMode.unreliable;
            writePos = 0;
            readPos = 0;
            ReadableLength = 0;
            Add((byte)headerType);
            if (SendMode == MessageSendMode.reliable)
                writePos += shortLength;

            return this;
        }

        /// <summary>Reinitializes a message for handling.</summary>
        /// <param name="headerType">The message's header type.</param>
        /// <param name="data">The bytes contained in the message.</param>
        internal Message Reinitialize(HeaderType headerType, byte[] data)
        {
            SendMode = headerType >= HeaderType.reliable ? MessageSendMode.reliable : MessageSendMode.unreliable;
            writePos = (ushort)data.Length;
            readPos = (ushort)(SendMode == MessageSendMode.reliable ? 3 : 1);

            if (data.Length > Bytes.Length)
            {
                RiptideLogger.Log("ERROR", $"Can't fully handle {data.Length} bytes because it exceeds the maximum of {Bytes.Length}, message will contain incomplete data!");
                Array.Copy(data, 0, Bytes, 0, Bytes.Length);
                ReadableLength = Bytes.Length;
            }
            else
            {
                Array.Copy(data, 0, Bytes, 0, data.Length);
                ReadableLength = data.Length;
            }

            return this;
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
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The Message instance that the <see cref="byte"/> array was added to.</returns>
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
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte[]', array will contain default elements!");
                amount = UnreadLength;
            }

            Array.Copy(Bytes, readPos, array, startIndex, amount); // Copy the bytes at readPos' position to the array that will be returned
            readPos += (ushort)amount;
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
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The Message instance that the <see cref="bool"/> array was added to.</returns>
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
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool[]', array will contain default elements!");
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
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool[]', array will contain default elements!");

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
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The Message instance that the <see cref="short"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * shortLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

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
        /// <returns>The Message instance that the <see cref="ushort"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * shortLength)
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
            if (UnreadLength < amount * shortLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'short[]', array will contain default elements!");
                amount = UnreadLength / shortLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = (short)ReadUShort();
        }

        /// <summary>Reads a number of ushorts from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ushorts to read.</param>
        /// <param name="array">The array to write the ushorts into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadUShorts(int amount, ushort[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * shortLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort[]', array will contain default elements!");
                amount = UnreadLength / shortLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = ReadUShort();
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
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The Message instance that the <see cref="int"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * intLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

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
        /// <returns>The Message instance that the <see cref="uint"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * intLength)
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
            if (UnreadLength < amount * intLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'int[]', array will contain default elements!");
                amount = UnreadLength / intLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = ReadInt();
        }

        /// <summary>Reads a number of uints from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of uints to read.</param>
        /// <param name="array">The array to write the uints into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadUInts(int amount, uint[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * intLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint[]', array will contain default elements!");
                amount = UnreadLength / intLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = (uint)ReadInt();
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

            return ReadLong();
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
            
            return (ulong)ReadLong();
        }

        /// <summary>Retrieves a <see cref="long"/> from the next 8 bytes, starting at the read position.</summary>
        /// <returns>The converted <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadLong()
        {
            // Convert the bytes at readPos' position to a ulong
#if BIG_ENDIAN
            Array.Reverse(Bytes, readPos, longLength);
#endif
            long value = BitConverter.ToInt64(Bytes, readPos);
            readPos += longLength;
            return value;
        }

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
        /// <returns>The Message instance that the <see cref="long"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * longLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

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
        /// <returns>The Message instance that the <see cref="ulong"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * longLength)
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
            if (UnreadLength < amount * longLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'long[]', array will contain default elements!");
                amount = UnreadLength / longLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = ReadLong();
        }

        /// <summary>Reads a number of ulongs from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ulongs to read.</param>
        /// <param name="array">The array to write the ulongs into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="array"/>.</param>
        private void ReadULongs(int amount, ulong[] array, int startIndex = 0)
        {
            if (UnreadLength < amount * longLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong[]', array will contain default elements!");
                amount = UnreadLength / longLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = (ulong)ReadLong();
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

            return ReadFloat();
        }

        /// <summary>Retrieves a <see cref="float"/> from the next 4 bytes, starting at the read position.</summary>
        /// <returns>The converted <see cref="float"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadFloat()
        {
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
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The Message instance that the <see cref="float"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * floatLength)
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
            if (UnreadLength < amount * floatLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'float[]', array will contain default elements!");
                amount = UnreadLength / floatLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = ReadFloat();
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

            return ReadDouble();
        }

        /// <summary>Retrieves a <see cref="double"/> from the next 8 bytes, starting at the read position.</summary>
        /// <returns>The converted <see cref="double"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double ReadDouble()
        {
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
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The Message instance that the <see cref="double"/> array was added to.</returns>
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

            if (UnwrittenLength < array.Length * doubleLength)
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
            if (UnreadLength < amount * doubleLength)
            {
                RiptideLogger.Log("ERROR", $"Message contains insufficient unread bytes ({UnreadLength}) to read type 'double[]', array will contain default elements!");
                amount = UnreadLength / doubleLength;
            }

            for (int i = 0; i < amount; i++)
                array[startIndex + i] = ReadDouble();
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
        /// <param name="isBigArray">
        ///   Whether or not the array being added has more than 255 elements. Does nothing if <paramref name="includeLength"/> is set to <see langword="false"/>.
        ///   <para>
        ///     Writes the length using 2 bytes (<see cref="ushort"/>) if <see langword="true"/>.<br/>
        ///     Writes the length using 1 <see cref="byte"/> if <see langword="false"/>.
        ///   </para>
        /// </param>
        /// <returns>The Message instance that the <see cref="string"/> array was added to.</returns>
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

    [StructLayout(LayoutKind.Explicit)]
    struct FloatConverter
    {
        [FieldOffset(0)] public byte byte0;
        [FieldOffset(1)] public byte byte1;
        [FieldOffset(2)] public byte byte2;
        [FieldOffset(3)] public byte byte3;

        [FieldOffset(0)] public float floatValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct DoubleConverter
    {
        [FieldOffset(0)] public byte byte0;
        [FieldOffset(1)] public byte byte1;
        [FieldOffset(2)] public byte byte2;
        [FieldOffset(3)] public byte byte3;
        [FieldOffset(4)] public byte byte4;
        [FieldOffset(5)] public byte byte5;
        [FieldOffset(6)] public byte byte6;
        [FieldOffset(7)] public byte byte7;

        [FieldOffset(0)] public double doubleValue;
    }
}
