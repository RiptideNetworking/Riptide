// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections;
using System.Collections.Generic;

namespace Riptide.Utils
{
	/// <summary>A list, that can remove and add the first element in O(1) time.</summary>
	/// <remarks>I also added SetUnchecked, since it was useful.</remarks>
	/// <typeparam name="T"></typeparam>
    internal class WrappingList<T> : IEnumerable<T>
	{
		private T[] buffer;
		private int start;
		private int count;

		internal WrappingList(int initialCapacity = 16)
			=> Initialize(NextPowerOfTwo(initialCapacity));

		private void Initialize(int initialCapacity) {
			buffer = new T[initialCapacity];
			start = 0;
			count = 0;
		}

		internal T this[int index] {
			get {
				if(index >= count || index < 0) throw new IndexOutOfRangeException($"index: {index} Count: {count}");
				return buffer[IndexConverter(index)];
			}
			set {
				if(index >= count || index < 0) throw new IndexOutOfRangeException($"index: {index} Count: {count}");
				buffer[IndexConverter(index)] = value;
			}
		}

		internal int Count => count;
		/// <summary>This will always be a power of 2</summary>
		internal int Capacity => buffer.Length;
		
		/// <summary>This is why Capacity has to be a power of 2</summary>
		private int IndexConverter(int i) => (start + i) & (Capacity - 1);

		internal void AddFirst(T item) {
			if(count == Capacity) Resize();
			start = IndexConverter(Capacity - 1);
			buffer[start] = item;
			count++;
		}

		internal void RemoveFirst() {
			if(count == 0) throw new InvalidOperationException("List is empty.");
			buffer[start] = default;
			start = IndexConverter(1);
			count--;
		}

		internal void Add(T item) {
			if(count == Capacity) Resize();
			this[count++] = item;
		}

		internal void Remove() {
			if(count == 0) throw new InvalidOperationException("List is empty.");
			this[--count] = default;
		}

		internal void Clear() => Initialize(16);

		public IEnumerator<T> GetEnumerator() {
			for(int i = 0; i < count; i++)
				yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		internal void SetUnchecked(int index, T item) {
			if(index >= count) {
				if(++index > Capacity) SetCapacity(index);
				count = --index;
				Add(item);
				return;
			}
			if(index < 0) {
				int newSize = count - index;
				if(newSize > Capacity) SetCapacity(newSize);
				start = IndexConverter(++index);
				count -= index;
				AddFirst(item);
				return;
			}
			this[index] = item;
		}

		internal void AddRange(IEnumerable<T> items) {
			foreach(T item in items) Add(item);
		}

		internal int IndexOf(T item) {
			for(int i = 0; i < count; i++) 
				if(this[i].Equals(item)) return i;
			return -1;
		}

		private void Resize() => SetCapacityUnchecked(Capacity << 1);

		internal void SetCapacity(int capacity) {
			capacity = NextPowerOfTwo(capacity);
			if(capacity <= Capacity) return;
			SetCapacityUnchecked(capacity);
		}

		private void SetCapacityUnchecked(int capacity) {
			T[] newBuffer = new T[capacity];
			for(int i = 0; i < count; i++)
				newBuffer[i] = this[i];
			buffer = newBuffer;
			start = 0;
		}

		private int NextPowerOfTwo(int x) {
			if(x <= 1) return 1;
			x--;
			x |= x >> 1;
			x |= x >> 2;
			x |= x >> 4;
			x |= x >> 8;
			x |= x >> 16;
			return x + 1;
		}
	}
}