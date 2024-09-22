// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System.Collections.Generic;

namespace Riptide.Utils
{
    internal class MovingList<T>
	{
		private readonly List<T> list;
		private int start;
        private int end;

		internal void Add(T item) {
			if(end == list.Count) {
				list.Add(item);
			} else {
				list[end] = item;
			}
			end++;
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
	}
}