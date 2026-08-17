using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Buriko
{
    public enum BurikoBlockReason
    {
        None = 0,
        WaitForInput,
        WaitForTime,
        Choice,
        Host,
        Completed,
        Faulted
    }

    public sealed class BurikoOperationInvocation
    {
        public BurikoOperationInvocation(
            BurikoOperationSpecification specification,
            IReadOnlyList<BurikoValue> arguments)
        {
            Specification = specification;
            Arguments = arguments;
        }

        public BurikoOperationSpecification Specification { get; }
        public IReadOnlyList<BurikoValue> Arguments { get; }
    }

    public readonly struct BurikoHostResponse
    {
        public BurikoHostResponse(BurikoValue returnValue, BurikoBlockReason blockReason = BurikoBlockReason.None)
        {
            ReturnValue = returnValue;
            BlockReason = blockReason;
        }

        public BurikoValue ReturnValue { get; }
        public BurikoBlockReason BlockReason { get; }

        public static BurikoHostResponse Continue => new BurikoHostResponse(BurikoValue.Null);
    }

    public interface IBurikoHost
    {
        BurikoHostResponse Execute(BurikoOperationInvocation invocation, BurikoMemory memory);
        void CommitPendingPresentation();
    }

    public interface IBurikoScriptRepository
    {
        CompiledScriptContainer Load(string scriptName);
    }
}

