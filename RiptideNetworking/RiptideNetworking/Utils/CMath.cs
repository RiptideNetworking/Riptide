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

		internal static byte Log256(this ulong value) {
			byte log = 0;
			while(value > byte.MaxValue) {
				value >>= 8;
				log++;
			}
			return log;
		}

		/// <remarks>Includes 0</remarks>
		internal static bool IsPowerOf256(this ulong value) {
			if(value == 0) return true;
			return (value & (value - 1)) == 0 && (value % 255 == 1);
		}

		internal static unsafe uint ToUInt(this float value) {
			return *(uint*)&value;
		}

		internal static unsafe float ToFloat(this uint value) {
			return *(float*)&value;
		}

		internal static unsafe ulong ToULong(this double value) {
			return *(ulong*)&value;
		}

		internal static unsafe double ToDouble(this ulong value) {
			return *(double*)&value;
		}

		internal static void Clear(this ulong[] ulongs) {
			for(int i = 0; i < ulongs.Length; i++)
				ulongs[i] = 0;
		}

		public static (ulong value, ulong carry) AddUlong(ulong val, ulong add) {
			ulong value = val + add;
			ulong carry = value < val ? 1UL : 0UL;
			return (value, carry);
		}

		public static (ulong value, ulong carry) SubtractUlong(ulong val, ulong sub) {
			ulong value = val - sub;
			ulong carry = value > val ? 1UL : 0UL;
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
			ulong value = val << shift;
			ulong carry = val >> (64 - shift);
			return (value, carry);
		}

		public static (ulong value, ulong carry) RightShiftUlong(ulong val, int shift) {
			ulong value = val >> shift;
			ulong carry = val << (64 - shift);
			return (value, carry);
		}
	}
}