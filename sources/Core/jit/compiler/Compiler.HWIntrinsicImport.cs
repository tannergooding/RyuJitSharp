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

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_XARCH
        var category = HWIntrinsicInfo.lookupCategory(intrinsic);
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
                assert((category is HW_Category_Special or HW_Category_Helper) || (sizeBytes != 0));
            }
        }

        if ((category is not HW_Category_Special and not HW_Category_Scalar) &&
            !isSupportedBaseType(intrinsic, simdBaseType))
        {
            return null;
        }

        if ((simdBaseType is not TYP_UNDEF) &&
            HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(intrinsic) && varTypeIsSmall(simdBaseType))
        {
            simdBaseType = varTypeIsUnsigned(simdBaseType) ? TYP_UINT : TYP_INT;
        }

        var simdSize = checked((byte)HWIntrinsicInfo.lookupSimdSize(this, intrinsic, sig));
        GenTree? immOp1 = null;
        GenTree? immOp2 = null;
        var immLowerBound = 0;
        var immUpperBound = 0;
        var setMethodHandle = false;
        getHWIntrinsicImmOps(intrinsic, sig, ref immOp1, ref immOp2);
        assert(immOp2 is null);

        if (immOp1 is not null)
        {
            immUpperBound = HWIntrinsicInfo.lookupImmUpperBound(intrinsic);
            var hasFullRangeImm = HWIntrinsicInfo.HasFullRangeImm(intrinsic);
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
                if (simdSize is not 16 and not 32 and not 64)
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
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize);
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
                        ? gtNewScalarHWIntrinsicNode(retType, intrinsic, op1)
                        : gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1);

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
                            // Pointer and vector overloads share these IDs.
                            var auxiliaryType = TYP_UNKNOWN;
                            if (!varTypeIsSimd(op1.Type))
                            {
                                auxiliaryType = TYP_U_IMPL;
                                retNode.Flags |= GTF_EXCEPT | GTF_GLOB_REF;
                            }

                            retNode.AsHWIntrinsic().AuxiliaryType = auxiliaryType;
                            break;
                        }
                    }
                    break;
                }

                case 2:
                {
                    assert((op1 is not null) && (op2 is not null));
                    retNode = isScalar
                        ? gtNewScalarHWIntrinsicNode(retType, intrinsic, op1, op2)
                        : gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                    if (intrinsic is NI_X86Base_Crc32 or NI_X86Base_X64_Crc32)
                    {
                        retNode.AsHWIntrinsic().SimdBaseType = sigReader.GetOp2TypeAsPrecise();
                    }
                    break;
                }

                case 3:
                {
                    assert((op1 is not null) && (op2 is not null) && (op3 is not null));
                    op3 = addRangeCheckIfNeeded(intrinsic, op3, immLowerBound, immUpperBound);
                    retNode = isScalar
                        ? gtNewScalarHWIntrinsicNode(retType, intrinsic, op1, op2, op3)
                        : gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    if (intrinsic is NI_AVX2_GatherVector128 or NI_AVX2_GatherVector256)
                    {
                        assert(varTypeIsSimd(op2.Type));
                        retNode.AsHWIntrinsic().AuxiliaryType = getBaseTypeOfSimdType(sigReader.op2ClsHnd);
                    }
                    break;
                }

                case 4:
                {
                    assert(!isScalar);
                    assert((op1 is not null) && (op2 is not null) && (op3 is not null) && (op4 is not null));
                    retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3, op4);
                    break;
                }
            }
        }
        else
        {
            retNode = impSpecialIntrinsic(intrinsic, clsHnd, method, sig, entryPoint, simdBaseType,
                retType, simdSize, mustExpand);
        }

        if (setMethodHandle && (retNode is not null))
        {
            var userCall = retNode;
            if (userCall.IsConvertMaskToVector)
            {
                // The fallback call replaces the mask producer, not its wrapper.
                var conversion = userCall.AsHWIntrinsic();
                assert(conversion.Operands.Length == 1);
                userCall = conversion.GetOp(1);
                assert(userCall.Type is TYP_MASK);
            }

            userCall.AsHWIntrinsic().MethodHandle = method;
#if FEATURE_READYTORUN
            userCall.AsHWIntrinsic().EntryPoint = entryPoint;
#endif
            gtUpdateNodeSideEffects(retNode);
        }

        if ((retNode is not null) && (retNode.Oper is GT_HWINTRINSIC))
        {
            assert(!retNode.MayThrow(this) || ((retNode.Flags & GTF_EXCEPT) != 0));
            assert(!retNode.RequiresAsgFlag || ((retNode.Flags & GTF_ASG) != 0));
            assert(!retNode.IsImplicitIndir || ((retNode.Flags & GTF_GLOB_REF) != 0));
        }

        return retNode;
#else
        NYI("Hardware-intrinsic import outside xarch");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("Hardware-intrinsic import outside xarch.");
#endif
    }

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_XARCH
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
#endif
}
