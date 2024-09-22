// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Riptide.Utils
{
    internal class MovingList<T> : IEnumerable<T>
	{
		private readonly List<T> list = new List<T>();
		private int start = 0;
        private int end = 0;

		internal void Add(T item) {
			if(end == list.Count) {
				list.Add(item);
			} else {
				list[end] = item;
			}
			end++;
		}

		internal T RemoveLast() {
			T item = list[--end];
			list[end] = default;
			return item;
		}

		internal T RemoveFirst() {
			T item = list[start];
			list[start++] = default;
			if(start > list.Count / 2) RearrangeList();
			return item;
		}

		protected void RearrangeList() {
			for(int i = start; i < end; i++) {
				list[i - start] = list[i];
				list[i] = default;
			}
			end -= start;
			start = 0;
		}

		internal void Clear() {
			list.Clear();
			start = 0;
			end = 0;
		}

		internal T this[int index] {
			get {
				if(start + index >= end || index < 0) throw new System.IndexOutOfRangeException();
				return list[start + index];
			}
			set {
				if(start + index >= end || index < 0) throw new System.IndexOutOfRangeException();
				list[index] = value;
			}
		}

		internal int Count => end - start;

		internal void AddRange(IEnumerable<T> items) {
			foreach(T item in items) Add(item);
		}

		internal bool Contains(T item) => this.Any(i => i.Equals(item));

		internal int IndexOf(T item) {
			int i = -1;
			if(this.Any(it => ++i >= 0 & it.Equals(item))) return i;
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