// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Riptide.Utils
{
	/// <summary>A list, that can remove the first element in O(1) time.</summary>
	/// <remarks>I also added SetUnchecked, since it was useful.</remarks>
	/// <typeparam name="T"></typeparam>
    internal class WrappingList<T> : IEnumerable<T>
	{
		private T[] buffer;
		private int capacity;
		private int start;
		private int count;

		internal WrappingList(int initialCapacity = 16) => Initialize(initialCapacity);

		private void Initialize(int initialCapacity) {
			capacity = initialCapacity;
			buffer = new T[capacity];
			start = 0;
			count = 0;
		}

		internal T this[int index] {
			get {
				if(index >= count || index < 0) throw new IndexOutOfRangeException($"index: {index} Count: {Count}");
				return buffer[IndexConverter(index)];
			}
			set {
				if(index >= count || index < 0) throw new IndexOutOfRangeException($"index: {index} Count: {Count}");
				buffer[IndexConverter(index)] = value;
			}
		}

		internal int Count => count;
		internal int Capacity => capacity;
		
		private int IndexConverter(int i) => (start + i) % capacity;

		internal void AddFirst(T item) {
			if(count == capacity) Resize();
			start = IndexConverter(capacity - 1);
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
			if(count == capacity) Resize();
			this[count++] = item;
		}

		internal void Remove() {
			if(count == 0) throw new InvalidOperationException("List is empty.");
			this[--count] = default;
		}

		internal void Clear() => Initialize(16);

		private void Resize() {
			int newCapacity = capacity * 2;
			T[] newBuffer = new T[newCapacity];

			for(int i = 0; i < count; i++)
				newBuffer[i] = this[i];

			buffer = newBuffer;
			capacity = newCapacity;
			start = 0;
		}

		public IEnumerator<T> GetEnumerator() {
			for(int i = 0; i < count; i++)
				yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		internal void SetUnchecked(int index, T item) {
			if(index >= 0 && index < Count) {
				this[index] = item;
				return;
			}
			while(Count < index) Add(default);
			Add(item);
		}

		internal void AddRange(IEnumerable<T> items) {
			foreach(T item in items) Add(item);
		}

		internal bool Contains(T item) => this.Any(i => i.Equals(item));

		internal int IndexOf(T item) {
			for(int i = 0; i < Count; i++) 
				if(this[i].Equals(item)) return i;
			return -1;
		}

		internal T[] ToArray() {
			T[] array = new T[Count];
			for(int i = 0; i < Count; i++)
				array[i] = this[i];
			return array;
		}

		internal List<T> ToList() => new List<T>(ToArray());
	}
}