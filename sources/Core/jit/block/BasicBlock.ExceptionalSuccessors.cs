// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    public BasicBlockVisit VisitAllSuccs(Compiler compiler, Func<BasicBlock, BasicBlockVisit> visitor, bool useProfile = false)
    {
        var adapter = new BasicBlockDelegateVisitor(visitor);
        return VisitAllSuccs(compiler, ref adapter, useProfile);
    }

    public BasicBlockVisit VisitAllSuccs<TVisitor>(Compiler compiler, ref TVisitor visitor, bool useProfile = false)
        where TVisitor : struct, IBasicBlockVisitor<TVisitor>, allows ref struct
    {
        if (useProfile && (Kind is BBJ_COND) && (TrueEdge != FalseEdge) &&
            (TrueEdge.Likelihood < FalseEdge.Likelihood))
        {
            // Visiting the unlikely successor first makes the likely path fall
            // through in the reverse-postorder layout.
            if ((visitor.Visit(TrueTarget) is BasicBlockVisit.Abort) || (visitor.Visit(FalseTarget) is BasicBlockVisit.Abort))
            {
                return BasicBlockVisit.Abort;
            }
        }
        else if (VisitRegularSuccs(compiler, ref visitor) is BasicBlockVisit.Abort)
        {
            return BasicBlockVisit.Abort;
        }

        if (Kind is BBJ_CALLFINALLYRET)
        {
            return BasicBlockVisit.Continue;
        }

        return VisitEHSuccs(compiler, ref visitor, skipJumpDestination: Kind is BBJ_CALLFINALLY);
    }

    public BasicBlockVisit VisitEHSuccs(Compiler compiler, Func<BasicBlock, BasicBlockVisit> visitor)
    {
        var adapter = new BasicBlockDelegateVisitor(visitor);
        return VisitEHSuccs(compiler, ref adapter);
    }

    public BasicBlockVisit VisitEHSuccs<TVisitor>(Compiler compiler, ref TVisitor visitor)
        where TVisitor : struct, IBasicBlockVisitor<TVisitor>, allows ref struct
    {
        // CALLFINALLYRET is a pseudo-block: codegen jumps directly to its successor.
        if (Kind is BBJ_CALLFINALLYRET)
        {
            return BasicBlockVisit.Continue;
        }

        return VisitEHSuccs(compiler, ref visitor, skipJumpDestination: false);
    }

    private BasicBlockVisit VisitEHSuccs<TVisitor>(
        Compiler compiler, ref TVisitor visitor, bool skipJumpDestination)
        where TVisitor : struct, IBasicBlockVisitor<TVisitor>, allows ref struct
    {
        if (!HasPotentialEHSuccs(compiler))
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
                    if (visitor.Visit(handler.ebdFilter) is BasicBlockVisit.Abort)
                    {
                        return BasicBlockVisit.Abort;
                    }

                    // Second-pass EH can bypass a filter and enter its handler directly.
                    if (visitor.Visit(handler.ebdHndBeg) is BasicBlockVisit.Abort)
                    {
                        return BasicBlockVisit.Abort;
                    }
                }
                else if (!skipJumpDestination || (Target != handler.ebdHndBeg))
                {
                    // EH-only visitation includes a CALLFINALLY target even if it is
                    // also a regular successor: its locals are live throughout this block.
                    if (visitor.Visit(handler.ebdHndBeg) is BasicBlockVisit.Abort)
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

        return VisitEHEnclosedHandlerSecondPassSuccs(compiler, ref visitor);
    }
}
