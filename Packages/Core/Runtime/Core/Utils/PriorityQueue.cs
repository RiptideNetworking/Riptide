// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;

namespace Riptide.Utils
{
    // PriorityQueue unfortunately doesn't exist in .NET Standard 2.1
    /// <summary>Represents a collection of items that have a value and a priority. On dequeue, the item with the lowest priority value is removed.</summary>
    /// <typeparam name="TElement">Specifies the type of elements in the queue.</typeparam>
    /// <typeparam name="TPriority">Specifies the type of priority associated with enqueued elements.</typeparam>
    public class PriorityQueue<TElement, TPriority>
    {
        /// <summary>Gets the number of elements contained in the <see cref="PriorityQueue{TElement, TPriority}"/>.</summary>
        public int Count { get; private set; }

        private const int DefaultCapacity = 8;
        private Entry<TElement, TPriority>[] heap;
        private readonly IComparer<TPriority> comparer;

        /// <summary>Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}"/> class.</summary>
        /// <param name="capacity">Initial capacity to allocate for the underlying heap array.</param>
        public PriorityQueue(int capacity = DefaultCapacity)
        {
            heap = new Entry<TElement, TPriority>[capacity];
            comparer = Comparer<TPriority>.Default;
        }

        /// <summary>Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}"/> class with the specified custom priority comparer.</summary>
        /// <param name="comparer">Custom comparer dictating the ordering of elements.</param>
        /// <param name="capacity">Initial capacity to allocate for the underlying heap array.</param>
        public PriorityQueue(IComparer<TPriority> comparer, int capacity = DefaultCapacity)
        {
            heap = new Entry<TElement, TPriority>[capacity];
            this.comparer = comparer;
        }

        /// <summary>Adds the specified element and associated priority to the <see cref="PriorityQueue{TElement, TPriority}"/>.</summary>
        /// <param name="element">The element to add.</param>
        /// <param name="priority">The priority with which to associate the new element.</param>
        public void Enqueue(TElement element, TPriority priority)
        {
            if (Count == heap.Length)
            {
                // Resizing is necessary
                Entry<TElement, TPriority>[] temp = new Entry<TElement, TPriority>[Count * 2];
                Array.Copy(heap, temp, heap.Length);
                heap = temp;
            }

            int index = Count;
            while (index > 0)
            {
                int parentIndex = GetParentIndex(index);
                if (comparer.Compare(priority, heap[parentIndex].Priority) < 0)
                {
                    heap[index] = heap[parentIndex];
                    index = parentIndex;
                }
                else
                    break;
            }

            heap[index] = new Entry<TElement, TPriority>(element, priority);
            Count++;
        }

        /// <summary>Removes and returns the lowest priority element.</summary>
        /// <exception cref="InvalidOperationException">The <see cref="PriorityQueue{TElement, TPriority}"/> is empty.</exception>
        public TElement Dequeue()
        {
            if (Count == 0)
                throw new InvalidOperationException($"Cannot dequeue from an empty {nameof(PriorityQueue<TElement, TPriority>)}!");

            TElement returnValue = heap[0].Element;
            Count--;

            if (Count > 0)
            {
                // The last element has to be moved into the gap left by the removed one, so sift it down
                // from the root until both of its children have a higher priority value than it does
                Entry<TElement, TPriority> last = heap[Count];
                int parent = 0;
                int leftChild = GetLeftChildIndex(parent);

                while (leftChild < Count)
                {
                    int rightChild = leftChild + 1;
                    int bestChild = (rightChild < Count && comparer.Compare(heap[rightChild].Priority, heap[leftChild].Priority) < 0) ? rightChild : leftChild;

                    if (comparer.Compare(last.Priority, heap[bestChild].Priority) <= 0)
                        break;

                    heap[parent] = heap[bestChild];
                    parent = bestChild;
                    leftChild = GetLeftChildIndex(parent);
                }

                heap[parent] = last;
            }

            return returnValue;
        }

        /// <summary>Removes the lowest priority element from the <see cref="PriorityQueue{TElement, TPriority}"/> and copies it and its associated priority to the <paramref name="element"/> and <paramref name="priority"/> arguments.</summary>
        /// <param name="element">When this method returns, contains the removed element.</param>
        /// <param name="priority">When this method returns, contains the priority associated with the removed element.</param>
        /// <returns>true if the element is successfully removed; false if the <see cref="PriorityQueue{TElement, TPriority}"/> is empty.</returns>
        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (Count > 0)
            {
                priority = heap[0].Priority;
                element = Dequeue();
                return true;
            }
            {
                element = default;
                priority = default;
                return false;
            }
        }

        /// <summary>Returns the lowest priority element.</summary>
        /// <exception cref="InvalidOperationException">The <see cref="PriorityQueue{TElement, TPriority}"/> is empty.</exception>
        public TElement Peek()
        {
            if (Count == 0)
                throw new InvalidOperationException($"Cannot peek an empty {nameof(PriorityQueue<TElement, TPriority>)}!");

            return heap[0].Element;
        }

        /// <summary>Returns the priority of the lowest priority element.</summary>
        /// <exception cref="InvalidOperationException">The <see cref="PriorityQueue{TElement, TPriority}"/> is empty.</exception>
        public TPriority PeekPriority()
        {
            if (Count == 0)
                throw new InvalidOperationException($"Cannot peek an empty {nameof(PriorityQueue<TElement, TPriority>)}!");

            return heap[0].Priority;
        }

        /// <summary>Removes all elements from the <see cref="PriorityQueue{TElement, TPriority}"/>.</summary>
        public void Clear()
        {
            Array.Clear(heap, 0, heap.Length);
            Count = 0;
        }

        private static int GetParentIndex(int index)
        {
            return (index - 1) / 2;
        }

        private static int GetLeftChildIndex(int index)
        {
            return (index * 2) + 1;
        }

        private readonly struct Entry<TEle, TPrio>
        {
            internal readonly TEle Element;
            internal readonly TPrio Priority;

            public Entry(TEle element, TPrio priority)
            {
                Element = element;
                Priority = priority;
            }
        }
    }
}
