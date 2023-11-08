// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Riptide.Utils
{
    /// <summary>Provides functionality for converting bits and bytes to various value types and vice versa.</summary>
    public class Converter
    {
        #region Byte/SByte
        /// <summary>Converts <paramref name="value"/> to 8 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="sbyte"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SByteToBits(sbyte value, byte[] array, int startBit) => ByteToBits((byte)value, array, startBit);
        /// <summary>Converts <paramref name="value"/> to 8 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="byte"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ByteToBits(byte value, byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            if (bit == 0)
                array[pos] = value;
            else
            {
                array[pos    ] |= (byte)(value << bit);
                array[pos + 1] = (byte)(value >> (8 - bit));
            }
        }

        /// <summary>Converts the 8 bits at <paramref name="startBit"/> in <paramref name="array"/> to an <see cref="sbyte"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="sbyte"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte SByteFromBits(byte[] array, int startBit) => (sbyte)ByteFromBits(array, startBit);
        /// <summary>Converts the 8 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="byte"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="byte"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ByteFromBits(byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            byte value = array[pos];
            if (bit == 0)
                return value;

            value >>= bit;
            return (byte)(value | (array[pos + 1] << (8 - bit)));
        }
        #endregion

        #region Bool
        /// <summary>Converts <paramref name="value"/> to a bit and writes it into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="bool"/> to convert.</param>
        /// <param name="array">The array to write the bit into.</param>
        /// <param name="startBit">The position in the array at which to write the bit.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BoolToBit(bool value, byte[] array, int startBit)
        {
            int pos = startBit / 8;
            int bit = startBit % 8;
            if (bit == 0)
                array[pos] = 0;

            if (value)
                array[pos] |= (byte)(1 << bit);
        }

        /// <summary>Converts the bit at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="bool"/>.</summary>
        /// <param name="array">The array to convert the bit from.</param>
        /// <param name="startBit">The position in the array from which to read the bit.</param>
        /// <returns>The converted <see cref="bool"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoolFromBit(byte[] array, int startBit)
        {
            int pos = startBit / 8;
            int bit = startBit % 8;
            return (array[pos] & (1 << bit)) != 0;
        }
        #endregion

        #region Short/UShort
        /// <summary>Converts a given <see cref="short"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="short"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromShort(short value, byte[] array, int startIndex) => FromUShort((ushort)value, array, startIndex);
        /// <summary>Converts a given <see cref="ushort"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="ushort"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromUShort(ushort value, byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            array[startIndex + 1] = (byte)value;
            array[startIndex    ] = (byte)(value >> 8);
#else
            array[startIndex    ] = (byte)value;
            array[startIndex + 1] = (byte)(value >> 8);
#endif
        }

        /// <summary>Converts the 2 bytes in the array at <paramref name="startIndex"/> to a <see cref="short"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="short"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ToShort(byte[] array, int startIndex) => (short)ToUShort(array, startIndex);
        /// <summary>Converts the 2 bytes in the array at <paramref name="startIndex"/> to a <see cref="ushort"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="ushort"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ToUShort(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            return (ushort)(array[startIndex + 1] | (array[startIndex    ] << 8));
#else
            return (ushort)(array[startIndex    ] | (array[startIndex + 1] << 8));
#endif
        }

        /// <summary>Converts <paramref name="value"/> to 16 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="short"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ShortToBits(short value, byte[] array, int startBit) => UShortToBits((ushort)value, array, startBit);
        /// <summary>Converts <paramref name="value"/> to 16 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="ushort"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UShortToBits(ushort value, byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            if (bit == 0)
            {
                array[pos] = (byte)value;
                array[pos + 1] = (byte)(value >> 8);
            }
            else
            {
                array[pos    ] |= (byte)(value << bit);
                array[pos + 1]  = (byte)(value >> (8 - bit));
                array[pos + 2]  = (byte)(value >> (16 - bit));
            }
        }

        /// <summary>Converts the 16 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="short"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="short"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ShortFromBits(byte[] array, int startBit) => (short)UShortFromBits(array, startBit);
        /// <summary>Converts the 16 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="ushort"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="ushort"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort UShortFromBits(byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            ushort value = (ushort)(array[pos] | (array[pos + 1] << 8));
            if (bit == 0)
                return value;
            
            value >>= bit;
            return (ushort)(value | (array[pos + 2] << (16 - bit)));
        }
        #endregion

        #region Int/UInt
        /// <summary>Converts a given <see cref="int"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="int"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromInt(int value, byte[] array, int startIndex) => FromUInt((uint)value, array, startIndex);
        /// <summary>Converts a given <see cref="uint"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="uint"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromUInt(uint value, byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            array[startIndex + 3] = (byte)value;
            array[startIndex + 2] = (byte)(value >> 8);
            array[startIndex + 1] = (byte)(value >> 16);
            array[startIndex    ] = (byte)(value >> 24);
#else
            array[startIndex    ] = (byte)value;
            array[startIndex + 1] = (byte)(value >> 8);
            array[startIndex + 2] = (byte)(value >> 16);
            array[startIndex + 3] = (byte)(value >> 24);
#endif
        }

        /// <summary>Converts the 4 bytes in the array at <paramref name="startIndex"/> to a <see cref="int"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt(byte[] array, int startIndex) => (int)ToUInt(array, startIndex);
        /// <summary>Converts the 4 bytes in the array at <paramref name="startIndex"/> to a <see cref="uint"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="uint"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUInt(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            return (uint)(array[startIndex + 3] | (array[startIndex + 2] << 8) | (array[startIndex + 1] << 16) | (array[startIndex    ] << 24));
#else
            return (uint)(array[startIndex    ] | (array[startIndex + 1] << 8) | (array[startIndex + 2] << 16) | (array[startIndex + 3] << 24));
#endif
        }

        /// <summary>Converts <paramref name="value"/> to 32 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="int"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IntToBits(int value, byte[] array, int startBit) => UIntToBits((uint)value, array, startBit);
        /// <summary>Converts <paramref name="value"/> to 32 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="uint"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UIntToBits(uint value, byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            if (bit == 0)
            {
                array[pos    ] = (byte)value;
                array[pos + 1] = (byte)(value >> 8);
                array[pos + 2] = (byte)(value >> 16);
                array[pos + 3] = (byte)(value >> 24);
            }
            else
            {
                array[pos    ] |= (byte)(value << bit);
                array[pos + 1] = (byte)(value >> (8 - bit));
                array[pos + 2] = (byte)(value >> (16 - bit));
                array[pos + 3] = (byte)(value >> (24 - bit));
                array[pos + 4] = (byte)(value >> (32 - bit));
            }
        }

        /// <summary>Converts the 32 bits at <paramref name="startBit"/> in <paramref name="array"/> to an <see cref="int"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IntFromBits(byte[] array, int startBit) => (int)UIntFromBits(array, startBit);
        /// <summary>Converts the 32 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="uint"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="uint"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint UIntFromBits(byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            uint value = (uint)(array[pos] | (array[pos + 1] << 8) | (array[pos + 2] << 16) | (array[pos + 3] << 24));
            if (bit == 0)
                return value;
            
            value >>= bit;
            return value | (uint)(array[pos + 4] << (32 - bit));
        }
        #endregion

        #region Long/ULong
        /// <summary>Converts a given <see cref="long"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="long"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromLong(long value, byte[] array, int startIndex) => FromULong((ulong)value, array, startIndex);
        /// <summary>Converts a given <see cref="ulong"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="ulong"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromULong(ulong value, byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            array[startIndex + 7] = (byte)value;
            array[startIndex + 6] = (byte)(value >> 8);
            array[startIndex + 5] = (byte)(value >> 16);
            array[startIndex + 4] = (byte)(value >> 24);
            array[startIndex + 3] = (byte)(value >> 32);
            array[startIndex + 2] = (byte)(value >> 40);
            array[startIndex + 1] = (byte)(value >> 48);
            array[startIndex    ] = (byte)(value >> 56);
#else
            array[startIndex    ] = (byte)value;
            array[startIndex + 1] = (byte)(value >> 8);
            array[startIndex + 2] = (byte)(value >> 16);
            array[startIndex + 3] = (byte)(value >> 24);
            array[startIndex + 4] = (byte)(value >> 32);
            array[startIndex + 5] = (byte)(value >> 40);
            array[startIndex + 6] = (byte)(value >> 48);
            array[startIndex + 7] = (byte)(value >> 56);
#endif
        }

        /// <summary>Converts the 8 bytes in the array at <paramref name="startIndex"/> to a <see cref="long"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToLong(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            Array.Reverse(array, startIndex, longLength);
#endif
            return BitConverter.ToInt64(array, startIndex);
        }
        /// <summary>Converts the 8 bytes in the array at <paramref name="startIndex"/> to a <see cref="ulong"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="ulong"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToULong(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            Array.Reverse(array, startIndex, ulongLength);
#endif
            return BitConverter.ToUInt64(array, startIndex);
        }

        /// <summary>Converts <paramref name="value"/> to 64 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="long"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LongToBits(long value, byte[] array, int startBit) => ULongToBits((ulong)value, array, startBit);
        /// <summary>Converts <paramref name="value"/> to 64 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="ulong"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ULongToBits(ulong value, byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            if (bit == 0)
            {
                array[pos    ] = (byte)value;
                array[pos + 1] = (byte)(value >> 8);
                array[pos + 2] = (byte)(value >> 16);
                array[pos + 3] = (byte)(value >> 24);
                array[pos + 4] = (byte)(value >> 32);
                array[pos + 5] = (byte)(value >> 40);
                array[pos + 6] = (byte)(value >> 48);
                array[pos + 7] = (byte)(value >> 56);
            }
            else
            {
                array[pos    ] |= (byte)(value << bit);
                array[pos + 1] = (byte)(value >> (8 - bit));
                array[pos + 2] = (byte)(value >> (16 - bit));
                array[pos + 3] = (byte)(value >> (24 - bit));
                array[pos + 4] = (byte)(value >> (32 - bit));
                array[pos + 5] = (byte)(value >> (40 - bit));
                array[pos + 6] = (byte)(value >> (48 - bit));
                array[pos + 7] = (byte)(value >> (56 - bit));
                array[pos + 8] = (byte)(value >> (64 - bit));
            }
        }

        /// <summary>Converts the 64 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="long"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long LongFromBits(byte[] array, int startBit) => (long)ULongFromBits(array, startBit);
        /// <summary>Converts the 64 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="ulong"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="ulong"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ULongFromBits(byte[] array, int startBit)
        {
            int pos = startBit / Message.BitsPerByte;
            int bit = startBit % Message.BitsPerByte;
            ulong value = BitConverter.ToUInt64(array, pos);
            if (bit == 0)
                return value;

            value >>= bit;
            return value | ((ulong)array[pos + 8] << (64 - bit));
        }
        #endregion

        #region Float
        /// <summary>Converts a given <see cref="float"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="float"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromFloat(float value, byte[] array, int startIndex)
        {
            FloatConverter converter = new FloatConverter { FloatValue = value };
#if BIG_ENDIAN
            array[startIndex + 3] = converter.Byte0;
            array[startIndex + 2] = converter.Byte1;
            array[startIndex + 1] = converter.Byte2;
            array[startIndex    ] = converter.Byte3;
#else
            array[startIndex    ] = converter.Byte0;
            array[startIndex + 1] = converter.Byte1;
            array[startIndex + 2] = converter.Byte2;
            array[startIndex + 3] = converter.Byte3;
#endif
        }

        /// <summary>Converts the 4 bytes in the array at <paramref name="startIndex"/> to a <see cref="float"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="float"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            return new FloatConverter { Byte3 = array[startIndex], Byte2 = array[startIndex + 1], Byte1 = array[startIndex + 2], Byte0 = array[startIndex + 3] }.FloatValue;
#else
            return new FloatConverter { Byte0 = array[startIndex], Byte1 = array[startIndex + 1], Byte2 = array[startIndex + 2], Byte3 = array[startIndex + 3] }.FloatValue;
#endif
        }

        /// <summary>Converts <paramref name="value"/> to 32 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="float"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FloatToBits(float value, byte[] array, int startBit)
        {
            UIntToBits(new FloatConverter { FloatValue = value }.UIntValue, array, startBit);
        }

        /// <summary>Converts the 32 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="float"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="float"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FloatFromBits(byte[] array, int startBit)
        {
            return new FloatConverter { UIntValue = UIntFromBits(array, startBit) }.FloatValue;
        }
        #endregion

        #region Double
        /// <summary>Converts a given <see cref="double"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="double"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromDouble(double value, byte[] array, int startIndex)
        {
            DoubleConverter converter = new DoubleConverter { DoubleValue = value };
#if BIG_ENDIAN
            array[startIndex + 7] = converter.Byte0;
            array[startIndex + 6] = converter.Byte1;
            array[startIndex + 5] = converter.Byte2;
            array[startIndex + 4] = converter.Byte3;
            array[startIndex + 3] = converter.Byte4;
            array[startIndex + 2] = converter.Byte5;
            array[startIndex + 1] = converter.Byte6;
            array[startIndex    ] = converter.Byte7;
#else
            array[startIndex    ] = converter.Byte0;
            array[startIndex + 1] = converter.Byte1;
            array[startIndex + 2] = converter.Byte2;
            array[startIndex + 3] = converter.Byte3;
            array[startIndex + 4] = converter.Byte4;
            array[startIndex + 5] = converter.Byte5;
            array[startIndex + 6] = converter.Byte6;
            array[startIndex + 7] = converter.Byte7;
#endif
        }

        /// <summary>Converts the 8 bytes in the array at <paramref name="startIndex"/> to a <see cref="double"/>.</summary>
        /// <param name="array">The array to read the bytes from.</param>
        /// <param name="startIndex">The position in the array at which to read the bytes.</param>
        /// <returns>The converted <see cref="double"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToDouble(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            Array.Reverse(array, startIndex, doubleLength);
#endif
            return BitConverter.ToDouble(array, startIndex);
        }

        /// <summary>Converts <paramref name="value"/> to 64 bits and writes them into <paramref name="array"/> at <paramref name="startBit"/>.</summary>
        /// <param name="value">The <see cref="double"/> to convert.</param>
        /// <param name="array">The array to write the bits into.</param>
        /// <param name="startBit">The position in the array at which to write the bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DoubleToBits(double value, byte[] array, int startBit)
        {
            ULongToBits(new DoubleConverter { DoubleValue = value }.ULongValue, array, startBit);
        }

        /// <summary>Converts the 64 bits at <paramref name="startBit"/> in <paramref name="array"/> to a <see cref="double"/>.</summary>
        /// <param name="array">The array to convert the bits from.</param>
        /// <param name="startBit">The position in the array from which to read the bits.</param>
        /// <returns>The converted <see cref="double"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DoubleFromBits(byte[] array, int startBit)
        {
            return new DoubleConverter { ULongValue = ULongFromBits(array, startBit) }.DoubleValue;
        }
        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct FloatConverter
    {
        [FieldOffset(0)] public byte Byte0;
        [FieldOffset(1)] public byte Byte1;
        [FieldOffset(2)] public byte Byte2;
        [FieldOffset(3)] public byte Byte3;

        [FieldOffset(0)] public float FloatValue;

        [FieldOffset(0)] public uint UIntValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DoubleConverter
    {
        [FieldOffset(0)] public byte Byte0;
        [FieldOffset(1)] public byte Byte1;
        [FieldOffset(2)] public byte Byte2;
        [FieldOffset(3)] public byte Byte3;
        [FieldOffset(4)] public byte Byte4;
        [FieldOffset(5)] public byte Byte5;
        [FieldOffset(6)] public byte Byte6;
        [FieldOffset(7)] public byte Byte7;

        [FieldOffset(0)] public double DoubleValue;

        [FieldOffset(0)] public ulong ULongValue;
    }
}
