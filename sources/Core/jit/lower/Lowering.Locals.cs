// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree TryRetypingFloatingPointStoreToIntegerStore(GenTree store)
    {
        assert(store.Oper.IsStore);
        if (!varTypeIsFloating(store.Type))
        {
            return store;
        }
        if (Configuration.TARGET_ARM64 && (store.Oper is GT_STOREIND) && store.AsStoreInd().IsVolatile)
        {
            store.Type = store.Type is TYP_DOUBLE ? TYP_LONG : TYP_INT;
            var bitCast = CompilerInstance.gtNewBitCastNode(store.Type, store.Data);
            BlockRange().InsertAfter(store.Data, bitCast);
            store.AsStoreInd().Data = bitCast;
            _ = LowerBitCast(bitCast);
            return store;
        }
        if ((store.Oper is GT_STORE_LCL_VAR) && !CompilerInstance.lvaGetDesc(store.AsLclVar().LclNum).lvDoNotEnregister)
        {
            return store;
        }

        var value = store.Data;
        assert(store.Type == value.Type);
        if (!value.Oper.IsCnsFltOrDbl)
        {
            return store;
        }
        if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARM)
        {
            // Xarch can contain the immediate and ARM constructs FP constants from integer bits.
            // Other targets only profit from integer stores for positive zero.
            if (!value.AsDblCon().IsPositiveZero)
            {
                return store;
            }
        }

        var doubleValue = value.AsDblCon().DconVal;
        var_types type;
        nint integerValue;
        if (store.Type is TYP_FLOAT)
        {
            type = TYP_INT;
            integerValue = BitConverter.SingleToInt32Bits((float)doubleValue);
        }
        else
        {
            if (!Configuration.TARGET_64BIT)
            {
                return store;
            }

            assert(store.Type is TYP_DOUBLE);
            type = TYP_LONG;
            integerValue = unchecked((nint)BitConverter.DoubleToInt64Bits(doubleValue));
        }

        var constant = new GenTreeIntCon(type, integerValue, null, value, NodeThreading.LIR);
        constant._vnPair.SetBoth(ValueNumStore.NoVN);
        BlockRange().ReplaceNode(value, constant);
        assert(!store.Oper.IsLocalStore || CompilerInstance.lvaGetDesc(store.AsLclVarCommon().LclNum).lvDoNotEnregister);
        if (store.Oper is GT_STORE_LCL_VAR)
        {
            var local = store.AsLclVarCommon();
            var replacement = new GenTreeLclFld(GT_STORE_LCL_FLD, type, local.LclNum, 0, constant, null,
                store, NodeThreading.LIR) {
                Flags = store.Flags,
            };
            replacement.CopySsaIdentityFrom(local);
            BlockRange().ReplaceNode(store, replacement);
            store = replacement;
        }
        store.Type = type;

        return store;
    }

    private unsafe GenTree? LowerStoreLocCommon(GenTreeLclVarCommon lclStore)
    {
        assert(lclStore.Oper is GT_STORE_LCL_FLD or GT_STORE_LCL_VAR);
        JITDUMP("lowering store lcl var/field (before):\n");
        DISPTREERANGE(BlockRange(), lclStore);
        JITDUMP("\n");

        lclStore = TryRetypingFloatingPointStoreToIntegerStore(lclStore).AsLclVarCommon();
        if ((lclStore.Oper is GT_STORE_LCL_FLD) && (JitConfig.JitEnableStoreLclFldCoalescing != 0))
        {
            LowerLocalStoreCoalescing(lclStore);
        }
        var src = lclStore.Op1;
        ref var descriptor = ref CompilerInstance.lvaGetDesc(lclStore.LclNum);
        var srcIsMultiReg = src.IsMultiRegNode;
        if (!srcIsMultiReg && varTypeIsStruct(descriptor.Type))
        {
            assert(descriptor.CanBeReplacedWithItsField(CompilerInstance) || descriptor.lvDoNotEnregister || !descriptor.lvPromoted);
            if (descriptor.CanBeReplacedWithItsField(CompilerInstance))
            {
                assert(descriptor.lvFieldCnt == 1);
                var fieldNumber = descriptor.lvFieldLclStart;
                ref var field = ref CompilerInstance.lvaGetDesc(fieldNumber);
                JITDUMP($"Replacing an independently promoted local var V{lclStore.LclNum:D2} with its only field V{fieldNumber:D2} for the store from a call [{lclStore.TreeId:D6}]\n");
                lclStore.LclNum = fieldNumber;
                lclStore.Type = field.Type;
                descriptor = ref field;
            }
        }
        if (srcIsMultiReg)
        {
            _ = CheckMultiRegLclVar(lclStore.AsLclVar(), src.GetMultiRegCount(CompilerInstance));
        }
        var localRegisterType = descriptor.GetRegisterType(lclStore);

        if ((lclStore.Type is TYP_STRUCT) && !srcIsMultiReg)
        {
            bool convertToStoreObj;
            if (lclStore.Oper is GT_STORE_LCL_FLD)
            {
                convertToStoreObj = true;
            }
            else if (src.Oper is GT_CALL)
            {
                if (Configuration.DEBUG)
                {
                    var layout = lclStore.GetLayout(CompilerInstance);
                    assert(layout is not null);
                    if (Configuration.TARGET_XARCH && !Configuration.UNIX_AMD64_ABI)
                    {
                        assert(layout.SlotCount == 1);
                        assert(localRegisterType is not TYP_UNDEF);
                    }
                    else
                    {
                        if (!CompilerInstance.IsHfa(layout.ClassHandle))
                        {
                            if (layout.SlotCount > 1)
                            {
                                if (!Configuration.TARGET_RISCV64 && !Configuration.TARGET_LOONGARCH64 &&
                                    !Configuration.TARGET_WASM)
                                {
                                    assert(src.AsCall().HasMultiRegRetVal);
                                }
                            }
                            else
                            {
                                var size = layout.Size;
                                assert((size <= 8) || (size == 16));
                                assert((((size - 1) & size) == 0) == (localRegisterType is not TYP_UNDEF));
                            }
                        }
                    }
                }
                if (!Configuration.WINDOWS_AMD64_ABI &&
                    !src.AsCall().HasMultiRegRetVal && (localRegisterType is TYP_UNDEF))
                {
                    lclStore.Op1 = SpillStructCallResult(src.AsCall());
                    JITDUMP("lowering store lcl var/field has to spill call src.\n");
                    return LowerStoreLocCommon(lclStore);
                }
                convertToStoreObj = false;
            }
            else if (!descriptor.IsEnregisterableType)
            {
                convertToStoreObj = true;
            }
            else if (src.Oper is GT_CNS_INT)
            {
                assert(src.IsIntegralConst(0), "expected an INIT_VAL for non-zero init.");
                var retypeZeroToRegType = Configuration.FEATURE_SIMD && varTypeIsSimd(localRegisterType);
                retypeZeroToRegType |= Configuration.TARGET_WASM && (localRegisterType != src.Type);
                if (retypeZeroToRegType)
                {
                    var zero = CompilerInstance.gtNewZeroConNode(localRegisterType);
                    BlockRange().InsertAfter(src, zero);
                    BlockRange().Remove(src);
                    lclStore.Op1 = src = zero;
                }
                convertToStoreObj = false;
            }
            else if (src.Oper is GT_LCL_VAR)
            {
                convertToStoreObj = false;
            }
            else if (src.Oper is GT_IND or GT_BLK or GT_LCL_FLD)
            {
                if (Configuration.TARGET_ARM64)
                {
                    convertToStoreObj = true;
                }
                else
                {
                    if (src.Type is TYP_STRUCT)
                    {
                        src.Type = localRegisterType;
                        if (src.Oper is GT_IND or GT_BLK)
                        {
                            if (src.Oper is GT_BLK)
                            {
                                var indir = new GenTreeIndir(GT_IND, localRegisterType, src.AsIndir().Addr, null,
                                    src, NodeThreading.LIR) {
                                    Flags = src.Flags,
                                };
                                BlockRange().ReplaceNode(src, indir);
                                src = indir;
                            }
                            _ = LowerIndir(src.AsIndir());
                        }
                        if (Configuration.TARGET_XARCH && varTypeIsSmall(localRegisterType))
                        {
                            src.Flags |= GTF_DONT_EXTEND;
                        }
                    }

                    convertToStoreObj = false;
                }
            }
            else
            {
                assert(src.Oper.IsInitVal);
                convertToStoreObj = true;
            }

            if (convertToStoreObj)
            {
                var layout = lclStore.GetLayout(CompilerInstance);
                assert(layout is not null);
                var localNumber = lclStore.LclNum;
                var address = CompilerInstance.gtNewLclAddrNode(TYP_BYREF, localNumber, lclStore.LclOffs);
                CompilerInstance.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.BlockOp);
                address.Flags |= lclStore.Flags & (GTF_VAR_DEF | GTF_VAR_USEASG);
                var store = new GenTreeBlk(TYP_STRUCT, address, src, layout, lclStore, NodeThreading.LIR) {
                    Flags = GTF_ASG | GTF_IND_NONFAULTING | GTF_IND_TGT_NOT_HEAP,
                };
                BlockRange().ReplaceNode(lclStore, store);
                BlockRange().InsertBefore(store, address);
                // Lowering may replace the block store; native lowering changes its operator in place.
                var nextNode = LowerStoreBlock(store);
                var loweredStore = nextNode is null ? BlockRange().LastNode : nextNode.Prev;
                if (Configuration.DEBUG)
                {
                    assert(loweredStore is not null);
                }
                JITDUMP("lowering store lcl var/field (after):\n");
                DISPTREERANGE(BlockRange(), loweredStore!);
                JITDUMP("\n");
                return nextNode;
            }
        }

        if ((src.Type is not TYP_STRUCT) && !varTypeUsesSameRegType(localRegisterType, src.Type))
        {
            assert(!srcIsMultiReg);
            assert(lclStore.Oper.IsLocalStore);
            assert(localRegisterType is not TYP_UNDEF);
            var bitcast = CompilerInstance.gtNewBitCastNode(localRegisterType, src);
            lclStore.Op1 = bitcast;
            BlockRange().InsertBefore(lclStore, bitcast);
            ContainCheckBitCast(bitcast);
        }
        var next = LowerStoreLoc(lclStore);
        JITDUMP("lowering store lcl var/field (after):\n");
        DISPTREERANGE(BlockRange(), lclStore);
        JITDUMP("\n");

        return next;
    }

    private void WidenSIMD12IfNecessary(GenTreeLclVarCommon node)
    {
        if (Configuration.FEATURE_SIMD && (node.Type is TYP_SIMD12) &&
            CompilerInstance.lvaMapSimd12ToSimd16(node.LclNum))
        {
            JITDUMP("Mapping TYP_SIMD12 lclvar node to TYP_SIMD16:\n");
            DISPNODE(node);
            JITDUMP("============\n");
            node.Type = TYP_SIMD16;
        }
    }

    private bool CheckMultiRegLclVar(GenTreeLclVar node, int registerCount)
    {
        var canEnregisterAsMultiReg = false;
        var canEnregisterAsSingleReg = false;
        if (Configuration.FEATURE_MULTIREG_RET || Configuration.FEATURE_HW_INTRINSICS)
        {
            ref var descriptor = ref CompilerInstance.lvaGetDesc(node.LclNum);
            if (descriptor.lvDoNotEnregister)
            {
                assert(!node.IsMultiReg);
                return false;
            }

            if (CompilerInstance.lvaEnregMultiRegVars && descriptor.lvPromoted)
            {
                if ((CompilerInstance.lvaGetPromotionType(in descriptor) is Compiler.lvaPromotionType.PROMOTION_TYPE_INDEPENDENT) &&
                    (registerCount == descriptor.lvFieldCnt))
                {
                    canEnregisterAsMultiReg = true;
                    if (Configuration.FEATURE_SIMD)
                    {
                        // A Vector3 field can span ABI registers, even when register and field counts match.
                        for (var i = 0; i < descriptor.lvFieldCnt; i++)
                        {
                            if (CompilerInstance.lvaGetDesc(descriptor.lvFieldLclStart + i).Type is TYP_SIMD12)
                            {
                                canEnregisterAsMultiReg = false;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                canEnregisterAsSingleReg = varTypeIsSimd(node.Type);
                if (Configuration.TARGET_XARCH && (node.Oper is GT_STORE_LCL_VAR) &&
                    varTypeIsStruct(node.Op1.Type) && (node.Op1.Oper is not GT_CALL))
                {
                    canEnregisterAsSingleReg = false;
                }
            }

            if (canEnregisterAsSingleReg || canEnregisterAsMultiReg)
            {
                if (canEnregisterAsMultiReg)
                {
                    node.SetMultiReg();
                }
            }
            else
            {
                CompilerInstance.lvaSetVarDoNotEnregister(node.LclNum, DoNotEnregisterReason.BlockOp);
            }
        }

        return canEnregisterAsSingleReg || canEnregisterAsMultiReg;
    }

    private void VerifyLclFldDoNotEnregister(int localNumber)
    {
        ref var descriptor = ref CompilerInstance.lvaGetDesc(localNumber);
        if (descriptor.lvTracked && !descriptor.lvDoNotEnregister)
        {
            assert(!_regAlloc.IsRegCandidate(in descriptor));
            CompilerInstance.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LocalField);
        }
    }

    private GenTree? LowerStoreLoc(GenTreeLclVarCommon storeLoc)
    {
        if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARM && !Configuration.TARGET_ARM64 &&
            !Configuration.TARGET_RISCV64 && !Configuration.TARGET_WASM && !Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Local-store lowering is not ported for this target.");
            return default!;
        }

        if (Configuration.TARGET_ARM && (storeLoc.Oper is GT_STORE_LCL_VAR) && varTypeIsSmall(storeLoc.Type) &&
            storeLoc.Op1.Oper.IsCnsIntOrI)
        {
            ref var descriptor = ref CompilerInstance.lvaGetDesc(storeLoc.LclNum);
            if (!descriptor.lvIsStructField && (descriptor.GetStackSlotHomeType() is TYP_INT))
            {
                storeLoc.Type = TYP_INT;
            }
        }
        if (Configuration.TARGET_XARCH && (storeLoc.Oper is GT_STORE_LCL_VAR) && (storeLoc.Type.Size == 2) &&
            storeLoc.Op1.Oper.IsCnsIntOrI && !CompilerInstance.lvaGetDesc(storeLoc.LclNum).lvIsStructField)
        {
            storeLoc.Type = TYP_INT;
        }

        if (storeLoc.Oper is GT_STORE_LCL_FLD)
        {
            VerifyLclFldDoNotEnregister(storeLoc.LclNum);
        }

        ContainCheckStoreLoc(storeLoc);
        var next = storeLoc.Next;
        if (Configuration.TARGET_ARM64 && CompilerInstance.opts.OptimizationEnabled)
        {
            _ = TryMoveAddSubRMWAfterIndir(storeLoc);
        }
        return next;
    }

    private void ContainCheckStoreLoc(GenTreeLclVarCommon storeLoc)
    {
        if (Configuration.TARGET_WASM)
        {
            // Wasm stores do not contain local-store sources.
            return;
        }

        if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARM && !Configuration.TARGET_ARM64 &&
            !Configuration.TARGET_RISCV64 && !Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Local-store containment is not ported for this target.");
            return;
        }

        assert(storeLoc.Oper.IsLocalStore);
        var source = storeLoc.Op1;
        if ((source.Oper is GT_BITCAST) && !varTypeIsSmall(storeLoc.Type))
        {
            var bitcastSource = source.AsUnOp().Op1;
            if (!bitcastSource.IsContained && !bitcastSource.IsRegOptional)
            {
                source.IsContained = true;
                return;
            }
        }

        ref var descriptor = ref CompilerInstance.lvaGetDesc(storeLoc.LclNum);
        if (Configuration.FEATURE_SIMD)
        {
            if ((Configuration.TARGET_ARM64 || Configuration.TARGET_ARM || Configuration.TARGET_RISCV64 ||
                Configuration.TARGET_LOONGARCH64) && (storeLoc.Type is TYP_SIMD8 or TYP_SIMD12))
            {
                if ((source.IsIntegralConst(0) || source.IsVectorZero) && descriptor.lvDoNotEnregister)
                {
                    MakeSrcContained(storeLoc, source);
                }

                return;
            }

            if (Configuration.TARGET_XARCH && varTypeIsSimd(storeLoc.Type))
            {
                assert(!source.Oper.IsCnsIntOrI);
                return;
            }
        }

        if ((Configuration.TARGET_XARCH || Configuration.TARGET_ARM || Configuration.TARGET_ARM64 ||
             Configuration.TARGET_RISCV64) &&
            (storeLoc.Oper is GT_STORE_LCL_VAR) && source.IsIconHandle() &&
            source.AsIntCon().FitsInAddrBase(CompilerInstance) &&
            source.AsIntCon().AddrNeedsReloc(CompilerInstance))
        {
            MakeSrcContained(storeLoc, source);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            if (IsContainableImmed(storeLoc, source))
            {
                MakeSrcContained(storeLoc, source);
            }

            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (IsContainableImmed(storeLoc, source))
            {
                MakeSrcContained(storeLoc, source);
            }

            var type = descriptor.GetRegisterType(storeLoc);
            if (IsContainableImmed(storeLoc, source) &&
                (!source.IsIntegralConst(0) || varTypeIsSmall(type)))
            {
                MakeSrcContained(storeLoc, source);
            }

            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            if (IsContainableImmed(storeLoc, source))
            {
                MakeSrcContained(storeLoc, source);
            }

            var type = descriptor.GetRegisterType(storeLoc);
            if (IsContainableImmed(storeLoc, source) &&
                (!source.IsIntegralConst(0) || varTypeIsSmall(type)))
            {
                MakeSrcContained(storeLoc, source);
            }

            return;
        }

        if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM)
        {
            var type = descriptor.GetRegisterType(storeLoc);
            if (IsContainableImmed(storeLoc, source) && (!source.IsIntegralConst(0) || varTypeIsSmall(type)))
            {
                MakeSrcContained(storeLoc, source);
            }
            else if ((Configuration.TARGET_X86 || Configuration.TARGET_ARM) && (source.Oper is GT_LONG))
            {
                MakeSrcContained(storeLoc, source);
            }

            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Local-store containment is not ported for this target.");
        return;
    }

    private void LowerLclVar(GenTreeLclVar local)
    {
        WidenSIMD12IfNecessary(local);
        ref var descriptor = ref CompilerInstance.lvaGetDesc(local.LclNum);
        if (local.IsMultiRegLclVar &&
            (!descriptor.lvPromoted ||
                (CompilerInstance.lvaGetPromotionType(in descriptor) is not Compiler.lvaPromotionType.PROMOTION_TYPE_INDEPENDENT) ||
                (descriptor.lvFieldCnt > Configuration.MAX_MULTIREG_COUNT)))
        {
            local.ClearMultiReg();
            if (local.Type is TYP_STRUCT)
            {
                CompilerInstance.lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.BlockOp);
            }
        }
    }

    private unsafe GenTreeLclVar SpillStructCallResult(GenTreeCall call)
    {
        var compiler = CompilerInstance;
        var spillNumber = compiler.lvaGrabTemp(true, "Return value temp for a non-enregisterable struct return");
        compiler.lvaSetVarDoNotEnregister(spillNumber, DoNotEnregisterReason.LocalField);
        compiler.lvaSetStruct(spillNumber, call.RetClsHnd, false);
        var offset = 0;
        if (Configuration.FEATURE_MULTIREG_RET)
        {
            assert(call.ReturnTypeDesc.ReturnRegCount == 1);
            offset = call.ReturnTypeDesc.GetReturnFieldOffset(0);
        }

        var spill = compiler.gtNewStoreLclFldNode(call.Type, spillNumber, checked((ushort)offset), call);
        BlockRange().InsertAfter(call, spill);
        ContainCheckStoreLoc(spill);
        var result = compiler.gtNewLclvNode(TYP_STRUCT, spillNumber);
        BlockRange().InsertAfter(spill, result);
        return result;
    }
}
