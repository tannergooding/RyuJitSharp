// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal interface INoGcRangeVisitor
{
    bool Visit(uint funcIndex, uint offset, uint size, uint firstSize, bool isFuncletProlog);
}

public partial class Emitter
{
    internal bool emitGenNoGCLst<TVisitor>(ref TVisitor visitor,
        bool skipMainPrologsAndEpilogs = false)
        where TVisitor : struct, INoGcRangeVisitor
    {
        for (var group = emitIGlist; group is not null; group = group.igNext)
        {
            if (skipMainPrologsAndEpilogs &&
                (group.igFlags & (InsGroupFlags.Prolog | InsGroupFlags.Epilog)) != 0)
            {
                continue;
            }
            if (((group.igFlags & InsGroupFlags.NoGCInterrupt) != 0) && (group.igSize > 0))
            {
                var descriptors = group.igData;
                if (descriptors is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.NonInterruptibleGroupHasNoInstructions);
                    return default!;
                }
                assert(descriptors.Length > 0);
                var firstSize = descriptors[0].idCodeSize();
                assert(firstSize > 0);
                if (!visitor.Visit(group.igFuncIdx, group.igOffs, group.igSize, firstSize,
                    (group.igFlags & InsGroupFlags.FuncletProlog) != 0))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
