// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgehopt.cpp.

using System;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private void fgDebugCheckTryFinallyExitsCore()
    {
        var allTryExitsValid = true;
        for (ushort xtnum = 0; xtnum < compHndBBtabCount; xtnum++)
        {
            ref var clause = ref compHndBBtab[xtnum];
            var isFinally = clause.ebdHandlerType is EH_HANDLER_FINALLY;
            var wasFinally = clause.ebdHandlerType is EH_HANDLER_FAULT_WAS_FINALLY;
            if (!isFinally && !wasFinally)
            {
                continue;
            }

            var firstTry = clause.ebdTryBeg;
            var lastTry = clause.ebdTryLast;
            assert(firstTry.TryIndex <= xtnum);
            assert(lastTry.TryIndex <= xtnum);
            var finallyBlock = isFinally ? clause.ebdHndBeg : null;
            for (var block = firstTry; ; block = block.Next
                     ?? throw new InvalidOperationException("Try region ended prematurely."))
            {
                assert(block.hasTryIndex);
                if (block.TryIndex == xtnum)
                {
                    foreach (var successor in block.Succs)
                    {
                        if (successor.hasTryIndex && (successor.TryIndex <= xtnum))
                        {
                            continue;
                        }

                        if (block.Kind is BBJ_CALLFINALLY)
                        {
                            continue;
                        }

                        var isCallToFinally = (successor.Kind is BBJ_CALLFINALLY)
                            && isFinally && (successor.Target == finallyBlock);
                        var isJumpToClone = successor.HasFlag(BBF_CLONED_FINALLY_BEGIN);
                        if (!isJumpToClone && (successor.Kind is BBJ_ALWAYS) && successor.isEmpty())
                        {
                            isJumpToClone = successor.Target.HasFlag(BBF_CLONED_FINALLY_BEGIN);
                        }

                        var isReturnFromFinally = (block.Kind is BBJ_CALLFINALLYRET)
                            || block.HasFlag(BBF_KEEP_BBJ_ALWAYS)
                            || block.HasFlag(BBF_CLONED_FINALLY_END);
                        var valid = isCallToFinally || isJumpToClone || isReturnFromFinally;
                        if (!valid)
                        {
                            JITDUMP($"fgCheckTryFinallyExits: EH#{xtnum} exit via " +
                                $"{FMT_BB(block.bbNum)} -> {FMT_BB(successor.bbNum)} is invalid\n");
                        }

                        allTryExitsValid &= valid;
                    }
                }

                if (block == lastTry)
                {
                    break;
                }
            }
        }

        if (!allTryExitsValid)
        {
            JITDUMP("fgCheckTryFinallyExits: method contains invalid try exit paths\n");
            assert(allTryExitsValid);
        }
    }
#endif
}
