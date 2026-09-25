// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void OptimizeCallIndirectTargetEvaluation(GenTreeCall call)
    {
        assert((call._callType is CT_INDIRECT) && (call._controlExpr is not null));
        if (!call._controlExpr.Oper.IsCall)
        {
            return;
        }

        JITDUMP("Indirect call target is itself a call; trying to reorder its evaluation with arguments\n");
        var compiler = CompilerInstance;
        _scratchSideEffects.Clear();

        var numMarked = 1;
        call._lirFlags |= LIR.Flags.Mark;
        LIR.ReadOnlyRange movingRange = new(null, null);
        for (var current = (GenTree)call; numMarked > 0;)
        {
            var previous = current.Prev;
            if ((current._lirFlags & LIR.Flags.Mark) == 0)
            {
                if (!movingRange.IsEmpty)
                {
                    assert(current.Next == movingRange.FirstNode);
                    movingRange = new LIR.ReadOnlyRange(current, movingRange.LastNode);
                    _scratchSideEffects.AddNode(compiler, current);
                }
                current = previous ?? throw new InvalidOperationException("Unbalanced indirect call data flow.");
                continue;
            }

            current._lirFlags &= ~LIR.Flags.Mark;
            numMarked--;
            if (current == call._controlExpr)
            {
                movingRange = new LIR.ReadOnlyRange(current, current);
                _scratchSideEffects.AddNode(compiler, current);
                if (numMarked > 0)
                {
                    current = previous ?? throw new InvalidOperationException("Unbalanced indirect call data flow.");
                }
                continue;
            }

            _ = current.VisitOperands(operand => {
                assert((operand._lirFlags & LIR.Flags.Mark) == 0);
                operand._lirFlags |= LIR.Flags.Mark;
                numMarked++;
                return GenTree.VisitResult.Continue;
            });

            if (!movingRange.IsEmpty)
            {
                if (current.Oper.ConsumesFlags || _scratchSideEffects.InterferesWith(compiler, current, true))
                {
#if DEBUG
                    JITDUMP($"  Stopping at [{current.TreeId:D6}]; it interferes with the current range we are moving\n");
#endif
                    movingRange = new LIR.ReadOnlyRange(null, null);
                }
                else
                {
                    assert(current.Next == movingRange.FirstNode);
                    BlockRange().Remove(current);
                    BlockRange().InsertAfter(movingRange.LastNode, current);
                }
            }

            if (numMarked > 0)
            {
                current = previous ?? throw new InvalidOperationException("Unbalanced indirect call data flow.");
            }
        }

        JITDUMP("Result of moved target evaluation:\n");
        DISPTREERANGE(BlockRange(), call);
    }

    private void ContainCheckCallOperands(GenTreeCall call)
    {
#if TARGET_AMD64
        var control = call._controlExpr;
        if (control is not null)
        {
            assert(control.Type is not TYP_VOID);
            if (control.Oper.IsIndir && IsSafeToContainMem(call, control))
            {
                control.ClearRegNum();
                MakeSrcContained(call, control);
            }
        }
#else
        throw new NotImplementedException("Call operand containment outside AMD64 is not ported.");
#endif
    }

    private unsafe GenTree? LowerCall(GenTree node)
    {
#if TARGET_AMD64
        var call = node.AsCall();
        var compiler = CompilerInstance;
        JITDUMP("lowering call (before):\n");
        DISPTREERANGE(BlockRange(), call);
        JITDUMP("\n");

#if DEBUG
        assert((call.HelperNum is not (CORINFO_HELP_RUNTIMEHANDLE_METHOD or CORINFO_HELP_RUNTIMEHANDLE_CLASS)) ||
            ((call._callDebugFlags & GTF_CALL_MD_RUNTIME_LOOKUP_EXPANDED) != 0));
#endif
        if (compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && Compiler.IsStaticHelperEligibleForExpansion(call))
        {
            assert(call._initClsHnd is null);
        }

        GenTree? nextNode;
        if (call.IsSpecialIntrinsic())
        {
            switch (compiler.lookupNamedIntrinsic(call._callMethHnd))
            {
                case NI_System_SpanHelpers_Memmove:
                {
                    if (LowerCallMemmove(call, out nextNode))
                    {
                        return nextNode;
                    }
                    break;
                }

                case NI_System_SpanHelpers_SequenceEqual:
                {
                    if (LowerCallMemcmp(call, out nextNode))
                    {
                        return nextNode;
                    }
                    break;
                }

                case NI_System_SpanHelpers_Fill:
                case NI_System_SpanHelpers_ClearWithoutReferences:
                {
                    if (LowerCallMemset(call, out var nextBlock))
                    {
                        return nextBlock;
                    }
                    break;
                }
            }
        }
        if (call.IsHelperCall(CORINFO_HELP_MEMCPY) && LowerCallMemmove(call, out nextNode))
        {
            return nextNode;
        }
        if (call.IsHelperCall(CORINFO_HELP_MEMSET) && LowerCallMemset(call, out var nextMemsetBlock))
        {
            return nextMemsetBlock;
        }

        call._directCallAddress = null;
        call.ClearOtherRegs();
#if HAS_FIXED_REGISTER_SET
        if ((call._callType is CT_INDIRECT) && compiler.opts.Tier0OptimizationEnabled)
        {
            OptimizeCallIndirectTargetEvaluation(call);
        }
#endif
        LowerArgsForCall(call);

        GenTree? controlExpr = null;
        var callWasExpandedEarly = false;
        if (call.IsDelegateInvoke)
        {
            controlExpr = LowerDelegateInvoke(call);
        }
        else
        {
            switch (call.Flags & GTF_CALL_VIRT_KIND_MASK)
            {
                case GTF_CALL_VIRT_STUB:
                {
                    controlExpr = LowerVirtualStubCall(call);
                    break;
                }

                case GTF_CALL_VIRT_VTABLE:
                {
                    assert(call.IsVirtualVtable);
                    if (!call.IsExpandedEarly)
                    {
                        assert(call._controlExpr is null);
                        controlExpr = LowerVirtualVtableCall(call);
                    }
                    else
                    {
                        callWasExpandedEarly = true;
                        controlExpr = call._controlExpr;
                    }
                    break;
                }

                case GTF_CALL_NONVIRT:
                {
                    if (call.IsUnmanaged)
                    {
                        controlExpr = LowerNonvirtPinvokeCall(call);
                    }
                    else if (call._callType is not CT_INDIRECT)
                    {
                        controlExpr = LowerDirectCall(call);
                    }
                    break;
                }

                default:
                {
                    noway_assert(false, "strange call type");
                    break;
                }
            }
        }

        assert((call._callType is not CT_INDIRECT) || (controlExpr is null));
        if (call.IsTailCallViaJitHelper)
        {
            if (controlExpr is not null)
            {
                var targetRange = LIR.SeqTree(compiler, controlExpr);
                ContainCheckRange(targetRange);
                BlockRange().InsertBefore(call, targetRange);
            }
            else
            {
                assert((call._callType is CT_INDIRECT) && (call._controlExpr is not null));
                controlExpr = call._controlExpr;
            }
            throw new NotImplementedException("Windows x86 JIT-helper tailcall lowering is not ported.");
        }

        if ((controlExpr is not null) && !callWasExpandedEarly)
        {
            var controlRange = LIR.SeqTree(compiler, controlExpr);
            JITDUMP("results of lowering call:\n");
            DISPRANGE(controlRange);
            ContainCheckRange(controlRange);
            BlockRange().InsertBefore(call, controlRange);
            call._controlExpr = controlExpr;
        }

        if (compiler.opts.IsCFGEnabled)
        {
            LowerCFGCall(call);
        }

        if (call.IsFastTailCall)
        {
            LowerFastTailCall(call);
        }
        else if (!call.IsHelperCall(CORINFO_HELP_VALIDATE_INDIRECT_CALL))
        {
            RequireOutgoingArgSpace(call, call.Args.OutgoingArgsStackSize);
        }

        if (varTypeIsStruct(call.Type))
        {
            LowerCallStruct(call);
        }

        ContainCheckCallOperands(call);
        JITDUMP("lowering call (after):\n");
        DISPTREERANGE(BlockRange(), call);
        JITDUMP("\n");
        return null;
#else
        throw new NotImplementedException("LowerCall outside AMD64 is not ported.");
#endif
    }
}
