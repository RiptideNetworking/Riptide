// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) not Tom Weiland but me
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

		internal FastBigInt CopySlice(int start, int length) {
			int memLength = Math.Min(length, maxIndex - minIndex + 1);
			FastBigInt slice = new FastBigInt(length);
			Buffer.BlockCopy(data, start * sizeof(ulong), slice.data, 0, memLength * sizeof(ulong));
			slice.maxIndex = length - 1;
			slice.AdjustMinAndMax();
			return slice;
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
			maxIndex = Math.Max(maxIndex, value.maxIndex + 2);
			EnsureCapacity();
			int offset = value.minIndex;
			int len = value.maxIndex - offset + 2;
			FastBigInt slice = value.CopySlice(offset, len);
			slice.Mult(mult);
			ulong carry = 0;
			for(int i = 0; i < len; i++) {
				(data[i + offset], carry) = AddUlong(data[i + offset], carry);
				ulong tempCarry;
				(data[i + offset], tempCarry) = AddUlong(data[i + offset], slice.data[i]);
				carry += tempCarry;
			}
			AdjustMinAndMax();
			if(carry != 0) throw new OverflowException("Addition overflow.");
		}

		internal void Mult(ulong mult) {
			if(mult.IsPowerOf2()) {
				LeftShift(mult.Log2());
				return;
			}
			maxIndex++;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minIndex; i <= maxIndex; i++) {
				ulong prevCarry = carry;
				(data[i], carry) = MultiplyUlong(data[i], mult);
				data[i] += prevCarry;
			}
			AdjustMinAndMax();
			if(carry != 0) throw new OverflowException("Multiplication overflow.");
		}

		internal ulong DivReturnMod(ulong div) {
			if(div == 0) throw new DivideByZeroException("Divisor cannot be zero.");

			if(div.IsPowerOf2()) {
				byte rightShift = div.Log2();
				return RightShift(rightShift);
			}
			// This should be replaced by minIndex = 0;
			// however for this case this is better performance
			// and won't throw an exeption if used correctly
			if(minIndex > 0) minIndex--;

			ulong carry = 0;
			for(int i = maxIndex; i >= minIndex; i--)
				(data[i], carry) = DivideUlong(data[i], carry, div);
			AdjustMinAndMax();
			if(minIndex != 0 && carry != 0) throw new OverflowException("Division overflow. \nYour \"Add\" and \"Get\" methods of your message probably don't line up correctly.");
			return carry;
		}

		internal void LeftShift(byte shiftBits) {
			if(shiftBits == 0) return;
			if(shiftBits == 64) {
				LeftShift1ULong();
				return;
			}
			if(shiftBits > 64) throw new ArgumentOutOfRangeException(nameof(shiftBits), "Shift bits cannot be greater than 64.");
			maxIndex++;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minIndex; i <= maxIndex; i++) {
				ulong val;
				ulong prevCarry = carry;
				(val, carry) = LeftShiftUlong(data[i], shiftBits);
				data[i] = val + prevCarry;
			}
			AdjustMinAndMax();
			if(carry != 0) throw new OverflowException("Left shift overflow.");
		}

		internal ulong RightShift(byte shiftBits) {
			if(shiftBits == 0) return 0;
			if(shiftBits == 64) return RightShift1ULong();
			if(shiftBits > 64) throw new ArgumentOutOfRangeException(nameof(shiftBits), "Shift bits cannot be greater than 64.");
			if(minIndex > 0) minIndex--;
			ulong carry = 0;
			for(int i = maxIndex; i >= minIndex; i--) {
				ulong val;
				ulong prevCarry = carry;
				(val, carry) = RightShiftUlong(data[i], shiftBits);
				data[i] = val + prevCarry;
			}
			AdjustMinAndMax();
			if(minIndex != 0 && carry != 0) throw new OverflowException("Right shift overflow.");
			return carry >> (64 - shiftBits);
		}

		internal void LeftShift1ULong() {
			maxIndex++;
			EnsureCapacity();
			ulong carry = 0;
			for(int i = minIndex; i <= maxIndex; i++)
				(data[i], carry) = (carry, data[i]);
			AdjustMinAndMax();
			if(carry != 0) throw new OverflowException("Left shift overflow.");
		}

		internal ulong RightShift1ULong() {
			if(minIndex > 0) minIndex--;
			ulong carry = 0;
			for(int i = maxIndex; i >= minIndex; i--)
				(data[i], carry) = (carry, data[i]);
			if(minIndex != 0 && carry != 0) throw new OverflowException("Right shift overflow.");
			return carry;
		}

		private void AdjustMinAndMax() {
			while(maxIndex > 0 && data[maxIndex] == 0) maxIndex--;
			while(minIndex < maxIndex && data[minIndex] == 0) minIndex++;
			if(maxIndex == 0) minIndex = 0;
		}

		private void EnsureCapacity() {
			if(maxIndex < data.Length) return;
			ulong[] newData = new ulong[data.Length * 2];
			Array.Copy(data, 0, newData, 0, data.Length);
			data = newData;
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
			ulong value = val << shift;
			ulong carry = val >> (64 - shift);
			return (value, carry);
		}

		public static (ulong value, ulong carry) RightShiftUlong(ulong val, int shift) {
			ulong value = val >> shift;
			ulong carry = val << (64 - shift);
			return (value, carry);
		}

		// this has not been tested at all
		public static (ulong value, ulong carry) DivideUlong(ulong val, ulong carry, ulong div) {
			if(carry == 0) return (val / div, val % div);
			ulong extra = IntermediateDivide(ref val, carry, div);
			return (val / div + extra, val % div);
		}

		private static ulong IntermediateDivide(ref ulong val, ulong carry, ulong div) {
			if(div <= uint.MaxValue) {
				ulong intermediate = val >> 32 | carry << 32;
				val &= uint.MaxValue;
				(ulong interDiv, ulong interMod) = (intermediate / div, intermediate % div);
				val |= interMod << 32;
				return interDiv << 32;
			}

			ulong value = 0;
			int shift = 0;
			while(carry >> shift != 0UL) {
				shift++;
				value <<= 1;
				carry <<= 1;
				carry += val >> 63;
				val <<= 1;
				if(carry < div) continue;
				carry -= div;
				value |= 1;
			}
			val >>= shift;
			int invShift = 64 - shift;
			val |= carry << invShift;
			return value << invShift;
		}
	}
}
