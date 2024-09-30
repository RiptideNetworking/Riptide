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

		internal static int Log256(this ulong value) {
			int log = 0;
			while(value > byte.MaxValue) {
				value >>= 8;
				log++;
			}
			return log;
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
	}
}