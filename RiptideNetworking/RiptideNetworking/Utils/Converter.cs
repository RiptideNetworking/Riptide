// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System.Runtime.CompilerServices;

namespace Riptide.Utils
{
    /// <summary>Provides functionality for converting bits and bytes to various value types and vice versa.</summary>
    public class Converter
    {
        /// <summary>The number of bits in a byte.</summary>
        public const int BitsPerByte = 8;
        /// <summary>The number of bits in a ulong.</summary>
        public const int BitsPerULong = sizeof(ulong) * BitsPerByte;

        #region Zig Zag Encoding
        /// <summary>Zig zag encodes <paramref name="value"/>.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The zig zag-encoded value.</returns>
        /// <remarks>Zig zag encoding allows small negative numbers to be represented as small positive numbers. All positive numbers are doubled and become even numbers,
        /// while all negative numbers become positive odd numbers. In contrast, simply casting a negative value to its unsigned counterpart would result in a large positive
        /// number which uses the high bit, rendering compression via <see cref="Message.AddVarULong(ulong)"/> and <see cref="Message.GetVarULong"/> ineffective.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ZigZagEncode(long value)
        {
            return (value >> 63) ^ (value << 1);
        }

        /// <summary>Zig zag decodes <paramref name="value"/>.</summary>
        /// <param name="value">The value to decode.</param>
        /// <returns>The zig zag-decoded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ZigZagDecode(long value)
        {
            return (value >> 1) ^ -(value & 1);
        }
        #endregion

		# region Int
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
			// this is probably better but dont wanna break anything
			// byte[] bytes = BitConverter.GetBytes(value);
    		// Buffer.BlockCopy(bytes, 0, array, 0, 4);
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
			// this is probably better but dont wanna break anything
			// return System.BitConverter.ToUInt32(array, 0);
        }
		#endregion
    }
}
