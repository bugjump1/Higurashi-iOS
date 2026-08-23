using System;
using System.Collections.Generic;
using System.IO;

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
            SetLocalFlag("LTextColor", 0xFFFFFF);
        }

        public int GetGlobalFlag(string name) => GetFlag(_globalFlags, name);
        public int GetLocalFlag(string name) => GetFlag(_localFlags, name);
        public bool HasGlobalFlag(string name) => _globalFlags.ContainsKey(name);
        public void SetGlobalFlag(string name, int value) => _globalFlags[name] = value;
        public void SetLocalFlag(string name, int value) => _localFlags[name] = value;

        // The original engine mirrors a choice into both names. Different
        // scripts use different names when reading the selected option.
        public void SetChoiceResult(int index)
        {
            SetLocalFlag("SelectResult", index);
            SetLocalFlag("LOCALWORK_NO_RESULT", index);
        }

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

        internal void WritePersistentState(BinaryWriter writer)
        {
            WriteIntegerDictionary(writer, _globalFlags);
            WriteIntegerDictionary(writer, _localFlags);
            writer.Write(_scopes.Count);
            for (var i = 0; i < _scopes.Count; i++)
            {
                writer.Write(_scopes[i].Count);
                foreach (var pair in _scopes[i])
                {
                    writer.Write(pair.Key);
                    pair.Value.WritePersistentState(writer);
                }
            }
        }

        internal BurikoMemorySnapshot ReadPersistentState(BinaryReader reader)
        {
            var globalFlags = ReadIntegerDictionary(reader);
            var localFlags = ReadIntegerDictionary(reader);
            var scopeCount = ReadCount(reader, 1024, "scope");
            var scopes = new Dictionary<string, MemoryObjectSnapshot>[scopeCount];
            for (var i = 0; i < scopeCount; i++)
            {
                var itemCount = ReadCount(reader, 100000, "scope item");
                scopes[i] = new Dictionary<string, MemoryObjectSnapshot>(StringComparer.Ordinal);
                for (var j = 0; j < itemCount; j++)
                {
                    scopes[i].Add(reader.ReadString(), MemoryObject.ReadPersistentState(reader));
                }
            }

            return new BurikoMemorySnapshot(globalFlags, localFlags, scopes);
        }

        private static void WriteIntegerDictionary(
            BinaryWriter writer,
            IReadOnlyDictionary<string, int> dictionary)
        {
            writer.Write(dictionary.Count);
            foreach (var pair in dictionary)
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value);
            }
        }

        private static Dictionary<string, int> ReadIntegerDictionary(BinaryReader reader)
        {
            var count = ReadCount(reader, 100000, "flag");
            var result = new Dictionary<string, int>(count, StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                result.Add(reader.ReadString(), reader.ReadInt32());
            }
            return result;
        }

        private static int ReadCount(BinaryReader reader, int maximum, string description)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > maximum)
            {
                throw new InvalidDataException("Invalid Buriko " + description + " count: " + count);
            }
            return count;
        }

        private static void WriteValue(BinaryWriter writer, BurikoValue value)
        {
            writer.Write((short)value.Kind);
            switch (value.Kind)
            {
                case BurikoValueKind.Int:
                case BurikoValueKind.Bool:
                    writer.Write(value.Integer);
                    break;
                case BurikoValueKind.String:
                    writer.Write(value.Text ?? string.Empty);
                    break;
                case BurikoValueKind.Variable:
                    WriteReference(writer, value.Reference);
                    break;
            }
        }

        private static BurikoValue ReadValue(BinaryReader reader)
        {
            var kind = (BurikoValueKind)reader.ReadInt16();
            switch (kind)
            {
                case BurikoValueKind.Null:
                    return BurikoValue.Null;
                case BurikoValueKind.Int:
                    return BurikoValue.FromInt(reader.ReadInt32());
                case BurikoValueKind.Bool:
                    return BurikoValue.FromBool(reader.ReadInt32() != 0);
                case BurikoValueKind.String:
                    return BurikoValue.FromString(reader.ReadString());
                case BurikoValueKind.Variable:
                    return BurikoValue.FromReference(ReadReference(reader));
                case BurikoValueKind.None:
                    return default;
                default:
                    throw new InvalidDataException("Invalid Buriko value kind: " + kind);
            }
        }

        private static void WriteReference(BinaryWriter writer, BurikoReference reference)
        {
            if (reference == null)
            {
                throw new InvalidDataException("A Buriko variable value has no reference.");
            }
            writer.Write(reference.Name);
            writer.Write(reference.Index);
            writer.Write(reference.Member != null);
            if (reference.Member != null)
            {
                WriteReference(writer, reference.Member);
            }
        }

        private static BurikoReference ReadReference(BinaryReader reader)
        {
            var name = reader.ReadString();
            var index = reader.ReadInt32();
            var member = reader.ReadBoolean() ? ReadReference(reader) : null;
            return new BurikoReference(name, index, member);
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

            public void WritePersistentState(BinaryWriter writer)
            {
                writer.Write(_type ?? string.Empty);
                writer.Write(_values.Length);
                for (var i = 0; i < _values.Length; i++)
                {
                    WriteValue(writer, _values[i]);
                    writer.Write(_members[i].Count);
                    foreach (var pair in _members[i])
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }
                }
            }

            public static MemoryObjectSnapshot ReadPersistentState(BinaryReader reader)
            {
                var type = reader.ReadString();
                var count = ReadCount(reader, 100000, "memory value");
                var values = new BurikoValue[count];
                var members = new Dictionary<string, int>[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = ReadValue(reader);
                    members[i] = ReadIntegerDictionary(reader);
                }
                return new MemoryObjectSnapshot(type, values, members);
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
