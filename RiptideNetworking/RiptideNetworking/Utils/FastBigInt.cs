// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Text;

namespace Riptide.Utils
{
    internal class FastBigInt
	{
        private ulong[] data;
        private int maxUlong = 0;
        private int minUlong = 0;

		internal FastBigInt(int initialCapacity) {
			data = new ulong[initialCapacity];
		}

		internal FastBigInt(int initialCapacity, ulong initialValue) {
			data = new ulong[initialCapacity];
			data[0] = initialValue;
		}

		internal FastBigInt(byte[] bytes) {
			data = new ulong[bytes.Length / sizeof(ulong) + 1];
			Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
			maxUlong = data.Length - 1;
			AdjustMinAndMax();
		}

		internal FastBigInt Copy() {
			FastBigInt copy = new FastBigInt(data.Length);
			Buffer.BlockCopy(data, 0, copy.data, 0, data.Length * sizeof(ulong));
			copy.maxUlong = maxUlong;
			copy.minUlong = minUlong;
			return copy;
		}

		public override string ToString() {
			StringBuilder s = new StringBuilder();
			for(int i = maxUlong; i >= minUlong; i--) {
				s.Append(data[i]);
				s.Append(' ');
			}
			s.Append('\n');
			s.Append("min: ");
			s.Append(minUlong);
			s.Append(" max: ");
			s.Append(maxUlong);
			return s.ToString();
		}

		internal ulong[] GetData() => data;
		internal bool HasReadNothing => data[0] == 0 && maxUlong == 0;

		internal int GetBytesInUse() {
			ulong max = data[maxUlong];
			int bytes = maxUlong * sizeof(ulong);
			while(max > 0) {
				max >>= 8;
				bytes++;
			}
			return bytes;
		}

		internal void Add(FastBigInt value, ulong mult) {
			minUlong = Math.Min(minUlong, value.minUlong);
			maxUlong = Math.Max(maxUlong, value.maxUlong) + 1;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minUlong; i < maxUlong; i++) {
				(ulong temp, ulong tempCarry) = CMath.MultiplyUlong(value.data[i], mult);
				(data[i], carry) = CMath.AddUlong(data[i], carry);
				carry += tempCarry;
				(data[i], tempCarry) = CMath.AddUlong(data[i], temp);
				carry += tempCarry;
			}
			AdjustMinAndMax();
		}

		internal void Mult(ulong mult) {
			if(mult.IsPowerOf2()) {
				LeftShift(mult.Log2());
				return;
			}
			maxUlong += 1;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minUlong; i <= maxUlong; i++) {
				ulong prevCarry = carry;
				(data[i], carry) = CMath.MultiplyUlong(data[i], mult);
				data[i] += prevCarry;
			}
			AdjustMinAndMax();
		}

		internal ulong DivReturnMod(ulong div) {
			if(div == 0) throw new DivideByZeroException("Divisor cannot be zero.");

			if(div.IsPowerOf2()) return RightShift(div.Log2());
			minUlong -= 1;
			ulong carry = 0;
			for(int i = maxUlong * 2; i >= minUlong * 2; i--) {
				int ui = i % 2 * 32;
				ulong mask = (0ul - (ulong)(i % 2)) ^ 0x00000000FFFFFFFFUL;
				carry <<= 32;
				ulong current = (data[i / 2] >> ui) & mask;
				current += carry;
				(current, carry) = (current / div, current % div);
				data[i / 2] = (current << ui) | (data[i / 2] & ~mask);
			}
			AdjustMinAndMax();
			return carry;
		}

		internal void LeftShift(byte shiftBits) {
			byte possibleUlongShift = (byte)((shiftBits + 63) / 64);
			maxUlong += possibleUlongShift;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minUlong; i <= maxUlong; i++) {
				ulong val;
				ulong prevCarry = carry;
				(val, carry) = CMath.RightShiftUlong(data[i], shiftBits);
				data[i] = val + prevCarry;
			}
			AdjustMinAndMax();
		}

		internal ulong RightShift(byte shiftBits) {
			byte possibleUlongShift = (byte)((shiftBits + 63) / 64);
			minUlong -= possibleUlongShift;
			ulong carry = 0;
			for(int i = maxUlong; i >= minUlong; i--) {
				ulong val;
				ulong prevCarry = carry;
				(val, carry) = CMath.RightShiftUlong(data[i], shiftBits);
				data[i] = val + prevCarry;
			}
			AdjustMinAndMax();
			return carry;
		}

		private void AdjustMinAndMax() {
			while(maxUlong > 0 && data[maxUlong] == 0) maxUlong--;
			while(minUlong < 0) minUlong++;
			while(minUlong < maxUlong && data[minUlong] == 0) minUlong++;
			if(minUlong >= maxUlong) {
				minUlong = 0;
				maxUlong = 0;
			}
		}

		private void EnsureCapacity() {
			if(maxUlong < data.Length) return;
			ulong[] newData = new ulong[data.Length * 2];
			Array.Copy(data, 0, newData, data.Length, data.Length);
			data = newData;
		}
	}
}
