// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private void ContainCheckHWIntrinsicUnary(GenTreeHWIntrinsic node, NamedIntrinsic intrinsicId,
        HWIntrinsicCategory category, var_types simdBaseType, uint simdSize)
    {
        var op1 = node.GetOp(1);
        switch (category)
        {
            case HW_Category_MemoryLoad:
            {
                ContainCheckHWIntrinsicAddr(node, op1, simdSize);
                break;
            }

            case HW_Category_SimpleSIMD:
            case HW_Category_SIMDScalar:
            case HW_Category_Scalar:
            {
                switch (intrinsicId)
                {
                    case NI_X86Base_CeilingScalar:
                    case NI_X86Base_FloorScalar:
                    case NI_X86Base_ReciprocalScalar:
                    case NI_X86Base_ReciprocalSqrtScalar:
                    case NI_X86Base_RoundCurrentDirectionScalar:
                    case NI_X86Base_RoundToNearestIntegerScalar:
                    case NI_X86Base_RoundToNegativeInfinityScalar:
                    case NI_X86Base_RoundToPositiveInfinityScalar:
                    case NI_X86Base_RoundToZeroScalar:
                    case NI_X86Base_SqrtScalar:
                    case NI_AVX512_GetExponentScalar:
                    case NI_AVX512_Reciprocal14Scalar:
                    case NI_AVX512_ReciprocalSqrt14Scalar:
                    {
                        return;
                    }

                    case NI_X86Base_ConvertToInt32:
                    case NI_X86Base_X64_ConvertToInt64:
                    case NI_X86Base_ConvertToUInt32:
                    case NI_X86Base_X64_ConvertToUInt64:
                    case NI_AVX2_ConvertToInt32:
                    case NI_AVX2_ConvertToUInt32:
                    {
                        if (varTypeIsIntegral(simdBaseType))
                        {
                            return;
                        }
                        break;
                    }

                    case NI_X86Base_ConvertToVector128Int16:
                    case NI_X86Base_ConvertToVector128Int32:
                    case NI_X86Base_ConvertToVector128Int64:
                    case NI_AVX2_ConvertToVector256Int16:
                    case NI_AVX2_ConvertToVector256Int32:
                    case NI_AVX2_ConvertToVector256Int64:
                    {
                        if (node.IsMemoryLoad())
                        {
                            ContainCheckHWIntrinsicAddr(node, op1, 16);
                            return;
                        }
                        break;
                    }

                    case NI_AVX2_BroadcastScalarToVector128:
                    case NI_AVX2_BroadcastScalarToVector256:
                    case NI_AVX512_BroadcastScalarToVector512:
                    {
                        if (node.IsMemoryLoad())
                        {
                            ContainCheckHWIntrinsicAddr(node, op1, 8);
                            return;
                        }

                        if (varTypeIsIntegral(simdBaseType) &&
                            op1 is GenTreeHWIntrinsic create &&
                            create.HWIntrinsicId is NI_Vector_CreateScalar or NI_Vector_CreateScalarUnsafe)
                        {
#if TARGET_X86
                            if (create.GetOp(1).Oper is GT_LONG)
                            {
                                return;
                            }
#endif
                            node.GetOpRef(1) = create.GetOp(1);
                            BlockRange().Remove(create);
                            op1 = node.GetOp(1);
                            op1.IsContained = false;

                            if (op1 is GenTreeCast cast && !cast.HasOverflowCheck &&
                                CompilerInstance.opts.Tier0OptimizationEnabled &&
                                !varTypeIsFloating(cast.CastType) &&
                                !varTypeIsFloating(cast.CastOp.Type) &&
#if TARGET_X86
                                cast.CastOp.Oper is not GT_LONG &&
#endif
                                cast.CastType.Size >= simdBaseType.Size &&
                                cast.CastOp.Type.Size >= simdBaseType.Size)
                            {
                                BlockRange().Remove(cast);
                                op1 = cast.CastOp;
                                op1.IsContained = false;
                                node.GetOpRef(1) = op1;
                            }
                        }
                        break;
                    }

                    case NI_AVX512_ConvertToVector128UInt32:
                    case NI_AVX512_ConvertToVector128UInt32WithSaturation:
                    case NI_AVX512_ConvertToVector256Int32:
                    case NI_AVX512_ConvertToVector256UInt32:
                    {
                        if (!varTypeIsFloating(simdBaseType))
                        {
                            return;
                        }
                        break;
                    }

                    case NI_AVX512_ConvertToVector128Byte:
                    case NI_AVX512_ConvertToVector128ByteWithSaturation:
                    case NI_AVX512_ConvertToVector128Int16:
                    case NI_AVX512_ConvertToVector128Int16WithSaturation:
                    case NI_AVX512_ConvertToVector128Int32:
                    case NI_AVX512_ConvertToVector128Int32WithSaturation:
                    case NI_AVX512_ConvertToVector128SByte:
                    case NI_AVX512_ConvertToVector128SByteWithSaturation:
                    case NI_AVX512_ConvertToVector128UInt16:
                    case NI_AVX512_ConvertToVector128UInt16WithSaturation:
                    case NI_AVX512_ConvertToVector256Byte:
                    case NI_AVX512_ConvertToVector256ByteWithSaturation:
                    case NI_AVX512_ConvertToVector256Int16:
                    case NI_AVX512_ConvertToVector256Int16WithSaturation:
                    case NI_AVX512_ConvertToVector256Int32WithSaturation:
                    case NI_AVX512_ConvertToVector256SByte:
                    case NI_AVX512_ConvertToVector256SByteWithSaturation:
                    case NI_AVX512_ConvertToVector256UInt16:
                    case NI_AVX512_ConvertToVector256UInt16WithSaturation:
                    case NI_AVX512_ConvertToVector256UInt32WithSaturation:
                    {
                        return;
                    }

#if TARGET_X86
                    case NI_Vector_CreateScalar:
                    case NI_Vector_CreateScalarUnsafe:
                    {
                        if (op1.Oper is GT_LONG)
                        {
                            assert(varTypeIsLong(simdBaseType));
                            foreach (var longOp in op1.Operands)
                            {
                                if (!varTypeIsSmall(longOp.Type))
                                {
                                    if (IsContainableMemoryOp(longOp) && IsSafeToContainMem(node, longOp))
                                    {
                                        MakeSrcContained(node, longOp);
                                    }
                                    else if (IsSafeToMarkRegOptional(node, longOp))
                                    {
                                        MakeSrcRegOptional(node, longOp);
                                    }
                                }
                            }
                            MakeSrcContained(node, op1);
                            return;
                        }
                        break;
                    }

                    case NI_Vector_ToScalar:
                    {
                        if (varTypeIsLong(simdBaseType))
                        {
                            return;
                        }
                        break;
                    }
#endif
                }

                assert(!node.IsMemoryLoad());
                TryMakeSrcContainedOrRegOptional(node, op1);
                break;
            }

            default:
            {
                throw new System.InvalidOperationException($"Unexpected unary hardware intrinsic category: {category}.");
            }
        }
    }
#endif
}
