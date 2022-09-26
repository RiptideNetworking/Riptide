// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;

namespace Riptide.Utils
{
    // PriorityQueue doesn't exist in .NET Standard 2.1
    /// <summary>Represents a collection of items that have a value and a priority. On dequeue, the item with the lowest priority value is removed.</summary>
    /// <typeparam name="TElement">Specifies the type of elements in the queue.</typeparam>
    /// <typeparam name="TPriority">Specifies the type of priority associated with enqueued elements.</typeparam>
    internal class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        /// <summary>Gets the number of elements contained in the <see cref="PriorityQueue{TElement, TPriority}"/>.</summary>
        public int Count => entries.Count;

        // Using a list is probably not optimal
        private readonly List<Entry<TElement, TPriority>> entries = new List<Entry<TElement, TPriority>>();

        /// <summary>Adds the specified element and associated priority to the <see cref="PriorityQueue{TElement, TPriority}"/>.</summary>
        /// <param name="element">The element to add.</param>
        /// <param name="priority">The priority with which to associate the new element.</param>
        public void Enqueue(TElement element, TPriority priority)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (priority.CompareTo(entries[i].Priority) < 0)
                {
                    entries.Insert(i, new Entry<TElement, TPriority>(element, priority));
                    return;
                }
            }

            entries.Add(new Entry<TElement, TPriority>(element, priority));
        }

        /// <summary>Removes and returns the lowest priority element.</summary>
        public TElement Dequeue()
        {
            Entry<TElement, TPriority> entry = entries[0];
            entries.RemoveAt(0);
            return entry.Element;
        }

        /// <summary>Returns the priority of the lowest priority element.</summary>
        public TPriority PeekPriority()
        {
            return entries[0].Priority;
        }

        /// <summary>Removes all elements from the <see cref="PriorityQueue{TElement, TPriority}"/>.</summary>
        public void Clear()
        {
            entries.Clear();
        }

        private struct Entry<TEle, TPrio>
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
