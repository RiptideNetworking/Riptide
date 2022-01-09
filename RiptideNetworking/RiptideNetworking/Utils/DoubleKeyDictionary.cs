
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections;
using System.Collections.Generic;

namespace RiptideNetworking.Utils
{
    /// <summary>Represents a collection of two keys for each value.</summary>
    /// <typeparam name="TKey1">Key type 1.</typeparam>
    /// <typeparam name="TKey2">Key type 2.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    public class DoubleKeyDictionary<TKey1, TKey2, TValue> : IEnumerable
    {
        /// <summary>A dictionary mapping key 1s to values.</summary>
        private readonly Dictionary<TKey1, TValue> key1Dictionary;
        /// <summary>A dictionary mapping key 2s to values.</summary>
        private readonly Dictionary<TKey2, TValue> key2Dictionary;

        /// <summary>Gets a collection containing the first set of keys in the dictionary.</summary>
        public IEnumerable<TKey1> FirstKeys => key1Dictionary.Keys;
        /// <summary>Gets a collection containing the second set of keys in the dictionary.</summary>
        public IEnumerable<TKey2> SecondKeys => key2Dictionary.Keys;
        /// <summary>Gets a collection containing the values in the dictionary.</summary>
        public IEnumerable<TValue> Values => key1Dictionary.Values;
        /// <summary>Gets the number of key1/key2/value sets in the dictionary.</summary>
        public int Count => key1Dictionary.Count;

        /// <summary>Initializes a new instance of the <see cref="DoubleKeyDictionary{TKey1, TKey2, TValue}"/> class that is empty and has the default initial capacity.</summary>
        public DoubleKeyDictionary()
        {
            key1Dictionary = new Dictionary<TKey1, TValue>();
            key2Dictionary = new Dictionary<TKey2, TValue>();
        }

        /// <summary>Initializes a new instance of the <see cref="DoubleKeyDictionary{TKey1, TKey2, TValue}"/> class that is empty and has the specefied initial capacity.</summary>
        /// <param name="capacity">The initial number of elements that the <see cref="DoubleKeyDictionary{TKey1, TKey2, TValue}"/> can contain.</param>
        public DoubleKeyDictionary(int capacity)
        {
            key1Dictionary = new Dictionary<TKey1, TValue>(capacity);
            key2Dictionary = new Dictionary<TKey2, TValue>(capacity);
        }

        /// <summary>Returns an enumerator that iterates through the dictionary.</summary>
        public IEnumerator GetEnumerator() => key1Dictionary.GetEnumerator();

        /// <summary>Adds the specified keys and value to the dictionary.</summary>
        /// <param name="key1">The key 1 of the element to add.</param>
        /// <param name="key2">The key 2 of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            if (key1 == null || key2 == null)
                throw new ArgumentNullException();

            key1Dictionary.Add(key1, value);
            key2Dictionary.Add(key2, value);
        }

        /// <summary>Gets the value associated with the key 1.</summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns><see langword="true"/> if the dictionary contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(TKey1 key, out TValue value) => key1Dictionary.TryGetValue(key, out value);

        /// <summary>Gets the value associated with the key 2.</summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns><see langword="true"/> if the dictionary contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(TKey2 key, out TValue value) => key2Dictionary.TryGetValue(key, out value);

        /// <summary>Determines whether the dictionary contains the specified key 1.</summary>
        /// <param name="key">The key to locate in the dictionary.</param>
        /// <returns><see langword="true"/> if the dictionary contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
        public bool ContainsKey(TKey1 key) => key1Dictionary.ContainsKey(key);

        /// <summary>Determines whether the dictionary contains the specified key 2.</summary>
        /// <param name="key">The key to locate in the dictionary.</param>
        /// <returns><see langword="true"/> if the dictionary contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
        public bool ContainsKey(TKey2 key) => key2Dictionary.ContainsKey(key);

        /// <summary>Removes the value with the specified keys from the dictionary</summary>
        /// <param name="key1">The key 1 of the element to remove.</param>
        /// <param name="key2">The key 2 of the element to remove.</param>
        /// <returns><see langword="true"/> if the element is successfully found and removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(TKey1 key1, TKey2 key2)
        {
            if (ContainsKey(key1))
            {
                key1Dictionary.Remove(key1);
                key2Dictionary.Remove(key2);
                return true;
            }
            return false;
        }

        /// <summary>Removes all keys and values from the dictionary.</summary>
        public void Clear()
        {
            key1Dictionary.Clear();
            key2Dictionary.Clear();
        }

        /// <summary>Gets the value associated with the specified key 1.</summary>
        /// <param name="key">The key 1 of the value to get.</param>
        /// <returns>The value associated with the specified key 1.</returns>
        public TValue this[TKey1 key] => key1Dictionary[key];

        /// <summary>Gets the value associated with the specified key 2.</summary>
        /// <param name="key">The key 2 of the value to get.</param>
        /// <returns>The value associated with the specified key 2.</returns>
        public TValue this[TKey2 key] => key2Dictionary[key];
    }
}
