using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Buriko
{
    public sealed class BurikoMemory
    {
        private readonly Dictionary<string, int> _globalFlags =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _localFlags =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Dictionary<string, MemoryObject>> _scopes =
            new List<Dictionary<string, MemoryObject>>();

        public BurikoMemory()
        {
            ResetScope();
        }

        public int GetGlobalFlag(string name) => GetFlag(_globalFlags, name);
        public int GetLocalFlag(string name) => GetFlag(_localFlags, name);
        public void SetGlobalFlag(string name, int value) => _globalFlags[name] = value;
        public void SetLocalFlag(string name, int value) => _localFlags[name] = value;

        public BurikoMemorySnapshot CaptureSnapshot()
        {
            var scopes = new Dictionary<string, MemoryObjectSnapshot>[_scopes.Count];
            for (var i = 0; i < _scopes.Count; i++)
            {
                scopes[i] = new Dictionary<string, MemoryObjectSnapshot>(StringComparer.Ordinal);
                foreach (var pair in _scopes[i])
                {
                    scopes[i].Add(pair.Key, pair.Value.CaptureSnapshot());
                }
            }

            return new BurikoMemorySnapshot(
                new Dictionary<string, int>(_globalFlags, StringComparer.Ordinal),
                new Dictionary<string, int>(_localFlags, StringComparer.Ordinal),
                scopes);
        }

        public void RestoreSnapshot(BurikoMemorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _globalFlags.Clear();
            foreach (var pair in snapshot.GlobalFlags)
            {
                _globalFlags.Add(pair.Key, pair.Value);
            }

            _localFlags.Clear();
            foreach (var pair in snapshot.LocalFlags)
            {
                _localFlags.Add(pair.Key, pair.Value);
            }

            _scopes.Clear();
            for (var i = 0; i < snapshot.Scopes.Length; i++)
            {
                var scope = NewScope();
                foreach (var pair in snapshot.Scopes[i])
                {
                    scope.Add(pair.Key, MemoryObject.FromSnapshot(pair.Value));
                }

                _scopes.Add(scope);
            }

            if (_scopes.Count == 0)
            {
                _scopes.Add(NewScope());
            }
        }

        public void ResetScope()
        {
            _scopes.Clear();
            _scopes.Add(NewScope());
        }

        public void PushScope()
        {
            _scopes.Add(NewScope());
        }

        public void PopScope()
        {
            if (_scopes.Count <= 1)
            {
                throw new InvalidOperationException("Cannot remove the root Buriko scope.");
            }

            _scopes.RemoveAt(_scopes.Count - 1);
        }

        public void Declare(string type, string name, int members)
        {
            if (members < 1)
            {
                members = 1;
            }

            var scope = _scopes[_scopes.Count - 1];
            if (scope.ContainsKey(name))
            {
                throw new InvalidOperationException("Buriko variable already declared: " + name);
            }

            scope.Add(name, new MemoryObject(type, members));
        }

        public BurikoValue Get(BurikoReference reference)
        {
            var memory = FindMemory(reference.Name);
            if (memory != null)
            {
                return memory.Get(reference);
            }

            if (_localFlags.TryGetValue(reference.Name, out var local))
            {
                return BurikoValue.FromInt(local);
            }

            if (_globalFlags.TryGetValue(reference.Name, out var global))
            {
                return BurikoValue.FromInt(global);
            }

            return BurikoValue.FromInt(0);
        }

        public void Set(BurikoReference reference, BurikoValue value)
        {
            var memory = FindMemory(reference.Name)
                ?? throw new KeyNotFoundException("Buriko variable is not declared: " + reference.Name);
            memory.Set(reference, value, this);
        }

        private MemoryObject FindMemory(string name)
        {
            for (var i = _scopes.Count - 1; i >= 0; i--)
            {
                if (_scopes[i].TryGetValue(name, out var memory))
                {
                    return memory;
                }
            }

            return null;
        }

        private static int GetFlag(IReadOnlyDictionary<string, int> dictionary, string name)
        {
            return dictionary.TryGetValue(name, out var value) ? value : 0;
        }

        private static Dictionary<string, MemoryObject> NewScope()
        {
            return new Dictionary<string, MemoryObject>(StringComparer.Ordinal);
        }

        private sealed class MemoryObject
        {
            private readonly string _type;
            private readonly BurikoValue[] _values;
            private readonly Dictionary<string, int>[] _members;

            public MemoryObject(string type, int count)
            {
                _type = type;
                _values = new BurikoValue[count];
                _members = new Dictionary<string, int>[count];
                for (var i = 0; i < count; i++)
                {
                    _values[i] = type == "char" ? BurikoValue.FromString(string.Empty) : BurikoValue.FromInt(0);
                    _members[i] = new Dictionary<string, int>(StringComparer.Ordinal);
                }
            }

            public BurikoValue Get(BurikoReference reference)
            {
                var index = NormalizeIndex(reference.Index);
                if (reference.Member == null)
                {
                    return _values[index];
                }

                return BurikoValue.FromInt(
                    _members[index].TryGetValue(reference.Member.Name, out var value) ? value : 0);
            }

            public void Set(BurikoReference reference, BurikoValue value, BurikoMemory memory)
            {
                var index = NormalizeIndex(reference.Index);
                if (reference.Member == null)
                {
                    _values[index] = _type == "char"
                        ? BurikoValue.FromString(value.AsString(memory))
                        : BurikoValue.FromInt(value.AsInt(memory));
                    return;
                }

                _members[index][reference.Member.Name] = value.AsInt(memory);
            }

            public MemoryObjectSnapshot CaptureSnapshot()
            {
                var values = new BurikoValue[_values.Length];
                Array.Copy(_values, values, _values.Length);
                var members = new Dictionary<string, int>[_members.Length];
                for (var i = 0; i < _members.Length; i++)
                {
                    members[i] = new Dictionary<string, int>(_members[i], StringComparer.Ordinal);
                }

                return new MemoryObjectSnapshot(_type, values, members);
            }

            public static MemoryObject FromSnapshot(MemoryObjectSnapshot snapshot)
            {
                var result = new MemoryObject(snapshot.Type, snapshot.Values.Length);
                Array.Copy(snapshot.Values, result._values, snapshot.Values.Length);
                for (var i = 0; i < snapshot.Members.Length; i++)
                {
                    result._members[i].Clear();
                    foreach (var pair in snapshot.Members[i])
                    {
                        result._members[i].Add(pair.Key, pair.Value);
                    }
                }

                return result;
            }

            private int NormalizeIndex(int index)
            {
                var normalized = index < 0 ? 0 : index;
                if (normalized >= _values.Length)
                {
                    throw new IndexOutOfRangeException("Buriko array index is outside the declared range.");
                }

                return normalized;
            }
        }

        public sealed class BurikoMemorySnapshot
        {
            internal BurikoMemorySnapshot(
                Dictionary<string, int> globalFlags,
                Dictionary<string, int> localFlags,
                Dictionary<string, MemoryObjectSnapshot>[] scopes)
            {
                GlobalFlags = globalFlags;
                LocalFlags = localFlags;
                Scopes = scopes;
            }

            internal Dictionary<string, int> GlobalFlags { get; }
            internal Dictionary<string, int> LocalFlags { get; }
            internal Dictionary<string, MemoryObjectSnapshot>[] Scopes { get; }
        }

        internal sealed class MemoryObjectSnapshot
        {
            public MemoryObjectSnapshot(
                string type,
                BurikoValue[] values,
                Dictionary<string, int>[] members)
            {
                Type = type;
                Values = values;
                Members = members;
            }

            public string Type { get; }
            public BurikoValue[] Values { get; }
            public Dictionary<string, int>[] Members { get; }
        }
    }
}
