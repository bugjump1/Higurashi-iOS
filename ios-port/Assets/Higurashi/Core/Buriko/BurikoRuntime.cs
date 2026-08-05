using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Higurashi.IOS.Buriko
{
    public sealed class BurikoRuntime
    {
        private const int PersistentStateMagic = 0x31524248; // HBR1
        private const int DefaultCommandBudget = 100_000;
        private readonly IBurikoScriptRepository _scripts;
        private readonly IBurikoHost _host;
        private readonly Stack<ScriptFrame> _callStack = new Stack<ScriptFrame>();
        private ScriptFrame _current;
        private int _remainingWaitMilliseconds;

        public BurikoRuntime(IBurikoScriptRepository scripts, IBurikoHost host)
        {
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Memory = new BurikoMemory();
        }

        public BurikoMemory Memory { get; }
        public BurikoBlockReason BlockReason { get; private set; }
        public Exception LastError { get; private set; }
        public string CurrentScriptName => _current?.Name;
        public int CurrentLine => _current?.LineNumber ?? 0;
        public int CallDepth => _callStack.Count + (_current == null ? 0 : 1);

        public void Start(string scriptName = "init")
        {
            DisposeFrames();
            Memory.ResetScope();
            _current = CreateFrame(scriptName, "main");
            BlockReason = BurikoBlockReason.None;
            LastError = null;
            _remainingWaitMilliseconds = 0;
        }

        public BurikoRuntimeSnapshot CaptureSnapshot()
        {
            if (_current == null)
            {
                throw new InvalidOperationException("Buriko runtime has not been started.");
            }

            var callers = _callStack
                .Select(frame => new BurikoFrameSnapshot(
                    frame.Name,
                    frame.Reader.BaseStream.Position,
                    frame.LineNumber))
                .ToArray();

            return new BurikoRuntimeSnapshot(
                new BurikoFrameSnapshot(
                    _current.Name,
                    _current.Reader.BaseStream.Position,
                    _current.LineNumber),
                callers,
                Memory.CaptureSnapshot(),
                BlockReason,
                _remainingWaitMilliseconds);
        }

        public void RestoreSnapshot(BurikoRuntimeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            DisposeFrames();
            Memory.RestoreSnapshot(snapshot.Memory);

            // Stack enumeration is top-to-bottom, so rebuild it in reverse order.
            for (var i = snapshot.CallersTopToBottom.Length - 1; i >= 0; i--)
            {
                _callStack.Push(CreateFrame(snapshot.CallersTopToBottom[i]));
            }

            _current = CreateFrame(snapshot.Current);
            BlockReason = snapshot.BlockReason;
            _remainingWaitMilliseconds = snapshot.RemainingWaitMilliseconds;
            LastError = null;
        }

        public void WritePersistentState(Stream output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            if (_current == null)
            {
                throw new InvalidOperationException("Buriko runtime has not been started.");
            }

            using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                writer.Write(PersistentStateMagic);
                WriteFrame(writer, new BurikoFrameSnapshot(
                    _current.Name,
                    _current.Reader.BaseStream.Position,
                    _current.LineNumber));
                writer.Write(_callStack.Count);
                foreach (var frame in _callStack)
                {
                    WriteFrame(writer, new BurikoFrameSnapshot(
                        frame.Name,
                        frame.Reader.BaseStream.Position,
                        frame.LineNumber));
                }
                Memory.WritePersistentState(writer);
                writer.Write((int)BlockReason);
                writer.Write(_remainingWaitMilliseconds);
            }
        }

        public void ReadPersistentState(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            BurikoRuntimeSnapshot snapshot;
            using (var reader = new BinaryReader(input, Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != PersistentStateMagic)
                {
                    throw new InvalidDataException("This is not a Higurashi iOS Buriko save state.");
                }
                var current = ReadFrame(reader);
                var callerCount = reader.ReadInt32();
                if (callerCount < 0 || callerCount > 1024)
                {
                    throw new InvalidDataException("Invalid Buriko caller count: " + callerCount);
                }
                var callers = new BurikoFrameSnapshot[callerCount];
                for (var i = 0; i < callers.Length; i++)
                {
                    callers[i] = ReadFrame(reader);
                }
                var memory = Memory.ReadPersistentState(reader);
                var blockReason = (BurikoBlockReason)reader.ReadInt32();
                var remainingWaitMilliseconds = reader.ReadInt32();
                snapshot = new BurikoRuntimeSnapshot(
                    current,
                    callers,
                    memory,
                    blockReason,
                    remainingWaitMilliseconds);
            }

            RestoreSnapshot(snapshot);
        }

        private static void WriteFrame(BinaryWriter writer, BurikoFrameSnapshot frame)
        {
            writer.Write(frame.ScriptName);
            writer.Write(frame.Position);
            writer.Write(frame.LineNumber);
        }

        private static BurikoFrameSnapshot ReadFrame(BinaryReader reader)
        {
            return new BurikoFrameSnapshot(
                reader.ReadString(),
                reader.ReadInt64(),
                reader.ReadInt32());
        }

        public BurikoBlockReason RunUntilBlocked(int commandBudget = DefaultCommandBudget)
        {
            if (_current == null)
            {
                throw new InvalidOperationException("Buriko runtime has not been started.");
            }

            if (BlockReason != BurikoBlockReason.None)
            {
                return BlockReason;
            }

            try
            {
                for (var executed = 0; executed < commandBudget; executed++)
                {
                    ExecuteNextCommand();
                    if (BlockReason != BurikoBlockReason.None)
                    {
                        return BlockReason;
                    }
                }

                throw new InvalidOperationException("Buriko command budget exhausted without reaching a wait point.");
            }
            catch (Exception exception)
            {
                LastError = exception;
                BlockReason = BurikoBlockReason.Faulted;
                return BlockReason;
            }
        }

        public void ResumeInput()
        {
            if (BlockReason == BurikoBlockReason.WaitForInput ||
                BlockReason == BurikoBlockReason.Choice ||
                BlockReason == BurikoBlockReason.Host)
            {
                BlockReason = BurikoBlockReason.None;
            }
        }

        // Certain original UI states (the Episode 08 Fragment browser) begin a
        // scenario script after the player chooses an item. Keep the active
        // frame on the call stack so its instruction after the modal resumes
        // exactly like the PC state machine.
        public void CallScriptFromUi(string scriptName, string block = "main")
        {
            if (_current == null)
            {
                throw new InvalidOperationException("Buriko runtime has not been started.");
            }
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                throw new ArgumentException("A script name is required.", nameof(scriptName));
            }

            CallScript(scriptName, string.IsNullOrWhiteSpace(block) ? "main" : block);
            BlockReason = BurikoBlockReason.None;
            _remainingWaitMilliseconds = 0;
            LastError = null;
        }

        public void AdvanceTime(int elapsedMilliseconds)
        {
            if (BlockReason != BurikoBlockReason.WaitForTime)
            {
                return;
            }

            _remainingWaitMilliseconds -= Math.Max(0, elapsedMilliseconds);
            if (_remainingWaitMilliseconds <= 0)
            {
                _remainingWaitMilliseconds = 0;
                BlockReason = BurikoBlockReason.None;
            }
        }

        private void ExecuteNextCommand()
        {
            var reader = _current.Reader;
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                throw new EndOfStreamException("Buriko script ended without a Return command: " + _current.Name);
            }

            var command = reader.ReadInt16();
            switch (command)
            {
                case 0:
                    Return();
                    break;
                case 1:
                    _current.LineNumber = reader.ReadInt32();
                    break;
                case 2:
                    ExecuteOperation(reader.ReadInt16(), reader);
                    break;
                case 3:
                    var condition = ReadValue(reader).AsInt(Memory);
                    var falseTarget = reader.ReadInt32();
                    if (condition != 1)
                    {
                        JumpToPosition(falseTarget);
                    }
                    break;
                case 4:
                    var type = reader.ReadString();
                    var name = reader.ReadString();
                    var memberValue = ReadValue(reader);
                    var members = memberValue.Kind == BurikoValueKind.Null ? 1 : memberValue.AsInt(Memory);
                    Memory.Declare(type, name, members);
                    break;
                case 5:
                    var reference = ReadReference(reader, true);
                    Memory.Set(reference, ReadValue(reader));
                    break;
                case 6:
                    JumpToPosition(reader.ReadInt32());
                    break;
                default:
                    throw new InvalidDataException("Unknown Buriko command: " + command);
            }
        }

        private BurikoValue ExecuteOperation(short operationCode, BinaryReader reader)
        {
            var specification = BurikoOperationCatalog.Get(operationCode);
            var arguments = new BurikoValue[specification.Signature.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = ReadValue(reader);
            }

            // Catalog variants may normalize an episode-specific raw opcode to
            // the canonical operation code used by the shared runtime/host.
            switch (specification.Code)
            {
                case 0:
                    Memory.SetLocalFlag(ReferenceName(arguments[0]), arguments[1].AsInt(Memory));
                    return BurikoValue.Null;
                case 1:
                    return BurikoValue.FromInt(Memory.GetLocalFlag(ReferenceName(arguments[0])));
                case 2:
                    Memory.SetLocalFlag(ReferenceName(arguments[0]), arguments[1].AsInt(Memory));
                    return BurikoValue.Null;
                case 3:
                    Memory.SetGlobalFlag(ReferenceName(arguments[0]), arguments[1].AsInt(Memory));
                    return BurikoValue.Null;
                case 4:
                    return BurikoValue.FromInt(Memory.GetLocalFlag(ReferenceName(arguments[0])));
                case 5:
                    return BurikoValue.FromInt(Memory.GetGlobalFlag(ReferenceName(arguments[0])));
                case 6:
                    CallScript(arguments[0].AsString(Memory), "main");
                    return BurikoValue.Null;
                case 7:
                    JumpScript(arguments[0].AsString(Memory), "main");
                    return BurikoValue.Null;
                case 8:
                    CallScript(_current.Name, arguments[0].AsString(Memory));
                    return BurikoValue.Null;
                case 9:
                    JumpSection(arguments[0].AsString(Memory));
                    return BurikoValue.Null;
                case 10:
                    _remainingWaitMilliseconds = Math.Max(0, arguments[0].AsInt(Memory));
                    BlockReason = _remainingWaitMilliseconds == 0
                        ? BurikoBlockReason.None
                        : BurikoBlockReason.WaitForTime;
                    return BurikoValue.Null;
                case 11:
                    BlockReason = BurikoBlockReason.WaitForInput;
                    return BurikoValue.Null;
                case 12:
                    Memory.SetLocalFlag("__CanSkip", arguments[0].AsBool(Memory) ? 1 : 0);
                    return BurikoValue.Null;
                case 13:
                    Memory.SetLocalFlag("__CanSave", arguments[0].AsBool(Memory) ? 1 : 0);
                    return BurikoValue.Null;
                case 14:
                    Memory.SetLocalFlag("__CanInput", arguments[0].AsBool(Memory) ? 1 : 0);
                    return BurikoValue.Null;
                case 150:
                    Return();
                    return BurikoValue.Null;
                case 162:
                    JumpSection(arguments[0].AsString(Memory));
                    return BurikoValue.Null;
                case 166:
                    JumpScript(arguments[0].AsString(Memory), arguments[1].AsString(Memory));
                    return BurikoValue.Null;
                case 127:
                    CallScript(arguments[0].AsString(Memory), arguments[1].AsString(Memory));
                    return BurikoValue.Null;
                default:
                    var response = _host.Execute(
                        new BurikoOperationInvocation(specification, arguments),
                        Memory);
                    if (response.BlockReason != BurikoBlockReason.None)
                    {
                        BlockReason = response.BlockReason;
                    }

                    return response.ReturnValue;
            }
        }

        private BurikoValue ReadValue(BinaryReader reader)
        {
            var type = reader.ReadInt16();
            switch (type)
            {
                case 1:
                    return BurikoValue.Null;
                case 2:
                    return BurikoValue.FromInt(reader.ReadInt32());
                case 3:
                    return BurikoValue.FromString(reader.ReadString());
                case 4:
                    return BurikoValue.FromBool(reader.ReadBoolean());
                case 5:
                    return BurikoValue.FromReference(ReadReferenceBody(reader));
                case 6:
                    return ExecuteOperation(reader.ReadInt16(), reader);
                case 8:
                    var math = reader.ReadInt16();
                    var left = ReadValue(reader).AsInt(Memory);
                    var right = ReadValue(reader).AsInt(Memory);
                    return BurikoValue.FromInt(PerformMath(math, left, right));
                default:
                    throw new InvalidDataException("Unknown Buriko value type: " + type);
            }
        }

        private BurikoReference ReadReference(BinaryReader reader, bool expectMarker)
        {
            if (expectMarker && reader.ReadInt16() != 5)
            {
                throw new InvalidDataException("Buriko assignment target is not a variable.");
            }

            return ReadReferenceBody(reader);
        }

        private BurikoReference ReadReferenceBody(BinaryReader reader)
        {
            var name = reader.ReadString();
            var index = ReadValue(reader).AsInt(Memory);
            var member = reader.ReadBoolean() ? ReadReference(reader, true) : null;
            return new BurikoReference(name, index, member);
        }

        private void CallScript(string name, string block)
        {
            _callStack.Push(_current);
            Memory.PushScope();
            _current = CreateFrame(name, block);
        }

        private void JumpScript(string name, string block)
        {
            _callStack.Clear();
            Memory.ResetScope();
            _current = CreateFrame(name, block);
        }

        private void JumpSection(string block)
        {
            _current.JumpToBlock(block);
        }

        private void Return()
        {
            if (_callStack.Count == 0)
            {
                BlockReason = BurikoBlockReason.Completed;
                return;
            }

            _current.Dispose();
            _current = _callStack.Pop();
            Memory.PopScope();
        }

        private ScriptFrame CreateFrame(string scriptName, string block)
        {
            var normalized = Path.GetFileNameWithoutExtension(scriptName).ToLowerInvariant();
            var container = _scripts.Load(normalized);
            var frame = new ScriptFrame(normalized, container);
            frame.JumpToBlock(block);
            return frame;
        }

        private ScriptFrame CreateFrame(BurikoFrameSnapshot snapshot)
        {
            var frame = CreateFrame(snapshot.ScriptName, "main");
            if (snapshot.Position < 0 || snapshot.Position > frame.Reader.BaseStream.Length)
            {
                frame.Dispose();
                throw new InvalidDataException("Buriko snapshot position is outside the script data segment.");
            }

            frame.Reader.BaseStream.Position = snapshot.Position;
            frame.LineNumber = snapshot.LineNumber;
            return frame;
        }

        private void DisposeFrames()
        {
            _current?.Dispose();
            _current = null;
            while (_callStack.Count > 0)
            {
                _callStack.Pop().Dispose();
            }
        }

        private void JumpToPosition(int position)
        {
            if (position < 0 || position > _current.Reader.BaseStream.Length)
            {
                throw new InvalidDataException("Buriko jump target is outside the script data segment.");
            }

            _current.Reader.BaseStream.Position = position;
        }

        private static string ReferenceName(BurikoValue value)
        {
            if (value.Kind != BurikoValueKind.Variable || value.Reference == null)
            {
                throw new InvalidDataException("Buriko operation expected a variable reference.");
            }

            return value.Reference.Name;
        }

        private static int PerformMath(short operation, int left, int right)
        {
            switch (operation)
            {
                case 0: return left == right ? 1 : 0;
                case 1: return left != right ? 1 : 0;
                case 2: return left <= right ? 1 : 0;
                case 3: return left >= right ? 1 : 0;
                case 4: return left < right ? 1 : 0;
                case 5: return left > right ? 1 : 0;
                case 6: return left + right;
                case 7: return left - right;
                case 8: return left * right;
                case 9: return left / right;
                case 10: return left % right;
                default: throw new InvalidDataException("Unknown Buriko math operation: " + operation);
            }
        }

        private sealed class ScriptFrame : IDisposable
        {
            public ScriptFrame(string name, CompiledScriptContainer container)
            {
                Name = name;
                Container = container;
                Reader = new BinaryReader(new MemoryStream(container.Data, false), Encoding.UTF8, false);
            }

            public string Name { get; }
            public CompiledScriptContainer Container { get; }
            public BinaryReader Reader { get; }
            public int LineNumber { get; set; }

            public void JumpToBlock(string name)
            {
                if (!Container.Blocks.TryGetValue(name, out var position))
                {
                    throw new KeyNotFoundException("Buriko block was not found: " + name);
                }

                Reader.BaseStream.Position = position;
            }

            public void Dispose()
            {
                Reader.Dispose();
            }
        }
    }

    public sealed class BurikoRuntimeSnapshot
    {
        internal BurikoRuntimeSnapshot(
            BurikoFrameSnapshot current,
            BurikoFrameSnapshot[] callersTopToBottom,
            BurikoMemory.BurikoMemorySnapshot memory,
            BurikoBlockReason blockReason,
            int remainingWaitMilliseconds)
        {
            Current = current;
            CallersTopToBottom = callersTopToBottom;
            Memory = memory;
            BlockReason = blockReason;
            RemainingWaitMilliseconds = remainingWaitMilliseconds;
        }

        internal BurikoFrameSnapshot Current { get; }
        internal BurikoFrameSnapshot[] CallersTopToBottom { get; }
        internal BurikoMemory.BurikoMemorySnapshot Memory { get; }
        internal BurikoBlockReason BlockReason { get; }
        internal int RemainingWaitMilliseconds { get; }
    }

    internal readonly struct BurikoFrameSnapshot
    {
        public BurikoFrameSnapshot(string scriptName, long position, int lineNumber)
        {
            ScriptName = scriptName;
            Position = position;
            LineNumber = lineNumber;
        }

        public string ScriptName { get; }
        public long Position { get; }
        public int LineNumber { get; }
    }
}
