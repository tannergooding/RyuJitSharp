// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.Intrinsics.X86;

namespace RyuJitSharp;

public sealed partial class Rationalizer
{
    private BasicBlock BlockRange
    {
        get
        {
            assert(_block is not null);
            return _block;
        }
    }

#if FEATURE_HW_INTRINSICS
    private unsafe void RewriteHWIntrinsicAsUserCall(ref GenTree use, GenTreeStack parents)
    {
        var node = use.AsHWIntrinsic();
        var intrinsic = node.HWIntrinsicId;
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        var type = node.Type;
        var operands = node.Operands;
        var method = node.MethodHandle;
        CompilerInstance.eeGetMethodSig(method, out var signature);
        GenTree? result = null;
        switch (intrinsic)
        {
#if TARGET_XARCH
            case NI_AVX_Compare:
            case NI_AVX_CompareScalar:
            case NI_AVX512_CompareMask:
            {
                assert(operands.Length == 3);
                if (operands[2].Oper.IsCnsIntOrI)
                {
                    var mode = unchecked((FloatComparisonMode)operands[2].AsIntConCommon().IntegralValue);
                    var id = HWIntrinsicInfo.lookupIdForFloatComparisonMode(intrinsic, mode, baseType, size);
                    if (id != intrinsic)
                    {
                        result = CompilerInstance.gtNewSimdHWIntrinsicNode(type, id, baseType, size, operands[0], operands[1]);
                    }
                }
                break;
            }
#endif
#if !TARGET_WASM
            case NI_Vector_CreateGeometricSequence:
            {
                assert(operands.Length == 2);
                if (operands[1].Oper.IsConst)
                {
#if TARGET_XARCH
                    var canGenerate = operands[0].Oper.IsConst || (size != 32) || !varTypeIsIntegral(baseType) ||
                        CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2);
#else
                    var canGenerate = !varTypeIsLong(baseType) || operands[0].Oper.IsConst || (size == 8);
#endif
                    if (canGenerate)
                    {
                        result = CompilerInstance.gtNewSimdCreateGeometricSequenceNode(type, operands[0], operands[1], baseType, size);
                    }
                }
                break;
            }
#endif
            case NI_Vector_Shuffle:
            case NI_Vector_ShuffleNative:
            case NI_Vector_ShuffleNativeFallback:
            {
                assert(operands.Length == 2);
                assert((node.Flags & GTF_REVERSE_OPS) == 0);
                var native = intrinsic is not NI_Vector_Shuffle;
                if (CompilerInstance.IsValidForShuffle(operands[1], size, baseType, out _, native))
                {
                    result = CompilerInstance.gtNewSimdShuffleNode(type, operands[0], operands[1], baseType, size, native);
                }
                break;
            }
#if TARGET_XARCH
            case NI_Vector_ExtractMostSignificantBits when size == 16:
            {
                assert(varTypeIsShort(baseType));
                return;
            }
#endif
            default:
            {
                if (signature.numArgs == 0)
                {
                    break;
                }
#if TARGET_XARCH
                var immediate = operands[^1];
                if (!HWIntrinsicInfo.isImmOp(intrinsic, immediate))
                {
                    break;
                }
                if (CompilerInstance.CheckHWIntrinsicImmRange(intrinsic, baseType, immediate, false, 0,
                    HWIntrinsicInfo.lookupImmUpperBound(intrinsic), HWIntrinsicInfo.HasFullRangeImm(intrinsic), out _))
                {
                    node.Flags &= ~(GTF_HW_USER_CALL | GTF_EXCEPT | GTF_CALL);
                    return;
                }
#else
                throw new NotImplementedException("Target-specific hardware immediate rationalization is not ported.");
#endif
                break;
            }
        }

        if (result is not null)
        {
            var first = Compiler.fgGetFirstNode(node);
            var insertionPoint = first.Prev;
            BlockRange.Remove(first, node);
            ReplaceUse(ref use, parents, node, result);
            CompilerInstance.gtSetEvalOrder(result);
            BlockRange.InsertAfter(insertionPoint, new LIR.Range(CompilerInstance.fgSetTreeSeq(result), result));
            return;
        }

        RewriteNodeAsCall(ref use, in signature, parents, method, node.EntryPoint, operands, false);
    }

    private static void ReplaceUse(ref GenTree use, GenTreeStack parents, GenTree original, GenTree replacement)
    {
        if (parents.Count > 1)
        {
            GetParent(parents).ReplaceOperand(ref use, replacement);
        }
        else
        {
            use = replacement;
        }
        assert(parents.Peek() == original);
        _ = parents.Pop();
        parents.Push(replacement);
    }

    private void RewriteHWIntrinsic(ref GenTree use, GenTreeStack parents)
    {
        var node = use.AsHWIntrinsic();
        assert(!node.IsUserCall);
#if TARGET_ARM64
        throw new NotImplementedException("ARM64 hardware rationalization requires mask-reduction rewrites.");
#else
        switch (node.HWIntrinsicId)
        {
#if TARGET_XARCH
            case NI_AVX512_BlendVariableMask:
            {
                RewriteHWIntrinsicBlendv(ref use, parents);
                break;
            }
            case NI_AVX512_ConvertMaskToVector:
            case NI_AVX512_MoveMask:
            {
                RewriteHWIntrinsicMaskOp(ref use, parents);
                break;
            }
#endif
            case NI_Vector_ExtractMostSignificantBits:
            {
                RewriteHWIntrinsicExtractMsb(ref use, parents);
                break;
            }
        }
#endif
    }

#if TARGET_XARCH
    private void RewriteHWIntrinsicBlendv(ref GenTree use, GenTreeStack parents)
    {
        var node = use.AsHWIntrinsic();
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        if (size == 64)
        {
            return;
        }

        var second = node.GetOp(2);
        ref var mask = ref node.GetOpRef(3);
        var effects = new SideEffectSet();
        if (mask.IsConvertVectorToMask)
        {
            var vector = mask.AsHWIntrinsic().GetOp(1);
            if (!vector.IsVectorPerElementMask(CompilerInstance, baseType, size))
            {
                switch (baseType)
                {
                    case TYP_SHORT:
                    case TYP_USHORT:
                    {
                        return;
                    }
                    case TYP_INT:
                    case TYP_UINT:
                    {
                        baseType = TYP_FLOAT;
                        break;
                    }
                    case TYP_LONG:
                    case TYP_ULONG:
                    {
                        baseType = TYP_DOUBLE;
                        break;
                    }
                }
            }
        }
        else if (effects.IsLirInvariantInRange(CompilerInstance, second, node))
        {
            var targetType = TYP_UNDEF;
            if (second.IsEmbeddedMaskingCompatible(CompilerInstance, size / baseType.Size, ref targetType))
            {
                if (targetType is not TYP_UNDEF)
                {
                    second.AsHWIntrinsic().SimdBaseType = targetType;
                }
                return;
            }
        }
        if (!ShouldRewriteToNonMaskHWIntrinsic(mask))
        {
            return;
        }
        parents.Push(mask);
        RewriteHWIntrinsicToNonMask(ref mask, parents);
        _ = parents.Pop();
        var intrinsic = size == 32
            ? (varTypeIsIntegral(baseType) ? NI_AVX2_BlendVariable : NI_AVX_BlendVariable)
            : NI_X86Base_BlendVariable;
        node.SimdBaseType = baseType;
        node.ChangeHWIntrinsicId(intrinsic);
    }

    private void RewriteHWIntrinsicMaskOp(ref GenTree use, GenTreeStack parents)
    {
        var node = use.AsHWIntrinsic();
        var intrinsic = node.HWIntrinsicId;
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        if ((size == 64) || ((intrinsic is NI_AVX512_MoveMask) && varTypeIsShort(baseType)))
        {
            return;
        }
        ref var operand = ref node.GetOpRef(1);
        if (!ShouldRewriteToNonMaskHWIntrinsic(operand))
        {
            return;
        }
        parents.Push(operand);
        RewriteHWIntrinsicToNonMask(ref operand, parents);
        _ = parents.Pop();
        if (intrinsic is NI_AVX512_ConvertMaskToVector)
        {
            ReplaceUse(ref use, parents, node, operand);
            BlockRange.Remove(node);
        }
        else
        {
            assert(intrinsic is NI_AVX512_MoveMask);
            switch (baseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    intrinsic = size == 32 ? NI_AVX2_MoveMask : NI_X86Base_MoveMask;
                    break;
                }
                case TYP_INT:
                case TYP_UINT:
                case TYP_FLOAT:
                {
                    baseType = TYP_FLOAT;
                    intrinsic = size == 32 ? NI_AVX_MoveMask : NI_X86Base_MoveMask;
                    break;
                }
                case TYP_LONG:
                case TYP_ULONG:
                case TYP_DOUBLE:
                {
                    baseType = TYP_DOUBLE;
                    intrinsic = size == 32 ? NI_AVX_MoveMask : NI_X86Base_MoveMask;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
            node.SimdBaseType = baseType;
            node.ChangeHWIntrinsicId(intrinsic);
        }
    }

    private void RewriteHWIntrinsicToNonMask(ref GenTree use, GenTreeStack parents)
    {
        var node = use.AsHWIntrinsic();
        assert(node.Type is TYP_MASK);
        assert(ShouldRewriteToNonMaskHWIntrinsic(node));
        var intrinsic = node.HWIntrinsicId;
        if (intrinsic is NI_AVX512_AndMask or NI_AVX512_AndNotMask or NI_AVX512_NotMask or NI_AVX512_OrMask or NI_AVX512_XorMask)
        {
            var operation = intrinsic switch {
                NI_AVX512_AndMask => GT_AND,
                NI_AVX512_AndNotMask => GT_AND_NOT,
                NI_AVX512_NotMask => GT_NOT,
                NI_AVX512_OrMask => GT_OR,
                _ => GT_XOR,
            };
            RewriteHWIntrinsicBitwiseOpToNonMask(ref use, parents, operation);
            return;
        }
        if (intrinsic is NI_AVX512_ConvertVectorToMask)
        {
            ReplaceUse(ref use, parents, node, node.GetOp(1));
            BlockRange.Remove(node);
            return;
        }

        var wide = node.SimdSize == 32;
        var integral = varTypeIsIntegral(node.SimdBaseType);
        intrinsic = intrinsic switch {
            NI_AVX512_CompareMask => NI_AVX_Compare,
            NI_AVX512_CompareEqualMask => wide ? (integral ? NI_AVX2_CompareEqual : NI_AVX_CompareEqual) : NI_X86Base_CompareEqual,
            NI_AVX512_CompareGreaterThanMask => wide ? (integral ? NI_AVX2_CompareGreaterThan : NI_AVX_CompareGreaterThan) : NI_X86Base_CompareGreaterThan,
            NI_AVX512_CompareLessThanMask => wide ? (integral ? NI_AVX2_CompareLessThan : NI_AVX_CompareLessThan) : NI_X86Base_CompareLessThan,
            NI_AVX512_CompareGreaterThanOrEqualMask => wide ? NI_AVX_CompareGreaterThanOrEqual : NI_X86Base_CompareGreaterThanOrEqual,
            NI_AVX512_CompareLessThanOrEqualMask => wide ? NI_AVX_CompareLessThanOrEqual : NI_X86Base_CompareLessThanOrEqual,
            NI_AVX512_CompareNotEqualMask => wide ? NI_AVX_CompareNotEqual : NI_X86Base_CompareNotEqual,
            NI_AVX512_CompareNotGreaterThanMask => wide ? NI_AVX_CompareNotGreaterThan : NI_X86Base_CompareNotGreaterThan,
            NI_AVX512_CompareNotGreaterThanOrEqualMask => wide ? NI_AVX_CompareNotGreaterThanOrEqual : NI_X86Base_CompareNotGreaterThanOrEqual,
            NI_AVX512_CompareNotLessThanMask => wide ? NI_AVX_CompareNotLessThan : NI_X86Base_CompareNotLessThan,
            NI_AVX512_CompareNotLessThanOrEqualMask => wide ? NI_AVX_CompareNotLessThanOrEqual : NI_X86Base_CompareNotLessThanOrEqual,
            NI_AVX512_CompareOrderedMask => wide ? NI_AVX_CompareOrdered : NI_X86Base_CompareOrdered,
            NI_AVX512_CompareUnorderedMask => wide ? NI_AVX_CompareUnordered : NI_X86Base_CompareUnordered,
            _ => throw new InvalidOperationException("Unexpected mask intrinsic."),
        };
        node.Type = Compiler.GetSimdTypeForSize(node.SimdSize);
        node.ChangeHWIntrinsicId(intrinsic);
    }

    private void RewriteHWIntrinsicBitwiseOpToNonMask(ref GenTree use, GenTreeStack parents, genTreeOps operation)
    {
        var node = use.AsHWIntrinsic();
        assert(node.Operands.Length is 1 or 2);
        assert(node.Type is TYP_MASK);
        var type = Compiler.GetSimdTypeForSize(node.SimdSize);
        ref var first = ref node.GetOpRef(1);
        parents.Push(first);
        RewriteHWIntrinsicToNonMask(ref first, parents);
        _ = parents.Pop();
        if (node.Operands.Length == 1)
        {
            assert(operation is GT_NOT);
            var second = CompilerInstance.gtNewAllBitsSetConNode(type);
            BlockRange.InsertBefore(node, second);
            var id = CompilerInstance.GetHWIntrinsicIdForBinOp(GT_XOR, first, second, node.SimdBaseType, node.SimdSize, false);
            node.Type = type;
            node.ResetHWIntrinsicId(id, first, second);
        }
        else
        {
            ref var second = ref node.GetOpRef(2);
            parents.Push(second);
            RewriteHWIntrinsicToNonMask(ref second, parents);
            _ = parents.Pop();
            var id = CompilerInstance.GetHWIntrinsicIdForBinOp(operation, first, second, node.SimdBaseType, node.SimdSize, false);
            node.Type = type;
            node.ChangeHWIntrinsicId(id);
        }
    }

    private bool ShouldRewriteToNonMaskHWIntrinsic(GenTree tree)
    {
        assert(tree.Type is TYP_MASK);
        if (!tree.Oper.IsHWIntrinsic)
        {
            return false;
        }
        var node = tree.AsHWIntrinsic();
        if (node.SimdSize == 64)
        {
            return false;
        }
        var intrinsic = node.HWIntrinsicId;
        switch (intrinsic)
        {
            case NI_AVX512_AndMask:
            case NI_AVX512_AndNotMask:
            case NI_AVX512_OrMask:
            case NI_AVX512_XorMask:
            {
                assert(node.Operands.Length == 2);
                return ShouldRewriteToNonMaskHWIntrinsic(node.GetOp(1)) && ShouldRewriteToNonMaskHWIntrinsic(node.GetOp(2));
            }
            case NI_AVX512_NotMask:
            {
                assert(node.Operands.Length == 1);
                return ShouldRewriteToNonMaskHWIntrinsic(node.GetOp(1));
            }
            case NI_AVX512_CompareMask:
            case NI_AVX512_CompareEqualMask:
            case NI_AVX512_CompareGreaterThanMask:
            case NI_AVX512_CompareGreaterThanOrEqualMask:
            case NI_AVX512_CompareLessThanMask:
            case NI_AVX512_CompareLessThanOrEqualMask:
            case NI_AVX512_CompareNotEqualMask:
            case NI_AVX512_CompareNotGreaterThanMask:
            case NI_AVX512_CompareNotGreaterThanOrEqualMask:
            case NI_AVX512_CompareNotLessThanMask:
            case NI_AVX512_CompareNotLessThanOrEqualMask:
            case NI_AVX512_CompareOrderedMask:
            case NI_AVX512_CompareUnorderedMask:
            {
                assert(node.Operands.Length is 2 or 3);
                if (varTypeIsFloating(node.SimdBaseType) || (intrinsic is NI_AVX512_CompareEqualMask))
                {
                    return true;
                }
                return !varTypeIsUnsigned(node.SimdBaseType) &&
                    (intrinsic is NI_AVX512_CompareGreaterThanMask or NI_AVX512_CompareLessThanMask);
            }
            case NI_AVX512_ConvertVectorToMask:
            {
                return true;
            }
        }
        return false;
    }
#endif

    private void RewriteHWIntrinsicExtractMsb(ref GenTree use, GenTreeStack parents)
    {
#if TARGET_XARCH
        var node = use.AsHWIntrinsic();
        var baseType = varTypeIsUnsigned(node.SimdBaseType) ? TYP_UBYTE : TYP_BYTE;
        var size = node.SimdSize;
        var type = Compiler.GetSimdTypeForSize(size);
        var first = node.GetOp(1);
        var indices = CompilerInstance.gtNewVconNode(type);
        // Pack the high byte of each 16-bit element into the low eight bytes;
        // pshufb's high index bit zeroes the other eight bytes in each lane.
        indices.SimdVal.u64[0] = 0x0F0D0B0907050301;
        indices.SimdVal.u64[1] = 0x8080808080808080;
        var shuffle = NI_X86Base_Shuffle;
        if (size == 32)
        {
            indices.SimdVal.u64[2] = 0x0F0D0B0907050301;
            indices.SimdVal.u64[3] = 0x8080808080808080;
            shuffle = NI_AVX2_Shuffle;
        }
        BlockRange.InsertAfter(first, indices);
        GenTree temporary = CompilerInstance.gtNewSimdHWIntrinsicNode(type, shuffle, baseType, size, first, indices);
        BlockRange.InsertAfter(indices, temporary);
        first = temporary;
        if (size == 32)
        {
            var otherType = baseType is TYP_UBYTE ? TYP_ULONG : TYP_LONG;
            var control = CompilerInstance.gtNewIconNode(TYP_INT, 0xD8);
            BlockRange.InsertAfter(first, control);
            temporary = CompilerInstance.gtNewSimdHWIntrinsicNode(type, NI_AVX2_Permute4x64, otherType, size, first, control);
            BlockRange.InsertAfter(control, temporary);
            first = temporary;
            temporary = CompilerInstance.gtNewSimdGetLowerNode(TYP_SIMD16, first, baseType, size);
            BlockRange.InsertAfter(first, temporary);
            first = temporary;
            size = 16;
        }
        node.ChangeHWIntrinsicId(NI_X86Base_MoveMask);
        node.SimdSize = size;
        node.SimdBaseType = baseType;
        node.SetOp(1, first);
#else
        throw new NotImplementedException("Target-specific most-significant-bit rationalization is not ported.");
#endif
    }
#endif
}
