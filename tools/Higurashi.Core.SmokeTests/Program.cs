using Higurashi.IOS.Data;
using Higurashi.IOS.Input;
using Higurashi.IOS.Playback;
using Higurashi.IOS.Buriko;
using System.Text;

internal static class Program
{
    private static int Main()
    {
        var tests = new Action[]
        {
            SingleTapAdvances,
            SwipeUpOpensHistory,
            ThreeFingerLeftStartsFastForward,
            ThreeFingerRightStartsFastRewind,
            NewTouchStopsFastTraversal,
            TimelineClearsFutureBranch,
            TimelineHonorsCapacity,
            FastTraversalRendersOneStepPerTick,
            SafePathRejectsTraversal,
            AssetCascadeFallsBackInOrder,
            CompiledScriptHeaderIsParsed,
            Episode02OperationCatalogNormalizesShiftedModCodes,
            BurikoRuntimeExecutesDialogueAndFlags,
            BurikoRuntimeCallsAndReturnsFromScript,
            BurikoRuntimeSnapshotRestoresExecutionAndMemory,
            BurikoRuntimePersistentStateRoundTrips,
            BurikoRuntimeHandlesModCrossScriptSectionCall
        };

        foreach (var test in tests)
        {
            test();
            Console.WriteLine("PASS " + test.Method.Name);
        }

        Console.WriteLine($"{tests.Length} smoke tests passed.");
        return 0;
    }

    private static void SingleTapAdvances()
    {
        var input = new TouchGestureInterpreter();
        Equal(NovelInputAction.None, Frame(input, 0, false, P(1, 500, 300, PointerPhase.Began)));
        Equal(NovelInputAction.Advance, Frame(input, 0.1, false, P(1, 502, 301, PointerPhase.Ended)));
    }

    private static void Episode02OperationCatalogNormalizesShiftedModCodes()
    {
        BurikoOperationCatalog.ConfigureForEpisode(2);
        var loading = BurikoOperationCatalog.Get(122);
        Equal((short)148, loading.Code);
        Equal("SetValidityOfLoading", loading.Name);
        Equal("b", loading.Signature);

        var voice = BurikoOperationCatalog.Get(132);
        Equal((short)130, voice.Code);
        Equal("ModPlayVoiceLS", voice.Name);
        Equal("iisib", voice.Signature);

        BurikoOperationCatalog.ConfigureForEpisode(1);
        Equal("ModPlayVoiceLS", BurikoOperationCatalog.Get(130).Name);
    }

    private static void SwipeUpOpensHistory()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false, P(1, 500, 100, PointerPhase.Began));
        Equal(NovelInputAction.OpenHistory, Frame(input, 0.2, false, P(1, 500, 500, PointerPhase.Ended)));
    }

    private static void ThreeFingerLeftStartsFastForward()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false,
            P(1, 800, 300, PointerPhase.Began),
            P(2, 800, 400, PointerPhase.Began),
            P(3, 800, 500, PointerPhase.Began));
        Equal(NovelInputAction.StartFastForward, Frame(input, 0.05, false,
            P(1, 500, 300, PointerPhase.Moved),
            P(2, 500, 400, PointerPhase.Moved),
            P(3, 500, 500, PointerPhase.Moved)));
    }

    private static void ThreeFingerRightStartsFastRewind()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false,
            P(1, 200, 300, PointerPhase.Began),
            P(2, 200, 400, PointerPhase.Began),
            P(3, 200, 500, PointerPhase.Began));
        Equal(NovelInputAction.StartFastRewind, Frame(input, 0.05, false,
            P(1, 500, 300, PointerPhase.Moved),
            P(2, 500, 400, PointerPhase.Moved),
            P(3, 500, 500, PointerPhase.Moved)));
    }

    private static void NewTouchStopsFastTraversal()
    {
        var input = new TouchGestureInterpreter();
        Frame(input, 0, false,
            P(1, 800, 300, PointerPhase.Began),
            P(2, 800, 400, PointerPhase.Began),
            P(3, 800, 500, PointerPhase.Began));
        Frame(input, 0.05, false,
            P(1, 500, 300, PointerPhase.Moved),
            P(2, 500, 400, PointerPhase.Moved),
            P(3, 500, 500, PointerPhase.Moved));
        Frame(input, 0.1, true,
            P(1, 500, 300, PointerPhase.Ended),
            P(2, 500, 400, PointerPhase.Ended),
            P(3, 500, 500, PointerPhase.Ended));

        Equal(NovelInputAction.StopFastTraversal,
            Frame(input, 0.2, true, P(4, 600, 300, PointerPhase.Began)));
    }

    private static void TimelineClearsFutureBranch()
    {
        var timeline = new CheckpointTimeline<int>(10);
        timeline.Push(1);
        timeline.Push(2);
        timeline.Push(3);
        True(timeline.TryMovePrevious(out var previous) && previous == 2);
        timeline.Push(9);
        Equal(3, timeline.Count);
        True(!timeline.CanMoveNext);
        True(timeline.TryGetCurrent(out var current) && current == 9);
    }

    private static void TimelineHonorsCapacity()
    {
        var timeline = new CheckpointTimeline<int>(3);
        timeline.Push(1);
        timeline.Push(2);
        timeline.Push(3);
        timeline.Push(4);
        Equal(3, timeline.Count);
        True(timeline.TryMovePrevious(out var previous) && previous == 3);
    }

    private static void FastTraversalRendersOneStepPerTick()
    {
        var driver = new CountingDriver();
        var traversal = new FastTraversalController(10);
        traversal.StartForward();
        True(traversal.Tick(1f, driver));
        Equal(1, driver.ForwardSteps);
    }

    private static void SafePathRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "higurashi-safe-path-test");
        var safe = SafePath.ResolveUnderRoot(root, "StreamingAssets/CG/test.png");
        True(safe.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));

        var rejected = false;
        try
        {
            SafePath.ResolveUnderRoot(root, "../escape.txt");
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

        True(rejected);
    }

    private static void AssetCascadeFallsBackInOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "higurashi-cascade-" + Guid.NewGuid().ToString("N"));
        try
        {
            var expected = Path.Combine(root, "StreamingAssets", "CG", "sprite", "rena0.png");
            Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
            File.WriteAllText(expected, "fixture");

            var resolver = new AssetCascadeResolver(root);
            True(resolver.TryResolve(
                "sprite/rena0.png",
                new[] { "CGAlt", "CG" },
                out var resolved));
            Equal(Path.GetFullPath(expected), resolved);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void CompiledScriptHeaderIsParsed()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(Encoding.ASCII.GetBytes("MGSC"));
            writer.Write(1);
            writer.Write(1);
            writer.Write(1);
            writer.Write(2);
            writer.Write("main");
            writer.Write(0);
            writer.Write(0);
            writer.Write((short)0);
        }

        stream.Position = 0;
        var script = CompiledScriptContainer.Read(stream);
        Equal(2, script.Data.Length);
        Equal(1, script.Blocks.Count);
        Equal(1, script.LineOffsets.Count);
    }

    private static void BurikoRuntimeExecutesDialogueAndFlags()
    {
        var data = BuildBytecode(writer =>
        {
            WriteLineNumber(writer, 0);
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GTest");
                WriteIntValue(writer, 7);
            });
            WriteOperation(writer, 17, () =>
            {
                WriteNullValue(writer);
                WriteStringValue(writer, "hello");
                WriteIntValue(writer, 0);
            });
            WriteOperation(writer, 11, null);
            writer.Write((short)0);
        });

        var host = new CapturingHost();
        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            host);
        runtime.Start();

        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        Equal(7, runtime.Memory.GetGlobalFlag("GTest"));
        Equal("hello", host.LastDialogue);

        runtime.ResumeInput();
        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
    }

    private static void BurikoRuntimeCallsAndReturnsFromScript()
    {
        var init = BuildBytecode(writer =>
        {
            WriteOperation(writer, 6, () => WriteStringValue(writer, "flow"));
            writer.Write((short)0);
        });
        var flow = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GFlowReached");
                WriteIntValue(writer, 1);
            });
            writer.Write((short)0);
        });

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(
                ("init", WrapScript(init)),
                ("flow", WrapScript(flow))),
            new CapturingHost());
        runtime.Start();

        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(1, runtime.Memory.GetGlobalFlag("GFlowReached"));
    }

    private static void BurikoRuntimeSnapshotRestoresExecutionAndMemory()
    {
        var data = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GCheckpoint");
                WriteIntValue(writer, 1);
            });
            WriteOperation(writer, 11, null);
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GCheckpoint");
                WriteIntValue(writer, 2);
            });
            WriteOperation(writer, 11, null);
            writer.Write((short)0);
        });

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            new CapturingHost());
        runtime.Start();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        var snapshot = runtime.CaptureSnapshot();

        runtime.ResumeInput();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        Equal(2, runtime.Memory.GetGlobalFlag("GCheckpoint"));

        runtime.RestoreSnapshot(snapshot);
        Equal(BurikoBlockReason.WaitForInput, runtime.BlockReason);
        Equal(1, runtime.Memory.GetGlobalFlag("GCheckpoint"));
        runtime.ResumeInput();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());
        Equal(2, runtime.Memory.GetGlobalFlag("GCheckpoint"));
    }

    private static void BurikoRuntimeHandlesModCrossScriptSectionCall()
    {
        var init = BuildBytecode(writer =>
        {
            WriteOperation(writer, 127, () =>
            {
                WriteStringValue(writer, "flow");
                WriteStringValue(writer, "entry");
            });
            writer.Write((short)0);
        });
        var flowData = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GModSectionReached");
                WriteIntValue(writer, 1);
            });
            writer.Write((short)0);
        });
        var flow = WrapScriptWithBlock(flowData, "entry");

        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(
                ("init", WrapScript(init)),
                ("flow", flow)),
            new CapturingHost());
        runtime.Start();

        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(1, runtime.Memory.GetGlobalFlag("GModSectionReached"));
    }

    private static void BurikoRuntimePersistentStateRoundTrips()
    {
        var data = BuildBytecode(writer =>
        {
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GPersistent");
                WriteIntValue(writer, 7);
            });
            WriteOperation(writer, 11, null);
            WriteOperation(writer, 3, () =>
            {
                WriteReferenceValue(writer, "GPersistent");
                WriteIntValue(writer, 9);
            });
            writer.Write((short)0);
        });
        var runtime = new BurikoRuntime(
            new DictionaryScriptRepository(("init", WrapScript(data))),
            new CapturingHost());
        runtime.Start();
        Equal(BurikoBlockReason.WaitForInput, runtime.RunUntilBlocked());

        using var state = new MemoryStream();
        runtime.WritePersistentState(state);
        runtime.ResumeInput();
        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(9, runtime.Memory.GetGlobalFlag("GPersistent"));

        state.Position = 0;
        runtime.ReadPersistentState(state);
        Equal(BurikoBlockReason.WaitForInput, runtime.BlockReason);
        Equal(7, runtime.Memory.GetGlobalFlag("GPersistent"));
        runtime.ResumeInput();
        Equal(BurikoBlockReason.Completed, runtime.RunUntilBlocked());
        Equal(9, runtime.Memory.GetGlobalFlag("GPersistent"));
    }

    private static byte[] BuildBytecode(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static CompiledScriptContainer WrapScript(byte[] data)
    {
        return WrapScriptWithBlock(data, "main");
    }

    private static CompiledScriptContainer WrapScriptWithBlock(byte[] data, string blockName)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(Encoding.ASCII.GetBytes("MGSC"));
            writer.Write(1);
            writer.Write(1);
            writer.Write(0);
            writer.Write(data.Length);
            writer.Write(blockName);
            writer.Write(0);
            writer.Write(data);
        }

        stream.Position = 0;
        return CompiledScriptContainer.Read(stream);
    }

    private static void WriteLineNumber(BinaryWriter writer, int line)
    {
        writer.Write((short)1);
        writer.Write(line);
    }

    private static void WriteOperation(BinaryWriter writer, short code, Action writeArguments)
    {
        writer.Write((short)2);
        writer.Write(code);
        writeArguments?.Invoke();
    }

    private static void WriteNullValue(BinaryWriter writer)
    {
        writer.Write((short)1);
    }

    private static void WriteIntValue(BinaryWriter writer, int value)
    {
        writer.Write((short)2);
        writer.Write(value);
    }

    private static void WriteStringValue(BinaryWriter writer, string value)
    {
        writer.Write((short)3);
        writer.Write(value);
    }

    private static void WriteReferenceValue(BinaryWriter writer, string name, int index = -1)
    {
        writer.Write((short)5);
        writer.Write(name);
        WriteIntValue(writer, index);
        writer.Write(false);
    }

    private static NovelInputAction Frame(
        TouchGestureInterpreter interpreter,
        double time,
        bool fast,
        params PointerSample[] pointers)
    {
        return interpreter.ProcessFrame(pointers, 1000, 700, time, fast);
    }

    private static PointerSample P(int id, float x, float y, PointerPhase phase)
    {
        return new PointerSample(id, x, y, phase);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    private static void True(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed.");
        }
    }

    private sealed class CountingDriver : INovelTraversalDriver
    {
        public int ForwardSteps { get; private set; }

        public bool StepForward()
        {
            ForwardSteps++;
            return true;
        }

        public bool StepBackward()
        {
            return false;
        }
    }

    private sealed class DictionaryScriptRepository : IBurikoScriptRepository
    {
        private readonly Dictionary<string, CompiledScriptContainer> _scripts;

        public DictionaryScriptRepository(params (string Name, CompiledScriptContainer Script)[] scripts)
        {
            _scripts = scripts.ToDictionary(item => item.Name, item => item.Script, StringComparer.OrdinalIgnoreCase);
        }

        public CompiledScriptContainer Load(string scriptName)
        {
            return _scripts.TryGetValue(scriptName, out var script)
                ? script
                : throw new FileNotFoundException(scriptName);
        }
    }

    private sealed class CapturingHost : IBurikoHost
    {
        public string LastDialogue { get; private set; }

        public BurikoHostResponse Execute(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            if (invocation.Specification.Code == 17)
            {
                LastDialogue = invocation.Arguments[1].AsString(memory);
            }

            return BurikoHostResponse.Continue;
        }
    }
}
