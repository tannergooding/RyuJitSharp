// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS && FEATURE_SIMD
    private unsafe GenTree? impXplatIntrinsic(
        NamedIntrinsic intrinsic,
        CORINFO_CLASS_HANDLE clsHnd,
        CORINFO_METHOD_HANDLE method,
        in CORINFO_SIG_INFO sig,
        in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType,
        var_types retType,
        byte simdSize,
        bool mustExpand)
    {
        assert(HWIntrinsicInfo.lookupIsa(intrinsic) == InstructionSet_Vector);

#if TARGET_XARCH
        if (simdSize == 32)
        {
            var potentiallyNotSupported = true;

            if (HWIntrinsicInfo.AvxOnlyCompatible(intrinsic))
            {
                switch (intrinsic)
                {
                    case NI_Vector_Abs:
                    {
                        potentiallyNotSupported = varTypeIsSigned(simdBaseType);
                        break;
                    }

                    case NI_Vector_IsNegative:
                    case NI_Vector_IsPositive:
                    {
                        potentiallyNotSupported = !varTypeIsUnsigned(simdBaseType);
                        break;
                    }

                    case NI_Vector_ExtractMostSignificantBits:
                    {
                        potentiallyNotSupported = varTypeIsSmall(simdBaseType);
                        break;
                    }

                    case NI_Vector_AddSaturate:
                    case NI_Vector_Dot:
                    case NI_Vector_Equals:
                    case NI_Vector_GreaterThan:
                    case NI_Vector_GreaterThanOrEqual:
                    case NI_Vector_IsZero:
                    case NI_Vector_LessThan:
                    case NI_Vector_LessThanOrEqual:
                    case NI_Vector_MaxNative:
                    case NI_Vector_MinNative:
                    case NI_Vector_MultiplyAddEstimate:
                    case NI_Vector_Narrow:
                    case NI_Vector_NarrowWithSaturation:
                    case NI_Vector_SubtractSaturate:
                    case NI_Vector_Sum:
                    case NI_Vector_WidenLower:
                    case NI_Vector_WidenUpper:
                    case NI_Vector_op_Addition:
                    case NI_Vector_op_Division:
                    case NI_Vector_op_Equality:
                    case NI_Vector_op_Inequality:
                    case NI_Vector_op_Multiply:
                    case NI_Vector_op_Subtraction:
                    case NI_Vector_op_UnaryNegation:
                    {
                        potentiallyNotSupported = varTypeIsIntegral(simdBaseType);
                        break;
                    }

                    case NI_Vector_IsFinite:
                    case NI_Vector_IsInfinity:
                    case NI_Vector_IsInteger:
                    case NI_Vector_IsNegativeInfinity:
                    case NI_Vector_IsPositiveInfinity:
                    case NI_Vector_IsSubnormal:
                    {
                        potentiallyNotSupported = varTypeIsFloating(simdBaseType);
                        break;
                    }

                    case NI_Vector_UnzipEven:
                    case NI_Vector_UnzipOdd:
                    {
                        potentiallyNotSupported = simdBaseType.Size != 4;
                        break;
                    }

                    case NI_Vector_CreateAlternatingSequence:
                    {
                        potentiallyNotSupported = !impStackTop(1).val.Oper.IsConst ||
                            !impStackTop(0).val.Oper.IsConst;
                        break;
                    }

                    case NI_Vector_CreateGeometricSequence:
                    case NI_Vector_CreateSequence:
                    {
                        potentiallyNotSupported = varTypeIsIntegral(simdBaseType) &&
                            (!impStackTop(1).val.Oper.IsConst || !impStackTop(0).val.Oper.IsConst);
                        break;
                    }

                    default:
                    {
                        potentiallyNotSupported = false;
                        break;
                    }
                }
            }

            if (potentiallyNotSupported && !compOpportunisticallyDependsOn(InstructionSet_AVX2))
            {
                return null;
            }
        }
#endif

        if (simdSize != 0)
        {
            assert(varTypeIsArithmetic(simdBaseType));
        }

        GenTree? retNode = null;
        GenTree op1;
        GenTree op2;
        GenTree op3;

        var isMinMaxIntrinsic = false;
        var isMax = false;
        var isMagnitude = false;
        var isNative = false;
        var isNumber = false;

        var isConcatIntrinsic = false;
        var leftUpper = false;
        var rightUpper = false;

        switch (intrinsic)
        {
            case NI_Vector_Abs:
            {
                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();
                retNode = gtNewSimdAbsNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_AddSaturate:
            case NI_Vector_SubtractSaturate:
            {
                assert(sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = impXplatSaturatingOperation(intrinsic, retType, ref op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_AndNot:
            {
                assert(sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                op2 = gtFoldExpr(gtNewSimdUnOpNode(GT_NOT, retType, op2, simdBaseType, simdSize));
                retNode = gtNewSimdBinOpNode(GT_AND, retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_As:
            case NI_Vector_AsByte:
            case NI_Vector_AsDouble:
            case NI_Vector_AsInt16:
            case NI_Vector_AsInt32:
            case NI_Vector_AsInt64:
            case NI_Vector_AsNInt:
            case NI_Vector_AsNUInt:
            case NI_Vector_AsSByte:
            case NI_Vector_AsSingle:
            case NI_Vector_AsUInt16:
            case NI_Vector_AsUInt32:
            case NI_Vector_AsUInt64:
            case NI_Vector_AsVector4:
            {
                assert(sig.numArgs == 1);
                retNode = impSIMDPopStack();
                assert(retNode.Type == GetSimdTypeForSize(GetSimdTypeSizeInBytes(sig.retTypeSigClass)));
                break;
            }

#if TARGET_XARCH
            case NI_Vector_AsVector:
            case NI_Vector_AsVector256:
            case NI_Vector_AsVector512:
            {
                assert(sig.numArgs == 1);
                var vectorTByteLength = getCompileTimeVectorTByteLength();

                if (vectorTByteLength == 0)
                {
                    break;
                }

                if (vectorTByteLength == simdSize)
                {
                    retNode = impSIMDPopStack();
                    assert(retNode.Type == GetSimdTypeForSize(GetSimdTypeSizeInBytes(sig.retTypeSigClass)));
                    break;
                }

                var convertIntrinsic = NI_Illegal;
                var convertSize = 0;

                switch (vectorTByteLength)
                {
                    case 16:
                    {
                        if (intrinsic == NI_Vector_AsVector)
                        {
                            convertIntrinsic = simdSize == 64 ? NI_Vector_GetLower128 : NI_Vector_GetLower;
                            convertSize = simdSize;
                        }
                        else
                        {
                            convertIntrinsic = simdSize == 64 ? NI_Vector_ToVector512 : NI_Vector_ToVector256;
                            convertSize = 16;
                        }
                        break;
                    }

                    case 32:
                    {
                        if (intrinsic == NI_Vector_AsVector)
                        {
                            convertIntrinsic = simdSize == 64 ? NI_Vector_GetLower : NI_Vector_ToVector256;
                            convertSize = simdSize;
                        }
                        else
                        {
                            assert(intrinsic == NI_Vector_AsVector512 && simdSize == 64);
                            convertIntrinsic = NI_Vector_ToVector512;
                            convertSize = 32;
                        }
                        break;
                    }

                    case 64:
                    {
                        if (intrinsic == NI_Vector_AsVector)
                        {
                            convertIntrinsic = NI_Vector_ToVector512;
                            convertSize = simdSize;
                        }
                        else
                        {
                            assert(intrinsic == NI_Vector_AsVector256 && simdSize == 32);
                            convertIntrinsic = NI_Vector_GetLower;
                            convertSize = 64;
                        }
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                assert(convertIntrinsic != NI_Illegal && convertSize != 0);
                op1 = impSIMDPopStack();
                retNode = gtNewSimdHWIntrinsicNode(retType, convertIntrinsic, simdBaseType, (byte)convertSize, op1);
                break;
            }
#endif

            case NI_Vector_AsVector128:
            {
                assert(sig.numArgs == 1 && retType == TYP_SIMD16);

                if (simdSize is 8 or 12)
                {
                    assert(simdBaseType == TYP_FLOAT);
                    op1 = impSIMDPopStack();
                    var firstZero = simdSize / 4;

                    if (op1.Oper.IsCnsVec)
                    {
                        var vecCon = op1.AsVecCon();
                        vecCon.Type = TYP_SIMD16;

                        for (var index = firstZero; index < 4; index++)
                        {
                            vecCon.SimdVal.f32[index] = 0.0f;
                        }
                        return vecCon;
                    }

                    op1 = gtNewSimdHWIntrinsicNode(retType, NI_Vector_AsVector128Unsafe, simdBaseType, simdSize, op1);

                    for (var index = firstZero; index < 4; index++)
                    {
                        op1 = gtNewSimdWithElementNode(retType, op1, gtNewIconNode(TYP_INT, index),
                            gtNewZeroConNode(TYP_FLOAT), simdBaseType, 16);
                    }

                    retNode = op1;
                }
                else if (simdSize == 16)
                {
                    retNode = impSIMDPopStack();
                    assert(retNode.Type == GetSimdTypeForSize(GetSimdTypeSizeInBytes(sig.retTypeSigClass)));
                }
#if TARGET_XARCH
                else if (simdSize is 32 or 64)
                {
                    var convertIntrinsic = simdSize == 32 ? NI_Vector_GetLower : NI_Vector_GetLower128;
                    retNode = gtNewSimdHWIntrinsicNode(retType, convertIntrinsic,
                        simdBaseType, simdSize, impSIMDPopStack());
                }
#endif
                else
                {
                    unreached();
                }
                break;
            }

            case NI_Vector_AsVector128Unsafe:
            {
                assert(sig.numArgs == 1 && retType == TYP_SIMD16);
                assert(simdBaseType == TYP_FLOAT && simdSize is 8 or 12);
                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, impSIMDPopStack());
                break;
            }

            case NI_Vector_AsVector2:
            case NI_Vector_AsVector3:
            {
                assert(simdSize == 16 && simdBaseType == TYP_FLOAT && sig.numArgs == 1);
                assert(retType is TYP_SIMD8 or TYP_SIMD12);
                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, impSIMDPopStack());
                break;
            }

            case NI_Vector_Ceiling:
            case NI_Vector_Floor:
            case NI_Vector_Round:
            case NI_Vector_Truncate:
            {
                if (intrinsic == NI_Vector_Round && sig.numArgs != 1)
                {
                    break;
                }

                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();
                retNode = !varTypeIsFloating(simdBaseType) ? op1 : intrinsic switch {
                    NI_Vector_Ceiling => gtNewSimdCeilNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_Floor => gtNewSimdFloorNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_Round => gtNewSimdRoundNode(retType, op1, simdBaseType, simdSize),
                    _ => gtNewSimdTruncNode(retType, op1, simdBaseType, simdSize),
                };
                break;
            }

            case NI_Vector_ConcatLowerLower:
            case NI_Vector_ConcatLowerUpper:
            case NI_Vector_ConcatUpperLower:
            case NI_Vector_ConcatUpperUpper:
            {
                isConcatIntrinsic = true;
                leftUpper = intrinsic is NI_Vector_ConcatUpperLower or NI_Vector_ConcatUpperUpper;
                rightUpper = intrinsic is NI_Vector_ConcatLowerUpper or NI_Vector_ConcatUpperUpper;
                break;
            }

            case NI_Vector_ConditionalSelect:
            {
                assert(sig.numArgs == 3);
                op3 = impSIMDPopStack();
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdCndSelNode(retType, op1, op2, op3, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_ConvertToDouble:
            {
                assert(sig.numArgs == 1 && varTypeIsLong(simdBaseType));

                if (!compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    break;
                }

                intrinsic = simdSize switch {
                    64 => NI_AVX512_ConvertToVector512Double,
                    32 => NI_AVX512_ConvertToVector256Double,
                    16 => NI_AVX512_ConvertToVector128Double,
                    _ => throw new System.InvalidOperationException(),
                };
                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, impSIMDPopStack());
                break;
            }

            case NI_Vector_ConvertToInt32:
            case NI_Vector_ConvertToInt32Native:
            case NI_Vector_ConvertToInt64:
            case NI_Vector_ConvertToInt64Native:
            case NI_Vector_ConvertToUInt32:
            case NI_Vector_ConvertToUInt32Native:
            case NI_Vector_ConvertToUInt64:
            case NI_Vector_ConvertToUInt64Native:
            {
                assert(sig.numArgs == 1);
                var native = intrinsic is NI_Vector_ConvertToInt32Native or NI_Vector_ConvertToInt64Native or
                    NI_Vector_ConvertToUInt32Native or NI_Vector_ConvertToUInt64Native;
                var destinationType = intrinsic switch {
                    NI_Vector_ConvertToInt32 or NI_Vector_ConvertToInt32Native => TYP_INT,
                    NI_Vector_ConvertToInt64 or NI_Vector_ConvertToInt64Native => TYP_LONG,
                    NI_Vector_ConvertToUInt32 or NI_Vector_ConvertToUInt32Native => TYP_UINT,
                    _ => TYP_ULONG,
                };

                assert(simdBaseType == (destinationType is TYP_INT or TYP_UINT ? TYP_FLOAT : TYP_DOUBLE));

                if (native && BlockNonDeterministicIntrinsics(mustExpand))
                {
                    break;
                }

                if (destinationType != TYP_INT && !compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    break;
                }

                op1 = impSIMDPopStack();
                retNode = native
                    ? gtNewSimdCvtNativeNode(retType, op1, destinationType, simdBaseType, simdSize)
                    : gtNewSimdCvtNode(retType, op1, destinationType, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_ConvertToSingle:
            {
                assert(sig.numArgs == 1 && varTypeIsInt(simdBaseType));

                if (simdBaseType == TYP_INT)
                {
                    intrinsic = simdSize switch {
                        64 => NI_AVX512_ConvertToVector512Single,
                        32 => NI_AVX_ConvertToVector256Single,
                        16 => NI_X86Base_ConvertToVector128Single,
                        _ => throw new System.InvalidOperationException(),
                    };
                }
                else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    intrinsic = simdSize switch {
                        64 => NI_AVX512_ConvertToVector512Single,
                        32 => NI_AVX512_ConvertToVector256Single,
                        16 => NI_AVX512_ConvertToVector128Single,
                        _ => throw new System.InvalidOperationException(),
                    };
                }
                else
                {
                    break;
                }

                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, impSIMDPopStack());
                break;
            }

            case NI_Vector_Create:
            {
                retNode = impSimdCreate(intrinsic, in sig, simdBaseType, retType, simdSize);
                break;
            }

            case NI_Vector_CreateAlternatingSequence:
            case NI_Vector_CreateSequence:
            {
                assert(sig.numArgs == 2);
                impSpillSideEffect(true, stackState.esStackDepth - 2,
                    intrinsic == NI_Vector_CreateSequence
                        ? "Spilling op1 side effects for vector CreateSequence"
                        : "Spilling op1 side effects for vector CreateAlternatingSequence");
                op2 = impPopStack().val;
                op1 = impPopStack().val;
                retNode = intrinsic == NI_Vector_CreateSequence
                    ? gtNewSimdCreateSequenceNode(retType, op1, op2, simdBaseType, simdSize)
                    : gtNewSimdCreateAlternatingSequenceNode(retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_CreateGeometricSequence:
            {
                assert(sig.numArgs == 2);

                if (!impStackTop(0).val.Oper.IsConst)
                {
                    if (opts.OptimizationEnabled)
                    {
                        op2 = impPopStack().val;
                        op1 = impPopStack().val;
                        retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                        retNode.AsHWIntrinsic().MethodHandle = method;
#if FEATURE_READYTORUN
                        retNode.AsHWIntrinsic().EntryPoint = entryPoint;
#endif
                    }
                    break;
                }

                impSpillSideEffect(true, stackState.esStackDepth - 2,
                    "Spilling op1 side effects for vector CreateGeometricSequence");
                op2 = impPopStack().val;
                op1 = impPopStack().val;
                retNode = gtNewSimdCreateGeometricSequenceNode(retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_CreateScalar:
            case NI_Vector_CreateScalarUnsafe:
            {
                assert(sig.numArgs == 1);
                op1 = impPopStack().val;
                retNode = intrinsic == NI_Vector_CreateScalar
                    ? gtNewSimdCreateScalarNode(retType, op1, simdBaseType, simdSize)
                    : gtNewSimdCreateScalarUnsafeNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_Dot:
            {
                assert(sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                var simdType = GetSimdTypeForSize(simdSize);

                if (simdSize == 64 || varTypeIsByte(simdBaseType) || varTypeIsLong(simdBaseType))
                {
                    var product = gtNewSimdBinOpNode(GT_MUL, simdType, op1, op2, simdBaseType, simdSize);
                    retNode = gtNewSimdSumNode(retType, product, simdBaseType, simdSize);
                }
                else
                {
                    var dot = gtNewSimdDotProdNode(simdType, op1, op2, simdBaseType, simdSize);
                    retNode = gtNewSimdToScalarNode(retType, dot, simdBaseType, simdSize);
                }
                break;
            }

            case NI_Vector_Equals:
            case NI_Vector_GreaterThan:
            case NI_Vector_GreaterThanOrEqual:
            case NI_Vector_LessThan:
            case NI_Vector_LessThanOrEqual:
            case NI_Vector_EqualsAny:
            case NI_Vector_GreaterThanAll:
            case NI_Vector_GreaterThanAny:
            case NI_Vector_GreaterThanOrEqualAll:
            case NI_Vector_GreaterThanOrEqualAny:
            case NI_Vector_LessThanAll:
            case NI_Vector_LessThanAny:
            case NI_Vector_LessThanOrEqualAll:
            case NI_Vector_LessThanOrEqualAny:
            case NI_Vector_op_Equality:
            case NI_Vector_op_Inequality:
            {
                assert(sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();

                var comparison = intrinsic switch {
                    NI_Vector_Equals or NI_Vector_EqualsAny or NI_Vector_op_Equality => GT_EQ,
                    NI_Vector_GreaterThan or NI_Vector_GreaterThanAll or NI_Vector_GreaterThanAny => GT_GT,
                    NI_Vector_GreaterThanOrEqual or NI_Vector_GreaterThanOrEqualAll or
                        NI_Vector_GreaterThanOrEqualAny => GT_GE,
                    NI_Vector_LessThan or NI_Vector_LessThanAll or NI_Vector_LessThanAny => GT_LT,
                    NI_Vector_LessThanOrEqual or NI_Vector_LessThanOrEqualAll or
                        NI_Vector_LessThanOrEqualAny => GT_LE,
                    _ => GT_NE,
                };

                retNode = intrinsic switch {
                    NI_Vector_EqualsAny or NI_Vector_GreaterThanAny or NI_Vector_GreaterThanOrEqualAny or
                        NI_Vector_LessThanAny or NI_Vector_LessThanOrEqualAny or NI_Vector_op_Inequality =>
                        gtNewSimdCmpOpAnyNode(comparison, retType, op1, op2, simdBaseType, simdSize),
                    NI_Vector_GreaterThanAll or NI_Vector_GreaterThanOrEqualAll or NI_Vector_LessThanAll or
                        NI_Vector_LessThanOrEqualAll or NI_Vector_op_Equality =>
                        gtNewSimdCmpOpAllNode(comparison, retType, op1, op2, simdBaseType, simdSize),
                    _ => gtNewSimdCmpOpNode(comparison, retType, op1, op2, simdBaseType, simdSize),
                };
                break;
            }

            case NI_Vector_ExtractMostSignificantBits:
            {
                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();

                if (simdSize == 64 || canUseEvexEncoding())
                {
                    op1 = gtFoldExpr(gtNewSimdCvtVectorToMaskNode(TYP_MASK, op1, simdBaseType, simdSize));
                    retNode = gtNewSimdHWIntrinsicNode(retType, NI_AVX512_MoveMask,
                        simdBaseType, simdSize, op1);
                    break;
                }

                switch (simdBaseType)
                {
                    case TYP_BYTE:
                    case TYP_UBYTE:
                    {
                        intrinsic = simdSize == 32 ? NI_AVX2_MoveMask : NI_X86Base_MoveMask;
                        break;
                    }

                    case TYP_SHORT:
                    case TYP_USHORT:
                    {
                        break;
                    }

                    case TYP_INT:
                    case TYP_UINT:
                    case TYP_FLOAT:
                    {
                        simdBaseType = TYP_FLOAT;
                        intrinsic = simdSize == 32 ? NI_AVX_MoveMask : NI_X86Base_MoveMask;
                        break;
                    }

                    case TYP_LONG:
                    case TYP_ULONG:
                    case TYP_DOUBLE:
                    {
                        simdBaseType = TYP_DOUBLE;
                        intrinsic = simdSize == 32 ? NI_AVX_MoveMask : NI_X86Base_MoveMask;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1);
                break;
            }

            case NI_Vector_FusedMultiplyAdd:
            {
                assert(sig.numArgs == 3 && varTypeIsFloating(simdBaseType));

                if (!compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    break;
                }

                op3 = impSIMDPopStack();
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdFmaNode(retType, op1, op2, op3, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_GetElement:
            {
                assert(sig.numArgs == 2);
                op2 = impPopStack().val;
                op1 = impSIMDPopStack();
                retNode = gtNewSimdGetElementNode(retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_GetLower:
            case NI_Vector_GetUpper:
            {
                assert(sig.numArgs == 1);

                if (simdSize == 8)
                {
                    break;
                }

                op1 = impSIMDPopStack();
                retNode = intrinsic == NI_Vector_GetLower
                    ? gtNewSimdGetLowerNode(retType, op1, simdBaseType, simdSize)
                    : gtNewSimdGetUpperNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_IsEvenInteger:
            case NI_Vector_IsOddInteger:
            {
                assert(sig.numArgs == 1);

                if (varTypeIsFloating(simdBaseType))
                {
                    break;
                }

                op1 = impSIMDPopStack();
                retNode = intrinsic == NI_Vector_IsEvenInteger
                    ? gtNewSimdIsEvenIntegerNode(retType, op1, simdBaseType, simdSize)
                    : gtNewSimdIsOddIntegerNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_IsFinite:
            case NI_Vector_IsInfinity:
            case NI_Vector_IsInteger:
            case NI_Vector_IsNaN:
            case NI_Vector_IsNegative:
            case NI_Vector_IsNegativeInfinity:
            case NI_Vector_IsNormal:
            case NI_Vector_IsPositive:
            case NI_Vector_IsPositiveInfinity:
            case NI_Vector_IsSubnormal:
            case NI_Vector_IsZero:
            {
                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();
                retNode = intrinsic switch {
                    NI_Vector_IsFinite => gtNewSimdIsFiniteNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsInfinity => gtNewSimdIsInfinityNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsInteger => gtNewSimdIsIntegerNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsNaN => gtNewSimdIsNaNNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsNegative => gtNewSimdIsNegativeNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsNegativeInfinity => gtNewSimdIsNegativeInfinityNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsNormal => gtNewSimdIsNormalNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsPositive => gtNewSimdIsPositiveNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsPositiveInfinity => gtNewSimdIsPositiveInfinityNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_IsSubnormal => gtNewSimdIsSubnormalNode(retType, op1, simdBaseType, simdSize),
                    _ => gtNewSimdIsZeroNode(retType, op1, simdBaseType, simdSize),
                };
                break;
            }

            case NI_Vector_LoadAligned:
            case NI_Vector_LoadAlignedNonTemporal:
            case NI_Vector_LoadUnsafe:
            {
                GenTree? offset = null;
                if (intrinsic == NI_Vector_LoadUnsafe)
                {
                    if (sig.numArgs == 2)
                    {
                        offset = impPopStack().val;
                    }
                    else
                    {
                        assert(sig.numArgs == 1);
                    }
                }
                else
                {
                    assert(sig.numArgs == 1);
                }

                op1 = impPopStack().val;
                if (op1.Oper == GT_CAST && op1.AsOp().Op1.Type == TYP_BYREF)
                {
                    op1 = op1.AsOp().Op1;
                }

                if (offset is not null)
                {
                    op3 = gtNewIconNode(offset.Type, simdBaseType.Size);
                    op2 = gtNewBinaryNode(GT_MUL, offset.Type, offset, op3);
                    op1 = gtNewBinaryNode(GT_ADD, op1.Type, op1, op2);
                }

                retNode = intrinsic switch {
                    NI_Vector_LoadAligned => gtNewSimdLoadAlignedNode(retType, op1, simdBaseType, simdSize),
                    NI_Vector_LoadAlignedNonTemporal => gtNewSimdLoadNonTemporalNode(retType, op1, simdBaseType, simdSize),
                    _ => gtNewSimdLoadNode(retType, op1, simdBaseType, simdSize),
                };
                break;
            }

            case NI_Vector_Max:
            case NI_Vector_MaxMagnitude:
            case NI_Vector_MaxMagnitudeNumber:
            case NI_Vector_MaxNative:
            case NI_Vector_MaxNumber:
            case NI_Vector_Min:
            case NI_Vector_MinMagnitude:
            case NI_Vector_MinMagnitudeNumber:
            case NI_Vector_MinNative:
            case NI_Vector_MinNumber:
            {
                isMinMaxIntrinsic = true;
                isMax = intrinsic is NI_Vector_Max or NI_Vector_MaxMagnitude or NI_Vector_MaxMagnitudeNumber or
                    NI_Vector_MaxNative or NI_Vector_MaxNumber;
                isMagnitude = intrinsic is NI_Vector_MaxMagnitude or NI_Vector_MaxMagnitudeNumber or
                    NI_Vector_MinMagnitude or NI_Vector_MinMagnitudeNumber;
                isNumber = intrinsic is NI_Vector_MaxMagnitudeNumber or NI_Vector_MaxNumber or
                    NI_Vector_MinMagnitudeNumber or NI_Vector_MinNumber;
                isNative = intrinsic is NI_Vector_MaxNative or NI_Vector_MinNative;
                break;
            }

            case NI_Vector_MultiplyAddEstimate:
            {
                assert(sig.numArgs == 3);

                if (BlockNonDeterministicIntrinsics(mustExpand))
                {
                    break;
                }

                op3 = impSIMDPopStack();
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();

                if (varTypeIsFloating(simdBaseType) && compExactlyDependsOn(InstructionSet_AVX2))
                {
                    retNode = gtNewSimdFmaNode(retType, op1, op2, op3, simdBaseType, simdSize);
                }
                else
                {
                    var product = gtNewSimdBinOpNode(GT_MUL, retType, op1, op2, simdBaseType, simdSize);
                    retNode = gtNewSimdBinOpNode(GT_ADD, retType, product, op3, simdBaseType, simdSize);
                }
                break;
            }

            case NI_Vector_Narrow:
            {
                assert(sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdNarrowNode(retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_NarrowWithSaturation:
            {
                assert(sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();

                if (simdBaseType == TYP_DOUBLE)
                {
                    retNode = gtNewSimdNarrowNode(retType, op1, op2, TYP_FLOAT, simdSize);
                }
                else if (simdSize == 16 && simdBaseType is TYP_SHORT or TYP_INT)
                {
                    var resultBaseType = simdBaseType == TYP_SHORT ? TYP_BYTE : TYP_SHORT;
                    retNode = gtNewSimdHWIntrinsicNode(retType,
                        NI_X86Base_PackSignedSaturate, resultBaseType, simdSize, op1, op2);
                }
                else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    var widenedSize = simdSize == 16 ? (byte)32 : (byte)64;
                    var widenedType = GetSimdTypeForSize(widenedSize);
                    var unsafeIntrinsic = simdSize == 16 ? NI_Vector_ToVector256Unsafe : NI_Vector_ToVector512Unsafe;

                    if (simdSize != 64)
                    {
                        op1 = gtNewSimdHWIntrinsicNode(widenedType, unsafeIntrinsic, simdBaseType, simdSize, op1);
                        op1 = gtNewSimdWithUpperNode(widenedType, op1, op2, simdBaseType, widenedSize);
                    }

                    intrinsic = (simdSize, simdBaseType) switch {
                        (16, TYP_USHORT) => NI_AVX512_ConvertToVector128ByteWithSaturation,
                        (16, TYP_UINT) => NI_AVX512_ConvertToVector128UInt16WithSaturation,
                        (16, TYP_LONG) => NI_AVX512_ConvertToVector128Int32WithSaturation,
                        (16, TYP_ULONG) => NI_AVX512_ConvertToVector128UInt32WithSaturation,
                        (_, TYP_SHORT) => NI_AVX512_ConvertToVector256SByteWithSaturation,
                        (_, TYP_USHORT) => NI_AVX512_ConvertToVector256ByteWithSaturation,
                        (_, TYP_INT) => NI_AVX512_ConvertToVector256Int16WithSaturation,
                        (_, TYP_UINT) => NI_AVX512_ConvertToVector256UInt16WithSaturation,
                        (_, TYP_LONG) => NI_AVX512_ConvertToVector256Int32WithSaturation,
                        (_, TYP_ULONG) => NI_AVX512_ConvertToVector256UInt32WithSaturation,
                        _ => throw new System.InvalidOperationException(),
                    };

                    if (simdSize == 64)
                    {
                        op1 = gtNewSimdHWIntrinsicNode(TYP_SIMD32, intrinsic, simdBaseType, simdSize, op1);
                        op2 = gtNewSimdHWIntrinsicNode(TYP_SIMD32, intrinsic, simdBaseType, simdSize, op2);
                        retNode = gtNewSimdWithUpperNode(retType, op1, op2, simdBaseType, simdSize);
                    }
                    else
                    {
                        retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, widenedSize, op1);
                    }
                }
                else
                {
                    retNode = gtNewSimdNarrowWithSaturationNode(retType, op1, op2, simdBaseType, simdSize);
                }
                break;
            }

            case NI_Vector_Reverse:
            {
                assert(sig.numArgs == 1);

                if (simdSize == 64 && varTypeIsByte(simdBaseType) &&
                    !compOpportunisticallyDependsOn(InstructionSet_AVX512v2))
                {
                    break;
                }

                retNode = gtNewSimdReverseNode(retType, impSIMDPopStack(), simdBaseType, simdSize);
                break;
            }

            case NI_Vector_ShiftLeft:
            {
                assert(sig.numArgs == 2);
                if (!varTypeIsSimd(impStackTop(0).val.Type))
                {
                    break;
                }

                if (simdSize == 16 && !compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    break;
                }

                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                intrinsic = simdSize == 64 ? NI_AVX512_ShiftLeftLogicalVariable : NI_AVX2_ShiftLeftLogicalVariable;
                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                break;
            }

            case NI_Vector_Shuffle:
            case NI_Vector_ShuffleNative:
            case NI_Vector_ShuffleNativeFallback:
            {
                assert(sig.numArgs is 2 or 3);
                var shuffleNative = intrinsic != NI_Vector_Shuffle;

                if (shuffleNative && BlockNonDeterministicIntrinsics(mustExpand))
                {
                    break;
                }

                var indices = impStackTop(0).val;
                var validForShuffle = IsValidForShuffle(indices, simdSize, simdBaseType,
                    out var canBecomeValidForShuffle, shuffleNative);

                if (!canBecomeValidForShuffle)
                {
                    return null;
                }

                if (!validForShuffle || !indices.Oper.IsCnsVec)
                {
                    assert(sig.numArgs == 2);

                    if (opts.OptimizationEnabled)
                    {
                        op2 = impSIMDPopStack();
                        op1 = impSIMDPopStack();
                        retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
                        retNode.AsHWIntrinsic().MethodHandle = method;
#if FEATURE_READYTORUN
                        retNode.AsHWIntrinsic().EntryPoint = entryPoint;
#endif
                        break;
                    }

                    if (!validForShuffle)
                    {
                        return null;
                    }
                }

                if (sig.numArgs == 2)
                {
                    op2 = impSIMDPopStack();
                    op1 = impSIMDPopStack();
                    retNode = gtNewSimdShuffleNode(retType, op1, op2, simdBaseType, simdSize, shuffleNative);
                }
                break;
            }

            case NI_Vector_Sqrt:
            {
                assert(sig.numArgs == 1);

                if (varTypeIsFloating(simdBaseType))
                {
                    retNode = gtNewSimdSqrtNode(retType, impSIMDPopStack(), simdBaseType, simdSize);
                }
                break;
            }

            case NI_Vector_StoreAligned:
            case NI_Vector_StoreAlignedNonTemporal:
            case NI_Vector_StoreUnsafe:
            {
                assert(retType == TYP_VOID);
                GenTree? offset = null;

                if (intrinsic == NI_Vector_StoreUnsafe && sig.numArgs == 3)
                {
                    impSpillSideEffect(true, stackState.esStackDepth - 3,
                        "Spilling op1 side effects for HWIntrinsic");
                    offset = impPopStack().val;
                }
                else
                {
                    assert(sig.numArgs == 2);
                    impSpillSideEffect(true, stackState.esStackDepth - 2,
                        "Spilling op1 side effects for HWIntrinsic");
                }

                op2 = impPopStack().val;

                if (op2.Oper == GT_CAST && op2.AsOp().Op1.Type == TYP_BYREF)
                {
                    op2 = op2.AsOp().Op1;
                }

                if (offset is not null)
                {
                    var multiplier = gtNewIconNode(offset.Type, simdBaseType.Size);
                    offset = gtNewBinaryNode(GT_MUL, offset.Type, offset, multiplier);
                    op2 = gtNewBinaryNode(GT_ADD, op2.Type, op2, offset);
                }

                op1 = impSIMDPopStack();
                retNode = intrinsic switch {
                    NI_Vector_StoreAligned => gtNewSimdStoreAlignedNode(op2, op1, simdBaseType, simdSize),
                    NI_Vector_StoreAlignedNonTemporal => gtNewSimdStoreNonTemporalNode(op2, op1, simdBaseType, simdSize),
                    _ => gtNewSimdStoreNode(op2, op1, simdBaseType, simdSize),
                };
                break;
            }

            case NI_Vector_Sum:
            case NI_Vector_ToScalar:
            {
                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();
                retNode = intrinsic == NI_Vector_Sum
                    ? gtNewSimdSumNode(retType, op1, simdBaseType, simdSize)
                    : gtNewSimdToScalarNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_UnzipEven:
            case NI_Vector_UnzipOdd:
            {
                assert(sig.numArgs == 2);
                if (simdSize == 16 && simdBaseType.Size != 4 &&
                    !compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    break;
                }

                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdUnzipNode(retType, op1, op2, simdBaseType, simdSize,
                    intrinsic == NI_Vector_UnzipOdd);
                break;
            }

            case NI_Vector_WidenLower:
            case NI_Vector_WidenUpper:
            {
                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();
                retNode = intrinsic == NI_Vector_WidenLower
                    ? gtNewSimdWidenLowerNode(retType, op1, simdBaseType, simdSize)
                    : gtNewSimdWidenUpperNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_WithElement:
            {
                assert(sig.numArgs == 3);
                op3 = impPopStack().val;
                op2 = impPopStack().val;
                op1 = impSIMDPopStack();
                retNode = gtNewSimdWithElementNode(retType, op1, op2, op3, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_WithLower:
            case NI_Vector_WithUpper:
            {
                assert(sig.numArgs == 2);
                if (simdSize == 16)
                {
                    break;
                }

                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = intrinsic == NI_Vector_WithLower
                    ? gtNewSimdWithLowerNode(retType, op1, op2, simdBaseType, simdSize)
                    : gtNewSimdWithUpperNode(retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_ZipLower:
            case NI_Vector_ZipUpper:
            {
                assert(sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdZipNode(retType, op1, op2, simdBaseType, simdSize,
                    intrinsic == NI_Vector_ZipUpper);
                break;
            }

            case NI_Vector_get_AllBitsSet:
            case NI_Vector_get_One:
            case NI_Vector_get_Zero:
            {
                assert(sig.numArgs == 0);
                retNode = intrinsic switch {
                    NI_Vector_get_AllBitsSet => gtNewAllBitsSetConNode(retType),
                    NI_Vector_get_One => gtNewOneConNode(retType, simdBaseType),
                    _ => gtNewZeroConNode(retType),
                };
                break;
            }

            case NI_Vector_get_Indices:
            {
                assert(sig.numArgs == 0);
                retNode = gtNewSimdGetIndicesNode(retType, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_get_E:
            case NI_Vector_get_Epsilon:
            case NI_Vector_get_NaN:
            case NI_Vector_get_NegativeInfinity:
            case NI_Vector_get_NegativeOne:
            case NI_Vector_get_NegativeZero:
            case NI_Vector_get_Pi:
            case NI_Vector_get_PositiveInfinity:
            case NI_Vector_get_SignSequence:
            case NI_Vector_get_Tau:
            {
                assert(sig.numArgs == 0);
                retNode = impXplatVectorConstant(intrinsic, retType, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_op_Addition:
            case NI_Vector_op_BitwiseAnd:
            case NI_Vector_op_BitwiseOr:
            case NI_Vector_op_ExclusiveOr:
            case NI_Vector_op_Subtraction:
            case NI_Vector_op_LeftShift:
            case NI_Vector_op_RightShift:
            case NI_Vector_op_UnsignedRightShift:
            {
                assert(sig.numArgs == 2);
                op2 = intrinsic is NI_Vector_op_LeftShift or NI_Vector_op_RightShift or
                    NI_Vector_op_UnsignedRightShift ? impPopStack().val : impSIMDPopStack();
                op1 = impSIMDPopStack();

                var oper = intrinsic switch {
                    NI_Vector_op_Addition => GT_ADD,
                    NI_Vector_op_BitwiseAnd => GT_AND,
                    NI_Vector_op_BitwiseOr => GT_OR,
                    NI_Vector_op_ExclusiveOr => GT_XOR,
                    NI_Vector_op_Subtraction => GT_SUB,
                    NI_Vector_op_LeftShift => GT_LSH,
                    NI_Vector_op_RightShift => varTypeIsUnsigned(simdBaseType) ? GT_RSZ : GT_RSH,
                    _ => GT_RSZ,
                };
                retNode = gtNewSimdBinOpNode(oper, retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_op_Division:
            case NI_Vector_op_Multiply:
            {
                assert(sig.numArgs == 2);
                if (intrinsic == NI_Vector_op_Division && varTypeIsIntegral(simdBaseType))
                {
                    if (varTypeIsLong(simdBaseType))
                    {
                        break;
                    }

                    impSpillSideEffect(true, stackState.esStackDepth - 2,
                        "Spilling op1 side effects for vector integer division");
                }

                var arg1 = sig.args;
                var arg2 = info.compCompHnd->getArgNext(arg1);
                CORINFO_CLASS_HANDLE argClass = default;
                var mutableSig = sig;
                var argType = strip(info.compCompHnd->getArgType(&mutableSig, arg2, &argClass)).VarType;
                op2 = getArgForHWIntrinsic(argType, argClass);
                argType = strip(info.compCompHnd->getArgType(&mutableSig, arg1, &argClass)).VarType;
                op1 = getArgForHWIntrinsic(argType, argClass);
                retNode = gtNewSimdBinOpNode(intrinsic == NI_Vector_op_Division ? GT_DIV : GT_MUL,
                    retType, op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_Vector_op_OnesComplement:
            case NI_Vector_op_UnaryNegation:
            case NI_Vector_op_UnaryPlus:
            {
                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();
                retNode = intrinsic switch {
                    NI_Vector_op_OnesComplement => gtNewSimdUnOpNode(GT_NOT, retType, op1, simdBaseType, simdSize),
                    NI_Vector_op_UnaryNegation => gtNewSimdUnOpNode(GT_NEG, retType, op1, simdBaseType, simdSize),
                    _ => op1,
                };
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        if (isMinMaxIntrinsic)
        {
            assert(sig.numArgs == 2 && retNode is null);
            if (isNative && BlockNonDeterministicIntrinsics(mustExpand))
            {
                return null;
            }

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();
            retNode = isNative
                ? gtNewSimdMinMaxNativeNode(retType, op1, op2, simdBaseType, simdSize, isMax)
                : gtNewSimdMinMaxNode(retType, op1, op2, simdBaseType, simdSize, isMax, isMagnitude, isNumber);
        }
        else if (isConcatIntrinsic)
        {
            assert(sig.numArgs == 2 && retNode is null);
            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();
            retNode = gtNewSimdConcatNode(retType, op1, op2, simdBaseType, simdSize, leftUpper, rightUpper);
        }
#if TARGET_XARCH
        else if (retType == TYP_MASK)
        {
            var vectorType = GetSimdTypeForSize(simdSize);
            assert(vectorType == GetSimdTypeForSize(GetSimdTypeSizeInBytes(sig.retTypeSigClass)));
            assert(retNode is not null);
            retNode = gtNewSimdCvtMaskToVectorNode(vectorType, gtFoldExpr(retNode), simdBaseType, simdSize);
        }
#endif

        return retNode;
    }

    private GenTree impXplatSaturatingOperation(
        NamedIntrinsic intrinsic, var_types retType, ref GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        var addition = intrinsic == NI_Vector_AddSaturate;
        var oper = addition ? GT_ADD : GT_SUB;

        if (varTypeIsFloating(simdBaseType))
        {
            return gtNewSimdBinOpNode(oper, retType, op1, op2, simdBaseType, simdSize);
        }

        if (varTypeIsSmall(simdBaseType))
        {
            var saturatingIntrinsic = simdSize switch {
                64 => addition ? NI_AVX512_AddSaturate : NI_AVX512_SubtractSaturate,
                32 => addition ? NI_AVX2_AddSaturate : NI_AVX2_SubtractSaturate,
                16 => addition ? NI_X86Base_AddSaturate : NI_X86Base_SubtractSaturate,
                _ => throw new System.InvalidOperationException(),
            };
            return gtNewSimdHWIntrinsicNode(retType, saturatingIntrinsic,
                simdBaseType, simdSize, op1, op2);
        }

        if (varTypeIsUnsigned(simdBaseType))
        {
            var constant = addition ? gtNewAllBitsSetConNode(retType) : gtNewZeroConNode(retType);
            var first = fgMakeMultiUse(ref op1);
            var result = gtNewSimdBinOpNode(oper, retType, op1, op2, simdBaseType, simdSize);
            var preserved = fgMakeMultiUse(ref result);
            var overflow = gtNewSimdCmpOpNode(addition ? GT_LT : GT_GT,
                retType, result, first, simdBaseType, simdSize);
            return gtNewSimdCndSelNode(retType, overflow, constant, preserved, simdBaseType, simdSize);
        }

        var minimum = gtNewVconNode(retType);
        var maximum = gtNewVconNode(retType);
        switch (simdBaseType)
        {
            case TYP_INT:
            {
                minimum.EvaluateBroadcastInPlace(TYP_INT, int.MinValue);
                maximum.EvaluateBroadcastInPlace(TYP_INT, int.MaxValue);
                break;
            }

            case TYP_LONG:
            {
                minimum.EvaluateBroadcastInPlace(TYP_LONG, long.MinValue);
                maximum.EvaluateBroadcastInPlace(TYP_LONG, long.MaxValue);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        var firstDuplicate = fgMakeMultiUse(ref op1);
        var secondDuplicate = fgMakeMultiUse(ref op2);
        var sum = gtNewSimdBinOpNode(oper, retType, op1, op2, simdBaseType, simdSize);
        var sumDuplicate = fgMakeMultiUse(ref sum);
        var sumDuplicate2 = gtCloneExpr(sumDuplicate);
        var sign = gtNewSimdIsNegativeNode(retType, sumDuplicate, simdBaseType, simdSize);
        var saturation = gtNewSimdCndSelNode(retType, sign, maximum, minimum, simdBaseType, simdSize);

        GenTree mask;
        if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            // Inputs are (result, left, right); 0x18 detects add overflow and 0x24 detects subtract overflow.
            mask = gtNewSimdTernaryLogicNode(retType, sum, firstDuplicate, secondDuplicate,
                gtNewIconNode(TYP_INT, addition ? 0x18 : 0x24), simdBaseType, simdSize);
        }
        else
        {
            var firstDuplicate2 = gtCloneExpr(firstDuplicate);
            var difference = gtNewSimdBinOpNode(GT_XOR, retType, sum, firstDuplicate,
                simdBaseType, simdSize);
            var operands = gtNewSimdBinOpNode(GT_XOR, retType, firstDuplicate2, secondDuplicate,
                simdBaseType, simdSize);
            mask = gtNewSimdBinOpNode(addition ? GT_AND_NOT : GT_AND,
                retType, difference, operands, simdBaseType, simdSize);
        }

        mask = gtNewSimdIsNegativeNode(retType, mask, simdBaseType, simdSize);
        return gtNewSimdCndSelNode(retType, mask, saturation, sumDuplicate2,
            simdBaseType, simdSize);
    }

    private GenTree? impXplatVectorConstant(
        NamedIntrinsic intrinsic, var_types retType, var_types simdBaseType, byte simdSize)
    {
        if (intrinsic == NI_Vector_get_SignSequence)
        {
            var scalarType = simdBaseType.ActualType;
            var one = gtNewOneConNode(scalarType);
            var negativeOne = varTypeIsFloating(simdBaseType)
                ? gtNewDconNode(simdBaseType, -1.0)
                : gtNewAllBitsSetConNode(scalarType);
            return gtNewSimdCreateAlternatingSequenceNode(
                retType, one, negativeOne, simdBaseType, simdSize);
        }

        if (intrinsic == NI_Vector_get_NegativeOne && !varTypeIsSigned(simdBaseType) &&
            !varTypeIsFloating(simdBaseType))
        {
            return null;
        }

        if ((intrinsic is NI_Vector_get_E or NI_Vector_get_Pi or NI_Vector_get_Tau or
            NI_Vector_get_NegativeZero) && !varTypeIsFloating(simdBaseType))
        {
            return null;
        }

        if ((intrinsic is NI_Vector_get_Epsilon or NI_Vector_get_NaN or
            NI_Vector_get_NegativeInfinity or NI_Vector_get_PositiveInfinity) &&
            simdBaseType is not (TYP_FLOAT or TYP_DOUBLE))
        {
            return null;
        }

        var constant = gtNewVconNode(retType);
        switch (intrinsic)
        {
            case NI_Vector_get_E:
            case NI_Vector_get_Pi:
            case NI_Vector_get_Tau:
            case NI_Vector_get_NegativeOne:
            case NI_Vector_get_NegativeZero:
            {
                if (varTypeIsFloating(simdBaseType))
                {
                    var value = intrinsic switch {
                        NI_Vector_get_E => 2.718281828459045,
                        NI_Vector_get_Pi => 3.141592653589793,
                        NI_Vector_get_Tau => 6.283185307179586,
                        NI_Vector_get_NegativeOne => -1.0,
                        _ => -0.0,
                    };
                    constant.EvaluateBroadcastInPlace(simdBaseType, value);
                }
                else
                {
                    constant.EvaluateBroadcastInPlace(simdBaseType, -1L);
                }
                break;
            }

            case NI_Vector_get_Epsilon:
            case NI_Vector_get_NaN:
            case NI_Vector_get_NegativeInfinity:
            case NI_Vector_get_PositiveInfinity:
            {
                if (simdBaseType == TYP_FLOAT)
                {
                    var bits = intrinsic switch {
                        NI_Vector_get_Epsilon => 1,
                        NI_Vector_get_NaN => unchecked((int)0xFFC00000),
                        NI_Vector_get_NegativeInfinity => unchecked((int)0xFF800000),
                        _ => 0x7F800000,
                    };
                    constant.EvaluateBroadcastInPlace(TYP_INT, bits);
                }
                else
                {
                    var bits = intrinsic switch {
                        NI_Vector_get_Epsilon => 1L,
                        NI_Vector_get_NaN => unchecked((long)0xFFF8000000000000),
                        NI_Vector_get_NegativeInfinity => unchecked((long)0xFFF0000000000000),
                        _ => 0x7FF0000000000000L,
                    };
                    constant.EvaluateBroadcastInPlace(TYP_LONG, bits);
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return constant;
    }
#endif
}
