using Higurashi.IOS.Buriko;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: Higurashi.RuntimeProbe <compiled-script-folder> [fallback-folder...]");
            return 2;
        }

        var startFromTitle = false;
        var stopAtTips = false;
        var episodeNumber = 1;
        var directories = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--start")
            {
                startFromTitle = true;
            }
            else if (args[i] == "--until-tips")
            {
                stopAtTips = true;
            }
            else if (args[i] == "--episode" && i + 1 < args.Length)
            {
                episodeNumber = int.Parse(args[++i]);
            }
            else
            {
                directories.Add(args[i]);
            }
        }
        if (directories.Count == 0)
        {
            Console.Error.WriteLine("No compiled-script folder was supplied.");
            return 2;
        }

        BurikoOperationCatalog.ConfigureForEpisode(episodeNumber);
        var host = new ProbeHost(startFromTitle, stopAtTips);
        var runtime = new BurikoRuntime(
            new DirectoryBurikoScriptRepository(directories.ToArray()), host);
        runtime.Start("init");

        for (var waits = 0; waits < 100_000; waits++)
        {
            var reason = runtime.RunUntilBlocked();
            switch (reason)
            {
                case BurikoBlockReason.WaitForTime:
                    runtime.AdvanceTime(int.MaxValue);
                    break;
                case BurikoBlockReason.WaitForInput:
                    if (startFromTitle && !stopAtTips && host.DialogueCount >= 5)
                    {
                        Console.WriteLine("Runtime reached five real dialogue checkpoints after title.");
                        PrintSummary(runtime, host);
                        return 0;
                    }
                    runtime.ResumeInput();
                    break;
                case BurikoBlockReason.Host:
                    Console.WriteLine("Runtime reached host boundary: " + host.LastBoundary);
                    PrintSummary(runtime, host);
                    return 0;
                case BurikoBlockReason.Completed:
                    Console.WriteLine("Runtime completed.");
                    PrintSummary(runtime, host);
                    return 0;
                case BurikoBlockReason.Faulted:
                    Console.Error.WriteLine(
                        $"Runtime fault at {runtime.CurrentScriptName}:{runtime.CurrentLine}\n{runtime.LastError}");
                    PrintSummary(runtime, host);
                    return 1;
                default:
                    Console.Error.WriteLine("Unexpected runtime block: " + reason);
                    return 1;
            }
        }

        Console.Error.WriteLine("Runtime probe exceeded its wait budget.");
        return 1;
    }

    private static void PrintSummary(BurikoRuntime runtime, ProbeHost host)
    {
        Console.WriteLine("  script: " + runtime.CurrentScriptName);
        Console.WriteLine("  line: " + runtime.CurrentLine);
        Console.WriteLine("  host operations: " + host.OperationCount);
        Console.WriteLine("  dialogue lines: " + host.DialogueCount);
        Console.WriteLine("  last dialogue: " + (host.LastDialogue ?? "<none>"));
    }

    private sealed class ProbeHost : IBurikoHost
    {
        private readonly bool _startFromTitle;
        private readonly bool _stopAtTips;

        public ProbeHost(bool startFromTitle, bool stopAtTips)
        {
            _startFromTitle = startFromTitle;
            _stopAtTips = stopAtTips;
        }

        public long OperationCount { get; private set; }
        public long DialogueCount { get; private set; }
        public string LastDialogue { get; private set; }
        public string LastBoundary { get; private set; }

        public BurikoHostResponse Execute(BurikoOperationInvocation invocation, BurikoMemory memory)
        {
            OperationCount++;
            switch (invocation.Specification.Code)
            {
                case 16:
                    DialogueCount++;
                    LastDialogue = invocation.Arguments[3].AsString(memory);
                    if (WaitsForInput(invocation.Arguments[4].AsInt(memory)))
                    {
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.WaitForInput);
                    }
                    break;
                case 17:
                    DialogueCount++;
                    LastDialogue = invocation.Arguments[1].AsString(memory);
                    if (WaitsForInput(invocation.Arguments[2].AsInt(memory)))
                    {
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.WaitForInput);
                    }
                    break;
                case 101:
                    LastBoundary = "TitleScreen";
                    if (_startFromTitle)
                    {
                        memory.SetLocalFlag("LOCALWORK_NO_RESULT", 0);
                        break;
                    }
                    return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                case 89:
                    if (_startFromTitle)
                    {
                        memory.SetLocalFlag("LOCALWORK_NO_RESULT", 1);
                    }
                    break;
                case 86:
                    LastBoundary = "ShowTips(" + invocation.Arguments[0].AsInt(memory) + ")";
                    if (_stopAtTips)
                    {
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                    }
                    break;
                case 87:
                    LastBoundary = "ShowChapterScreen";
                    if (_stopAtTips)
                    {
                        return new BurikoHostResponse(BurikoValue.Null, BurikoBlockReason.Host);
                    }
                    break;
            }

            return BurikoHostResponse.Continue;
        }

        public void CommitPendingPresentation()
        {
        }

        private static bool WaitsForInput(int textMode)
        {
            return textMode == 0 || textMode == 2;
        }
    }
}
