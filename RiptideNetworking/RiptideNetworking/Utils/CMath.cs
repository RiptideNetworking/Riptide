// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md


namespace Riptide.Utils
{
	internal static class CMath
	{
		internal static ushort Clamp(this ushort value, ushort min, ushort max) {
			return value < min ? min : value > max ? max : value;
		}

		/// <remarks>Rounds down and includes 0.</remarks>
		internal static byte Log2(this ulong value) {
			byte bits = 0;
			for(byte step = 32; step > 0; step >>= 1) {
				if(value < 1UL << step) continue;
				value >>= step;
				bits += step;
			}
			return bits;
		}


		/// <remarks>Includes 0</remarks>
		internal static bool IsPowerOf2(this ulong value) {
			return (value & (value - 1)) == 0;
		}

		internal static unsafe uint ToUInt(this float value) => *(uint*)&value;
		internal static unsafe float ToFloat(this uint value) => *(float*)&value;
		internal static unsafe ulong ToULong(this double value) => *(ulong*)&value;
		internal static unsafe double ToDouble(this ulong value) => *(double*)&value;
		internal static unsafe ulong ToULong(this bool value) => *(byte*)&value;

		internal static void Clear(this ulong[] ulongs) {
			for(int i = 0; i < ulongs.Length; i++)
				ulongs[i] = 0;
		}

		public static (ulong value, ulong carry) AddUlong(ulong val, ulong add) {
			ulong value = val + add;
			ulong carry = (value < val).ToULong();
			return (value, carry);
		}

		public static (ulong value, ulong carry) SubtractUlong(ulong val, ulong sub) {
			ulong value = val - sub;
			ulong carry = (value > val).ToULong();
			return (value, carry);
		}

		public static (ulong value, ulong carry) MultiplyUlong(ulong val, ulong mult) {
			ulong xLow = val & 0xFFFFFFFF;
			ulong xHigh = val >> 32;
			ulong yLow = mult & 0xFFFFFFFF;
			ulong yHigh = mult >> 32;

			ulong low = xLow * yLow;
			ulong high = xHigh * yHigh;

			ulong cross1 = xLow * yHigh;
			ulong cross2 = xHigh * yLow;

			ulong value = low + (cross1 << 32) + (cross2 << 32);
			ulong carry = high + (cross1 >> 32) + (cross2 >> 32);
			return (value, carry);
		}

		public static (ulong value, ulong carry) LeftShiftUlong(ulong val, int shift) {
			if(shift == 0) return (val, 0);
			if(shift == 64) return (0, val);
			ulong value = val << shift;
			ulong carry = val >> (64 - shift);
			return (value, carry);
		}

		public static (ulong value, ulong carry) RightShiftUlong(ulong val, int shift) {
			if(shift == 0) return (val, 0);
			if(shift == 64) return (0, val);
			ulong value = val >> shift;
			ulong carry = val << (64 - shift);
			return (value, carry);
		}

		public static (ulong value, ulong carry) DivideUlong(ulong val, ulong carry, ulong div) {
			// this has not been tested at all
			if(carry == 0) return (val / div, val % div);
			ulong value = 0;
			for(int i = 63; i >= 0; i--) {
				value <<= 1;
				carry <<= 1;
				carry += val & 1;
				val >>= 1;
				if(carry < div) continue;
				carry -= div;
				value |= 1;
				if(carry >> i == 0) {
					carry <<= 63 - i;
					value <<= 63 - i;
					break;
				}
			}
			val += carry;
			return (val / div + value, val % div);
		}

		public static byte Conv(this sbyte value) => (byte)(value + (1 << 7));
		public static sbyte Conv(this byte value) => (sbyte)(value - (1 << 7));
		public static ushort Conv(this short value) => (ushort)(value + (1 << 15));
		public static short Conv(this ushort value) => (short)(value - (1 << 15));
		public static uint Conv(this int value) => (uint)(value + (1 << 31));
		public static int Conv(this uint value) => (int)(value - (1u << 31));
		public static ulong Conv(this long value) => (ulong)(value + (1L << 63));
		public static long Conv(this ulong value) => (long)(value - (1UL << 63));
	}
}