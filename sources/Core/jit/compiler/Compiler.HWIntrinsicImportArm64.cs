// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_ARM64
namespace RyuJitSharp;

public partial class Compiler
{
    private void getHWIntrinsicImmOpsArm64Core(
        NamedIntrinsic intrinsic, in CORINFO_SIG_INFO sig, ref GenTree? immOp1, ref GenTree? immOp2)
    {
        if (!HWIntrinsicInfo.HasImmediateOperand(intrinsic))
        {
            return;
        }

        HWIntrinsicInfo.GetImmOpsPositions(intrinsic, sig.numArgs, out var first, out var second);

        if (first >= 0)
        {
            immOp1 = impStackTop(first).val;
            assert(HWIntrinsicInfo.isImmOp(intrinsic, immOp1));
        }

        if (second >= 0)
        {
            immOp2 = impStackTop(second).val;
            assert(HWIntrinsicInfo.isImmOp(intrinsic, immOp2));
        }
    }

    private GenTreeHWIntrinsic? impNonConstFallbackArm64Core(
        NamedIntrinsic intrinsic, var_types simdType, var_types simdBaseType)
    {
        var isRightShift = true;
        switch (intrinsic)
        {
            case NI_AdvSimd_ShiftLeftLogical:
            case NI_AdvSimd_ShiftLeftLogicalScalar:
            {
                isRightShift = false;
                goto case NI_AdvSimd_ShiftRightLogical;
            }
            case NI_AdvSimd_ShiftRightLogical:
            case NI_AdvSimd_ShiftRightLogicalScalar:
            case NI_AdvSimd_ShiftRightArithmetic:
            case NI_AdvSimd_ShiftRightArithmeticScalar:
            {
                var op2 = impPopStack().val;
                var op1 = impSIMDPopStack();

                // Variable AdvSimd shifts express right shifts as negative per-element counts.
                if (isRightShift)
                {
                    op2 = gtNewUnaryNode(GT_NEG, op2.Type.ActualType, op2);
                }

                var fallbackIntrinsic = intrinsic switch
                {
                    NI_AdvSimd_ShiftLeftLogical or NI_AdvSimd_ShiftRightLogical => NI_AdvSimd_ShiftLogical,
                    NI_AdvSimd_ShiftLeftLogicalScalar or NI_AdvSimd_ShiftRightLogicalScalar =>
                        NI_AdvSimd_ShiftLogicalScalar,
                    NI_AdvSimd_ShiftRightArithmetic => NI_AdvSimd_ShiftArithmetic,
                    NI_AdvSimd_ShiftRightArithmeticScalar => NI_AdvSimd_ShiftArithmeticScalar,
                    _ => unreachableArm64FallbackIntrinsic(),
                };
                var simdSize = unchecked((byte)simdType.Size);
                var count = gtNewSimdCreateBroadcastNode(simdType, op2, simdBaseType, simdSize);

                return gtNewSimdHWIntrinsicNode(simdType, fallbackIntrinsic, simdBaseType, simdSize, op1, count);
            }
            default:
            {
                return null;
            }
        }
    }

    private static NamedIntrinsic unreachableArm64FallbackIntrinsic()
    {
        unreached();
        throw new FatalJitException(CORJIT_RECOVERABLEERROR, "Unreachable ARM64 fallback intrinsic.");
    }

    private unsafe GenTree? impSpecialIntrinsicArm64Core(
        NamedIntrinsic intrinsic, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE method,
        in CORINFO_SIG_INFO sig, in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType, var_types retType, byte simdSize, bool mustExpand)
    {
        var isa = HWIntrinsicInfo.lookupIsa(intrinsic);
        if (isa == InstructionSet_Vector)
        {
            return impXplatIntrinsic(intrinsic, clsHnd, method, in sig, in entryPoint,
                simdBaseType, retType, simdSize, mustExpand);
        }

        var category = HWIntrinsicInfo.lookupCategory(intrinsic);
        int numArgs = sig.numArgs;

        if (intrinsic == NI_ArmBase_Yield)
        {
            assert(sig.numArgs == 0);
            assert(sig.retType.VarType == TYP_VOID);
            assert(simdSize == 0);

            return gtNewScalarHWIntrinsicNode(TYP_VOID, intrinsic);
        }

        var isScalar = category == HW_Category_Scalar;
        assert(numArgs >= 0);
        assert(varTypeIsArithmetic(simdBaseType));

        GenTree? retNode = null;
        GenTree op1;
        GenTree op2;
        GenTree op3;
        GenTree op4;
#if DEBUG
        var isValidScalarIntrinsic = false;
#endif
        fixed (CORINFO_SIG_INFO* signature = &sig)
        {
            switch (intrinsic)
            {
                case NI_AdvSimd_BitwiseClear:
                case NI_Sve_BitwiseClear:
                {
                    assert(sig.numArgs == 2);
                    // AND_NOT/OR_NOT are introduced only in lowering so earlier tree optimizations see NOT.
                    op2 = impSIMDPopStack();
                    op1 = impSIMDPopStack();
                    op2 = gtFoldExpr(gtNewSimdUnOpNode(GT_NOT, retType, op2, simdBaseType, simdSize));
                    retNode = gtNewSimdBinOpNode(GT_AND, retType, op1, op2, simdBaseType, simdSize);
                    break;
                }
                case NI_AdvSimd_OrNot:
                {
                    assert(sig.numArgs == 2);
                    op2 = impSIMDPopStack();
                    op1 = impSIMDPopStack();
                    op2 = gtFoldExpr(gtNewSimdUnOpNode(GT_NOT, retType, op2, simdBaseType, simdSize));
                    retNode = gtNewSimdBinOpNode(GT_OR, retType, op1, op2, simdBaseType, simdSize);
                    break;
                }
                case NI_AdvSimd_LoadVector64:
                case NI_AdvSimd_LoadVector128:
                {
                    assert(sig.numArgs == 1);
                    op1 = impPopStack().val;
                    if ((op1.Oper == GT_CAST) && (op1.AsCast().Op1.Type == TYP_BYREF))
                    {
                        op1 = op1.AsCast().Op1;
                    }
                    retNode = gtNewSimdLoadNode(retType, op1, simdBaseType, simdSize);
                    break;
                }
                case NI_AdvSimd_Store:
                case NI_AdvSimd_Arm64_Store:
                {
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = impPopStack().val;
                    if (op2.Type == TYP_STRUCT)
                    {
                        info.compNeedsConsecutiveRegisters = true;
                        var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                        if (op2.Oper != GT_LCL_VAR)
                        {
                            var temp = lvaGrabTemp(true, "StoreVectorN");
                            impStoreToTemp(temp, op2, CHECK_SPILL_NONE);
                            op2 = gtNewLclVarNode(argType, temp);
                        }
                        op2 = gtConvertTableOpToFieldListArm64(op2, fieldCount);
                        argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                        op1 = getArgForHWIntrinsic(argType, argClass);
                        if ((op1.Oper == GT_CAST) && (op1.AsCast().Op1.Type == TYP_BYREF))
                        {
                            op1 = op1.AsCast().Op1;
                        }
                        retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    }
                    else
                    {
                        // AdvSimd.Store also contains the Vector128 overload; metadata initially supplies 8.
                        if (op2.Type == TYP_SIMD16)
                        {
                            simdSize = 16;
                        }
                        op1 = impPopStack().val;
                        if ((op1.Oper == GT_CAST) && (op1.AsCast().Op1.Type == TYP_BYREF))
                        {
                            op1 = op1.AsCast().Op1;
                        }
                        retNode = gtNewSimdStoreNode(op1, op2, simdBaseType, simdSize);
                    }
                    break;
                }
                case NI_AdvSimd_StoreVectorAndZip:
                case NI_AdvSimd_Arm64_StoreVectorAndZip:
                {
                    assert(sig.numArgs == 2);
                    assert(retType == TYP_VOID);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = impPopStack().val;
                    var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    assert(op2.Type == TYP_STRUCT);
                    if ((op1.Oper == GT_CAST) && (op1.AsCast().Op1.Type == TYP_BYREF))
                    {
                        op1 = op1.AsCast().Op1;
                    }
                    if (op2.Oper != GT_LCL_VAR)
                    {
                        var temp = lvaGrabTemp(true, "StoreVectorNx2 temp tree");
                        impStoreToTemp(temp, op2, CHECK_SPILL_NONE);
                        op2 = gtNewLclVarNode(argType, temp);
                    }
                    op2 = gtConvertTableOpToFieldListArm64(op2, fieldCount);
                    intrinsic = simdSize == 8 ? NI_AdvSimd_StoreVectorAndZip : NI_AdvSimd_Arm64_StoreVectorAndZip;
                    info.compNeedsConsecutiveRegisters = true;
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    break;
                }
                case NI_AdvSimd_StoreSelectedScalar:
                case NI_AdvSimd_Arm64_StoreSelectedScalar:
                {
                    assert(sig.numArgs == 3);
                    assert(retType == TYP_VOID);
                    if (!mustExpand && !impStackTop(0).val.Oper.IsCnsIntOrI &&
                        (impStackTop(1).val.Type == TYP_STRUCT))
                    {
                        // Field-list intrinsics cannot yet be rewritten as user calls during rationalization.
                        return null;
                    }

                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    op3 = impPopStack().val;
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = impPopStack().val;
                    var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                    if (op2.Type == TYP_STRUCT)
                    {
                        info.compNeedsConsecutiveRegisters = true;
                        intrinsic = simdSize == 8 ? NI_AdvSimd_StoreSelectedScalar : NI_AdvSimd_Arm64_StoreSelectedScalar;
                        if (op2.Oper != GT_LCL_VAR)
                        {
                            var temp = lvaGrabTemp(true, "StoreSelectedScalarN");
                            impStoreToTemp(temp, op2, CHECK_SPILL_NONE);
                            op2 = gtNewLclVarNode(argType, temp);
                        }
                        op2 = gtConvertTableOpToFieldListArm64(op2, fieldCount);
                    }
                    else
                    {
                        _ = getBaseTypeAndSizeOfSimdType(argClass, out var byteCount);
                        simdSize = unchecked((byte)byteCount);
                    }
                    assert(HWIntrinsicInfo.isImmOp(intrinsic, op3));
                    HWIntrinsicInfo.lookupImmBounds(intrinsic, simdSize, simdBaseType, 1, out var lower, out var upper);
                    op3 = addRangeCheckIfNeeded(intrinsic, op3, lower, upper);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    if ((op1.Oper == GT_CAST) && (op1.AsCast().Op1.Type == TYP_BYREF))
                    {
                        op1 = op1.AsCast().Op1;
                    }
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    break;
                }
                case NI_AdvSimd_Load2xVector64AndUnzip:
                case NI_AdvSimd_Load3xVector64AndUnzip:
                case NI_AdvSimd_Load4xVector64AndUnzip:
                case NI_AdvSimd_Arm64_Load2xVector128AndUnzip:
                case NI_AdvSimd_Arm64_Load3xVector128AndUnzip:
                case NI_AdvSimd_Arm64_Load4xVector128AndUnzip:
                case NI_AdvSimd_Load2xVector64:
                case NI_AdvSimd_Load3xVector64:
                case NI_AdvSimd_Load4xVector64:
                case NI_AdvSimd_Arm64_Load2xVector128:
                case NI_AdvSimd_Arm64_Load3xVector128:
                case NI_AdvSimd_Arm64_Load4xVector128:
                case NI_AdvSimd_LoadAndReplicateToVector64x2:
                case NI_AdvSimd_LoadAndReplicateToVector64x3:
                case NI_AdvSimd_LoadAndReplicateToVector64x4:
                case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2:
                case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3:
                case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4:
                {
                    info.compNeedsConsecutiveRegisters = true;
                    goto case NI_AdvSimd_Arm64_LoadPairScalarVector64;
                }
                case NI_AdvSimd_Arm64_LoadPairScalarVector64:
                case NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal:
                case NI_AdvSimd_Arm64_LoadPairVector128:
                case NI_AdvSimd_Arm64_LoadPairVector128NonTemporal:
                case NI_AdvSimd_Arm64_LoadPairVector64:
                case NI_AdvSimd_Arm64_LoadPairVector64NonTemporal:
                {
                    op1 = impPopStack().val;
                    if ((op1.Oper == GT_CAST) && (op1.AsCast().Op1.Type == TYP_BYREF))
                    {
                        op1 = op1.AsCast().Op1;
                    }
                    assert(HWIntrinsicInfo.IsMultiReg(intrinsic));
                    op1 = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1);
                    retNode = impStoreMultiRegValueToVar(op1, sig.retTypeSigClass, CorInfoCallConvExtension.Managed);
                    break;
                }
                case NI_Sve_CreateFalseMaskByte:
                case NI_Sve_CreateFalseMaskDouble:
                case NI_Sve_CreateFalseMaskInt16:
                case NI_Sve_CreateFalseMaskInt32:
                case NI_Sve_CreateFalseMaskInt64:
                case NI_Sve_CreateFalseMaskSByte:
                case NI_Sve_CreateFalseMaskSingle:
                case NI_Sve_CreateFalseMaskUInt16:
                case NI_Sve_CreateFalseMaskUInt32:
                case NI_Sve_CreateFalseMaskUInt64:
                {
                    if (retType == TYP_SIMD)
                    {
                        retNode = gtNewSimdVconNode(retType, simdBaseType, SimdScalableKind.SimdScalableRepeated, 0);
                        break;
                    }
                    retNode = gtNewZeroConNode(retType);
                    break;
                }
                case NI_Sve_CreateTrueMaskByte:
                case NI_Sve_CreateTrueMaskDouble:
                case NI_Sve_CreateTrueMaskInt16:
                case NI_Sve_CreateTrueMaskInt32:
                case NI_Sve_CreateTrueMaskInt64:
                case NI_Sve_CreateTrueMaskSByte:
                case NI_Sve_CreateTrueMaskSingle:
                case NI_Sve_CreateTrueMaskUInt16:
                case NI_Sve_CreateTrueMaskUInt32:
                case NI_Sve_CreateTrueMaskUInt64:
                {
                    assert(sig.numArgs == 1);
                    assert(retType == TYP_MASK);
                    op1 = impPopStack().val;
                    if (op1.Oper.IsIntegralConst)
                    {
                        var pattern = op1.AsIntConCommon().IntegralValue;
                        if (retType == TYP_SIMD)
                        {
                            if ((pattern == (long)SveMaskPattern.SveMaskPatternAll) ||
                                (pattern == (long)SveMaskPattern.SveMaskPatternLargestPowerOf2))
                            {
                                retNode = gtNewSimdVconNode(retType, simdBaseType,
                                    SimdScalableKind.SimdScalableRepeated, simdAllBitsSetForElementTypeArm64(simdBaseType));
                                break;
                            }
                        }
                        else
                        {
                            simdmask_t mask = default;
                            if (EvaluateSimdPatternToMask<simd16_t>(simdBaseType, ref mask, (SveMaskPattern)pattern))
                            {
                                retNode = gtNewMskConNode(mask);
                                break;
                            }
                        }
                    }
                    retNode = gtNewSimdHWIntrinsicNode(TYP_MASK, intrinsic, simdBaseType, simdSize, op1);
                    break;
                }
                case NI_Sve_Load2xVectorAndUnzip:
                case NI_Sve_Load3xVectorAndUnzip:
                case NI_Sve_Load4xVectorAndUnzip:
                {
                    info.compNeedsConsecutiveRegisters = true;
                    assert(sig.numArgs == 2);
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    if ((op2.Oper == GT_CAST) && (op2.AsCast().Op1.Type == TYP_BYREF))
                    {
                        op2 = op2.AsCast().Op1;
                    }
                    assert(HWIntrinsicInfo.IsMultiReg(intrinsic));
                    assert(HWIntrinsicInfo.IsExplicitMaskedOperation(intrinsic));
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    break;
                }
                case NI_AdvSimd_LoadAndInsertScalarVector64x2:
                case NI_AdvSimd_LoadAndInsertScalarVector64x3:
                case NI_AdvSimd_LoadAndInsertScalarVector64x4:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
                {
                    assert(sig.numArgs == 3);
                    if (!mustExpand && !impStackTop(1).val.Oper.IsCnsIntOrI)
                    {
                        return null;
                    }
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    op3 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = impPopStack().val;
                    if ((op3.Oper == GT_CAST) && (op3.AsCast().Op1.Type == TYP_BYREF))
                    {
                        op3 = op3.AsCast().Op1;
                    }
                    assert(HWIntrinsicInfo.IsMultiReg(intrinsic));
                    assert(op1.Type == TYP_STRUCT);
                    info.compNeedsConsecutiveRegisters = true;
                    var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                    if (op1.Oper != GT_LCL_VAR)
                    {
                        var temp = lvaGrabTemp(true, "LoadAndInsertScalar temp tree");
                        impStoreToTemp(temp, op1, CHECK_SPILL_NONE);
                        op1 = gtNewLclVarNode(argType, temp);
                    }
                    op1 = gtConvertParamOpToFieldListArm64(op1, fieldCount, argClass);
                    op1 = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    retNode = impStoreMultiRegValueToVar(op1, sig.retTypeSigClass, CorInfoCallConvExtension.Managed);
                    break;
                }
                case NI_AdvSimd_VectorTableLookup:
                case NI_AdvSimd_Arm64_VectorTableLookup:
                {
                    assert(sig.numArgs == 2);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = impPopStack().val;
                    if (op1.Type == TYP_STRUCT)
                    {
                        info.compNeedsConsecutiveRegisters = true;
                        var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                        if (op1.Oper != GT_LCL_VAR)
                        {
                            var temp = lvaGrabTemp(true, "VectorTableLookup temp tree");
                            impStoreToTemp(temp, op1, CHECK_SPILL_NONE);
                            op1 = gtNewLclVarNode(argType, temp);
                        }
                        op1 = gtConvertTableOpToFieldListArm64(op1, fieldCount);
                    }
                    else
                    {
                        assert(varTypeIsSimd(op1.Type));
                    }
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    break;
                }
                case NI_AdvSimd_VectorTableLookupExtension:
                case NI_AdvSimd_Arm64_VectorTableLookupExtension:
                {
                    assert(sig.numArgs == 3);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    op3 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    if (op2.Type == TYP_STRUCT)
                    {
                        info.compNeedsConsecutiveRegisters = true;
                        var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                        if (op2.Oper != GT_LCL_VAR)
                        {
                            var temp = lvaGrabTemp(true, "VectorTableLookupExtension temp tree");
                            impStoreToTemp(temp, op2, CHECK_SPILL_NONE);
                            op2 = gtNewLclVarNode(argType, temp);
                        }
                        op2 = gtConvertTableOpToFieldListArm64(op2, fieldCount);
                    }
                    else
                    {
                        assert(varTypeIsSimd(op1.Type));
                    }
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    break;
                }
                case NI_Sve_StoreAndZip:
                {
                    assert(sig.numArgs == 3);
                    assert(retType == TYP_VOID);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    op3 = impPopStack().val;
                    var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                    if (op3.Type == TYP_STRUCT)
                    {
                        info.compNeedsConsecutiveRegisters = true;
                        switch (fieldCount)
                        {
                            case 2:
                            {
                                intrinsic = NI_Sve_StoreAndZipx2;
                                break;
                            }
                            case 3:
                            {
                                intrinsic = NI_Sve_StoreAndZipx3;
                                break;
                            }
                            case 4:
                            {
                                intrinsic = NI_Sve_StoreAndZipx4;
                                break;
                            }
                            default:
                            {
                                assert(false, conditionExpression: "!\"unsupported\"");
                                break;
                            }
                        }
                        if (op3.Oper != GT_LCL_VAR)
                        {
                            var temp = lvaGrabTemp(true, "SveStoreN");
                            impStoreToTemp(temp, op3, CHECK_SPILL_NONE);
                            op3 = gtNewLclVarNode(argType, temp);
                        }
                        op3 = gtConvertTableOpToFieldListArm64(op3, fieldCount);
                    }
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    break;
                }
                case NI_Sve_StoreNarrowing:
                {
                    assert(sig.numArgs == 3);
                    assert(retType == TYP_VOID);
                    var arg = info.compCompHnd->getArgNext(sig.args);
                    var argClass = info.compCompHnd->getArgClass(signature, arg);
                    var tempClass = NO_CLASS_HANDLE;
                    var pointerType = strip(info.compCompHnd->getArgType(signature, arg, &tempClass));
                    assert(pointerType == CORINFO_TYPE_PTR);
                    pointerType = info.compCompHnd->getChildType(argClass, &tempClass);
                    assert(pointerType.PreciseVarType < simdBaseType);
                    op3 = impPopStack().val;
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    node.AuxiliaryType = pointerType.PreciseVarType;
                    retNode = node;
                    break;
                }
                case NI_Sve_SaturatingDecrementBy8BitElementCount:
                case NI_Sve_SaturatingIncrementBy8BitElementCount:
                case NI_Sve_SaturatingDecrementBy16BitElementCountScalar:
                case NI_Sve_SaturatingDecrementBy32BitElementCountScalar:
                case NI_Sve_SaturatingDecrementBy64BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy16BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy32BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy64BitElementCountScalar:
                {
#if DEBUG
                    isValidScalarIntrinsic = true;
#endif
                    goto case NI_Sve_SaturatingDecrementBy16BitElementCount;
                }
                case NI_Sve_SaturatingDecrementBy16BitElementCount:
                case NI_Sve_SaturatingDecrementBy32BitElementCount:
                case NI_Sve_SaturatingDecrementBy64BitElementCount:
                case NI_Sve_SaturatingIncrementBy16BitElementCount:
                case NI_Sve_SaturatingIncrementBy32BitElementCount:
                case NI_Sve_SaturatingIncrementBy64BitElementCount:
                {
                    assert(sig.numArgs == 3);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    op3 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = impPopStack().val;
                    assert(HWIntrinsicInfo.isImmOp(intrinsic, op2));
                    HWIntrinsicInfo.lookupImmBounds(intrinsic, simdSize, simdBaseType, 1, out var lower, out var upper);
                    op2 = addRangeCheckIfNeeded(intrinsic, op2, lower, upper);
                    assert(HWIntrinsicInfo.isImmOp(intrinsic, op3));
                    HWIntrinsicInfo.lookupImmBounds(intrinsic, simdSize, simdBaseType, 2, out lower, out upper);
                    op3 = addRangeCheckIfNeeded(intrinsic, op3, lower, upper);
                    var node = isScalar
                        ? gtNewScalarHWIntrinsicNode(retType, intrinsic, op1, op2, op3)
                        : gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    node.SimdBaseType = simdBaseType;
                    retNode = node;
                    break;
                }
                case NI_Sve_SaturatingDecrementByActiveElementCount:
                case NI_Sve_SaturatingIncrementByActiveElementCount:
                {
                    assert(sig.numArgs == 2);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = impPopStack().val;
                    var op1BaseType = getBaseTypeOfSimdType(argClass);
                    if (!varTypeIsMask(op2.Type))
                    {
                        op2 = gtNewSimdCvtVectorToMaskNode(TYP_MASK, op2, simdBaseType, simdSize);
                    }
                    var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    node.SimdBaseType = simdBaseType;
                    node.AuxiliaryType = op1BaseType;
                    retNode = node;
                    break;
                }
                case NI_Sve_GatherPrefetch8Bit:
                case NI_Sve_GatherPrefetch16Bit:
                case NI_Sve_GatherPrefetch32Bit:
                case NI_Sve_GatherPrefetch64Bit:
                case NI_Sve_Prefetch16Bit:
                case NI_Sve_Prefetch32Bit:
                case NI_Sve_Prefetch64Bit:
                case NI_Sve_Prefetch8Bit:
                {
                    assert((sig.numArgs == 3) || (sig.numArgs == 4));
                    assert(!isScalar);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var argClass = NO_CLASS_HANDLE;
                    HWIntrinsicInfo.lookupImmBounds(intrinsic, simdSize, simdBaseType, 1, out var lower, out var upper);
                    if (sig.numArgs == 3)
                    {
                        var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                        op3 = getArgForHWIntrinsic(argType, argClass);
                        assert(HWIntrinsicInfo.isImmOp(intrinsic, op3));
                        op3 = addRangeCheckIfNeeded(intrinsic, op3, lower, upper);
                        argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                        op2 = getArgForHWIntrinsic(argType, argClass);
                        var op2BaseType = getBaseTypeOfSimdType(argClass);
                        argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                        op1 = impPopStack().val;
#if DEBUG
                        if (intrinsic is NI_Sve_GatherPrefetch8Bit or NI_Sve_GatherPrefetch16Bit or
                            NI_Sve_GatherPrefetch32Bit or NI_Sve_GatherPrefetch64Bit)
                        {
                            assert(varTypeIsSimd(op2.Type));
                        }
                        else
                        {
                            assert(varTypeIsIntegral(op2.Type));
                        }
#endif
                        var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                        node.AuxiliaryType = op2BaseType;
                        retNode = node;
                    }
                    else
                    {
                        var arg4 = info.compCompHnd->getArgNext(arg3);
                        var argType = strip(info.compCompHnd->getArgType(signature, arg4, &argClass)).VarType;
                        op4 = getArgForHWIntrinsic(argType, argClass);
                        assert(HWIntrinsicInfo.isImmOp(intrinsic, op4));
                        op4 = addRangeCheckIfNeeded(intrinsic, op4, lower, upper);
                        argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                        op3 = getArgForHWIntrinsic(argType, argClass);
                        var op3BaseType = getBaseTypeOfSimdType(argClass);
                        argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                        op2 = getArgForHWIntrinsic(argType, argClass);
                        argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                        op1 = impPopStack().val;
                        assert(varTypeIsSimd(op3.Type));
                        var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize,
                            op1, op2, op3, op4);
                        node.AuxiliaryType = op3BaseType;
                        retNode = node;
                    }
                    break;
                }
                case NI_Sve_ConditionalExtractAfterLastActiveElementScalar:
                case NI_Sve_ConditionalExtractLastActiveElementScalar:
                {
                    assert(sig.numArgs == 3);
#if DEBUG
                    isValidScalarIntrinsic = true;
#endif
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    op3 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    var node = gtNewScalarHWIntrinsicNode(retType, intrinsic, op1, op2, op3);
                    node.SimdBaseType = simdBaseType;
                    retNode = node;
                    break;
                }
                case NI_Sve_ExtractAfterLastActiveElementScalar:
                case NI_Sve_ExtractLastActiveElementScalar:
                {
                    assert(sig.numArgs == 2);
#if DEBUG
                    isValidScalarIntrinsic = true;
#endif
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    var node = gtNewScalarHWIntrinsicNode(retType, intrinsic, op1, op2);
                    node.SimdBaseType = simdBaseType;
                    retNode = node;
                    break;
                }
                case NI_Sve_MultiplyAddRotateComplexBySelectedScalar:
                case NI_Sve2_MultiplyAddRotateComplexBySelectedScalar:
                case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar:
                case NI_Sve2_DotProductRotateComplexBySelectedIndex:
                {
                    assert(sig.numArgs == 5);
                    assert(!isScalar);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var arg4 = info.compCompHnd->getArgNext(arg3);
                    var arg5 = info.compCompHnd->getArgNext(arg4);
                    var argClass = NO_CLASS_HANDLE;
                    HWIntrinsicInfo.lookupImmBounds(intrinsic, simdSize, simdBaseType, 1,
                        out var rotationLower, out var rotationUpper);
                    HWIntrinsicInfo.lookupImmBounds(intrinsic, simdSize, simdBaseType, 2,
                        out var indexLower, out var indexUpper);
                    var argType = strip(info.compCompHnd->getArgType(signature, arg5, &argClass)).VarType;
                    var op5 = getArgForHWIntrinsic(argType, argClass);
                    assert(HWIntrinsicInfo.isImmOp(intrinsic, op5));
                    op5 = addRangeCheckIfNeeded(intrinsic, op5, rotationLower, rotationUpper);
                    argType = strip(info.compCompHnd->getArgType(signature, arg4, &argClass)).VarType;
                    op4 = getArgForHWIntrinsic(argType, argClass);
                    assert(HWIntrinsicInfo.isImmOp(intrinsic, op4));
                    op4 = addRangeCheckIfNeeded(intrinsic, op4, indexLower, indexUpper);
                    argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    op3 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    retNode = new GenTreeHWIntrinsic(retType, intrinsic, simdBaseType, simdSize,
                        op1, op2, op3, op4, op5);
                    break;
                }
                case NI_Sve2_VectorTableLookup:
                {
                    assert(sig.numArgs == 2);
                    assert(retType != TYP_VOID);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    _ = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    _ = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    var op1BaseType = getBaseTypeOfSimdType(argClass);
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    if (op1.Type == TYP_STRUCT)
                    {
                        info.compNeedsConsecutiveRegisters = true;
                        var fieldCount = unchecked((uint)info.compCompHnd->getClassNumInstanceFields(argClass));
                        op1 = gtConvertTableOpToFieldListArm64(op1, fieldCount);
                    }
                    var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    node.AuxiliaryType = op1BaseType;
                    retNode = node;
                    break;
                }
                case NI_Sve2_AddWideningEven:
                case NI_Sve2_AddWideningOdd:
                case NI_Sve2_SubtractWideningEven:
                case NI_Sve2_SubtractWideningOdd:
                {
                    assert(sig.numArgs == 2);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    _ = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    _ = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    var op1BaseType = getBaseTypeOfSimdType(argClass);
                    var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    node.SimdBaseType = simdBaseType;
                    node.AuxiliaryType = op1BaseType;
                    retNode = node;
                    break;
                }
                case NI_Sve2_AddSaturate:
                {
                    assert(sig.numArgs == 2);
                    assert(retType != TYP_VOID);
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var argClass = NO_CLASS_HANDLE;
                    _ = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    var op1BaseType = getBaseTypeOfSimdType(argClass);
                    _ = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    var op2BaseType = getBaseTypeOfSimdType(argClass);
                    assert(op1BaseType == simdBaseType);
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    node.SimdBaseType = simdBaseType;
                    node.AuxiliaryType = op2BaseType;
                    retNode = node;
                    break;
                }
                default:
                {
                    return null;
                }
            }
        }
#if DEBUG
        assert(!isScalar || isValidScalarIntrinsic);
#endif

        return retNode;
    }

    private GenTreeFieldList gtConvertTableOpToFieldListArm64(GenTree op, uint fieldCount)
    {
        var lclNum = op.AsLclVar().LclNum;
        ref var local = ref lvaGetDesc(lclNum);
        var fieldSize = unchecked((uint)local.lvExactSize) / fieldCount;
        var fieldType = GetSimdTypeForSize(unchecked((int)fieldSize));

        var fields = new GenTreeFieldList();
        var offset = 0;

        for (uint field = 0; field < fieldCount; field++)
        {
            var node = gtNewLclFldNode(fieldType, lclNum, unchecked((ushort)offset));
            fields.AddField(this, node, unchecked((ushort)offset), fieldType);
            offset = unchecked(offset + (int)fieldSize);
        }

        return fields;
    }

    private unsafe GenTreeFieldList gtConvertParamOpToFieldListArm64(
        GenTree op, uint fieldCount, CORINFO_CLASS_HANDLE clsHnd)
    {
        var lclNum = op.AsLclVar().LclNum;
        ref var local = ref lvaGetDesc(lclNum);
        var fieldSize = unchecked((uint)local.lvExactSize) / fieldCount;

        var fields = new GenTreeFieldList();
        var offset = 0;

        for (uint field = 0; field < fieldCount; field++)
        {
            var handle = info.compCompHnd->getFieldInClass(clsHnd, unchecked((int)field));
            CORINFO_CLASS_HANDLE structType;
            _ = info.compCompHnd->getFieldType(handle, &structType).PreciseVarType;
            _ = getBaseTypeAndSizeOfSimdType(structType, out var sizeBytes);
            var simdType = GetSimdTypeForSize(sizeBytes);

            var node = gtNewLclFldNode(simdType, lclNum, unchecked((ushort)offset));
            fields.AddField(this, node, unchecked((ushort)offset), simdType);
            offset = unchecked(offset + (int)fieldSize);
        }

        return fields;
    }

    private static ulong simdAllBitsSetForElementTypeArm64(var_types baseType)
    {
        switch (baseType.Size)
        {
            case 1:
            {
                return 0xFF;
            }
            case 2:
            {
                return 0xFFFF;
            }
            case 4:
            {
                return 0xFFFFFFFF;
            }
            case 8:
            {
                return ulong.MaxValue;
            }
            default:
            {
                unreached();
                throw new FatalJitException(CORJIT_RECOVERABLEERROR, "Unreachable ARM64 SIMD element size.");
            }
        }
    }
}
#endif
