
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RiptideNetworking.Utils
{
    /// <summary>Provides functionality for converting bytes to various value types and vice versa.</summary>
    public class RiptideConverter
    {
        /// <summary>How many bytes a <see cref="bool"/> is represented by.</summary>
        public const byte BoolLength = sizeof(bool);
        /// <summary>How many bytes a <see cref="short"/> is represented by.</summary>
        public const byte ShortLength = sizeof(short);
        /// <summary>How many bytes a <see cref="ushort"/> is represented by.</summary>
        public const byte UShortLength = sizeof(ushort);
        /// <summary>How many bytes an <see cref="int"/> is represented by.</summary>
        public const byte IntLength = sizeof(int);
        /// <summary>How many bytes an <see cref="uint"/> is represented by.</summary>
        public const byte UIntLength = sizeof(uint);
        /// <summary>How many bytes a <see cref="long"/> is represented by.</summary>
        public const byte LongLength = sizeof(long);
        /// <summary>How many bytes a <see cref="ulong"/> is represented by.</summary>
        public const byte ULongLength = sizeof(ulong);
        /// <summary>How many bytes a <see cref="float"/> is represented by.</summary>
        public const byte FloatLength = sizeof(float);
        /// <summary>How many bytes a <see cref="double"/> is represented by.</summary>
        public const byte DoubleLength = sizeof(double);

        #region Short/UShort
        /// <summary>Converts a given <see cref="short"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="short"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromShort(short value, byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            array[startIndex + 1] = (byte)value;
            array[startIndex    ] = (byte)(value >> 8);
#else
            array[startIndex    ] = (byte)value;
            array[startIndex + 1] = (byte)(value >> 8);
#endif
        }
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
        public static short ToShort(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            return (short)(array[startIndex + 1] | (array[startIndex    ] << 8));
#else
            return (short)(array[startIndex    ] | (array[startIndex + 1] << 8));
#endif
        }
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
        #endregion

        #region Int/UInt
        /// <summary>Converts a given <see cref="int"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="int"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromInt(int value, byte[] array, int startIndex)
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
        public static int ToInt(byte[] array, int startIndex)
        {
#if BIG_ENDIAN
            return array[startIndex + 3] | (array[startIndex + 2] << 8) | (array[startIndex + 1] << 16) | (array[startIndex    ] << 24);
#else
            return array[startIndex    ] | (array[startIndex + 1] << 8) | (array[startIndex + 2] << 16) | (array[startIndex + 3] << 24);
#endif
        }
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
        #endregion

        #region Long/ULong
        /// <summary>Converts a given <see cref="long"/> to bytes and writes them into the given array.</summary>
        /// <param name="value">The <see cref="long"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromLong(long value, byte[] array, int startIndex)
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
            return (ulong)BitConverter.ToInt64(array, startIndex);
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
            FloatConverter converter = new FloatConverter { floatValue = value };
#if BIG_ENDIAN
            array[startIndex + 3] = converter.byte0;
            array[startIndex + 2] = converter.byte1;
            array[startIndex + 1] = converter.byte2;
            array[startIndex    ] = converter.byte3;
#else
            array[startIndex    ] = converter.byte0;
            array[startIndex + 1] = converter.byte1;
            array[startIndex + 2] = converter.byte2;
            array[startIndex + 3] = converter.byte3;
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
            return new FloatConverter { byte3 = array[startIndex], byte2 = array[startIndex + 1], byte1 = array[startIndex + 2], byte0 = array[startIndex + 3] }.floatValue;
#else
            return new FloatConverter { byte0 = array[startIndex], byte1 = array[startIndex + 1], byte2 = array[startIndex + 2], byte3 = array[startIndex + 3] }.floatValue;
#endif
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
            DoubleConverter converter = new DoubleConverter { doubleValue = value };
#if BIG_ENDIAN
            array[startIndex + 7] = converter.byte0;
            array[startIndex + 6] = converter.byte1;
            array[startIndex + 5] = converter.byte2;
            array[startIndex + 4] = converter.byte3;
            array[startIndex + 3] = converter.byte4;
            array[startIndex + 2] = converter.byte5;
            array[startIndex + 1] = converter.byte6;
            array[startIndex    ] = converter.byte7;
#else
            array[startIndex    ] = converter.byte0;
            array[startIndex + 1] = converter.byte1;
            array[startIndex + 2] = converter.byte2;
            array[startIndex + 3] = converter.byte3;
            array[startIndex + 4] = converter.byte4;
            array[startIndex + 5] = converter.byte5;
            array[startIndex + 6] = converter.byte6;
            array[startIndex + 7] = converter.byte7;
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
        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct FloatConverter
    {
        [FieldOffset(0)] public byte byte0;
        [FieldOffset(1)] public byte byte1;
        [FieldOffset(2)] public byte byte2;
        [FieldOffset(3)] public byte byte3;

        [FieldOffset(0)] public float floatValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DoubleConverter
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
