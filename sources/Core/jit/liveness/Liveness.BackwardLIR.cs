// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    internal unsafe void ComputeLifeLIR(nint[] life, BasicBlock block, nint[] keepAliveVars)
    {
        noway_assert(VarSetOps.IsSubset(_compiler, keepAliveVars, life));
        assert(block.IsLIR);
        var first = block.FirstNode;
        if (first is null)
        {
            return;
        }

        GenTree? next;
        var end = first.Prev;
        for (var node = block.LastNode; node != end; node = next)
        {
            assert(node is not null);
            next = node.Prev;
            switch (node.Oper)
            {
                case GT_CALL:
                {
                    var call = node.AsCall();
                    if (TLiveness.EliminateDeadCode && ((call.Type is TYP_VOID) || call.IsUnusedValue) &&
                        !call.HasSideEffects(_compiler))
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf("Removing dead call:\n");
                            _compiler.gtDispTree(call, topOnly: true);
                        }
#endif
                        _ = node.VisitOperands(operand => {
                            if (operand.IsValue)
                            {
                                operand.IsUnusedValue = true;
                            }
                            if (operand.Oper is GT_PUTARG_STK)
                            {
                                operand.AsPutArgStk().Op1.IsUnusedValue = true;
                                operand.BashToNOP();
                            }
                            return GenTree.VisitResult.Continue;
                        });
                        block.Remove(node);

                        if (!_compiler.opts.ShouldUsePInvokeHelpers &&
                            ((call.IsTailCall && _compiler.compMethodRequiresPInvokeFrame) ||
                             (call.IsUnmanaged && !call.IsSuppressGCTransition)) &&
                            _compiler.lvaTable[_compiler.info.compLvFrameListRoot].lvTracked)
                        {
                            _compiler.fgStmtRemoved = true;
                        }
                    }
                    else
                    {
                        _ = ComputeLifeCall(life, keepAliveVars, call);
                    }
                    break;
                }

                case GT_LCL_VAR:
                case GT_LCL_FLD:
                {
                    var local = node.AsLclVarCommon();
                    ref var descriptor = ref _compiler.lvaTable[local.LclNum];
                    if (TLiveness.EliminateDeadCode && node.IsUnusedValue)
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf("Removing dead LclVar use:\n");
                            _compiler.gtDispTree(local, topOnly: true);
                        }
#endif
                        block.Delete(node);
                        if (descriptor.lvTracked)
                        {
                            _compiler.fgStmtRemoved = true;
                        }
                    }
                    else if (descriptor.lvTracked)
                    {
                        ComputeLifeTrackedLocalUse(life, in descriptor, local);
                    }
                    else
                    {
                        _ = ComputeLifeUntrackedLocal(life, keepAliveVars, in descriptor, local);
                    }
                    break;
                }

                case GT_LCL_ADDR:
                {
                    if (TLiveness.EliminateDeadCode && node.IsUnusedValue)
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf("Removing dead LclVar address:\n");
                            _compiler.gtDispTree(node, topOnly: true);
                        }
#endif
                        var tracked = _compiler.lvaTable[node.AsLclVarCommon().LclNum].lvTracked;
                        block.Delete(node);
                        if (tracked)
                        {
                            _compiler.fgStmtRemoved = true;
                        }
                    }
                    else
                    {
                        // The call defines these locals, not its address evaluation;
                        // intervening uses must remain live.
                        if (IsTrackedCallDefinition(block, node))
                        {
                            break;
                        }
                        var dead = ComputeLifeLocal(life, keepAliveVars, node);
                        if (TLiveness.EliminateDeadCode && dead && block.TryGetUse(node, out var use) &&
                            (use.User().Oper is GT_STOREIND or GT_STORE_BLK))
                        {
                            var store = use.User().AsIndir();
                            if (TryRemoveDeadStoreLIR(store, node.AsLclVarCommon(), block))
                            {
#if DEBUG
                                if (_compiler.verbose)
                                {
                                    jitprintf("Removing dead LclVar address:\n");
                                    _compiler.gtDispTree(node, topOnly: true);
                                }
#endif
                                block.Remove(node);
                                var data = store.Data;
                                data.IsUnusedValue = true;
                                if (data.Oper.IsIndir)
                                {
                                    var replacement = Lowering.TransformUnusedIndirection(data.AsIndir(), _compiler, block);
                                    if (next == data)
                                    {
                                        next = replacement;
                                    }
                                }
                                else if (data.Oper is GT_LCL_VAR or GT_LCL_FLD)
                                {
#if DEBUG
                                    if (_compiler.verbose)
                                    {
                                        jitprintf("Removing dead store data:\n");
                                        _compiler.gtDispTree(data, topOnly: true);
                                    }
#endif
                                    if (next == data)
                                    {
                                        next = data.Prev;
                                    }
                                    assert(end != data);
                                    block.Delete(data);
                                }
                            }
                        }
                    }
                    break;
                }

                case GT_STORE_LCL_VAR:
                case GT_STORE_LCL_FLD:
                {
                    var local = node.AsLclVarCommon();
                    ref var descriptor = ref _compiler.lvaTable[local.LclNum];
                    var dead = descriptor.lvTracked
                        ? ComputeLifeTrackedLocalDef(life, keepAliveVars, in descriptor, local)
                        : ComputeLifeUntrackedLocal(life, keepAliveVars, in descriptor, local);
                    if (TLiveness.EliminateDeadCode && dead && TryRemoveDeadStoreLIR(node, local, block))
                    {
                        var value = local.Data;
                        value.IsUnusedValue = true;
                        if (value.Oper.IsIndir)
                        {
                            var replacement = Lowering.TransformUnusedIndirection(value.AsIndir(), _compiler, block);
                            // Native bashing retains the predecessor's address; managed replacement does not.
                            if (next == value)
                            {
                                next = replacement;
                            }
                        }
                    }
                    break;
                }

                case GT_LABEL:
                case GT_FTN_ADDR:
                case GT_CNS_INT:
                case GT_CNS_LNG:
                case GT_CNS_DBL:
                case GT_CNS_STR:
#if FEATURE_SIMD
                case GT_CNS_VEC:
#endif
#if FEATURE_MASKED_HW_INTRINSICS
                case GT_CNS_MSK:
#endif
                case GT_PHYSREG:
                {
                    if (TLiveness.EliminateDeadCode && node.IsUnusedValue)
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf("Removing dead node:\n");
                            _compiler.gtDispTree(node, topOnly: true);
                        }
#endif
                        block.Remove(node);
                    }
                    break;
                }

                case GT_LOCKADD:
                case GT_XORR:
                case GT_XAND:
                case GT_XADD:
                case GT_XCHG:
                case GT_CMPXCHG:
                case GT_MEMORYBARRIER:
                case GT_JMP:
                case GT_STOREIND:
                case GT_BOUNDS_CHECK:
                case GT_STORE_BLK:
                case GT_JCMP:
                case GT_JTEST:
                case GT_JCC:
                case GT_JTRUE:
                case GT_RETURN:
                case GT_RETURN_SUSPEND:
                case GT_PATCHPOINT:
                case GT_PATCHPOINT_FORCED:
                case GT_NONLOCAL_JMP:
                case GT_SWITCH:
                case GT_RETFILT:
                case GT_START_NONGC:
                case GT_START_PREEMPTGC:
                case GT_PROF_HOOK:
                case GT_SWITCH_TABLE:
                case GT_RETURNTRAP:
                case GT_PUTARG_STK:
                case GT_IL_OFFSET:
                case GT_RECORD_ASYNC_RESUME:
                case GT_KEEPALIVE:
                case GT_SWIFT_ERROR_RET:
                case GT_GCPOLL:
                case GT_WASM_JEXCEPT:
                {
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case GT_HWINTRINSIC:
                {
                    var intrinsic = node.AsHWIntrinsic();
                    if (!intrinsic.IsMemoryStore(out _) && !HWIntrinsicInfo.HasSpecialSideEffect(intrinsic.HWIntrinsicId))
                    {
                        _ = TryRemoveNonLocalLIR(node, block);
                    }
                    break;
                }
#endif

                case GT_NO_OP:
                {
                    break;
                }

                case GT_NOP:
                {
                    if ((node.Flags & GTF_ORDER_SIDEEFF) == 0)
                    {
                        _ = TryRemoveNonLocalLIR(node, block);
                    }
                    break;
                }

                case GT_BLK:
                {
                    if (!TryRemoveNonLocalLIR(node, block) && node.IsUnusedValue)
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf($"Transform an unused BLK node [{node.TreeId:D6}]\n");
                        }
#endif
                        _ = Lowering.TransformUnusedIndirection(node.AsIndir(), _compiler, block);
                    }
                    break;
                }

                default:
                {
                    _ = TryRemoveNonLocalLIR(node, block);
                    break;
                }
            }
        }
    }
}
