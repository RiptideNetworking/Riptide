using System;
using System.Collections;
using System.Collections.Generic;

namespace RiptideNetworking
{
    public class DoubleKeyDictionary<TKey1, TKey2, TValue> : IEnumerable
    {
        private Dictionary<TKey1, TValue> key1Dictionary;
        private Dictionary<TKey2, TValue> key2Dictionary;

        public IEnumerable<TKey1> FirstKeys => key1Dictionary.Keys;
        public IEnumerable<TKey2> SecondKeys => key2Dictionary.Keys;
        public IEnumerable<TValue> Values => key1Dictionary.Values;
        public int Count => key1Dictionary.Count;

        public DoubleKeyDictionary()
        {
            key1Dictionary = new Dictionary<TKey1, TValue>();
            key2Dictionary = new Dictionary<TKey2, TValue>();
        }
        
        public DoubleKeyDictionary(int capacity)
        {
            key1Dictionary = new Dictionary<TKey1, TValue>(capacity);
            key2Dictionary = new Dictionary<TKey2, TValue>(capacity);
        }

        public IEnumerator GetEnumerator() => key1Dictionary.GetEnumerator();

        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            if (key1 == null || key2 == null)
                throw new ArgumentNullException();

            key1Dictionary.Add(key1, value);
            key2Dictionary.Add(key2, value);
        }

        public bool TryGetValue(TKey1 key, out TValue value) => key1Dictionary.TryGetValue(key, out value);

        public bool TryGetValue(TKey2 key, out TValue value) => key2Dictionary.TryGetValue(key, out value);

        public bool ContainsKey(TKey1 key) => key1Dictionary.ContainsKey(key);

        public bool ContainsKey(TKey2 key) => key2Dictionary.ContainsKey(key);

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

        public void Clear()
        {
            key1Dictionary.Clear();
            key2Dictionary.Clear();
        }

        public TValue this[TKey1 key]
        {
            get => key1Dictionary[key];
        }

        public TValue this[TKey2 key]
        {
            get => key2Dictionary[key];
        }
    }
}
