// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) not Tom Weiland but me
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md


namespace Riptide.Utils
{
	internal static class CMath
	{
		internal static ushort Clamp(this ushort value, ushort min, ushort max) {
			if(value < min) return min;
			if(value > max) return max;
			return value;
		}

		/// <remarks>Rounds down and includes 0 as 0.</remarks>
		internal static byte Log2(this ulong value) {
			byte bits = 0;
			for(byte step = 32; step > 0; step >>= 1) {
				if(value < 1UL << step) continue;
				value >>= step;
				bits += step;
			}
			return bits;
		}

		internal static bool IsPowerOf2(this ulong value) {
			if(value == 0) return false;
			return (value & (value - 1)) == 0;
		}

		internal static unsafe uint ToUInt(this float value) => *(uint*)&value;
		internal static unsafe float ToFloat(this uint value) => *(float*)&value;
		internal static unsafe ulong ToULong(this double value) => *(ulong*)&value;
		internal static unsafe double ToDouble(this ulong value) => *(double*)&value;
		internal static unsafe ulong ToULong(this bool value) => *(byte*)&value;

		public static byte Conv(this sbyte value) => (byte)(value + (1 << 7));
		public static sbyte Conv(this byte value) => (sbyte)(value - (1 << 7));
		public static ushort Conv(this short value) => (ushort)(value + (1 << 15));
		public static short Conv(this ushort value) => (short)(value - (1 << 15));
		public static uint Conv(this int value) => (uint)(value + (1 << 31));
		public static int Conv(this uint value) => (int)value - (1 << 31);
		public static ulong Conv(this long value) => (ulong)(value + (1L << 63));
		public static long Conv(this ulong value) => (long)value - (1L << 63);
	}
}