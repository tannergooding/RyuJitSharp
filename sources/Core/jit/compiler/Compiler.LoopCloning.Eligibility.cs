// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BBKinds;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optIsLoopClonable(FlowGraphNaturalLoop loop, LoopCloneContext context)
    {
        if (loop.Header.isRunRarely)
        {
            JITDUMP($"Loop cloning: rejecting loop L{loop.Index:D2}. Loop is cold.\n");
            return false;
        }

        var requireIterable = !MethodHasGuardedDevirtualization;
        var iterInfo = context.GetLoopIterInfo(loop.Index);
        if (requireIterable && iterInfo is null)
        {
            JITDUMP($"Loop cloning: rejecting loop L{loop.Index:D2}. Could not analyze iteration.\n");
            return false;
        }

        var cloneLoopsWithEH = true;
#if DEBUG
        cloneLoopsWithEH = JitConfig.JitCloneLoopsWithEH > 0;
#endif
        if (!optCanDuplicateLoop(loop, cloneLoopsWithEH, out var reason))
        {
            JITDUMP($"Loop cloning: rejecting loop L{loop.Index:D2}: {reason}\n");
            return false;
        }

        if (bbIsHandlerBeg(loop.Header))
        {
            JITDUMP($"Loop cloning: rejecting loop L{loop.Index:D2}. Header block is a handler start.\n");
            return false;
        }
        assert(loop.EntryEdges.Length == 1);
        var preheader = loop.EntryEdge(0).SourceBlock;
        if (!BasicBlock.sameEHRegion(preheader, loop.Header))
        {
            JITDUMP($"Loop cloning: rejecting loop L{loop.Index:D2}. " +
                "Preheader and header blocks are in different EH regions.\n");
            return false;
        }
        assert(!requireIterable || !lvaGetDesc(iterInfo!.IterVar).IsAddressExposed);

        if (requireIterable)
        {
            assert(iterInfo!.HasConstLimit || iterInfo.HasInvariantLocalLimit || iterInfo.HasArrayLengthLimit);
            if (!iterInfo.IsIncreasingLoop() && !iterInfo.IsDecreasingLoop())
            {
                JITDUMP($"Loop cloning: rejecting loop L{loop.Index:D2}. " +
                    $"Loop test ({iterInfo.TestOper().ToString()[3..]}) doesn't agree with the direction " +
                    $"({iterInfo.IterOper().ToString()[3..]}) of the loop.\n");
                return false;
            }
#if DEBUG
            assert(iterInfo.Iterator().Oper is genTreeOps.GT_LCL_VAR &&
                iterInfo.Iterator().AsLclVarCommon().LclNum == iterInfo.IterVar);
#endif
        }
        return true;
    }
}
