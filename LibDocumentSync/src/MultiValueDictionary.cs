using System;
using System.Collections.Generic;

namespace DocumentSync {
    public class MultiValueDictionary<TKey, TValue> {
        Dictionary<TKey, List<TValue>> mValues;

        public MultiValueDictionary() {
            mValues = new Dictionary<TKey, List<TValue>>();
        }

        public void Add(TKey key, TValue item) {
            if (!mValues.ContainsKey(key))
                mValues.Add(key, new List<TValue>());
            mValues[key].Add(item);
        }

        public Dictionary<TKey, List<TValue>>.Enumerator GetEnumerator() {
            return mValues.GetEnumerator();
        }

        public bool RemoveAll(TKey key) {
            return mValues.Remove(key);
        }
    }
}
