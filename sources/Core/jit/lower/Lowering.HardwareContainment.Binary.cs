// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private void ContainCheckHWIntrinsicBinary(GenTreeHWIntrinsic node, NamedIntrinsic intrinsicId,
        HWIntrinsicCategory category, var_types simdBaseType, uint simdSize,
        bool isCommutative, bool isContainedImm)
    {
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);

        switch (category)
        {
            case HW_Category_MemoryLoad:
            {
                if (intrinsicId is NI_AVX_MaskLoad or NI_AVX2_MaskLoad)
                {
                    ContainCheckHWIntrinsicAddr(node, op1, simdSize);
                }
                else
                {
                    ContainCheckHWIntrinsicAddr(node, op2, simdSize);
                }
                break;
            }

            case HW_Category_MemoryStore:
            {
                ContainCheckHWIntrinsicAddr(node, op1, simdSize);
                break;
            }

            case HW_Category_SimpleSIMD:
            case HW_Category_SIMDScalar:
            case HW_Category_Scalar:
            {
                GenTree? containedOperand = null;
                GenTree? regOptionalOperand = null;
                var swapOperands = false;

                var op2Contained = IsContainableHWIntrinsicOp(node, op2, out var supportsOp2RegOptional);
                var supportsOp1RegOptional = false;

                if (op2Contained)
                {
                    containedOperand = op2;
                }
                else if (isCommutative &&
                    IsContainableHWIntrinsicOp(node, op1, out supportsOp1RegOptional))
                {
                    containedOperand = op1;
                    swapOperands = true;
                }
                else
                {
                    if (supportsOp1RegOptional)
                    {
                        regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op1);
                    }
                    if (supportsOp2RegOptional)
                    {
                        regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op2);
                    }
                    if (ReferenceEquals(regOptionalOperand, op1))
                    {
                        swapOperands = true;
                    }
                }

                if (containedOperand is not null)
                {
                    var isEmbeddedBroadcastCompatible = containedOperand is GenTreeVecCon &&
                        node.IsEmbeddedBroadcastCompatibleHWIntrinsic(CompilerInstance);

                    var oper = node.GetOperForHWIntrinsicId(out _);
                    if (isEmbeddedBroadcastCompatible && BlockRange().TryGetUse(node, out var use))
                    {
                        var user = use.User();
                        if (oper is GT_XOR && user is GenTreeHWIntrinsic fma &&
                            fma.OperIsVectorFusedMultiplyOp)
                        {
                            isEmbeddedBroadcastCompatible =
                                !containedOperand.IsVectorNegativeZero(fma.SimdBaseType);
                        }
                        else if (oper is (GT_AND or GT_AND_NOT) && user is GenTreeHWIntrinsic comparison)
                        {
                            var isEquality = comparison.HWIntrinsicId is
                                NI_Vector_op_Equality or NI_Vector_op_Inequality;
                            if (isEquality && (comparison.GetOp(1).IsVectorZero ||
                                comparison.GetOp(2).IsVectorZero))
                            {
                                isEmbeddedBroadcastCompatible = false;
                            }
                        }
                    }

                    if (isEmbeddedBroadcastCompatible)
                    {
                        TryFoldCnsVecForEmbeddedBroadcast(node, containedOperand.AsVecCon());
                    }
                    else
                    {
                        ContainHWIntrinsicOperand(node, containedOperand);
                    }
                }
                else if (regOptionalOperand is not null)
                {
                    MakeSrcRegOptional(node, regOptionalOperand);
                }

                if (swapOperands)
                {
                    var currentOp1 = node.GetOp(1);
                    node.SetOp(1, node.GetOp(2));
                    node.SetOp(2, currentOp1);
                }
                break;
            }

            case HW_Category_IMM:
            {
                assert(!isCommutative);
                switch (intrinsicId)
                {
                    case NI_X86Base_Extract:
                    case NI_X86Base_X64_Extract:
                    case NI_AVX_ExtractVector128:
                    case NI_AVX2_ExtractVector128:
                    case NI_AVX512_ExtractVector128:
                    case NI_AVX512_ExtractVector256:
                    case NI_AVX2_ConvertToVector128Half:
                    case NI_AVX2_ConvertToVector256Half:
                    {
                        break;
                    }

                    case NI_AVX2_Shuffle:
                    case NI_AVX512_Shuffle:
                    case NI_X86Base_Shuffle:
                    {
                        if (varTypeIsByte(simdBaseType))
                        {
                            TryMakeSrcContainedOrRegOptional(node, op2);
                        }
                        else if (isContainedImm)
                        {
                            TryMakeSrcContainedOrRegOptional(node, op1);
                        }
                        break;
                    }

                    case NI_X86Base_ShuffleHigh:
                    case NI_X86Base_ShuffleLow:
                    case NI_AVX2_Permute4x64:
                    case NI_AVX2_ShuffleHigh:
                    case NI_AVX2_ShuffleLow:
                    case NI_AVX512_ClassifyMask:
                    case NI_AVX512_ClassifyScalarMask:
                    case NI_AVX512_Permute2x64:
                    case NI_AVX512_Permute4x32:
                    case NI_AVX512_Permute4x64:
                    case NI_AVX512_ShuffleHigh:
                    case NI_AVX512_ShuffleLow:
                    case NI_AVX512_RotateLeft:
                    case NI_AVX512_RotateRight:
                    case NI_AES_KeygenAssist:
                    case NI_AVX512_GetMantissa:
                    case NI_AVX512_RoundScale:
                    case NI_AVX512_Reduce:
                    case NI_X86Base_ShiftLeftLogical128BitLane:
                    case NI_X86Base_ShiftRightLogical128BitLane:
                    case NI_AVX2_ShiftLeftLogical128BitLane:
                    case NI_AVX2_ShiftRightLogical128BitLane:
                    case NI_AVX512_ShiftLeftLogical128BitLane:
                    case NI_AVX512_ShiftRightLogical128BitLane:
                    {
                        if (isContainedImm)
                        {
                            TryMakeSrcContainedOrRegOptional(node, op1);
                        }
                        break;
                    }

                    case NI_AVX_Permute:
                    case NI_X86Base_ShiftLeftLogical:
                    case NI_X86Base_ShiftRightArithmetic:
                    case NI_X86Base_ShiftRightLogical:
                    case NI_AVX2_ShiftLeftLogical:
                    case NI_AVX2_ShiftRightArithmetic:
                    case NI_AVX2_ShiftRightLogical:
                    case NI_AVX512_ShiftLeftLogical:
                    case NI_AVX512_ShiftRightArithmetic:
                    case NI_AVX512_ShiftRightLogical:
                    {
                        if (HWIntrinsicInfo.isImmOp(intrinsicId, op2))
                        {
                            if (isContainedImm)
                            {
                                TryMakeSrcContainedOrRegOptional(node, op1);
                            }
                        }
                        else
                        {
                            TryMakeSrcContainedOrRegOptional(node, op2);
                        }
                        break;
                    }

                    case NI_AVX512_GetMantissaScalar:
                    case NI_AVX512_RoundScaleScalar:
                    case NI_AVX512_ReduceScalar:
                    case NI_AVX10v1_RoundScaleScalar:
                    case NI_AVX512_ShiftLeftMask:
                    case NI_AVX512_ShiftRightMask:
                    {
                        return;
                    }

                    default:
                    {
                        throw new System.InvalidOperationException(
                            $"Unhandled binary immediate hardware intrinsic: {intrinsicId}.");
                    }
                }
                break;
            }

            case HW_Category_Helper:
            {
                assert(!isCommutative);
                switch (intrinsicId)
                {
                    case NI_Vector_GetElement:
                    {
                        if (op2.Oper.IsConst)
                        {
                            MakeSrcContained(node, op2);
                        }
                        if ((op1 is GenTreeVecCon) ||
                            (IsContainableMemoryOp(op1) && IsSafeToContainMem(node, op1)))
                        {
                            MakeSrcContained(node, op1);
                        }
                        break;
                    }

                    case NI_Vector_op_Division:
                    {
                        break;
                    }

                    default:
                    {
                        throw new System.InvalidOperationException(
                            $"Unhandled binary helper hardware intrinsic: {intrinsicId}.");
                    }
                }
                break;
            }

            default:
            {
                throw new System.InvalidOperationException(
                    $"Unexpected binary hardware intrinsic category: {category}.");
            }
        }
    }
#endif
}
