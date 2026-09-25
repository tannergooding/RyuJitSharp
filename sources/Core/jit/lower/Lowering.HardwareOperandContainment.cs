// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private bool IsContainableHWIntrinsicOp(GenTreeHWIntrinsic parentNode, GenTree childNode,
        out bool supportsRegOptional)
    {
        var parentIntrinsicId = parentNode.HWIntrinsicId;
        var parentBaseType = parentNode.SimdBaseType;
        var category = HWIntrinsicInfo.lookupCategory(parentIntrinsicId);
        assert(HWIntrinsicInfo.SupportsContainment(parentIntrinsicId));

        if (parentIntrinsicId is NI_AVX512_BlendVariableMask &&
            childNode.NodeOrContainedOperandsMayThrow(CompilerInstance))
        {
            supportsRegOptional = false;
            return false;
        }

        var expectedSize = (int)parentNode.SimdSize;
        var operandSize = (int)childNode.Type.Size;
        var supportsMemoryOp = true;
        var supportsSimdLoad = true;

        switch (category)
        {
            case HW_Category_MemoryLoad:
            {
                assert(childNode.Type.ActualType is TYP_I_IMPL);
                operandSize = expectedSize;
                break;
            }

            case HW_Category_SimpleSIMD:
            case HW_Category_IMM:
            {
                var ins = HWIntrinsicInfo.lookupIns(parentIntrinsicId, parentBaseType, CompilerInstance);
                var tupleType = Emitter.insTupleTypeInfo(ins);
                var sizeFromTupleType = false;

                switch (parentIntrinsicId)
                {
                    case NI_X86Base_ConvertToVector128Int16:
                    case NI_X86Base_ConvertToVector128Int32:
                    case NI_X86Base_ConvertToVector128Int64:
                    case NI_AVX2_ConvertToVector256Int16:
                    case NI_AVX2_ConvertToVector256Int32:
                    case NI_AVX2_ConvertToVector256Int64:
                    {
                        if (parentNode.IsMemoryLoad())
                        {
                            operandSize = expectedSize;
                        }
                        else
                        {
                            sizeFromTupleType = true;
                        }
                        break;
                    }

                    case NI_X86Base_ShiftLeftLogical128BitLane:
                    case NI_X86Base_ShiftRightLogical128BitLane:
                    case NI_AVX2_ShiftLeftLogical128BitLane:
                    case NI_AVX2_ShiftRightLogical128BitLane:
                    {
                        if (!CompilerInstance.canUseEvexEncoding())
                        {
                            supportsMemoryOp = false;
                        }
                        else
                        {
                            sizeFromTupleType = true;
                        }
                        break;
                    }

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
                        assert((tupleType & INS_TT_MEM128) != 0);
                        if (!HWIntrinsicInfo.isImmOp(parentIntrinsicId, parentNode.GetOp(2)))
                        {
                            tupleType = INS_TT_MEM128;
                            expectedSize = TYP_SIMD16.Size;
                        }
                        else
                        {
                            tupleType &= ~INS_TT_MEM128;
                            if (!CompilerInstance.canUseEvexEncoding())
                            {
                                supportsMemoryOp = false;
                            }
                            else
                            {
                                sizeFromTupleType = true;
                            }
                        }
                        break;
                    }

                    case NI_X86Base_Insert:
                    case NI_X86Base_X64_Insert:
                    {
                        if (ins is INS_insertps)
                        {
                            supportsMemoryOp = false;
                            var immediate = parentNode.GetOp(3);
                            if (immediate.Oper.IsCnsIntOrI)
                            {
                                var value = immediate.AsIntConCommon().IntegralValue;
                                assert(value >= 0 && value <= 255);
                                expectedSize = TYP_FLOAT.Size;
                                supportsMemoryOp = value <= 0x3F;
                            }
                        }
                        else
                        {
                            assert(varTypeIsIntegral(childNode.Type));
                            expectedSize = parentBaseType.Size;
                            supportsSimdLoad = false;
                        }
                        break;
                    }

                    default:
                    {
                        sizeFromTupleType = true;
                        break;
                    }
                }

                if (sizeFromTupleType)
                {
                    switch (tupleType)
                    {
                        case INS_TT_NONE:
                        case INS_TT_FULL:
                        case INS_TT_FULL_MEM:
                        {
                            break;
                        }

                        case INS_TT_HALF:
                        case INS_TT_HALF_MEM:
                        {
                            expectedSize /= 2;
                            break;
                        }

                        case INS_TT_QUARTER_MEM:
                        {
                            expectedSize /= 4;
                            break;
                        }

                        case INS_TT_EIGHTH_MEM:
                        {
                            expectedSize /= 8;
                            break;
                        }

                        case INS_TT_MOVDDUP:
                        {
                            if (expectedSize == TYP_SIMD16.Size)
                            {
                                expectedSize /= 2;
                            }
                            break;
                        }

                        case INS_TT_TUPLE1_FIXED:
                        case INS_TT_TUPLE1_SCALAR:
                        case INS_TT_TUPLE2:
                        case INS_TT_TUPLE4:
                        case INS_TT_TUPLE8:
                        {
                            var multiplier = tupleType switch {
                                INS_TT_TUPLE2 => 2,
                                INS_TT_TUPLE4 => 4,
                                INS_TT_TUPLE8 => 8,
                                _ => 1,
                            };
                            expectedSize = CodeGen.instInputSize(ins) * multiplier;
                            break;
                        }

                        default:
                        {
                            throw new System.InvalidOperationException($"Unexpected tuple type: {tupleType}.");
                        }
                    }
                }
                break;
            }

            case HW_Category_SIMDScalar:
            {
                expectedSize = parentBaseType.Size;
                switch (parentIntrinsicId)
                {
                    case NI_Vector_CreateScalar:
                    case NI_Vector_CreateScalarUnsafe:
                    {
                        if (varTypeIsIntegral(childNode.Type))
                        {
                            expectedSize = parentBaseType.ActualType.Size;
                            supportsSimdLoad = false;
                        }
                        break;
                    }

                    case NI_AVX2_BroadcastScalarToVector128:
                    case NI_AVX2_BroadcastScalarToVector256:
                    case NI_AVX512_BroadcastScalarToVector512:
                    {
                        if (parentNode.IsMemoryLoad())
                        {
                            operandSize = expectedSize;
                        }
                        break;
                    }

                    default:
                    {
                        supportsSimdLoad = !varTypeIsIntegral(childNode.Type);
                        break;
                    }
                }
                break;
            }

            case HW_Category_Scalar:
            {
                assert(varTypeIsIntegral(childNode.Type));
                expectedSize = parentIntrinsicId is NI_X86Base_Crc32 ? parentBaseType.Size : parentNode.Type.Size;
                break;
            }

            default:
            {
                throw new System.InvalidOperationException($"Unexpected intrinsic category: {category}.");
            }
        }

        supportsMemoryOp = supportsMemoryOp && operandSize >= expectedSize;
        supportsSimdLoad = supportsSimdLoad && supportsMemoryOp;

        var supportsUnalignedLoad = expectedSize < TYP_SIMD16.Size || CompilerInstance.canUseVexEncoding();
        supportsRegOptional = supportsMemoryOp && supportsUnalignedLoad &&
            IsSafeToMarkRegOptional(parentNode, childNode);

        if (childNode is not GenTreeHWIntrinsic intrinsic)
        {
            if (!supportsMemoryOp)
            {
                return false;
            }
            if (IsContainableMemoryOp(childNode))
            {
                return supportsUnalignedLoad && IsSafeToContainMem(parentNode, childNode);
            }
            if (childNode.IsCnsNonZeroFltOrDbl)
            {
                return true;
            }
            if (childNode is GenTreeVecCon constant)
            {
                return !constant.IsAllBitsSet && !constant.IsZero;
            }
            return false;
        }

        var intrinsicId = intrinsic.HWIntrinsicId;
        var childBaseType = intrinsic.SimdBaseType;
        var supportsSimdScalarLoad = supportsSimdLoad && expectedSize <= childBaseType.Size;
        switch (intrinsicId)
        {
            case NI_Vector_CreateScalar:
            case NI_Vector_CreateScalarUnsafe:
            {
                if (!supportsSimdScalarLoad)
                {
                    return false;
                }

                var source = intrinsic.GetOp(1);
                if (IsInvariantInRange(source, parentNode, intrinsic))
                {
#if TARGET_X86
                    if (source.IsContained && source.Oper is not GT_LONG)
#else
                    if (source.IsContained)
#endif
                    {
                        return true;
                    }
                    if (source.IsRegOptional && varTypeIsFloating(source.Type))
                    {
                        return true;
                    }
                }
                return false;
            }

            case NI_X86Base_LoadAlignedVector128:
            case NI_AVX_LoadAlignedVector256:
            case NI_AVX512_LoadAlignedVector512:
            {
                return supportsSimdLoad && (CompilerInstance.opts.OptimizationEnabled ||
                    (!CompilerInstance.canUseVexEncoding() && expectedSize == TYP_SIMD16.Size));
            }

            case NI_X86Base_LoadScalarVector128:
            {
                assert(intrinsic.IsMemoryLoad());
                return supportsSimdScalarLoad;
            }

            case NI_X86Base_MoveAndDuplicate:
            case NI_AVX2_BroadcastScalarToVector128:
            case NI_AVX2_BroadcastScalarToVector256:
            case NI_AVX512_BroadcastScalarToVector512:
            {
                if (!CompilerInstance.opts.Tier0OptimizationEnabled || JitConfig.EnableEmbeddedBroadcast == 0 ||
                    varTypeIsSmall(parentBaseType) || parentBaseType.Size != childBaseType.Size)
                {
                    return false;
                }
                if (intrinsicId is NI_X86Base_MoveAndDuplicate)
                {
                    assert(childBaseType is TYP_DOUBLE);
                }

                if (parentNode.IsEmbeddedBroadcastCompatibleHWIntrinsic(CompilerInstance))
                {
                    var broadcastOperand = intrinsic.GetOp(1);
                    if (broadcastOperand is GenTreeHWIntrinsic create &&
                        create.HWIntrinsicId is NI_Vector_CreateScalar or NI_Vector_CreateScalarUnsafe)
                    {
                        broadcastOperand = create.GetOp(1);
                    }

                    if (IsContainableHWIntrinsicOp(intrinsic, broadcastOperand, out _) &&
                        IsSafeToContainMem(parentNode, intrinsic))
                    {
                        return true;
                    }
                }
                return false;
            }

            case NI_X86Base_LoadAndDuplicateToVector128:
            case NI_AVX_BroadcastScalarToVector128:
            case NI_AVX_BroadcastScalarToVector256:
            {
                assert(intrinsic.IsMemoryLoad());
                assert(varTypeIsFloating(childBaseType));
                return JitConfig.EnableEmbeddedBroadcast != 0 && parentBaseType == childBaseType &&
                    parentNode.IsEmbeddedBroadcastCompatibleHWIntrinsic(CompilerInstance);
            }

            default:
            {
                return false;
            }
        }
    }

    private void TryMakeSrcContainedOrRegOptional(GenTreeHWIntrinsic parentNode, GenTree childNode)
    {
        if (IsContainableHWIntrinsicOp(parentNode, childNode, out var supportsRegOptional))
        {
            if (childNode is GenTreeVecCon constant &&
                parentNode.IsEmbeddedBroadcastCompatibleHWIntrinsic(CompilerInstance))
            {
                TryFoldCnsVecForEmbeddedBroadcast(parentNode, constant);
            }
            else
            {
                ContainHWIntrinsicOperand(parentNode, childNode);
            }
        }
        else if (supportsRegOptional)
        {
            MakeSrcRegOptional(parentNode, childNode);
        }
    }
#endif
}
