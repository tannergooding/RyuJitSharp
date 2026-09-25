// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerRet(GenTreeUnOp ret)
    {
#if WINDOWS_AMD64_ABI
        assert(ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
        JITDUMP("lowering return node\n");
        DISPNODE(ret);
        JITDUMP("============\n");

        var compiler = CompilerInstance;
        var value = GetRetValueRef(ret);
        var needBitcast = (ret.Type is not TYP_VOID) && !varTypeUsesSameRegType(ret.Type, value.Type);
        var doPrimitiveBitcast = needBitcast && !varTypeIsStruct(ret.Type) && !varTypeIsStruct(value.Type);
        if (doPrimitiveBitcast)
        {
            var bitcast = compiler.gtNewBitCastNode(ret.Type, value);
            GetRetValueRef(ret) = bitcast;
            BlockRange().InsertBefore(ret, bitcast);
            ContainCheckBitCast(bitcast);
        }
        else if (ret.Type is not TYP_VOID)
        {
#if FEATURE_MULTIREG_RET
            if (compiler.compMethodReturnsMultiRegRetType && (value.Oper is GT_LCL_VAR))
            {
                _ = CheckMultiRegLclVar(value.AsLclVar(), compiler.compRetTypeDesc.ReturnRegCount);
            }
#endif
#if DEBUG
            if (varTypeIsStruct(ret.Type) != varTypeIsStruct(value.Type))
            {
                if (varTypeIsStruct(ret.Type))
                {
                    assert(compiler.info.compRetNativeType is not TYP_STRUCT);
                    var returnType = compiler.info.compRetNativeType.ActualType;
                    var valueType = value.Type.ActualType;
                    assert((returnType == valueType) || value.IsCnsInitVal || (returnType.Size <= valueType.Size));
                }
            }
#endif
            if (value.Oper is GT_FIELD_LIST)
            {
                LowerRetFieldList(ret, value.AsFieldList());
            }
            else if (varTypeIsStruct(ret.Type))
            {
                LowerRetStruct(ret);
            }
            else if (varTypeIsStruct(value.Type))
            {
                assert(value.Oper is GT_LCL_VAR);
                LowerRetSingleRegStructLclVar(ret);
            }
        }

        if (compiler.compMethodRequiresPInvokeFrame)
        {
            assert(compiler.compCurBB is not null);
            InsertPInvokeMethodEpilog(compiler.compCurBB, ret);
        }
        ContainCheckRet(ret);
#else
        throw new System.NotImplementedException("Return lowering outside Windows AMD64 is not ported.");
#endif
    }

    private static ref GenTree GetRetValueRef(GenTreeUnOp ret)
    {
        assert(ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
#if SWIFT_SUPPORT
        if (ret.Oper is GT_SWIFT_ERROR_RET)
        {
            return ref ret.AsOp().Op2Ref;
        }
#endif
        return ref ret.Op1Ref;
    }

    private unsafe void LowerRetFieldList(GenTreeUnOp ret, GenTreeFieldList fieldList)
    {
        var compiler = CompilerInstance;
        var returnDescriptor = compiler.compRetTypeDesc;
        var registerCount = returnDescriptor.ReturnRegCount;

        LowerFieldListRegisterInfo GetRegisterInfo(int index)
        {
            var offset = returnDescriptor.GetReturnFieldOffset((byte)index);
            var type = returnDescriptor.GetReturnRegType((byte)index);
            return new LowerFieldListRegisterInfo(offset, type);
        }

        if (!IsFieldListCompatibleWithRegisters(fieldList, registerCount, GetRegisterInfo))
        {
            var layout = compiler.typGetObjLayout(compiler.info.compMethodInfo->args.retTypeClass);
            var localNumber = StoreFieldListToNewLocal(layout, fieldList);
            ref var descriptor = ref compiler.lvaGetDesc(localNumber);
            var value = compiler.gtNewLclvNode(descriptor.Type, localNumber);
            GetRetValueRef(ret) = value;
            BlockRange().InsertBefore(ret, value);
            _ = LowerNode(value);
            BlockRange().Remove(fieldList);

            if (registerCount == 1)
            {
                ret.ChangeType(compiler.info.compRetNativeType.ActualType);
                LowerRetSingleRegStructLclVar(ret);
            }
            else
            {
                descriptor.lvIsMultiRegRet = true;
            }
            return;
        }

        LowerFieldListToFieldListOfRegisters(fieldList, registerCount, GetRegisterInfo);
    }

    private void ContainCheckRet(GenTreeUnOp ret)
    {
        assert(ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);

#if LOWER_DECOMPOSE_LONGS
        if (ret.Type is TYP_LONG)
        {
            var value = ret.Op1;
            assert(value.Oper is GT_LONG);
            MakeSrcContained(ret, value);
        }
#endif
#if FEATURE_MULTIREG_RET
        if (ret.Type is TYP_STRUCT)
        {
            var value = ret.Op1;
            if (value.Oper is GT_LCL_VAR)
            {
                ref var descriptor = ref CompilerInstance.lvaGetDesc(value.AsLclVarCommon().LclNum);
                assert(descriptor.lvIsMultiRegRet);
                if (descriptor.lvDoNotEnregister || !descriptor.IsEnregisterableType)
                {
                    if (!value.IsMultiRegLclVar)
                    {
                        MakeSrcContained(ret, value);
                    }
                }
            }
        }
#endif
    }
}
