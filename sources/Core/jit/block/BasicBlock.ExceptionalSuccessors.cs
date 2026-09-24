// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    public BasicBlockVisit VisitEHSuccs(Compiler compiler, Func<BasicBlock, BasicBlockVisit> visitor)
    {
        // CALLFINALLYRET is a pseudo-block: codegen jumps directly to its successor.
        if ((Kind is BBJ_CALLFINALLYRET) || !HasPotentialEHSuccs(compiler))
        {
            return BasicBlockVisit.Continue;
        }

        ref var handler = ref compiler.ehGetBlockExnFlowDsc(this);
        if (!Unsafe.IsNullRef(in handler))
        {
            while (true)
            {
                if (handler.HasFilter)
                {
                    if (visitor(handler.ebdFilter) is BasicBlockVisit.Abort)
                    {
                        return BasicBlockVisit.Abort;
                    }

                    // Second-pass EH can bypass a filter and enter its handler directly.
                    if (visitor(handler.ebdHndBeg) is BasicBlockVisit.Abort)
                    {
                        return BasicBlockVisit.Abort;
                    }
                }
                else
                {
                    // EH-only visitation includes a CALLFINALLY target even if it is
                    // also a regular successor: its locals are live throughout this block.
                    if (visitor(handler.ebdHndBeg) is BasicBlockVisit.Abort)
                    {
                        return BasicBlockVisit.Abort;
                    }
                }

                if (handler.ebdEnclosingTryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    break;
                }

                handler = ref compiler.ehGetDsc(handler.ebdEnclosingTryIndex);
            }
        }

        return VisitEHEnclosedHandlerSecondPassSuccs(compiler, visitor);
    }
}
