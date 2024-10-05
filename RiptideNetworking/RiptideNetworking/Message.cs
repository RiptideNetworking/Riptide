// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Riptide
{
    /// <summary>The send mode of a <see cref="Message"/>.</summary>
    public enum MessageSendMode : byte
    {
        /// <summary>Guarantees order but not delivery. Notifies the sender of what happened via the <see cref="Connection.NotifyDelivered"/> and <see cref="Connection.NotifyLost"/>
        /// events. The receiver must handle notify messages via the <see cref="Connection.NotifyReceived"/> event, <i>which is different from the other two send modes</i>.</summary>
        Notify = MessageHeader.Notify,
        /// <summary>Guarantees neither delivery nor order.</summary>
        Unreliable = MessageHeader.Unreliable,
        /// <summary>Guarantees delivery but not order.</summary>
        Reliable = MessageHeader.Reliable,
		/// <summary>Guarantees both delivery and order and doesn't give up until the connection gives up.</summary>
		Queued = MessageHeader.Queued
    }

    /// <summary>Provides functionality for converting data to bytes and vice versa.</summary>
    public class Message
    {
        /// <summary>The maximum number of bits required for a message's header.</summary>
        public const int MaxHeaderSize = NotifyHeaderBits;
        /// <summary>The number of bits used by the <see cref="MessageHeader"/>.</summary>
        internal const int HeaderBits = 4;
        /// <summary>A bitmask that, when applied, only keeps the bits corresponding to the <see cref="MessageHeader"/> value.</summary>
        internal const byte HeaderBitmask = (1 << HeaderBits) - 1;
        /// <summary>The header size for unreliable messages. Does not count the 2 bytes used for the message ID.</summary>
        /// <remarks>4 bits - header.</remarks>
        internal const int UnreliableHeaderBits = HeaderBits;
        /// <summary>The header size for reliable messages. Does not count the 2 bytes used for the message ID.</summary>
        /// <remarks>4 bits - header, 16 bits - sequence ID.</remarks>
        internal const int ReliableHeaderBits = HeaderBits + 2 * BitsPerByte;
		/// <summary>The header size for queued messages. Does not count the 2 bytes used for the message ID.</summary>
        /// <remarks>4 bits - header, 16 bits - sequence ID.</remarks>
		internal const int QueuedHeaderBits = HeaderBits + 2 * BitsPerByte;
        /// <summary>The header size for notify messages.</summary>
        /// <remarks>4 bits - header, 24 bits - ack, 16 bits - sequence ID.</remarks>
        internal const int NotifyHeaderBits = HeaderBits + 5 * BitsPerByte;
        /// <summary>The minimum number of bytes contained in an unreliable message.</summary>
        internal const int MinUnreliableBytes = UnreliableHeaderBits / BitsPerByte + (UnreliableHeaderBits % BitsPerByte == 0 ? 0 : 1);
        /// <summary>The minimum number of bytes contained in a reliable message.</summary>
        internal const int MinReliableBytes = ReliableHeaderBits / BitsPerByte + (ReliableHeaderBits % BitsPerByte == 0 ? 0 : 1);
        /// <summary>The minimum number of bytes contained in a notify message.</summary>
        internal const int MinNotifyBytes = NotifyHeaderBits / BitsPerByte + (NotifyHeaderBits % BitsPerByte == 0 ? 0 : 1);
        /// <summary>The number of bits in a byte.</summary>
        private const int BitsPerByte = Converter.BitsPerByte;
        /// <summary>The number of bits in each data segment.</summary>
        private const int BitsPerSegment = Converter.BitsPerULong;

        /// <summary>The maximum number of bytes that a message can contain, including the <see cref="MaxHeaderSize"/>.</summary>
        public static int MaxSize { get; private set; }
        /// <summary>The maximum number of bytes of payload data that a message can contain. This value represents how many bytes can be added to a message <i>on top of</i> the <see cref="MaxHeaderSize"/>.</summary>
        public static int MaxPayloadSize
        {
            get => MaxSize - (MaxHeaderSize / BitsPerByte + (MaxHeaderSize % BitsPerByte == 0 ? 0 : 1));
            set
            {
                if (Peer.ActiveCount > 0)
                    throw new InvalidOperationException($"Changing the '{nameof(MaxPayloadSize)}' is not allowed while a {nameof(Server)} or {nameof(Client)} is running!");

                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), $"'{nameof(MaxPayloadSize)}' cannot be negative!");

                MaxSize = MaxHeaderSize / BitsPerByte + (MaxHeaderSize % BitsPerByte == 0 ? 0 : 1) + value;
                maxByteCount = MaxSize;
                maxArraySize = MaxSize / sizeof(ulong) + (MaxSize % sizeof(ulong) == 0 ? 0 : 1);
                ByteBuffer = new byte[MaxSize];
                PendingMessage.ClearPool();
            }
        }
        /// <summary>An intermediary buffer to help convert <see cref="data"/> to a byte array when sending.</summary>
        internal static byte[] ByteBuffer;
        /// <summary>The maximum number of bits a message can contain.</summary>
        private static int maxByteCount;
        /// <summary>The maximum size of the <see cref="data"/> array.</summary>
        private static int maxArraySize; // TODO implement along with standard init size

        static Message()
        {
            MaxSize = MaxHeaderSize / BitsPerByte + (MaxHeaderSize % BitsPerByte == 0 ? 0 : 1) + 1225;
            maxByteCount = MaxSize;
            maxArraySize = MaxSize / sizeof(ulong) + (MaxSize % sizeof(ulong) == 0 ? 0 : 1);
            ByteBuffer = new byte[MaxSize];
        }

		/// <summary>The maximum value of Id.</summary>
		public static ushort MaxId { private get; set; } = ushort.MaxValue;
		/// <summary>The sequence Id of the message, assuming it has one.</summary>
		internal ushort SequenceId
        {
			get
			{
				PeekBits(sizeof(ushort) * Converter.BitsPerByte, HeaderBits, out ushort sequenceId);
				return sequenceId;
			}
			set => SetBits(value, sizeof(ushort) * Converter.BitsPerByte, Message.HeaderBits);
		}
		/// <summary>Wether anything has been read from the message.</summary>
		public bool HasReadNothing => writeValue.HasReadNothing;
        /// <summary>How many of this message's bytes are in use. Rounds up to the next byte because only whole bytes can be sent.</summary>
        public int BytesInUse => data.GetBytesInUse();
        /// <inheritdoc cref="data"/>
        internal ulong[] Data => data.GetData(); // TODO check for no bad writes

		/// <summary>The message's send mode.</summary>
        public MessageSendMode SendMode { get; private set; }
        /// <summary>The message's data.</summary>
        private FastBigInt data;
		/// <summary>The mult for new values.</summary>
		private FastBigInt writeValue;

        /// <summary>Initializes a reusable <see cref="Message"/> instance.</summary>
        private Message() {
			data = new FastBigInt(maxArraySize);
			writeValue = new FastBigInt(maxArraySize, 1);
		}

        /// <summary>Creates a message that can be used for receiving/handling.</summary>
        /// <param name="bytes">The bytes of the received data.</param>
        /// <param name="contentLength">The number of bytes which this message will contain.</param>
        /// <returns>The message, ready to be used for handling.</returns>
        internal static Message Create(byte[] bytes, int contentLength) {
            Message msg = new Message {
                data = new FastBigInt(contentLength, bytes),
                writeValue = new FastBigInt(contentLength, 1)
            };
            return msg;
		}
        /// <summary>Gets a message instance that can be used for sending.</summary>
        /// <param name="sendMode">The mode in which the message should be sent.</param>
        /// <returns>A message instance ready to be sent.</returns>
        /// <remarks>This method is primarily intended for use with <see cref="MessageSendMode.Notify"/> as notify messages don't have a built-in message ID, and unlike
        /// <see cref="Create(MessageSendMode, ushort)"/> and <see cref="Create(MessageSendMode, Enum)"/>, this overload does not add a message ID to the message.</remarks>
        public static Message Create(MessageSendMode sendMode)
        {
            return Create((MessageHeader)sendMode);
        }
        /// <summary>Gets a message instance that can be used for sending.</summary>
        /// <param name="sendMode">The mode in which the message should be sent.</param>
        /// <param name="id">The message ID.</param>
        /// <returns>A message instance ready to be sent.</returns>
        public static Message Create(MessageSendMode sendMode, ushort id)
        {
            return Create(sendMode).AddUShort(id, 0, MaxId);
        }
        /// <inheritdoc cref="Create(MessageSendMode, ushort)"/>
        /// <remarks>NOTE: <paramref name="id"/> will be cast to a <see cref="ushort"/>. You should ensure that its value never exceeds that of <see cref="ushort.MaxValue"/>, otherwise you'll encounter unexpected behaviour when handling messages.</remarks>
        public static Message Create(MessageSendMode sendMode, Enum id)
        {
            return Create(sendMode, (ushort)(object)id);
        }
        /// <summary>Gets a message instance that can be used for sending.</summary>
        /// <param name="header">The message's header type.</param>
        /// <returns>A message instance ready to be sent.</returns>
        internal static Message Create(MessageHeader header)
        {
            return new Message().Init(header);
        }

		/// <summary>Logs info of the message</summary>
		public void LogStuff(string added = "") {
			RiptideLogger.Log(LogType.Info, data.ToStringBinary() + "\n"
				+ writeValue.ToStringBinary() + "\nSendMode: " + SendMode + "\n" + added);
		}

        #region Functions
        /// <summary>Initializes the message so that it can be used for sending.</summary>
        /// <param name="header">The message's header type.</param>
        /// <returns>The message, ready to be used for sending.</returns>
        private Message Init(MessageHeader header) {
            SetHeader(header);
            return this;
        }

        /// <summary>Sets the message's header bits to the given <paramref name="header"/> and determines the appropriate <see cref="MessageSendMode"/> and read/write positions.</summary>
        /// <param name="header">The header to use for this message.</param>
        private void SetHeader(MessageHeader header)
        {
			AddByte((byte)header, 0, 15);
            if (header == MessageHeader.Notify)
				SetHeaderRoom(MessageSendMode.Notify, NotifyHeaderBits - HeaderBits);
			else if (header == MessageHeader.Queued)
				SetHeaderRoom(MessageSendMode.Queued, QueuedHeaderBits - HeaderBits);
            else if (header >= MessageHeader.Reliable)
				SetHeaderRoom(MessageSendMode.Reliable, ReliableHeaderBits - HeaderBits);
            else SetHeaderRoom(MessageSendMode.Unreliable, UnreliableHeaderBits - HeaderBits);
        }

		/// <summary>Adds room for stuff like sequenceId.</summary>
		/// <param name="sendMode">The send mode of this message.</param>
		/// <param name="headerBits">The amount of header bits, that the specific send mode needs.</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		private void SetHeaderRoom(MessageSendMode sendMode, byte headerBits) {
			if(headerBits >= 64) throw new ArgumentOutOfRangeException(nameof(headerBits), "Header bits must be less than 64");
			AddBits(0, headerBits);
			SendMode = sendMode;
		}

		/// <summary>Removes the header of a message.</summary>
		/// <remarks>Makes the message unsendable but allows you to use Get methods.</remarks>
		public void RemoveHeader() {
			MessageHeader header = (MessageHeader)GetBits(HeaderBits);
			if(header == MessageHeader.Notify)
				GetBits(NotifyHeaderBits - HeaderBits);
			else if(header == MessageHeader.Queued)
				GetBits(QueuedHeaderBits - HeaderBits);
			else if(header >= MessageHeader.Reliable)
				GetBits(ReliableHeaderBits - HeaderBits);
			else GetBits(UnreliableHeaderBits - HeaderBits);
			GetUShort(0, MaxId);
		}

		internal Message GetInfo(out MessageHeader header) {
			header = (MessageHeader)GetByte(0, 15);
			switch(header) {
				case MessageHeader.Notify: SendMode = MessageSendMode.Notify; break;
				case MessageHeader.Reliable: SendMode = MessageSendMode.Reliable; break;
				case MessageHeader.Queued: SendMode = MessageSendMode.Queued; break;
				default: SendMode = MessageSendMode.Unreliable; break;
			}
			return this;
		}
        #endregion

        #region Add & Retrieve Data
        #region Message
        /// <summary>Adds <paramref name="message"/>'s unread bits to the message.</summary>
        /// <param name="message">The message whose unread bits to add.</param>
        /// <returns>The message that the bits were added to.</returns>
        /// <remarks>This method does not move <paramref name="message"/>'s internal read position!</remarks>
        public Message AddMessage(Message message)
			=> AddMessage(message, message.BytesInUse);
        /// <summary>Adds a range of bits from <paramref name="message"/> to the message.</summary>
        /// <param name="message">The message whose bits to add.</param>
        /// <param name="amount">The number of bytes to add.</param>
        /// <returns>The message that the bits were added to.</returns>
        /// <remarks>This method does not move <paramref name="message"/>'s internal read position!</remarks>
        public Message AddMessage(Message message, int amount)
        {
			Message msg = message.Copy();

			for(int i = 0; i < amount; i++)
				AddByte(msg.GetByte());
			
			return this;
        }
        #endregion

        #region Bits
		/// <summary>Sets bits to the message.</summary>
		/// <param name="bitfield">The bitfield to add.</param>
		/// <param name="amount">The amount of bits.</param>
		/// <returns></returns>
		public Message AddBits(ulong bitfield, int amount) {
			if(amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), $"'{nameof(amount)}' cannot be negative!");
			if(amount > sizeof(ulong) * BitsPerByte) throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot add more than {sizeof(ulong) * BitsPerByte} bits at a time!");
			ulong mask = (1UL << amount) - 1;
			bitfield &= mask;
			AddULong(bitfield, 0, mask);
			return this;
		}
		/// <summary>Gets bits from a message.</summary>
		/// <param name="amount">The amount of bits.</param>
		/// <returns></returns>
		public ulong GetBits(int amount) {
			if(amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), $"'{nameof(amount)}' cannot be negative!");
			if(amount > sizeof(ulong) * BitsPerByte) throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot get more than {sizeof(ulong) * BitsPerByte} bits at a time!");
			return GetULong(0, (1UL << amount) - 1);
		}
        /// <summary>Sets up to 64 bits at the specified position in the message.</summary>
        /// <param name="bitfield">The bits to write into the message.</param>
        /// <param name="amount">The number of bits to set.</param>
        /// <param name="startBit">The bit position in the message at which to start writing.</param>
        /// <returns>The message instance.</returns>
        /// <remarks>This method can be used to directly set a range of bits anywhere in the message without moving its internal write position. Data which was previously added to
        /// the message and which falls within the range of bits being set will be <i>overwritten</i>, meaning that improper use of this method will likely corrupt the message!</remarks>
        public Message SetBits(ulong bitfield, int amount, int startBit)
        {
            if (amount > sizeof(ulong) * BitsPerByte)
                throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot set more than {sizeof(ulong) * BitsPerByte} bits at a time!");

            Converter.SetBits(bitfield, amount, data.GetData(), startBit);
            return this;
        }

        /// <summary>Retrieves up to 8 bits from the specified position in the message.</summary>
        /// <param name="amount">The number of bits to peek.</param>
        /// <param name="startBit">The bit position in the message at which to start peeking.</param>
        /// <param name="bitfield">The bits that were retrieved.</param>
        /// <returns>The message instance.</returns>
        /// <remarks>This method can be used to retrieve a range of bits from anywhere in the message without moving its internal read position.</remarks>
        internal Message PeekBits(int amount, int startBit, out byte bitfield)
        {
            if (amount > BitsPerByte)
                throw new ArgumentOutOfRangeException(nameof(amount), $"This '{nameof(PeekBits)}' overload cannot be used to peek more than {BitsPerByte} bits at a time!");

            Converter.GetBits(amount, data.GetData(), startBit, out bitfield);
            return this;
        }
        /// <summary>Retrieves up to 16 bits from the specified position in the message.</summary>
        /// <inheritdoc cref="PeekBits(int, int, out byte)"/>
        internal Message PeekBits(int amount, int startBit, out ushort bitfield)
        {
            if (amount > sizeof(ushort) * BitsPerByte)
                throw new ArgumentOutOfRangeException(nameof(amount), $"This '{nameof(PeekBits)}' overload cannot be used to peek more than {sizeof(ushort) * BitsPerByte} bits at a time!");

            Converter.GetBits(amount, data.GetData(), startBit, out bitfield);
            return this;
        }
        #endregion

        #region Varint
		/// <summary>Copies a message.</summary>
		/// <remarks>Useful for saving a recieved message,
		/// that would otherwhise be returned to the pool.
		/// <para>Recieved messages should not be written to and
		/// can break if their data is not purely whole bytes.</para></remarks>
		/// <returns>The copy of the message.</returns>
		public Message Copy() {
            Message message = new Message {
                SendMode = SendMode,
				data = data.Copy(),
                writeValue = writeValue.Copy(),
            };
			return message;
        }

		internal ushort GetMessageID() => GetUShort(0, MaxId);

		/// <summary>Creates a QueuedAck message containing sequence ID.</summary>
		/// <param name="sequenceId">The sequence id to queue.</param>
		/// <returns>The new message.</returns>
		internal static Message QueuedAck(ushort sequenceId) {
            Message message = new Message().Init(MessageHeader.QueuedAck);
			message.AddUShort(sequenceId);
			return message;
        }

        /// <summary>Adds a positive or negative number to the message, using fewer bits for smaller values.</summary>
        /// <inheritdoc cref="AddVarULong(ulong)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message AddVarLong(long value) => AddVarULong((ulong)Converter.ZigZagEncode(value));
        /// <summary>Adds a positive number to the message, using fewer bits for smaller values.</summary>
        /// <param name="value">The value to add.</param>
        /// <returns>The message that the value was added to.</returns>
        /// <remarks>The value is added in segments of 8 bits, 1 of which is used to indicate whether or not another segment follows. As a result, small values are
        /// added to the message using fewer bits, while large values will require a few more bits than they would if they were added via <see cref="AddByte(byte, byte, byte)"/>,
        /// <see cref="AddUShort(ushort, ushort, ushort)"/>, <see cref="AddUInt(uint, uint, uint)"/>, or <see cref="AddULong(ulong, ulong, ulong)"/> (or their signed counterparts).</remarks>
        public Message AddVarULong(ulong value)
        {
            do
            {
                byte byteValue = (byte)(value & 0b_0111_1111);
                value >>= 7;
                if (value != 0) // There's more to write
                    byteValue |= 0b_1000_0000;

                AddByte(byteValue);
            }
            while (value != 0);

            return this;
        }

        /// <summary>Retrieves a positive or negative number from the message, using fewer bits for smaller values.</summary>
        /// <inheritdoc cref="GetVarULong()"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetVarLong() => Converter.ZigZagDecode((long)GetVarULong());
        /// <summary>Retrieves a positive number from the message, using fewer bits for smaller values.</summary>
        /// <returns>The value that was retrieved.</returns>
        /// <remarks>The value is retrieved in segments of 8 bits, 1 of which is used to indicate whether or not another segment follows. As a result, small values are
        /// retrieved from the message using fewer bits, while large values will require a few more bits than they would if they were retrieved via <see cref="GetByte(byte, byte)"/>,
        /// <see cref="GetUShort(ushort, ushort)"/>, <see cref="GetUInt(uint, uint)"/>, or <see cref="GetULong(ulong, ulong)"/> (or their signed counterparts).</remarks>
        public ulong GetVarULong()
        {
            ulong byteValue;
            ulong value = 0;
            int shift = 0;

            do
            {
                byteValue = GetByte();
                value |= (byteValue & 0b_0111_1111) << shift;
                shift += 7;
            }
            while ((byteValue & 0b_1000_0000) != 0);

            return value;
        }
        #endregion

        #region Byte & SByte
		/// <summary>Adds an <see cref="byte"/> to the message.</summary>
        /// <param name="value">The <see cref="byte"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="byte"/> was added to.</returns>
		public Message AddByte(byte value, byte min = byte.MinValue, byte max = byte.MaxValue) {
			return AddULong(value, min, max);
		}

		/// <summary>Retrieves an <see cref="byte"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The <see cref="byte"/> that was retrieved.</returns>
		public byte GetByte(byte min = byte.MinValue, byte max = byte.MaxValue) {
			return (byte)GetULong(min, max);
		}

		/// <summary>Adds an <see cref="sbyte"/> to the message.</summary>
        /// <param name="value">The <see cref="sbyte"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="sbyte"/> was added to.</returns>
		public Message AddSByte(sbyte value, sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue)
			=> AddByte(value.Conv(), min.Conv(), max.Conv());

		/// <summary>Retrieves an <see cref="sbyte"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The <see cref="sbyte"/> that was retrieved.</returns>
		public sbyte GetSByte(sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue) {
			return (sbyte)GetByte(min.Conv(), max.Conv());
		}

        /// <summary>Adds a <see cref="byte"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddBytes(byte[] array, bool includeLength = true, byte min = byte.MinValue, byte max = byte.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

			for (int i = 0; i < array.Length; i++)
				AddByte(array[i], min, max);

            return this;
        }

        /// <summary>Adds a <see cref="byte"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="startIndex">The position at which to start adding from the array.</param>
        /// <param name="amount">The amount of bytes to add from the startIndex of the array.</param>
        /// <param name="includeLength">Whether or not to include the <paramref name="amount"/> in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddBytes(byte[] array, int startIndex, int amount, bool includeLength = true, byte min = byte.MinValue, byte max = byte.MaxValue)
        {
            if (startIndex < 0 || startIndex >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            if (startIndex + amount > array.Length)
                throw new ArgumentException(nameof(amount), $"The source array is not long enough to read {amount} {Helper.CorrectForm(amount, ByteName)} starting at {startIndex}!");

            if (includeLength)
                AddVarULong((uint)amount);

			for (int i = startIndex; i < startIndex + amount; i++)
			{
				AddByte(array[i], min, max);
			}

            return this;
        }

        /// <summary>Adds an <see cref="sbyte"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddSBytes(sbyte[] array, bool includeLength = true, sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddSByte(array[i], min, max);
            }

            return this;
        }

        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public byte[] GetBytes(byte min = byte.MinValue, byte max = byte.MaxValue) => GetBytes((int)GetVarULong(), min, max);
        /// <summary>Retrieves a <see cref="byte"/> array from the message.</summary>
        /// <param name="amount">The amount of bytes to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public byte[] GetBytes(int amount, byte min = byte.MinValue, byte max = byte.MaxValue)
        {
            byte[] array = new byte[amount];
            ReadBytes(amount, array, 0, min, max);
            return array;
        }
        /// <summary>Populates a <see cref="byte"/> array with bytes retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetBytes(byte[] intoArray, int startIndex = 0, byte min = byte.MinValue, byte max = byte.MaxValue) => GetBytes((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates a <see cref="byte"/> array with bytes retrieved from the message.</summary>
        /// <param name="amount">The amount of bytes to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetBytes(int amount, byte[] intoArray, int startIndex = 0, byte min = byte.MinValue, byte max = byte.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, ByteName));

            ReadBytes(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Retrieves an <see cref="sbyte"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public sbyte[] GetSBytes(sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue) => GetSBytes((int)GetVarULong(), min, max);
        /// <summary>Retrieves an <see cref="sbyte"/> array from the message.</summary>
        /// <param name="amount">The amount of sbytes to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public sbyte[] GetSBytes(int amount, sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue)
        {
            sbyte[] array = new sbyte[amount];
            ReadSBytes(amount, array, 0, min, max);
            return array;
        }
        /// <summary>Populates a <see cref="sbyte"/> array with bytes retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="intoArray"/>.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetSBytes(sbyte[] intoArray, int startIndex = 0, sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue) => GetSBytes((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates a <see cref="sbyte"/> array with bytes retrieved from the message.</summary>
        /// <param name="amount">The amount of sbytes to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating <paramref name="intoArray"/>.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetSBytes(int amount, sbyte[] intoArray, int startIndex = 0, sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, SByteName));

            ReadSBytes(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Reads a number of bytes from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of bytes to read.</param>
        /// <param name="intoArray">The array to write the bytes into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadBytes(int amount, byte[] intoArray, int startIndex = 0, byte min = byte.MinValue, byte max = byte.MaxValue)
        {
			for (int i = 0; i < amount; i++)
				intoArray[startIndex + i] = GetByte(min, max);
        }

        /// <summary>Reads a number of sbytes from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of sbytes to read.</param>
        /// <param name="intoArray">The array to write the sbytes into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadSBytes(int amount, sbyte[] intoArray, int startIndex = 0, sbyte min = sbyte.MinValue, sbyte max = sbyte.MaxValue)
        {
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetSByte(min, max);
            }
        }
        #endregion

        #region Bool
		/// <summary>Adds a <see cref="bool"/> to the message.</summary>
        /// <param name="value">The <see cref="bool"/> to add.</param>
        /// <returns>The message that the <see cref="bool"/> was added to.</returns>
		public Message AddBool(bool value) => AddByte((byte)(value ? 1 : 0), 0, 1);
		/// <summary>Retrieves a <see cref="bool"/> from the message.</summary>
        /// <returns>The <see cref="bool"/> that was retrieved.</returns>
		public bool GetBool() => GetByte(0, 1) == 1;

        /// <summary>Adds a <see cref="bool"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddBools(bool[] array, bool includeLength = true)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
                AddBool(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see cref="bool"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public bool[] GetBools() => GetBools((int)GetVarULong());
        /// <summary>Retrieves a <see cref="bool"/> array from the message.</summary>
        /// <param name="amount">The amount of bools to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public bool[] GetBools(int amount)
        {
            bool[] array = new bool[amount];
            ReadBools(amount, array);
            return array;
        }
        /// <summary>Populates a <see cref="bool"/> array with bools retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetBools(bool[] intoArray, int startIndex = 0) => GetBools((int)GetVarULong(), intoArray, startIndex);
        /// <summary>Populates a <see cref="bool"/> array with bools retrieved from the message.</summary>
        /// <param name="amount">The amount of bools to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetBools(int amount, bool[] intoArray, int startIndex = 0)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, BoolName));

            ReadBools(amount, intoArray, startIndex);
        }

        /// <summary>Reads a number of bools from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of bools to read.</param>
        /// <param name="intoArray">The array to write the bools into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
        private void ReadBools(int amount, bool[] intoArray, int startIndex = 0)
        {
            for (int i = 0; i < amount; i++)
                intoArray[startIndex + i] = GetBool();
        }
        #endregion

        #region Short & UShort
		/// <summary>Adds a <see cref="short"/> to the message.</summary>
        /// <param name="value">The <see cref="short"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="short"/> was added to.</returns>
		public Message AddUShort(ushort value, ushort min = ushort.MinValue, ushort max = ushort.MaxValue) {
			return AddULong(value, min, max);
		}

		/// <summary>Retrieves a <see cref="ushort"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The <see cref="ushort"/> that was retrieved.</returns>
		public ushort GetUShort(ushort min = ushort.MinValue, ushort max = ushort.MaxValue) {
			return (ushort)GetULong(min, max);
		}

		/// <summary>Adds a <see cref="short"/> to the message.</summary>
        /// <param name="value">The <see cref="short"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="short"/> was added to.</returns>
		public Message AddShort(short value, short min = short.MinValue, short max = short.MaxValue)
			=> AddUShort(value.Conv(), min.Conv(), max.Conv());

		/// <summary>Retrieves a <see cref="short"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The <see cref="short"/> that was retrieved.</returns>
		public short GetShort(short min = short.MinValue, short max = short.MaxValue) {
			return (short)GetUShort(min.Conv(), max.Conv());
		}

        /// <summary>Adds a <see cref="short"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddShorts(short[] array, bool includeLength = true, short min = short.MinValue, short max = short.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddShort(array[i], min, max);
            }

            return this;
        }

        /// <summary>Adds a <see cref="ushort"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddUShorts(ushort[] array, bool includeLength = true, ushort min = ushort.MinValue, ushort max = ushort.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddUShort(array[i], min, max);
            }

            return this;
        }

        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public short[] GetShorts(short min = short.MinValue, short max = short.MaxValue) => GetShorts((int)GetVarULong(), min, max);
        /// <summary>Retrieves a <see cref="short"/> array from the message.</summary>
        /// <param name="amount">The amount of shorts to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public short[] GetShorts(int amount, short min = short.MinValue, short max = short.MaxValue)
        {
            short[] array = new short[amount];
            ReadShorts(amount, array, 0, min, max);
            return array;
        }
        /// <summary>Populates a <see cref="short"/> array with shorts retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetShorts(short[] intoArray, int startIndex = 0, short min = short.MinValue, short max = short.MaxValue) => GetShorts((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates a <see cref="short"/> array with shorts retrieved from the message.</summary>
        /// <param name="amount">The amount of shorts to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetShorts(int amount, short[] intoArray, int startIndex = 0, short min = short.MinValue, short max = short.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, ShortName));

            ReadShorts(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public ushort[] GetUShorts(ushort min = ushort.MinValue, ushort max = ushort.MaxValue) => GetUShorts((int)GetVarULong(), min, max);
        /// <summary>Retrieves a <see cref="ushort"/> array from the message.</summary>
        /// <param name="amount">The amount of ushorts to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public ushort[] GetUShorts(int amount, ushort min = ushort.MinValue, ushort max = ushort.MaxValue)
        {
            ushort[] array = new ushort[amount];
            ReadUShorts(amount, array, 0, min, max);
            return array;
        }
        /// <summary>Populates a <see cref="ushort"/> array with ushorts retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetUShorts(ushort[] intoArray, int startIndex = 0, ushort min = ushort.MinValue, ushort max = ushort.MaxValue) => GetUShorts((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates a <see cref="ushort"/> array with ushorts retrieved from the message.</summary>
        /// <param name="amount">The amount of ushorts to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetUShorts(int amount, ushort[] intoArray, int startIndex = 0, ushort min = ushort.MinValue, ushort max = ushort.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, UShortName));

            ReadUShorts(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Reads a number of shorts from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of shorts to read.</param>
        /// <param name="intoArray">The array to write the shorts into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadShorts(int amount, short[] intoArray, int startIndex = 0, short min = short.MinValue, short max = short.MaxValue)
        {
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetShort(min, max);
            }
        }

        /// <summary>Reads a number of ushorts from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ushorts to read.</param>
        /// <param name="intoArray">The array to write the ushorts into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadUShorts(int amount, ushort[] intoArray, int startIndex = 0, ushort min = ushort.MinValue, ushort max = ushort.MaxValue)
        {
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetUShort(min, max);
            }
        }
        #endregion

        #region Int & UInt
		/// <summary>Adds a <see cref="uint"/> to the message.</summary>
        /// <param name="value">The <see cref="uint"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="uint"/> was added to.</returns>
		public Message AddUInt(uint value, uint min = uint.MinValue, uint max = uint.MaxValue) {
			return AddULong(value, min, max);
		}

		/// <summary>Retrieves an <see cref="uint"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
		/// <returns>The <see cref="uint"/> that was retrieved.</returns>
		public uint GetUInt(uint min = uint.MinValue, uint max = uint.MaxValue) {
			return (uint)GetULong(min, max);
		}

		/// <summary>Adds an <see cref="int"/> to the message.</summary>
		/// <param name="value">The <see cref="int"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="int"/> was added to.</returns>
		public Message AddInt(int value, int min = int.MinValue, int max = int.MaxValue)
			=> AddUInt(value.Conv(), min.Conv(), max.Conv());

		/// <summary>Retrieves an <see cref="int"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
		/// <returns>The <see cref="int"/> that was retrieved.</returns>
		public int GetInt(int min = int.MinValue, int max = int.MaxValue) {
			return (int)GetUInt(min.Conv(), max.Conv());
		}

        /// <summary>Adds an <see cref="int"/> array message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddInts(int[] array, bool includeLength = true, int min = int.MinValue, int max = int.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddInt(array[i], min, max);
            }

            return this;
        }

        /// <summary>Adds a <see cref="uint"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddUInts(uint[] array, bool includeLength = true, uint min = uint.MinValue, uint max = uint.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddUInt(array[i], min, max);
            }

            return this;
        }

        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public int[] GetInts() => GetInts((int)GetVarULong(), int.MinValue, int.MaxValue);
        /// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <param name="amount">The amount of ints to retrieve.</param>
        /// <returns>The array that was retrieved.</returns>
        public int[] GetInts(int amount)
        {
            int[] array = new int[amount];
            ReadInts(amount, array, 0, int.MinValue, int.MaxValue);
            return array;
        }
		/// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
		public int[] GetInts(int min, int max) => GetInts((int)GetVarULong(), min, max);
		/// <summary>Retrieves an <see cref="int"/> array from the message.</summary>
        /// <param name="amount">The amount of ints to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
		public int[] GetInts(int amount, int min, int max)
		{
			int[] array = new int[amount];
			ReadInts(amount, array, 0, min, max);
			return array;
		}
        /// <summary>Populates an <see cref="int"/> array with ints retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetInts(int[] intoArray, int startIndex = 0, int min = int.MinValue, int max = int.MaxValue) => GetInts((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates an <see cref="int"/> array with ints retrieved from the message.</summary>
        /// <param name="amount">The amount of ints to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetInts(int amount, int[] intoArray, int startIndex = 0, int min = int.MinValue, int max = int.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, IntName));

            ReadInts(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public uint[] GetUInts(uint min = uint.MinValue, uint max = uint.MaxValue) => GetUInts((int)GetVarULong(), min, max);
        /// <summary>Retrieves a <see cref="uint"/> array from the message.</summary>
        /// <param name="amount">The amount of uints to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public uint[] GetUInts(int amount, uint min = uint.MinValue, uint max = uint.MaxValue)
        {
            uint[] array = new uint[amount];
            ReadUInts(amount, array, 0, min, max);
            return array;
        }
        /// <summary>Populates a <see cref="uint"/> array with uints retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetUInts(uint[] intoArray, int startIndex = 0, uint min = uint.MinValue, uint max = uint.MaxValue) => GetUInts((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates a <see cref="uint"/> array with uints retrieved from the message.</summary>
        /// <param name="amount">The amount of uints to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetUInts(int amount, uint[] intoArray, int startIndex = 0, uint min = uint.MinValue, uint max = uint.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, UIntName));

            ReadUInts(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Reads a number of ints from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ints to read.</param>
        /// <param name="intoArray">The array to write the ints into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadInts(int amount, int[] intoArray, int startIndex = 0, int min = int.MinValue, int max = int.MaxValue)
        {
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetInt(min, max);
            }
        }

        /// <summary>Reads a number of uints from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of uints to read.</param>
        /// <param name="intoArray">The array to write the uints into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadUInts(int amount, uint[] intoArray, int startIndex = 0, uint min = uint.MinValue, uint max = uint.MaxValue)
        {
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetUInt(min, max);
            }
        }
        #endregion

        #region Long & ULong
		/// <summary>Adds a <see cref="ulong"/> to the message.</summary>
        /// <param name="value">The <see cref="ulong"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="ulong"/> was added to.</returns>
		public Message AddULong(ulong value, ulong min = ulong.MinValue, ulong max = ulong.MaxValue) {
			if(value > max || value < min) throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between {min} and {max} (inclusive)");
			data.Add(writeValue, value - min);
			if(max - min + 1 == 0) writeValue.LeftShift(sizeof(ulong) * BitsPerByte);
			else writeValue.Mult(max - min + 1);
			return this;
		}

		/// <summary>Retrieves a <see cref="ulong"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The <see cref="ulong"/> that was retrieved.</returns>
		public ulong GetULong(ulong min = ulong.MinValue, ulong max = ulong.MaxValue) {
			if(min > max) throw new ArgumentOutOfRangeException(nameof(min), "min must be <= max");
			if(max - min + 1 == 0) {
				ulong val = data.RightShift(sizeof(ulong) * BitsPerByte);
				return val + min;
			}
			ulong value = data.DivReturnMod(max - min + 1);
			return value + min;
		}

		/// <summary>Adds a <see cref="long"/> to the message.</summary>
        /// <param name="value">The <see cref="long"/> to add.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the <see cref="long"/> was added to.</returns>
		public Message AddLong(long value, long min = long.MinValue, long max = long.MaxValue)
			=> AddULong(value.Conv(), min.Conv(), max.Conv());

		/// <summary>Retrieves a <see cref="long"/> from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The <see cref="long"/> that was retrieved.</returns>
		public long GetLong(long min = long.MinValue, long max = long.MaxValue)
			=> (long)GetULong(min.Conv(), max.Conv());

        /// <summary>Adds a <see cref="long"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddLongs(long[] array, bool includeLength = true, long min = long.MinValue, long max = long.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddLong(array[i], min, max);
            }

            return this;
        }

        /// <summary>Adds a <see cref="ulong"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddULongs(ulong[] array, bool includeLength = true, ulong min = ulong.MinValue, ulong max = ulong.MaxValue)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddULong(array[i], min, max);
            }

            return this;
        }

        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public long[] GetLongs(long min = long.MinValue, long max = long.MaxValue) => GetLongs((int)GetVarULong(), min, max);
        /// <summary>Retrieves a <see cref="long"/> array from the message.</summary>
        /// <param name="amount">The amount of longs to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public long[] GetLongs(int amount, long min = long.MinValue, long max = long.MaxValue)
        {
            long[] array = new long[amount];
            ReadLongs(amount, array, 0, min, max);
            return array;
        }
        /// <summary>Populates a <see cref="long"/> array with longs retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetLongs(long[] intoArray, int startIndex = 0, long min = long.MinValue, long max = long.MaxValue) => GetLongs((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates a <see cref="long"/> array with longs retrieved from the message.</summary>
        /// <param name="amount">The amount of longs to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetLongs(int amount, long[] intoArray, int startIndex = 0, long min = long.MinValue, long max = long.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, LongName));

            ReadLongs(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public ulong[] GetULongs(ulong min = ulong.MinValue, ulong max = ulong.MaxValue) => GetULongs((int)GetVarULong(), min, max);
        /// <summary>Retrieves a <see cref="ulong"/> array from the message.</summary>
        /// <param name="amount">The amount of ulongs to retrieve.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        /// <returns>The array that was retrieved.</returns>
        public ulong[] GetULongs(int amount, ulong min = ulong.MinValue, ulong max = ulong.MaxValue)
        {
            ulong[] array = new ulong[amount];
            ReadULongs(amount, array, 0, min, max);
            return array;
        }
        /// <summary>Populates a <see cref="ulong"/> array with ulongs retrieved from the message.</summary>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetULongs(ulong[] intoArray, int startIndex = 0, ulong min = ulong.MinValue, ulong max = ulong.MaxValue) => GetULongs((int)GetVarULong(), intoArray, startIndex, min, max);
        /// <summary>Populates a <see cref="ulong"/> array with ulongs retrieved from the message.</summary>
        /// <param name="amount">The amount of ulongs to retrieve.</param>
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        public void GetULongs(int amount, ulong[] intoArray, int startIndex = 0, ulong min = ulong.MinValue, ulong max = ulong.MaxValue)
        {
            if (startIndex + amount > intoArray.Length)
                throw new ArgumentException(nameof(amount), ArrayNotLongEnoughError(amount, intoArray.Length, startIndex, ULongName));

            ReadULongs(amount, intoArray, startIndex, min, max);
        }

        /// <summary>Reads a number of longs from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of longs to read.</param>
        /// <param name="intoArray">The array to write the longs into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadLongs(int amount, long[] intoArray, int startIndex = 0, long min = long.MinValue, long max = long.MaxValue)
        {
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetLong(min, max);
            }
        }

        /// <summary>Reads a number of ulongs from the message and writes them into the given array.</summary>
        /// <param name="amount">The amount of ulongs to read.</param>
        /// <param name="intoArray">The array to write the ulongs into.</param>
        /// <param name="startIndex">The position at which to start writing into the array.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
        private void ReadULongs(int amount, ulong[] intoArray, int startIndex = 0, ulong min = ulong.MinValue, ulong max = ulong.MaxValue)
        {
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetULong(min, max);
            }
        }
        #endregion

        #region Float
        /// <summary>Adds a <see cref="float"/> to the message.</summary>
        /// <param name="value">The <see cref="float"/> to add.</param>
        /// <returns>The message that the <see cref="float"/> was added to.</returns>
        public Message AddFloat(float value)
        {
			return AddUInt(value.ToUInt());
        }

        /// <summary>Retrieves a <see cref="float"/> from the message.</summary>
        /// <returns>The <see cref="float"/> that was retrieved.</returns>
        public float GetFloat()
        {
			return GetUInt().ToFloat();
        }

        /// <summary>Adds a <see cref="float"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddFloats(float[] array, bool includeLength = true)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddFloat(array[i]);
            }

            return this;
        }

        /// <summary>Retrieves a <see cref="float"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public float[] GetFloats() => GetFloats((int)GetVarULong());
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
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetFloats(float[] intoArray, int startIndex = 0) => GetFloats((int)GetVarULong(), intoArray, startIndex);
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
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetFloat();
            }
        }
        #endregion

        #region Double
        /// <summary>Adds a <see cref="double"/> to the message.</summary>
        /// <param name="value">The <see cref="double"/> to add.</param>
        /// <returns>The message that the <see cref="double"/> was added to.</returns>
        public Message AddDouble(double value)
        {
            return AddULong(value.ToULong());
        }

        /// <summary>Retrieves a <see cref="double"/> from the message.</summary>
        /// <returns>The <see cref="double"/> that was retrieved.</returns>
        public double GetDouble()
        {
            return GetULong().ToDouble();
        }

        /// <summary>Adds a <see cref="double"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddDoubles(double[] array, bool includeLength = true)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                AddDouble(array[i]);
            }

            return this;
        }

        /// <summary>Retrieves a <see cref="double"/> array from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public double[] GetDoubles() => GetDoubles((int)GetVarULong());
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
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetDoubles(double[] intoArray, int startIndex = 0) => GetDoubles((int)GetVarULong(), intoArray, startIndex);
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
            for (int i = 0; i < amount; i++)
            {
                intoArray[startIndex + i] = GetDouble();
            }
        }
        #endregion

        #region String
        /// <summary>Adds a <see cref="string"/> to the message.</summary>
        /// <param name="value">The <see cref="string"/> to add.</param>
        /// <returns>The message that the <see cref="string"/> was added to.</returns>
        public Message AddString(string value)
        {
            AddBytes(Encoding.UTF8.GetBytes(value));
            return this;
        }

        /// <summary>Retrieves a <see cref="string"/> from the message.</summary>
        /// <returns>The <see cref="string"/> that was retrieved.</returns>
        public string GetString()
        {
            int length = (int)GetVarULong(); // Get the length of the string (in bytes, NOT characters)
            string value = Encoding.UTF8.GetString(GetBytes(length), 0, length);
            return value;
        }

        /// <summary>Adds a <see cref="string"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The message that the array was added to.</returns>
        public Message AddStrings(string[] array, bool includeLength = true)
        {
            if (includeLength)
                AddVarULong((uint)array.Length);

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
        public string[] GetStrings() => GetStrings((int)GetVarULong());
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
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetStrings(string[] intoArray, int startIndex = 0) => GetStrings((int)GetVarULong(), intoArray, startIndex);
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

		#region States of T
		/// <summary>Adds one of the possible values.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="element"></param>
		/// <param name="possibleValues"></param>
		/// <exception cref="ArgumentException"></exception>
		public void AddElement<T>(T element, T[] possibleValues) {
			if (possibleValues == null || possibleValues.Length == 0)
				throw new ArgumentException("Possible values array cannot be null or empty", nameof(possibleValues));

			int index = Array.IndexOf(possibleValues, element);
			if (index == -1)
				throw new ArgumentException($"Element {element} is not a valid value for this message", nameof(element));

			AddInt(index, 0, possibleValues.Length - 1);
		}

		/// <summary>Retrieves one of the possible values.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="possibleValues"></param>
		/// <returns>The retrieved element.</returns>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="Exception"></exception>
		public T GetElement<T>(T[] possibleValues) {
			if (possibleValues == null || possibleValues.Length == 0)
				throw new ArgumentException("Possible values array cannot be null or empty", nameof(possibleValues));

			int index = GetInt(0, possibleValues.Length - 1);
			if (index < 0 || index >= possibleValues.Length)
				throw new Exception($"Received invalid index {index} for possible values array of length {possibleValues.Length}");

			return possibleValues[index];
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
                AddVarULong((uint)array.Length);

            for (int i = 0; i < array.Length; i++)
                AddSerializable(array[i]);

            return this;
        }

        /// <summary>Retrieves an array of serializables from the message.</summary>
        /// <returns>The array that was retrieved.</returns>
        public T[] GetSerializables<T>() where T : IMessageSerializable, new() => GetSerializables<T>((int)GetVarULong());
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
        /// <param name="intoArray">The array to populate.</param>
        /// <param name="startIndex">The position at which to start populating the array.</param>
        public void GetSerializables<T>(T[] intoArray, int startIndex = 0) where T : IMessageSerializable, new() => GetSerializables<T>((int)GetVarULong(), intoArray, startIndex);
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
        /// <param name="intoArray">The array to write the serializables into.</param>
        /// <param name="startIndex">The position at which to start writing into <paramref name="intoArray"/>.</param>
        private void ReadSerializables<T>(int amount, T[] intoArray, int startIndex = 0) where T : IMessageSerializable, new()
        {
            for (int i = 0; i < amount; i++)
                intoArray[startIndex + i] = GetSerializable<T>();
        }
        #endregion

        #region Overload Versions
        /// <inheritdoc cref="AddByte(byte, byte, byte)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddByte(byte, byte, byte)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(byte value) => AddByte(value);
        /// <inheritdoc cref="AddSByte(sbyte, sbyte, sbyte)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSByte(sbyte, sbyte, sbyte)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(sbyte value) => AddSByte(value);
        /// <inheritdoc cref="AddBool(bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddBool(bool)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(bool value) => AddBool(value);
        /// <inheritdoc cref="AddShort(short, short, short)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddShort(short, short, short)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(short value) => AddShort(value);
        /// <inheritdoc cref="AddUShort(ushort, ushort, ushort)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUShort(ushort, ushort, ushort)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(ushort value) => AddUShort(value);
        /// <inheritdoc cref="AddInt(int, int, int)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddInt(int, int, int)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(int value) => AddInt(value);
        /// <inheritdoc cref="AddUInt(uint, uint, uint)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUInt(uint, uint, uint)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(uint value) => AddUInt(value);
        /// <inheritdoc cref="AddLong(long, long, long)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddLong(long, long, long)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(long value) => AddLong(value);
        /// <inheritdoc cref="AddULong(ulong, ulong, ulong)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddULong(ulong, ulong, ulong)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(ulong value) => AddULong(value);
        /// <inheritdoc cref="AddFloat(float)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddFloat(float)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(float value) => AddFloat(value);
        /// <inheritdoc cref="AddDouble(double)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddDouble(double)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(double value) => AddDouble(value);
        /// <inheritdoc cref="AddString(string)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddString(string)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(string value) => AddString(value);
        /// <inheritdoc cref="AddSerializable{T}(T)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSerializable{T}(T)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add<T>(T value) where T : IMessageSerializable => AddSerializable(value);

        /// <inheritdoc cref="AddBytes(byte[], bool, byte, byte)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddBytes(byte[], bool, byte, byte)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(byte[] array, bool includeLength = true) => AddBytes(array, includeLength);
        /// <inheritdoc cref="AddSBytes(sbyte[], bool, sbyte, sbyte)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSBytes(sbyte[], bool, sbyte, sbyte)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(sbyte[] array, bool includeLength = true) => AddSBytes(array, includeLength);
        /// <inheritdoc cref="AddBools(bool[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddBools(bool[], bool)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(bool[] array, bool includeLength = true) => AddBools(array, includeLength);
        /// <inheritdoc cref="AddShorts(short[], bool, short, short)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddShorts(short[], bool, short, short)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(short[] array, bool includeLength = true) => AddShorts(array, includeLength);
        /// <inheritdoc cref="AddUShorts(ushort[], bool, ushort, ushort)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUShorts(ushort[], bool, ushort, ushort)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(ushort[] array, bool includeLength = true) => AddUShorts(array, includeLength);
        /// <inheritdoc cref="AddInts(int[], bool, int, int)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddInts(int[], bool, int, int)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(int[] array, bool includeLength = true) => AddInts(array, includeLength);
        /// <inheritdoc cref="AddUInts(uint[], bool, uint, uint)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddUInts(uint[], bool, uint, uint)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(uint[] array, bool includeLength = true) => AddUInts(array, includeLength);
        /// <inheritdoc cref="AddLongs(long[], bool, long, long)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddLongs(long[], bool, long, long)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(long[] array, bool includeLength = true) => AddLongs(array, includeLength);
        /// <inheritdoc cref="AddULongs(ulong[], bool, ulong, ulong)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddULongs(ulong[], bool, ulong, ulong)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(ulong[] array, bool includeLength = true) => AddULongs(array, includeLength);
        /// <inheritdoc cref="AddFloats(float[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddFloats(float[], bool)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(float[] array, bool includeLength = true) => AddFloats(array, includeLength);
        /// <inheritdoc cref="AddDoubles(double[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddDoubles(double[], bool)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(double[] array, bool includeLength = true) => AddDoubles(array, includeLength);
        /// <inheritdoc cref="AddStrings(string[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddStrings(string[], bool)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Message Add(string[] array, bool includeLength = true) => AddStrings(array, includeLength);
        /// <inheritdoc cref="AddSerializables{T}(T[], bool)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddSerializables{T}(T[], bool)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
