// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Riptide.Utils
{
	/// <summary>A list, that can remove the first element in O(1) time.</summary>
	/// <remarks>I also added SetUnchecked, since it was useful.</remarks>
	/// <typeparam name="T"></typeparam>
    internal class MovingList<T> : IEnumerable<T>
	{
		private readonly List<T> list = new List<T>();
		private int start = 0;
        private int end = 0;

		internal void Add(T item) {
			if(end == list.Count) list.Add(item);
			else list[end] = item;
			end++;
		}

		internal void RemoveLast() {
			if(Count <= 0) throw new System.InvalidOperationException("Cannot remove from an empty list");
			list[--end] = default;
		}

		internal void RemoveFirst() {
			if(Count <= 0) throw new System.InvalidOperationException("Cannot remove from an empty list");
			list[start++] = default;
			if(start > list.Count / 2) RearrangeList();
		}

		private void RearrangeList() {
			int c = Count;
			for(int i = 0; i < c; i++) {
				list[i] = list[i + start];
				list[i + start] = default;
			}
			start = 0;
			end = c;
		}

		internal void Clear() {
			list.Clear();
			start = 0;
			end = 0;
		}

		internal void SetUnchecked(int index, T item) {
			if(index >= 0 && index < Count) {
				this[index] = item;
				return;
			}
			while(Count < index) Add(default);
			Add(item);
		}

		internal T this[int index] {
			get {
				int i = start + index;
				if(i >= end || index < 0) throw new System.IndexOutOfRangeException($"index: {index} Count: {Count}");
				return list[i];
			}
			set {
				int i = start + index;
				if(i >= end || index < 0) throw new System.IndexOutOfRangeException($"index: {index} Count: {Count}");
				list[i] = value;
			}
		}

		internal int Count => end - start;
		internal int Capacity => list.Capacity - start;

		internal void AddRange(IEnumerable<T> items) {
			foreach(T item in items) Add(item);
		}

		internal bool Contains(T item) => this.Any(i => i.Equals(item));

		internal int IndexOf(T item) {
		for(int i = 0; i < Count; i++) 
			if(this[i].Equals(item)) return i;
		return -1;
	}

		public IEnumerator<T> GetEnumerator() {
            for(int i = start; i < end; i++) {
                yield return list[i];
            }
        }

		IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
	}
}