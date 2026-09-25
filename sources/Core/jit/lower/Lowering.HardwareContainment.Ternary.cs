// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private void ContainCheckHWIntrinsicTernary(GenTreeHWIntrinsic node, NamedIntrinsic intrinsicId,
        HWIntrinsicCategory category, var_types simdBaseType, uint simdSize, bool isContainedImm)
    {
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);

        switch (category)
        {
            case HW_Category_MemoryLoad:
            {
                if (op3.IsVectorZero)
                {
                    MakeSrcContained(node, op3);
                }
                ContainCheckHWIntrinsicAddr(node, op1, simdSize);
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
                if (HWIntrinsicInfo.IsFmaIntrinsic(intrinsicId))
                {
                    var supportsOp1RegOptional = false;
                    var supportsOp2RegOptional = false;
                    var supportsOp3RegOptional = false;
                    GenTree? containedOperand = null;
                    GenTree? regOptionalOperand = null;
                    GenTree? user = null;

                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        user = use.User();
                    }
                    var resultOpNum = node.GetResultOpNumForRmwIntrinsic(user, op1, op2, op3);

                    if ((resultOpNum != 3) &&
                        IsContainableHWIntrinsicOp(node, op3, out supportsOp3RegOptional))
                    {
                        containedOperand = op3;
                    }
                    else if ((resultOpNum != 2) &&
                        IsContainableHWIntrinsicOp(node, op2, out supportsOp2RegOptional))
                    {
                        containedOperand = op2;
                    }
                    else if ((resultOpNum != 1) && !HWIntrinsicInfo.CopiesUpperBits(intrinsicId) &&
                        IsContainableHWIntrinsicOp(node, op1, out supportsOp1RegOptional))
                    {
                        containedOperand = op1;
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
                        if (supportsOp3RegOptional)
                        {
                            regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op3);
                        }
                    }

                    if (containedOperand is not null)
                    {
                        if (containedOperand is GenTreeVecCon vecCon &&
                            node.IsEmbeddedBroadcastCompatibleHWIntrinsic(CompilerInstance))
                        {
                            TryFoldCnsVecForEmbeddedBroadcast(node, vecCon);
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
                }
                else if (HWIntrinsicInfo.IsPermuteVar2x(intrinsicId))
                {
#if DEBUG
                    assert(CompilerInstance.canUseEvexEncodingDebugOnly());
#endif
                    var supportsOp1RegOptional = false;
                    var supportsOp3RegOptional = false;
                    GenTree? containedOperand = null;
                    GenTree? regOptionalOperand = null;
                    var swapOperands = false;
                    var isOp2Cns = op2 is GenTreeVecCon;
                    GenTree? user = null;

                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        user = use.User();
                    }
                    var resultOpNum = node.GetResultOpNumForRmwIntrinsic(user, op1, op2, op3);

                    if (((resultOpNum != 3) || !isOp2Cns) &&
                        IsContainableHWIntrinsicOp(node, op3, out supportsOp3RegOptional))
                    {
                        containedOperand = op3;
                    }
                    else if ((resultOpNum != 2) && isOp2Cns &&
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
                        if (supportsOp3RegOptional)
                        {
                            regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op3);
                        }
                        swapOperands = ReferenceEquals(regOptionalOperand, op1);
                    }

                    if (containedOperand is not null)
                    {
                        if (containedOperand is GenTreeVecCon vecCon &&
                            node.IsEmbeddedBroadcastCompatibleHWIntrinsic(CompilerInstance))
                        {
                            TryFoldCnsVecForEmbeddedBroadcast(node, vecCon);
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
                        node.SetOp(1, node.GetOp(3));
                        node.SetOp(3, currentOp1);

                        var toggleBit = simdBaseType.Size switch
                        {
                            1 => simdSize,
                            2 => simdSize / 2,
                            4 => simdSize / 4,
                            8 => simdSize / 8,
                            _ => throw new System.InvalidOperationException("Invalid permutation element size."),
                        };
                        var repeatedToggle = simdBaseType.Size switch
                        {
                            1 => toggleBit * 0x0101010101010101UL,
                            2 => toggleBit * 0x0001000100010001UL,
                            4 => toggleBit * 0x0000000100000001UL,
                            8 => toggleBit,
                            _ => throw new System.InvalidOperationException("Invalid permutation element size."),
                        };
                        ref var indices = ref op2.AsVecCon().SimdVal;
                        for (var i = 0; i < (simdSize / 8); i++)
                        {
                            indices.u64[i] ^= repeatedToggle;
                        }
                    }
                }
                else
                {
                    switch (intrinsicId)
                    {
                        case NI_X86Base_BlendVariable:
                        case NI_AVX_BlendVariable:
                        case NI_AVX2_BlendVariable:
                        {
                            TryMakeSrcContainedOrRegOptional(node, op2);
                            break;
                        }

                        case NI_AVX512_BlendVariableMask:
                        {
                            if (op1.IsVectorZero)
                            {
                                MakeSrcContained(node, op1);
                            }

                            if (IsInvariantInRange(op2, node))
                            {
                                var targetMaskSize = checked((int)(simdSize / (uint)simdBaseType.Size));
                                var targetBaseType = TYP_UNDEF;
                                if (op2.IsEmbeddedMaskingCompatible(CompilerInstance,
                                    targetMaskSize, ref targetBaseType, out var broadcastOperandIndex))
                                {
                                    if (targetBaseType is not TYP_UNDEF)
                                    {
                                        var masked = op2.AsHWIntrinsic();
                                        masked.SimdBaseType = targetBaseType;
                                        if (broadcastOperandIndex != 0)
                                        {
                                            var broadcast = masked.GetOp(broadcastOperandIndex).AsHWIntrinsic();
                                            var oldConstant = broadcast.GetOp(1);
                                            ulong value = oldConstant is GenTreeDblCon floating
                                                ? BitConverter.SingleToUInt32Bits((float)floating.DconVal)
                                                : unchecked((uint)oldConstant.AsIntCon().IconValue);
                                            var widened = CompilerInstance.gtNewLconNode(
                                                unchecked((long)((value << 32) | value)));
                                            broadcast.SimdBaseType = TYP_LONG;
                                            broadcast.SetOp(1, widened);
                                            BlockRange().InsertBefore(broadcast, widened);
                                            BlockRange().Remove(oldConstant);
                                            MakeSrcContained(broadcast, widened);
                                        }
                                    }
                                    MakeSrcContained(node, op2);
                                    op2.Flags |= GTF_HW_EM_OP;
                                    break;
                                }
                            }
                            TryMakeSrcContainedOrRegOptional(node, op2);
                            break;
                        }

                        case NI_AVX512_CompressMask:
                        case NI_AVX512_ExpandMask:
                        {
                            if (op1.IsVectorZero)
                            {
                                MakeSrcContained(node, op1);
                            }
                            break;
                        }

                        case NI_X86Base_X64_BigMul:
                        case NI_AVX2_MultiplyNoFlags:
                        case NI_AVX2_X64_MultiplyNoFlags:
                        {
                            var contained = IsContainableHWIntrinsicOp(node, op2, out var op2Optional)
                                ? op2 : null;
                            var op1Optional = false;
                            var swap = false;
                            if (contained is null && IsContainableHWIntrinsicOp(node, op1, out op1Optional))
                            {
                                contained = op1;
                                swap = true;
                            }
                            GenTree? optional = null;
                            if (contained is null)
                            {
                                if (op1Optional)
                                {
                                    optional = PreferredRegOptionalOperand(optional, op1);
                                }
                                if (op2Optional)
                                {
                                    optional = PreferredRegOptionalOperand(optional, op2);
                                }
                                swap = ReferenceEquals(optional, op1);
                            }
                            if (contained is not null)
                            {
                                ContainHWIntrinsicOperand(node, contained);
                            }
                            else if (optional is not null)
                            {
                                MakeSrcRegOptional(node, optional);
                            }
                            if (swap)
                            {
                                var currentOp1 = node.GetOp(1);
                                node.SetOp(1, node.GetOp(2));
                                node.SetOp(2, currentOp1);
                            }
                            break;
                        }

                        case NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction:
                        case NI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction:
                        {
                            var contained = IsContainableHWIntrinsicOp(node, op3, out var op3Optional)
                                ? op3 : null;
                            var op2Optional = false;
                            var swap = false;
                            if (contained is null && IsContainableHWIntrinsicOp(node, op2, out op2Optional))
                            {
                                contained = op2;
                                swap = true;
                            }
                            GenTree? optional = null;
                            if (contained is null)
                            {
                                if (op2Optional)
                                {
                                    optional = PreferredRegOptionalOperand(optional, op2);
                                }
                                if (op3Optional)
                                {
                                    optional = PreferredRegOptionalOperand(optional, op3);
                                }
                                swap = ReferenceEquals(optional, op2);
                            }
                            if (contained is not null)
                            {
                                ContainHWIntrinsicOperand(node, contained);
                            }
                            else if (optional is not null)
                            {
                                MakeSrcRegOptional(node, optional);
                            }
                            if (swap)
                            {
                                var currentOp2 = node.GetOp(2);
                                node.SetOp(2, node.GetOp(3));
                                node.SetOp(3, currentOp2);
                            }
                            break;
                        }

                        default:
                        {
                            if (intrinsicId is not (NI_X86Base_DivRem or NI_X86Base_X64_DivRem or
                                NI_AVX512v3_MultiplyWideningAndAdd or
                                NI_AVX512v3_MultiplyWideningAndAddSaturate) &&
                                (intrinsicId < FIRST_NI_AVXVNNI || intrinsicId > LAST_NI_AVXVNNIINT_V512))
                            {
                                throw new System.InvalidOperationException(
                                    $"Unhandled ternary hardware intrinsic: {intrinsicId}.");
                            }
                            TryMakeSrcContainedOrRegOptional(node, op3);
                            break;
                        }
                    }
                }
                break;
            }

            case HW_Category_IMM:
            {
                switch (intrinsicId)
                {
                    case NI_X86Base_AlignRight:
                    case NI_X86Base_Blend:
                    case NI_X86Base_DotProduct:
                    case NI_X86Base_MultipleSumAbsoluteDifferences:
                    case NI_X86Base_Shuffle:
                    case NI_X86Base_X64_Insert:
                    case NI_AVX_Blend:
                    case NI_AVX_Compare:
                    case NI_AVX_CompareScalar:
                    case NI_AVX_DotProduct:
                    case NI_AVX_InsertVector128:
                    case NI_AVX_Permute2x128:
                    case NI_AVX_Shuffle:
                    case NI_AVX2_AlignRight:
                    case NI_AVX2_Blend:
                    case NI_AVX2_InsertVector128:
                    case NI_AVX2_MultipleSumAbsoluteDifferences:
                    case NI_AVX2_Permute2x128:
                    case NI_AVX512_AlignRight32:
                    case NI_AVX512_AlignRight64:
                    case NI_AVX512_AlignRight:
                    case NI_AVX512_GetMantissaScalar:
                    case NI_AVX512_InsertVector128:
                    case NI_AVX512_InsertVector256:
                    case NI_AVX512_Range:
                    case NI_AVX512_RangeScalar:
                    case NI_AVX512_ReduceScalar:
                    case NI_AVX512_RoundScaleScalar:
                    case NI_AVX512_Shuffle2x128:
                    case NI_AVX512_Shuffle4x128:
                    case NI_AVX512_Shuffle:
                    case NI_AVX512_SumAbsoluteDifferencesInBlock32:
                    case NI_AVX512_CompareMask:
                    case NI_AVX512_CompareScalarMask:
                    case NI_AES_CarrylessMultiply:
                    case NI_AES_V256_CarrylessMultiply:
                    case NI_AES_V512_CarrylessMultiply:
                    case NI_AVX10v1_RoundScaleScalar:
                    case NI_AVX10v2_MinMax:
                    case NI_AVX10v2_MinMaxScalar:
                    case NI_AVX10v2_MultipleSumAbsoluteDifferences:
                    case NI_GFNI_GaloisFieldAffineTransform:
                    case NI_GFNI_GaloisFieldAffineTransformInverse:
                    case NI_GFNI_V256_GaloisFieldAffineTransform:
                    case NI_GFNI_V256_GaloisFieldAffineTransformInverse:
                    case NI_GFNI_V512_GaloisFieldAffineTransform:
                    case NI_GFNI_V512_GaloisFieldAffineTransformInverse:
                    {
                        if (isContainedImm)
                        {
                            TryMakeSrcContainedOrRegOptional(node, op2);
                        }
                        break;
                    }

                    case NI_X86Base_Insert:
                    {
                        if (!isContainedImm)
                        {
                            break;
                        }
                        if (simdBaseType is TYP_FLOAT)
                        {
                            var value = node.GetOp(3).AsIntConCommon().IconValue;
                            var destinationIndex = (value & 0x30) >> 4;
                            var zeroMask = value & 0x0F;
                            if (op1.IsVectorZero)
                            {
                                assert(value == ((value & 0xC0) | (value & 0x30) |
                                    ((zeroMask | ~(1 << checked((int)destinationIndex))) & 0x0F)));
                                MakeSrcContained(node, op1);
                            }
                            else if (op2.IsVectorZero)
                            {
                                assert(value == ((value & 0xC0) | (value & 0x30) |
                                    ((zeroMask | (1 << checked((int)destinationIndex))) & 0x0F)));
                                MakeSrcContained(node, op2);
                            }
                        }
                        if (!op2.IsContained)
                        {
                            TryMakeSrcContainedOrRegOptional(node, op2);
                        }
                        break;
                    }

                    default:
                    {
                        throw new System.InvalidOperationException(
                            $"Unhandled ternary immediate hardware intrinsic: {intrinsicId}.");
                    }
                }
                break;
            }

            default:
            {
                throw new System.InvalidOperationException(
                    $"Unexpected ternary hardware intrinsic category: {category}.");
            }
        }
    }
#endif
}
