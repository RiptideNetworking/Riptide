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
        private int maxIndex = 0;
        private int minIndex = 0;

		internal FastBigInt(int initialCapacity) {
			data = new ulong[initialCapacity];
		}

		internal FastBigInt(int initialCapacity, ulong initialValue) {
			data = new ulong[initialCapacity];
			data[0] = initialValue;
		}

		internal FastBigInt(int capacity, byte[] bytes) {
			data = new ulong[capacity / sizeof(ulong) + 1];
			Buffer.BlockCopy(bytes, 0, data, 0, capacity);
			maxIndex = data.Length - 1;
			AdjustMinAndMax();
		}

		internal FastBigInt Copy() {
			FastBigInt copy = new FastBigInt(data.Length);
			Buffer.BlockCopy(data, 0, copy.data, 0, data.Length * sizeof(ulong));
			copy.maxIndex = maxIndex;
			copy.minIndex = minIndex;
			return copy;
		}

		public override string ToString() {
			StringBuilder s = new StringBuilder(2 * (maxIndex - minIndex) + 6);
			for(int i = 0; i <= maxIndex; i++) {
				s.Append(data[i]);
				s.Append(' ');
			}
			s.Append('\n');
			s.Append("min: ");
			s.Append(minIndex);
			s.Append(" max: ");
			s.Append(maxIndex);
			return s.ToString();
		}

		public string ToStringBinary() {
			StringBuilder s = new StringBuilder(2 * (maxIndex - minIndex) + 6);
			for(int i = 0; i <= maxIndex; i++) {
				s.Append(Convert.ToString((long)data[i], 2).PadLeft(64, '0'));
				s.Append(' ');
			}
			s.Append('\n');
			s.Append("min: ");
			s.Append(minIndex);
			s.Append(" max: ");
			s.Append(maxIndex);
			return s.ToString();
		}

		internal ulong[] GetData() => data;
		internal bool HasReadNothing => data[0] == 0 && maxIndex == 0;

		internal int GetBytesInUse() {
			ulong max = data[maxIndex];
			int bytes = maxIndex * sizeof(ulong);
			while(max > 0) {
				max >>= 8;
				bytes++;
			}
			return bytes;
		}

		internal void Add(FastBigInt value, ulong mult) {
			minIndex = Math.Min(minIndex, value.minIndex);
			maxIndex = Math.Max(maxIndex, value.maxIndex + 1);
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minIndex; i < maxIndex; i++) {
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
			maxIndex += 1;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minIndex; i <= maxIndex; i++) {
				ulong prevCarry = carry;
				(data[i], carry) = CMath.MultiplyUlong(data[i], mult);
				data[i] += prevCarry;
			}
			AdjustMinAndMax();
		}

		internal ulong DivReturnMod(ulong div) {
			if(div == 0) throw new DivideByZeroException("Divisor cannot be zero.");

			if(div.IsPowerOf2()) {
				byte rightShift = div.Log2();
				return RightShift(rightShift) >> (64 - rightShift);
			}
			if(minIndex > 0) minIndex -= 1;
			ulong carry = 0;
			for(int i = maxIndex * 2; i >= 0; i--) {
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
			if(shiftBits > 64) throw new ArgumentOutOfRangeException(nameof(shiftBits), "Shift bits cannot be greater than 64.");
			maxIndex += 1;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minIndex; i <= maxIndex; i++) {
				ulong val;
				ulong prevCarry = carry;
				(val, carry) = CMath.LeftShiftUlong(data[i], shiftBits);
				data[i] = val + prevCarry;
			}
			AdjustMinAndMax();
		}

		internal ulong RightShift(byte shiftBits) {
			if(shiftBits > 64) throw new ArgumentOutOfRangeException(nameof(shiftBits), "Shift bits cannot be greater than 64.");
			if(minIndex > 0) minIndex -= 1;
			ulong carry = 0;
			for(int i = maxIndex; i >= 0; i--) {
				ulong val;
				ulong prevCarry = carry;
				(val, carry) = CMath.RightShiftUlong(data[i], shiftBits);
				data[i] = val + prevCarry;
			}
			AdjustMinAndMax();
			return carry;
		}

		private void AdjustMinAndMax() {
			while(maxIndex > 0 && data[maxIndex] == 0) maxIndex--;
			while(minIndex < 0) minIndex++;
			while(minIndex < maxIndex && data[minIndex] == 0) minIndex++;
			if(minIndex > maxIndex) {
				minIndex = 0;
				maxIndex = 0;
			}
		}

		private void EnsureCapacity() {
			if(maxIndex < data.Length) return;
			ulong[] newData = new ulong[data.Length * 2];
			Array.Copy(data, 0, newData, 0, data.Length);
			data = newData;
		}
	}
}
