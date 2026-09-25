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
#if TARGET_XARCH
        assert(store.Oper.IsStore);
        if (!varTypeIsFloating(store.Type))
        {
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

        var doubleValue = value.AsDblCon().DconVal;
        var_types type;
        nint integerValue;
        if (store.Type is TYP_FLOAT)
        {
            type = TYP_INT;
            integerValue = BitConverter.SingleToInt32Bits((float)doubleValue);
        }
#if TARGET_64BIT
        else
        {
            assert(store.Type is TYP_DOUBLE);
            type = TYP_LONG;
            integerValue = unchecked((nint)BitConverter.DoubleToInt64Bits(doubleValue));
        }
#else
        else
        {
            return store;
        }
#endif

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
#else
        throw new NotImplementedException("Non-xarch floating-store retyping is not ported.");
#endif
    }

    private unsafe GenTree? LowerStoreLocCommon(GenTreeLclVarCommon lclStore)
    {
#if WINDOWS_AMD64_ABI
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
#if DEBUG
                JITDUMP($"Replacing an independently promoted local var V{lclStore.LclNum:D2} with its only field V{fieldNumber:D2} for the store from a call [{lclStore.TreeId:D6}]\n");
#endif
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
#if DEBUG
                var layout = lclStore.GetLayout(CompilerInstance);
                assert(layout is not null);
                assert(layout.SlotCount == 1);
                assert(localRegisterType is not TYP_UNDEF);
#endif
                convertToStoreObj = false;
            }
            else if (!descriptor.IsEnregisterableType)
            {
                convertToStoreObj = true;
            }
            else if (src.Oper is GT_CNS_INT)
            {
                assert(src.IsIntegralConst(0), "expected an INIT_VAL for non-zero init.");
#if FEATURE_SIMD
                if (varTypeIsSimd(localRegisterType))
                {
                    var zero = CompilerInstance.gtNewZeroConNode(localRegisterType);
                    BlockRange().InsertAfter(src, zero);
                    BlockRange().Remove(src);
                    lclStore.Op1 = src = zero;
                }
#endif
                convertToStoreObj = false;
            }
            else if (src.Oper is GT_LCL_VAR)
            {
                convertToStoreObj = false;
            }
            else if (src.Oper is GT_IND or GT_BLK or GT_LCL_FLD)
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
                    if (varTypeIsSmall(localRegisterType))
                    {
                        src.Flags |= GTF_DONT_EXTEND;
                    }
                }
                convertToStoreObj = false;
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
                _ = LowerNode(store);
                JITDUMP("lowering store lcl var/field (after):\n");
                DISPTREERANGE(BlockRange(), store);
                JITDUMP("\n");
                return store.Next;
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
#else
        throw new NotImplementedException("Non-Windows-AMD64 common local-store lowering is not ported.");
#endif
    }

    private void WidenSIMD12IfNecessary(GenTreeLclVarCommon node)
    {
#if FEATURE_SIMD
        if ((node.Type is TYP_SIMD12) && CompilerInstance.lvaMapSimd12ToSimd16(node.LclNum))
        {
            JITDUMP("Mapping TYP_SIMD12 lclvar node to TYP_SIMD16:\n");
            DISPNODE(node);
            JITDUMP("============\n");
            node.Type = TYP_SIMD16;
        }
#endif
    }

    private bool CheckMultiRegLclVar(GenTreeLclVar node, int registerCount)
    {
        var canEnregisterAsMultiReg = false;
        var canEnregisterAsSingleReg = false;
#if FEATURE_MULTIREG_RET || FEATURE_HW_INTRINSICS
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
#if FEATURE_SIMD
                // A Vector3 field can span ABI registers, even when register and field counts match.
                for (var i = 0; i < descriptor.lvFieldCnt; i++)
                {
                    if (CompilerInstance.lvaGetDesc(descriptor.lvFieldLclStart + i).Type is TYP_SIMD12)
                    {
                        canEnregisterAsMultiReg = false;
                        break;
                    }
                }
#endif
            }
        }
        else
        {
            canEnregisterAsSingleReg = varTypeIsSimd(node.Type);
#if TARGET_XARCH
            if ((node.Oper is GT_STORE_LCL_VAR) && varTypeIsStruct(node.Op1.Type) && (node.Op1.Oper is not GT_CALL))
            {
                canEnregisterAsSingleReg = false;
            }
#endif
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
#endif
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
#if TARGET_XARCH
        if ((storeLoc.Oper is GT_STORE_LCL_VAR) && (storeLoc.Type.Size == 2) &&
            storeLoc.Op1.Oper.IsCnsIntOrI && !CompilerInstance.lvaGetDesc(storeLoc.LclNum).lvIsStructField)
        {
            storeLoc.Type = TYP_INT;
        }
        if (storeLoc.Oper is GT_STORE_LCL_FLD)
        {
            VerifyLclFldDoNotEnregister(storeLoc.LclNum);
        }

        ContainCheckStoreLoc(storeLoc);
        return storeLoc.Next;
#else
        throw new System.NotImplementedException("Non-xarch local-store lowering is not ported.");
#endif
    }

    private void ContainCheckStoreLoc(GenTreeLclVarCommon storeLoc)
    {
#if TARGET_XARCH
        assert(storeLoc.Oper.IsLocalStore);
        var source = storeLoc.Op1;
        if (source.Oper is GT_BITCAST)
        {
            var bitcastSource = source.AsUnOp().Op1;
            if (!bitcastSource.IsContained && !bitcastSource.IsRegOptional)
            {
                source.IsContained = true;
                return;
            }
        }

        ref var descriptor = ref CompilerInstance.lvaGetDesc(storeLoc.LclNum);
#if FEATURE_SIMD
        if (varTypeIsSimd(storeLoc.Type))
        {
            assert(!source.Oper.IsCnsIntOrI);
            return;
        }
#endif
        var type = descriptor.GetRegisterType(storeLoc);
        if (IsContainableImmed(storeLoc, source) && (!source.IsIntegralConst(0) || varTypeIsSmall(type)))
        {
            MakeSrcContained(storeLoc, source);
        }
#if TARGET_X86
        else if (source.Oper is GT_LONG)
        {
            MakeSrcContained(storeLoc, source);
        }
#endif
#else
        throw new System.NotImplementedException("Non-xarch local-store containment is not ported.");
#endif
    }
}
