// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Import a hardware intrinsic as a GT_HWINTRINSIC node if possible.</summary>
    /// <param name="intrinsic">Id of the intrinsic function.</param>
    /// <param name="clsHnd">Class handle containing the intrinsic function.</param>
    /// <param name="method">Method handle of the intrinsic function.</param>
    /// <param name="sig">Signature of the intrinsic call.</param>
    /// <param name="entryPoint">The entry point information required for R2R scenarios.</param>
    /// <param name="mustExpand">True if the intrinsic must expand instead of returning to an ordinary call.</param>
    /// <returns>The imported tree, or null if not a supported intrinsic.</returns>
    public unsafe GenTree? impHWIntrinsic(
        NamedIntrinsic intrinsic,
        CORINFO_CLASS_HANDLE clsHnd,
        CORINFO_METHOD_HANDLE method,
        in CORINFO_SIG_INFO sig,
        in CORINFO_CONST_LOOKUP entryPoint,
        bool mustExpand)
    {
        // NextCallRetAddr requires a CALL.
        if (!mustExpand && info.compHasNextCallRetAddr)
        {
            return null;
        }

        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            NYI("Hardware-intrinsic import without hardware-intrinsic and SIMD support");
            fatal(CORJIT_IMPLLIMITATION);
            throw new FatalJitException("Hardware-intrinsic import without hardware-intrinsic and SIMD support.");
        }

        var category = HWIntrinsicInfo.lookupCategory(intrinsic);
        _ = HWIntrinsicInfo.lookupIsa(intrinsic);
        var numArgs = (int)sig.numArgs;
        var retType = sig.retType.VarType.ActualType;
        var simdBaseType = TYP_UNDEF;
        GenTree? retNode = null;

        if (retType is TYP_STRUCT)
        {
            simdBaseType = getBaseTypeAndSizeOfSimdType(sig.retTypeSigClass, out var sizeBytes);
            if (HWIntrinsicInfo.IsMultiReg(intrinsic))
            {
                assert(sizeBytes == 0);
            }
            else if (Configuration.TARGET_ARM64 &&
                (intrinsic is NI_AdvSimd_LoadAndInsertScalar or NI_AdvSimd_Arm64_LoadAndInsertScalar))
            {
                var retFieldType = impNormStructType(sig.retTypeSigClass, out var retFieldBaseType);
                if (retFieldType is TYP_STRUCT)
                {
                    assert(retFieldBaseType is TYP_UNDEF);
                    var fieldCount = info.compCompHnd->getClassNumInstanceFields(sig.retTypeSigClass);
                    assert(fieldCount > 1);
                    var fieldHandle = info.compCompHnd->getFieldInClass(sig.retTypeClass, 0);
                    CORINFO_CLASS_HANDLE structType;
                    _ = info.compCompHnd->getFieldType(fieldHandle, &structType);
                    simdBaseType = getBaseTypeAndSizeOfSimdType(structType, out var fieldSizeBytes);

                    switch (fieldCount)
                    {
                        case 2:
                        {
                            intrinsic = fieldSizeBytes == 8
                                ? NI_AdvSimd_LoadAndInsertScalarVector64x2
                                : NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2;
                            break;
                        }

                        case 3:
                        {
                            intrinsic = fieldSizeBytes == 8
                                ? NI_AdvSimd_LoadAndInsertScalarVector64x3
                                : NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3;
                            break;
                        }

                        case 4:
                        {
                            intrinsic = fieldSizeBytes == 8
                                ? NI_AdvSimd_LoadAndInsertScalarVector64x4
                                : NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4;
                            break;
                        }

                        default:
                        {
                            assert(false, "unsupported");
                            break;
                        }
                    }
                }
                else
                {
                    assert(retFieldType is TYP_SIMD8 or TYP_SIMD16);
                    assert(isSupportedBaseType(intrinsic, simdBaseType));
                    retType = GetSimdTypeForSize(sizeBytes);
                }
            }
            else
            {
                // A struct result must be a supported SIMD type even when an
                // argument would otherwise supply a supported base type.
                if (!isSupportedBaseType(intrinsic, simdBaseType))
                {
                    return null;
                }

                assert(sizeBytes != 0);
                retType = GetSimdTypeForSize(sizeBytes);
            }
        }

        simdBaseType = getBaseTypeFromArgIfNeeded(intrinsic, sig, simdBaseType);
        byte simdSize = 0;

        if (simdBaseType is TYP_UNDEF)
        {
            if (category is HW_Category_Scalar or HW_Category_Special)
            {
                simdBaseType = sig.retType.PreciseVarType;
                if (simdBaseType is TYP_VOID)
                {
                    simdBaseType = TYP_UNDEF;
                }
            }
            else
            {
                simdBaseType = getBaseTypeAndSizeOfSimdType(clsHnd, out var sizeBytes);
                if (Configuration.TARGET_ARM64 && (simdBaseType is TYP_UNDEF) &&
                    HWIntrinsicInfo.HasScalarInputVariant(intrinsic))
                {
                    assert(sizeBytes == 0);
                    intrinsic = HWIntrinsicInfo.GetScalarInputVariant(intrinsic);
                    category = HWIntrinsicInfo.lookupCategory(intrinsic);
                    _ = HWIntrinsicInfo.lookupIsa(intrinsic);
                    simdBaseType = sig.retType.PreciseVarType;
                    assert(simdBaseType is not TYP_VOID and not TYP_UNDEF and not TYP_STRUCT);
                }
                else
                {
                    assert((category is HW_Category_Special or HW_Category_Helper) || (sizeBytes != 0));
                }
            }
        }
        else if (Configuration.TARGET_ARM64 && (simdBaseType is TYP_STRUCT) &&
            HWIntrinsicInfo.BaseTypeFromValueTupleArg(intrinsic))
        {
            assert(HWIntrinsicInfo.BaseTypeFromFirstArg(intrinsic) || HWIntrinsicInfo.BaseTypeFromSecondArg(intrinsic));
            var arg = sig.args;
            if (HWIntrinsicInfo.BaseTypeFromSecondArg(intrinsic))
            {
                arg = info.compCompHnd->getArgNext(arg);
            }

            CORINFO_CLASS_HANDLE argClass;
            fixed (CORINFO_SIG_INFO* sigPointer = &sig)
            {
                argClass = info.compCompHnd->getArgClass(sigPointer, arg);
            }
            if (Configuration.DEBUG)
            {
                var fieldCount = info.compCompHnd->getClassNumInstanceFields(argClass);
                assert(fieldCount > 1);
            }

            CORINFO_CLASS_HANDLE classHnd;
            var fieldHandle = info.compCompHnd->getFieldInClass(argClass, 0);
            _ = info.compCompHnd->getFieldType(fieldHandle, &classHnd);
            assert(isIntrinsicType(classHnd));
            simdBaseType = getBaseTypeAndSizeOfSimdType(classHnd, out var sizeBytes);
            simdSize = checked((byte)sizeBytes);
            assert(simdSize > 0);
        }

        if ((category is not HW_Category_Special and not HW_Category_Scalar) &&
            !isSupportedBaseType(intrinsic, simdBaseType))
        {
            return null;
        }

        if (Configuration.TARGET_XARCH && (simdBaseType is not TYP_UNDEF) &&
            HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(intrinsic) && varTypeIsSmall(simdBaseType))
        {
            simdBaseType = varTypeIsUnsigned(simdBaseType) ? TYP_UINT : TYP_INT;
        }

        if (simdSize == 0)
        {
            simdSize = checked((byte)HWIntrinsicInfo.lookupSimdSize(this, intrinsic, sig));
        }

        GenTree? immOp1 = null;
        GenTree? immOp2 = null;
        var immLowerBound = 0;
        var immUpperBound = 0;
        var setMethodHandle = false;
        getHWIntrinsicImmOps(intrinsic, sig, ref immOp1, ref immOp2);
        if (Configuration.TARGET_ARM64)
        {
            if (immOp2 is not null)
            {
                var immSimdSize = simdSize;
                var immSimdBaseType = simdBaseType;
                getHWIntrinsicImmTypes(intrinsic, sig, 2, ref immSimdSize, ref immSimdBaseType);
                HWIntrinsicInfo.lookupImmBounds(intrinsic, immSimdSize, immSimdBaseType, 2,
                    out immLowerBound, out immUpperBound);

                if (!CheckHWIntrinsicImmRange(intrinsic, simdBaseType, immOp2, mustExpand,
                    immLowerBound, immUpperBound, false, out var useFallback))
                {
                    if (useFallback)
                    {
                        return impNonConstFallback(intrinsic, retType, simdBaseType);
                    }
                    else if (immOp2.Oper.IsCnsIntOrI)
                    {
                        return impUnsupportedNamedIntrinsic(
                            CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION, method, sig, mustExpand);
                    }
                    else
                    {
                        assert(!mustExpand);
                        if (opts.OptimizationEnabled)
                        {
                            setMethodHandle = true;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }
        else
        {
            assert(immOp2 is null);
        }

        if (immOp1 is not null)
        {
            var hasFullRangeImm = false;
            if (Configuration.TARGET_ARM64)
            {
                var immSimdSize = simdSize;
                var immSimdBaseType = simdBaseType;
                getHWIntrinsicImmTypes(intrinsic, sig, 1, ref immSimdSize, ref immSimdBaseType);
                HWIntrinsicInfo.lookupImmBounds(intrinsic, immSimdSize, immSimdBaseType, 1,
                    out immLowerBound, out immUpperBound);
            }
            else if (Configuration.TARGET_XARCH)
            {
                immUpperBound = HWIntrinsicInfo.lookupImmUpperBound(intrinsic);
                hasFullRangeImm = HWIntrinsicInfo.HasFullRangeImm(intrinsic);
            }
            else if (Configuration.TARGET_WASM)
            {
                immUpperBound = HWIntrinsicInfo.lookupImmUpperBound(intrinsic, simdSize, simdBaseType);
            }

            if (!CheckHWIntrinsicImmRange(intrinsic, simdBaseType, immOp1, mustExpand,
                immLowerBound, immUpperBound, hasFullRangeImm, out var useFallback))
            {
                if (useFallback)
                {
                    return impNonConstFallback(intrinsic, retType, simdBaseType);
                }
                else if (immOp1.Oper.IsCnsIntOrI)
                {
                    return impUnsupportedNamedIntrinsic(
                        CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION, method, sig, mustExpand);
                }
                else
                {
                    assert(!mustExpand);
                    if (opts.OptimizationEnabled)
                    {
                        // Optimized compilation may expose a constant before
                        // rationalization decides whether to restore a call.
                        setMethodHandle = true;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        if (HWIntrinsicInfo.IsFloatingPointUsed(intrinsic))
        {
            compFloatingPointUsed = true;
        }

        var nodeRetType = retType;
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS && Configuration.TARGET_ARM64 &&
            HWIntrinsicInfo.ReturnsPerElementMask(intrinsic))
        {
            nodeRetType = TYP_MASK;
        }

        if (impIsTableDrivenHWIntrinsic(intrinsic, category))
        {
            var isScalar = category is HW_Category_Scalar;
            assert(numArgs >= 0);
            if (!isScalar)
            {
                if (HWIntrinsicInfo.lookupIns(intrinsic, simdBaseType, this) is INS_invalid)
                {
                    assert(false, "Unexpected HW intrinsic");
                    return null;
                }
                if ((Configuration.TARGET_ARM64 && (simdSize is not 8 and not 16 and not SIZE_UNKNOWN)) ||
                    (Configuration.TARGET_XARCH && (simdSize is not 16 and not 32 and not 64)) ||
                    (Configuration.TARGET_WASM && (simdSize is not 16)))
                {
                    assert(false, "Unexpected SIMD size");
                    return null;
                }
            }

            GenTree? op1 = null;
            GenTree? op2 = null;
            GenTree? op3 = null;
            GenTree? op4 = null;
            HWIntrinsicSignatureReader sigReader = default;
            fixed (CORINFO_SIG_INFO* sigPointer = &sig)
            {
                sigReader.Read(info.compCompHnd, sigPointer);
            }

            switch (numArgs)
            {
                case 4:
                {
                    op4 = getArgForHWIntrinsic(sigReader.GetOp4Type(), sigReader.op4ClsHnd);
                    op4 = addRangeCheckIfNeeded(intrinsic, op4, immLowerBound, immUpperBound);
                    op3 = getArgForHWIntrinsic(sigReader.GetOp3Type(), sigReader.op3ClsHnd);
                    op2 = getArgForHWIntrinsic(sigReader.GetOp2Type(), sigReader.op2ClsHnd);
                    op1 = getArgForHWIntrinsic(sigReader.GetOp1Type(), sigReader.op1ClsHnd);
                    break;
                }

                case 3:
                {
                    op3 = getArgForHWIntrinsic(sigReader.GetOp3Type(), sigReader.op3ClsHnd);
                    op2 = getArgForHWIntrinsic(sigReader.GetOp2Type(), sigReader.op2ClsHnd);
                    op1 = getArgForHWIntrinsic(sigReader.GetOp1Type(), sigReader.op1ClsHnd);
                    break;
                }

                case 2:
                {
                    op2 = getArgForHWIntrinsic(sigReader.GetOp2Type(), sigReader.op2ClsHnd);
                    op2 = addRangeCheckIfNeeded(intrinsic, op2, immLowerBound, immUpperBound);
                    op1 = getArgForHWIntrinsic(sigReader.GetOp1Type(), sigReader.op1ClsHnd);
                    break;
                }

                case 1:
                {
                    op1 = getArgForHWIntrinsic(sigReader.GetOp1Type(), sigReader.op1ClsHnd);
                    break;
                }
            }

            switch (numArgs)
            {
                case 0:
                {
                    assert(!isScalar);
                    retNode = gtNewSimdHWIntrinsicNode(nodeRetType, intrinsic, simdBaseType, simdSize);
                    break;
                }

                case 1:
                {
                    assert(op1 is not null);
                    if ((category is HW_Category_MemoryLoad) && (op1.Oper is GT_CAST) &&
                        (op1.AsUnOp().Op1.Type is TYP_BYREF))
                    {
                        op1 = op1.AsUnOp().Op1;
                    }

                    retNode = isScalar
                        ? gtNewScalarHWIntrinsicNode(nodeRetType, intrinsic, op1)
                        : gtNewSimdHWIntrinsicNode(nodeRetType, intrinsic, simdBaseType, simdSize, op1);

                    switch (intrinsic)
                    {
                        case NI_X86Base_ConvertToVector128Int16:
                        case NI_X86Base_ConvertToVector128Int32:
                        case NI_X86Base_ConvertToVector128Int64:
                        case NI_AVX2_BroadcastScalarToVector128:
                        case NI_AVX2_BroadcastScalarToVector256:
                        case NI_AVX2_ConvertToVector256Int16:
                        case NI_AVX2_ConvertToVector256Int32:
                        case NI_AVX2_ConvertToVector256Int64:
                        {
                            if (Configuration.TARGET_XARCH)
                            {
                                // Pointer and vector overloads share these IDs.
                                var auxiliaryType = TYP_UNKNOWN;
                                if (!varTypeIsSimd(op1.Type))
                                {
                                    auxiliaryType = TYP_U_IMPL;
                                    retNode.Flags |= GTF_EXCEPT | GTF_GLOB_REF;
                                }

                                retNode.AsHWIntrinsic().AuxiliaryType = auxiliaryType;
                            }

                            break;
                        }

                        case NI_Sve_ConvertToDouble:
                        case NI_Sve_ConvertToInt32:
                        case NI_Sve_ConvertToInt64:
                        case NI_Sve_ConvertToSingle:
                        case NI_Sve_ConvertToUInt32:
                        case NI_Sve_ConvertToUInt64:
                        {
                            if (Configuration.TARGET_ARM64)
                            {
                                // ConditionalSelect needs the result vector's base type for containment.
                                retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sig.retTypeSigClass);
                            }
                            break;
                        }
                    }
                    break;
                }

                case 2:
                {
                    assert((op1 is not null) && (op2 is not null));
                    retNode = isScalar
                        ? gtNewScalarHWIntrinsicNode(nodeRetType, intrinsic, op1, op2)
                        : gtNewSimdHWIntrinsicNode(nodeRetType, intrinsic, simdBaseType, simdSize, op1, op2);
                    if (Configuration.TARGET_XARCH &&
                        (intrinsic is NI_X86Base_Crc32 or NI_X86Base_X64_Crc32))
                    {
                        retNode.AsHWIntrinsic().SimdBaseType = sigReader.GetOp2TypeAsPrecise();
                    }
                    else if (Configuration.TARGET_ARM64)
                    {
                        switch (intrinsic)
                        {
                            case NI_Crc32_ComputeCrc32:
                            case NI_Crc32_ComputeCrc32C:
                            case NI_Crc32_Arm64_ComputeCrc32:
                            case NI_Crc32_Arm64_ComputeCrc32C:
                            {
                                retNode.AsHWIntrinsic().SimdBaseType = sigReader.GetOp2TypeAsPrecise();
                                break;
                            }

                            case NI_AdvSimd_AddWideningUpper:
                            case NI_AdvSimd_SubtractWideningUpper:
                            {
                                assert(varTypeIsSimd(op1.Type));
                                retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sigReader.op1ClsHnd);
                                break;
                            }

                            case NI_AdvSimd_Arm64_AddSaturateScalar:
                            {
                                assert(varTypeIsSimd(op2.Type));
                                retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sigReader.op2ClsHnd);
                                break;
                            }

                            case NI_ArmBase_Arm64_MultiplyHigh:
                            {
                                if (sig.retType is CORINFO_TYPE_ULONG)
                                {
                                    retNode.AsHWIntrinsic().SimdBaseType = TYP_ULONG;
                                }
                                else
                                {
                                    assert(sig.retType is CORINFO_TYPE_LONG);
                                    retNode.AsHWIntrinsic().SimdBaseType = TYP_LONG;
                                }
                                break;
                            }

                            case NI_Sve_CreateWhileLessThanMaskByte:
                            case NI_Sve_CreateWhileLessThanMaskDouble:
                            case NI_Sve_CreateWhileLessThanMaskInt16:
                            case NI_Sve_CreateWhileLessThanMaskInt32:
                            case NI_Sve_CreateWhileLessThanMaskInt64:
                            case NI_Sve_CreateWhileLessThanMaskSByte:
                            case NI_Sve_CreateWhileLessThanMaskSingle:
                            case NI_Sve_CreateWhileLessThanMaskUInt16:
                            case NI_Sve_CreateWhileLessThanMaskUInt32:
                            case NI_Sve_CreateWhileLessThanMaskUInt64:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskByte:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskDouble:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskInt16:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskInt32:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskInt64:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskSByte:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskSingle:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskUInt16:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskUInt32:
                            case NI_Sve_CreateWhileLessThanOrEqualMaskUInt64:
                            case NI_Sve2_CreateWhileGreaterThanMaskByte:
                            case NI_Sve2_CreateWhileGreaterThanMaskDouble:
                            case NI_Sve2_CreateWhileGreaterThanMaskInt16:
                            case NI_Sve2_CreateWhileGreaterThanMaskInt32:
                            case NI_Sve2_CreateWhileGreaterThanMaskInt64:
                            case NI_Sve2_CreateWhileGreaterThanMaskSByte:
                            case NI_Sve2_CreateWhileGreaterThanMaskSingle:
                            case NI_Sve2_CreateWhileGreaterThanMaskUInt16:
                            case NI_Sve2_CreateWhileGreaterThanMaskUInt32:
                            case NI_Sve2_CreateWhileGreaterThanMaskUInt64:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskByte:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskDouble:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt16:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt32:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt64:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskSByte:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskSingle:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt16:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt32:
                            case NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt64:
                            {
                                retNode.AsHWIntrinsic().AuxiliaryType = sigReader.op1JitType.PreciseVarType;
                                break;
                            }

                            case NI_Sve_ShiftLeftLogical:
                            case NI_Sve_ShiftRightArithmetic:
                            case NI_Sve_ShiftRightLogical:
                            {
                                retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sigReader.op2ClsHnd);
                                break;
                            }
                        }
                    }
                    break;
                }

                case 3:
                {
                    assert((op1 is not null) && (op2 is not null) && (op3 is not null));
                    if (Configuration.TARGET_ARM64 && (intrinsic is NI_AdvSimd_LoadAndInsertScalar))
                    {
                        op2 = addRangeCheckIfNeeded(intrinsic, op2, immLowerBound, immUpperBound);
                        if ((op1.Oper is GT_CAST) && (op1.AsUnOp().Op1.Type is TYP_BYREF))
                        {
                            op1 = op1.AsUnOp().Op1;
                        }
                    }
                    else if (Configuration.TARGET_ARM64 &&
                        (intrinsic is NI_AdvSimd_Insert or NI_AdvSimd_InsertScalar))
                    {
                        op2 = addRangeCheckIfNeeded(intrinsic, op2, immLowerBound, immUpperBound);
                    }
                    else if (Configuration.TARGET_WASM && (intrinsic is NI_PackedSimd_ReplaceScalar))
                    {
                        op2 = addRangeCheckIfNeeded(intrinsic, op2, immLowerBound, immUpperBound);
                    }
                    else
                    {
                        op3 = addRangeCheckIfNeeded(intrinsic, op3, immLowerBound, immUpperBound);
                    }

                    retNode = isScalar
                        ? gtNewScalarHWIntrinsicNode(nodeRetType, intrinsic, op1, op2, op3)
                        : gtNewSimdHWIntrinsicNode(nodeRetType, intrinsic, simdBaseType, simdSize, op1, op2, op3);

                    switch (intrinsic)
                    {
                        case NI_AVX2_GatherVector128:
                        case NI_AVX2_GatherVector256:
                        {
                            if (Configuration.TARGET_XARCH)
                            {
                                assert(varTypeIsSimd(op2.Type));
                                retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sigReader.op2ClsHnd);
                            }
                            break;
                        }

                        case NI_Sve_GatherVector:
                        case NI_Sve_GatherVectorByteZeroExtend:
                        case NI_Sve_GatherVectorByteZeroExtendFirstFaulting:
                        case NI_Sve_GatherVectorFirstFaulting:
                        case NI_Sve_GatherVectorInt16SignExtend:
                        case NI_Sve_GatherVectorInt16SignExtendFirstFaulting:
                        case NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend:
                        case NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting:
                        case NI_Sve_GatherVectorInt32SignExtend:
                        case NI_Sve_GatherVectorInt32SignExtendFirstFaulting:
                        case NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend:
                        case NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting:
                        case NI_Sve_GatherVectorSByteSignExtend:
                        case NI_Sve_GatherVectorSByteSignExtendFirstFaulting:
                        case NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend:
                        case NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting:
                        case NI_Sve_GatherVectorUInt16ZeroExtend:
                        case NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting:
                        case NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend:
                        case NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting:
                        case NI_Sve_GatherVectorUInt32ZeroExtend:
                        case NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting:
                        case NI_Sve_GatherVectorWithByteOffsets:
                        case NI_Sve_GatherVectorWithByteOffsetFirstFaulting:
                        case NI_Sve2_GatherVectorByteZeroExtendNonTemporal:
                        case NI_Sve2_GatherVectorInt16SignExtendNonTemporal:
                        case NI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal:
                        case NI_Sve2_GatherVectorInt32SignExtendNonTemporal:
                        case NI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal:
                        case NI_Sve2_GatherVectorNonTemporal:
                        case NI_Sve2_GatherVectorSByteSignExtendNonTemporal:
                        case NI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal:
                        case NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal:
                        case NI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal:
                        case NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal:
                        case NI_Sve2_GatherVectorWithByteOffsetsNonTemporal:
                        {
                            if (Configuration.TARGET_ARM64)
                            {
                                assert(varTypeIsSimd(op3.Type));
                                if (numArgs == 3)
                                {
                                    retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sigReader.op3ClsHnd);
                                }
                            }
                            break;
                        }
                    }
                    break;
                }

                case 4:
                {
                    assert(!isScalar);
                    assert((op1 is not null) && (op2 is not null) && (op3 is not null) && (op4 is not null));
                    retNode = gtNewSimdHWIntrinsicNode(nodeRetType, intrinsic, simdBaseType, simdSize, op1, op2, op3, op4);
                    if (Configuration.TARGET_ARM64 && (intrinsic is NI_Sve_Scatter))
                    {
                        assert(varTypeIsSimd(op3.Type));
                        if (numArgs == 4)
                        {
                            retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sigReader.op3ClsHnd);
                        }
                    }
                    break;
                }
            }
        }
        else
        {
            retNode = impSpecialIntrinsic(intrinsic, clsHnd, method, sig, entryPoint, simdBaseType,
                nodeRetType, simdSize, mustExpand);
            if (Configuration.FEATURE_MASKED_HW_INTRINSICS && Configuration.TARGET_ARM64 && (retNode is not null))
            {
                nodeRetType = retNode.Type;
            }
        }

        if (setMethodHandle && (retNode is not null))
        {
            var userCall = retNode;
            if (Configuration.TARGET_XARCH && userCall.IsConvertMaskToVector)
            {
                // The fallback call replaces the mask producer, not its wrapper.
                var conversion = userCall.AsHWIntrinsic();
                assert(conversion.Operands.Length == 1);
                userCall = conversion.GetOp(1);
                assert(userCall.Type is TYP_MASK);
            }

            userCall.AsHWIntrinsic().MethodHandle = method;
            if (Configuration.FEATURE_READYTORUN)
            {
                userCall.AsHWIntrinsic().EntryPoint = entryPoint;
            }

            gtUpdateNodeSideEffects(retNode);
        }

        if (Configuration.FEATURE_MASKED_HW_INTRINSICS && Configuration.TARGET_ARM64 &&
            HWIntrinsicInfo.IsExplicitMaskedOperation(intrinsic))
        {
            assert(numArgs > 0);
            assert(retNode is not null);
            switch (intrinsic)
            {
                case NI_Sve_CreateBreakAfterPropagateMask:
                case NI_Sve_CreateBreakBeforePropagateMask:
                {
                    var node = retNode.AsHWIntrinsic();
                    node.SetOp(3, gtNewSimdCvtVectorToMaskNode(TYP_MASK, node.GetOp(3), simdBaseType, simdSize));
                    goto case NI_Sve_CreateBreakAfterMask;
                }

                case NI_Sve_CreateBreakAfterMask:
                case NI_Sve_CreateBreakBeforeMask:
                case NI_Sve_CreateMaskForFirstActiveElement:
                case NI_Sve_CreateMaskForNextActiveElement:
                case NI_Sve_GetActiveElementCount:
                case NI_Sve_TestAnyTrue:
                case NI_Sve_TestFirstTrue:
                case NI_Sve_TestLastTrue:
                {
                    var node = retNode.AsHWIntrinsic();
                    node.SetOp(2, gtNewSimdCvtVectorToMaskNode(TYP_MASK, node.GetOp(2), simdBaseType, simdSize));
                    goto default;
                }

                default:
                {
                    var node = retNode.AsHWIntrinsic();
                    node.SetOp(1, gtNewSimdCvtVectorToMaskNode(TYP_MASK, node.GetOp(1), simdBaseType, simdSize));
                    break;
                }
            }

            if (HWIntrinsicInfo.IsMultiReg(intrinsic))
            {
                assert(HWIntrinsicInfo.IsExplicitMaskedOperation(retNode.AsHWIntrinsic().HWIntrinsicId));
                assert(HWIntrinsicInfo.IsMultiReg(retNode.AsHWIntrinsic().HWIntrinsicId));
                retNode = impStoreMultiRegValueToVar(retNode, sig.retTypeSigClass, CorInfoCallConvExtension.Managed);
            }
        }

        if (Configuration.FEATURE_MASKED_HW_INTRINSICS && Configuration.TARGET_ARM64 &&
            HWIntrinsicInfo.IsEmbeddedMaskedOperation(intrinsic))
        {
            if (intrinsic is NI_Sve_CreateBreakPropagateMask)
            {
                assert(retNode is not null);
                var node = retNode.AsHWIntrinsic();
                node.SetOp(1, gtNewSimdCvtVectorToMaskNode(TYP_MASK, node.GetOp(1), simdBaseType, simdSize));
                node.SetOp(2, gtNewSimdCvtVectorToMaskNode(TYP_MASK, node.GetOp(2), simdBaseType, simdSize));
            }
        }

        if (Configuration.FEATURE_MASKED_HW_INTRINSICS && Configuration.TARGET_ARM64 && (nodeRetType is TYP_MASK))
        {
            assert(retNode is not null);
            retNode = gtNewSimdCvtMaskToVectorNode(retType, retNode, simdBaseType, simdSize);
        }

        if ((retNode is not null) && (retNode.Oper is GT_HWINTRINSIC))
        {
            assert(!retNode.MayThrow(this) || ((retNode.Flags & GTF_EXCEPT) != 0));
            assert(!retNode.RequiresAsgFlag || ((retNode.Flags & GTF_ASG) != 0));
            assert(!retNode.IsImplicitIndir || ((retNode.Flags & GTF_GLOB_REF) != 0));
        }

        return retNode;
    }

    private unsafe var_types getBaseTypeFromArgIfNeeded(
        NamedIntrinsic intrinsic, in CORINFO_SIG_INFO sig, var_types simdBaseType)
    {
        if (HWIntrinsicInfo.BaseTypeFromSecondArg(intrinsic) || HWIntrinsicInfo.BaseTypeFromFirstArg(intrinsic))
        {
            var arg = sig.args;
            if (HWIntrinsicInfo.BaseTypeFromSecondArg(intrinsic))
            {
                arg = info.compCompHnd->getArgNext(arg);
            }

            fixed (CORINFO_SIG_INFO* sigPointer = &sig)
            {
                var argClass = info.compCompHnd->getArgClass(sigPointer, arg);
                simdBaseType = getBaseTypeOfSimdType(argClass);
                if (simdBaseType is TYP_UNDEF)
                {
                    CORINFO_CLASS_HANDLE tmpClass;
                    var baseJitType = strip(info.compCompHnd->getArgType(sigPointer, arg, &tmpClass));
                    if (baseJitType is CORINFO_TYPE_PTR)
                    {
                        baseJitType = info.compCompHnd->getChildType(argClass, &tmpClass);
                    }

                    simdBaseType = baseJitType.PreciseVarType;
                }
            }

            assert(simdBaseType is not TYP_UNDEF);
        }

        return simdBaseType;
    }
}
