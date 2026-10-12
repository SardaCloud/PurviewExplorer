// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections;
using System.Collections.Generic;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>
    /// Ordered, case-insensitive map used for JSON objects and Purview items
    /// (PowerShell property names are case-insensitive).
    /// </summary>
    public sealed class JsonMap : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> _entries = new List<KeyValuePair<string, object>>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int Count => _entries.Count;

        public IEnumerable<string> Keys
        {
            get { foreach (var e in _entries) yield return e.Key; }
        }

        public object this[string key]
        {
            get => TryGetValue(key, out var v) ? v : null;
            set
            {
                if (_index.TryGetValue(key, out var i))
                    _entries[i] = new KeyValuePair<string, object>(_entries[i].Key, value);
                else
                {
                    _index[key] = _entries.Count;
                    _entries.Add(new KeyValuePair<string, object>(key, value));
                }
            }
        }

        public bool ContainsKey(string key) => key != null && _index.ContainsKey(key);

        public bool TryGetValue(string key, out object value)
        {
            if (key != null && _index.TryGetValue(key, out var i))
            {
                value = _entries[i].Value;
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>Inserts (or moves) a key to the given position.</summary>
        public void Insert(int position, string key, object value)
        {
            Remove(key);
            position = Math.Max(0, Math.Min(position, _entries.Count));
            _entries.Insert(position, new KeyValuePair<string, object>(key, value));
            Reindex();
        }

        public bool Remove(string key)
        {
            if (key == null || !_index.TryGetValue(key, out var i)) return false;
            _entries.RemoveAt(i);
            Reindex();
            return true;
        }

        private void Reindex()
        {
            _index.Clear();
            for (int i = 0; i < _entries.Count; i++) _index[_entries[i].Key] = i;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
