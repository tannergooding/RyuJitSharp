// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private const MemoryKindSet GcHeapAndByrefExposed = (1 << (int)GcHeap) | (1 << (int)ByrefExposed);

    private unsafe void PerNodeLocalVarLiveness(GenTree tree)
    {
        assert(tree is not null);
        switch (tree.Oper)
        {
            case GT_QMARK:
            case GT_COLON:
            {
                noway_assert(false, "unexpected GT_QMARK/GT_COLON");
                break;
            }

            case GT_LCL_VAR:
            case GT_LCL_FLD:
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                MarkUseDef(tree.AsLclVarCommon());
                break;
            }

            case GT_LCL_ADDR:
            {
                if (TLiveness.IsLIR)
                {
                    assert((_compiler.compCurBB is not null) && _compiler.compCurBB.IsLIR);
                    // Call definitions take effect at the call, not at the address calculation.
                    if (IsTrackedCallDefinition(_compiler.compCurBB, tree))
                    {
                        break;
                    }

                    MarkUseDef(tree.AsLclVarCommon());
                }
                break;
            }

            case GT_IND:
            case GT_BLK:
            {
                if (TLiveness.ComputeMemoryLiveness)
                {
                    // Volatile reads model def-then-use, allowing a subsequent ordinary read to CSE.
                    if ((tree.Flags & GTF_IND_VOLATILE) != 0)
                    {
                        _curMemoryDef |= GcHeapAndByrefExposed;
                    }
                    _curMemoryUse |= GcHeapAndByrefExposed;
                }
                break;
            }

            case GT_LOCKADD:
            case GT_XORR:
            case GT_XAND:
            case GT_XADD:
            case GT_XCHG:
            case GT_CMPXCHG:
            {
                if (TLiveness.ComputeMemoryLiveness)
                {
                    _curMemoryUse |= GcHeapAndByrefExposed;
                    _curMemoryDef |= GcHeapAndByrefExposed;
                    _curMemoryHavoc |= GcHeapAndByrefExposed;
                }
                break;
            }

            case GT_STOREIND:
            case GT_STORE_BLK:
            case GT_MEMORYBARRIER:
            {
                if (TLiveness.ComputeMemoryLiveness)
                {
                    _curMemoryDef |= GcHeapAndByrefExposed;
                }
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                PerNodeLocalVarLiveness(tree.AsHWIntrinsic());
                break;
            }
#endif

            case GT_CALL:
            {
                var call = tree.AsCall();
                if (TLiveness.ComputeMemoryLiveness)
                {
                    var modHeap = true;
                    if (call.IsHelperCall())
                    {
                        var helper = call.HelperNum;
                        if (!helper.MutatesHeap && !helper.MayRunCctor)
                        {
                            modHeap = false;
                        }
                    }

                    if (modHeap)
                    {
                        _curMemoryUse |= GcHeapAndByrefExposed;
                        _curMemoryDef |= GcHeapAndByrefExposed;
                        _curMemoryHavoc |= GcHeapAndByrefExposed;
                    }
                }

                if ((call.IsUnmanaged || call.IsTailCallViaJitHelper) && _compiler.compMethodRequiresPInvokeFrame)
                {
                    assert(!_compiler.opts.ShouldUsePInvokeHelpers || (_compiler.info.compLvFrameListRoot == BAD_VAR_NUM));
                    if (!_compiler.opts.ShouldUsePInvokeHelpers && !call.IsSuppressGCTransition)
                    {
                        MarkFrameRootUse();
                    }
                }

                _ = call.VisitPhysicalLocalDefNodes(_compiler, local => {
                    MarkUseDef(local.AsLclVarCommon());
                    return GenTree.VisitResult.Continue;
                });
                break;
            }
        }
    }

#if FEATURE_HW_INTRINSICS
    private void PerNodeLocalVarLiveness(GenTreeHWIntrinsic intrinsic)
    {
        if (TLiveness.ComputeMemoryLiveness)
        {
            if (intrinsic.IsMemoryStoreOrBarrier)
            {
                _curMemoryDef |= GcHeapAndByrefExposed;
            }
            else if (intrinsic.IsMemoryLoad())
            {
                _curMemoryUse |= GcHeapAndByrefExposed;
            }
        }
    }
#endif

    private void MarkUseDef(GenTreeLclVarCommon tree)
    {
        assert((tree.Oper.IsLocal && (tree.Oper is not GT_PHI_ARG)) || (tree.Oper is GT_LCL_ADDR));

        var lclNum = tree.LclNum;
        ref var descriptor = ref _compiler.lvaGetDesc(lclNum);
        if ((descriptor.lvRefCnt(_compiler.lvaRefCountState) == 0) &&
            (!varTypeIsPromotable(descriptor.Type) || !descriptor.lvPromoted))
        {
            JITDUMP($"Found reference to V{lclNum:D2} with zero refCnt.\n");
            assert(false, "We should never encounter a reference to a lclVar that has a zero refCnt.");
            descriptor.setLvRefCnt(1);
        }

        // SSA models a partial store as a use and a def of the whole local.
        // Outside SSA, partial stores contribute neither to the local use nor def set.
        var isDef = (tree.Flags & GTF_VAR_DEF) != 0;
        var isFullDef = isDef && ((tree.Flags & GTF_VAR_USEASG) == 0);
        var isUse = TLiveness.SsaLiveness ? !isFullDef : !isDef;

        if (descriptor.lvTracked)
        {
            assert(descriptor._varIndex < _compiler.lvaTrackedCount);
            assert(!descriptor.IsAddressExposed || (TLiveness.TrackAddressExposedLocals && !TLiveness.ComputeMemoryLiveness));
#if DEBUG
            if (TLiveness.IsLIR && (descriptor.Type is not TYP_STRUCT) && !varTypeIsMultiReg(descriptor.Type))
            {
                assert(descriptor.lvDoNotEnregister || descriptor.IsDefinedViaAddress ||
                    (tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR));
            }
#endif
            if (isUse && !VarSetOps.IsMember(_compiler, _curDefSet, descriptor._varIndex))
            {
                VarSetOps.AddElemD(_compiler, _curUseSet, descriptor._varIndex);
            }
            if (TLiveness.SsaLiveness ? isDef : isFullDef)
            {
                VarSetOps.AddElemD(_compiler, _curDefSet, descriptor._varIndex);
            }
        }
        else
        {
            if (TLiveness.ComputeMemoryLiveness && descriptor.IsAddressExposed)
            {
                if (isUse)
                {
                    _curMemoryUse |= 1 << (int)ByrefExposed;
                }
                if (isDef)
                {
                    _curMemoryDef |= 1 << (int)ByrefExposed;
                    _compiler.byrefStatesMatchGcHeapStates = false;
                }
            }

            if (varTypeIsPromotable(descriptor.Type))
            {
                var promotionType = _compiler.lvaGetPromotionType(in descriptor);
                if (promotionType is not Compiler.PROMOTION_TYPE_NONE)
                {
                    for (var field = descriptor.lvFieldLclStart; field < descriptor.lvFieldLclStart + descriptor.lvFieldCnt; field++)
                    {
                        if (!_compiler.lvaTable[field].lvTracked)
                        {
                            continue;
                        }

                        var varIndex = _compiler.lvaTable[field]._varIndex;
                        if (isUse && !VarSetOps.IsMember(_compiler, _curDefSet, varIndex))
                        {
                            VarSetOps.AddElemD(_compiler, _curUseSet, varIndex);
                        }
                        if (TLiveness.SsaLiveness ? isDef : isFullDef)
                        {
                            VarSetOps.AddElemD(_compiler, _curDefSet, varIndex);
                        }
                    }
                }
            }
        }
    }
}
