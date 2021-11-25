using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace RiptideNetworking.Transports.Utils
{
    public class RiptideConverter
    {
        /// <summary>Converts a given <see cref="ushort"/> to bytes and writes them into the array.</summary>
        /// <param name="value">The <see cref="ushort"/> to convert.</param>
        /// <param name="array">The array to write the bytes into.</param>
        /// <param name="startIndex">The position in the array at which to write the bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes(ushort value, byte[] array, int startIndex = 0)
        {
#if BIG_ENDIAN
            array[startIndex + 1] = (byte)value;
            array[startIndex    ] = (byte)(value >> 8);
#else
            array[startIndex    ] = (byte)value;
            array[startIndex + 1] = (byte)(value >> 8);
#endif
        }
    }
}
