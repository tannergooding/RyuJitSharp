// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerHWIntrinsicCreateArm64(GenTreeHWIntrinsic node)
    {
        var id = node.HWIntrinsicId;
        var simdType = node.Type;
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        simd_t value = default;
        if ((size == 8) && (simdType is TYP_DOUBLE))
        {
            simdType = TYP_SIMD8;
        }

        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(baseType));
        assert(size != 0);
        var constant = GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value);
        var scalar = HWIntrinsicInfo.IsVectorCreateScalar(id);
        var count = node.Operands.Length;
        if (constant && (count == 1) && IsValidConstForMovImm(node))
        {
            constant = false;
        }

        if (constant)
        {
            assert(size is 8 or 16);
            foreach (var operand in node.Operands)
            {
                BlockRange().Remove(operand);
            }

            var vector = CompilerInstance.gtNewVconNode(simdType);
            vector.SimdVal = value;
            BlockRange().InsertBefore(node, vector);
            if (BlockRange().TryGetUse(node, out var use))
            {
                use.ReplaceWith(vector);
            }
            else
            {
                vector.IsUnusedValue = true;
            }

            BlockRange().Remove(node);
            return LowerNode(vector);
        }
        else if (count == 1)
        {
            if (scalar)
            {
                var operand = node.GetOp(1);
                var zero = CompilerInstance.gtNewZeroConNode(simdType);
                BlockRange().InsertBefore(operand, zero);
                _ = LowerNode(zero);
                var index = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                BlockRange().InsertAfter(zero, index);
                _ = LowerNode(index);
                node.ResetHWIntrinsicId(NI_AdvSimd_Insert, zero, index, operand);
                return LowerNode(node);
            }

            if (varTypeIsLong(baseType) || (baseType is TYP_DOUBLE))
            {
                id = simdType is TYP_SIMD8 ? NI_AdvSimd_Arm64_DuplicateToVector64 : NI_AdvSimd_Arm64_DuplicateToVector128;
            }
            else
            {
                id = simdType is TYP_SIMD8 ? NI_AdvSimd_DuplicateToVector64 : NI_AdvSimd_DuplicateToVector128;
            }

            node.ChangeHWIntrinsicId(id);
            return LowerNode(node);
        }

        var current = InsertNewSimdCreateScalarUnsafeNode(simdType, node.GetOp(1), baseType, size);
        for (var index = 1; index < count - 1; index++)
        {
            var operand = node.GetOp(index + 1);
            var insertionPoint = LIR.LastNode(current, operand);
            var position = CompilerInstance.gtNewIconNode(TYP_INT, index);
            current = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AdvSimd_Insert,
                baseType, size, current, position, operand);
            BlockRange().InsertAfter(insertionPoint, position, current);
            _ = LowerNode(current);
        }

        var last = node.GetOp(count);
        var lastIndex = CompilerInstance.gtNewIconNode(TYP_INT, count - 1);
        BlockRange().InsertBefore(last, lastIndex);
        node.ResetHWIntrinsicId(NI_AdvSimd_Insert, current, lastIndex, last);
        return LowerNode(node);
    }

    private GenTree? LowerHWIntrinsicCmpOpArm64(GenTreeHWIntrinsic node, genTreeOps cmpOp)
    {
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        var simdType = Compiler.GetSimdTypeForSize(size);
        assert(node.HWIntrinsicId is NI_Vector_op_Equality or NI_Vector_op_Inequality);
        assert(varTypeIsSimd(simdType) && varTypeIsArithmetic(baseType));
        assert((size != 0) && (node.Type is TYP_INT));
        assert(cmpOp is GT_EQ or GT_NE);

        var first = node.GetOp(1);
        var second = node.GetOp(2);
        var zero = first.IsVectorZero ? first : second.IsVectorZero ? second : null;
        GenTree comparison;
        var compareType = TYP_LONG;
        var expectedBits = (nint)(-1);

        if (!varTypeIsFloating(baseType) && (zero is not null))
        {
            comparison = zero == first ? second : first;
            if (size != 8)
            {
                node.SetOp(1, comparison);
                _ = ReplaceWithLclVar(new LIR.Use(BlockRange(), ref node.GetOpRef(1), node));
                var value = node.GetOp(1);
                var clone = CompilerInstance.gtClone(value) ??
                    throw new InvalidOperationException("ARM64 comparison local could not be cloned.");
                BlockRange().InsertAfter(value, clone);
                comparison = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AdvSimd_Arm64_MaxPairwise,
                    TYP_UINT, size, value, clone);
                BlockRange().InsertBefore(node, comparison);
                _ = LowerNode(comparison);
            }

            BlockRange().Remove(zero);
            compareType = TYP_INT;
            expectedBits = 0;
        }
        else
        {
            var id = baseType switch {
                TYP_BYTE or TYP_UBYTE or TYP_SHORT or TYP_USHORT or TYP_INT or TYP_UINT or TYP_FLOAT =>
                    NI_AdvSimd_CompareEqual,
                TYP_LONG or TYP_ULONG or TYP_DOUBLE =>
                    size == 8 ? NI_AdvSimd_Arm64_CompareEqualScalar : NI_AdvSimd_Arm64_CompareEqual,
                _ => throw new InvalidOperationException($"Unexpected ARM64 vector comparison base type: {baseType}."),
            };
            comparison = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, id, baseType, size, first, second);
            BlockRange().InsertBefore(node, comparison);
            _ = LowerNode(comparison);

            if (size != 8)
            {
                node.SetOp(1, comparison);
                _ = ReplaceWithLclVar(new LIR.Use(BlockRange(), ref node.GetOpRef(1), node));
                var value = node.GetOp(1);
                var clone = CompilerInstance.gtClone(value) ??
                    throw new InvalidOperationException("ARM64 comparison local could not be cloned.");
                BlockRange().InsertAfter(value, clone);
                comparison = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AdvSimd_Arm64_MinPairwise,
                    TYP_UINT, size, value, clone);
                BlockRange().InsertAfter(clone, comparison);
                _ = LowerNode(comparison);
            }
        }

        var index = CompilerInstance.gtNewIconNode(TYP_INT, 0);
        BlockRange().InsertAfter(comparison, index);
        var extracted = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_LONG, NI_AdvSimd_Extract,
            TYP_ULONG, size, comparison, index);
        BlockRange().InsertAfter(index, extracted);
        _ = LowerNode(extracted);
        var expected = CompilerInstance.gtNewIconNode(TYP_LONG, expectedBits);
        BlockRange().InsertAfter(extracted, expected);

        var compare = new GenTreeOp(cmpOp, compareType, extracted, expected, node, NodeThreading.LIR) {
            Flags = node.Flags,
        };
        BlockRange().ReplaceNode(node, compare);
        _ = LowerNodeCC(compare, new GenCondition(cmpOp is GT_EQ ? GenCondition.EQ : GenCondition.NE));
        compare.Type = TYP_VOID;
        compare.IsUnusedValue = false;
        _ = LowerNode(compare);
        return compare.Next;
    }

    private GenTree? LowerHWIntrinsicDotArm64(GenTreeHWIntrinsic node)
    {
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        var simdType = Compiler.GetSimdTypeForSize(size);
        assert(node.HWIntrinsicId is NI_Vector_Dot);
        assert(varTypeIsSimd(simdType) && varTypeIsArithmetic(baseType));
        assert((size != 0) && varTypeIsSimd(node.Type));

        var needsBroadcast = BlockRange().TryGetUse(node, out var use) &&
            !(use.User().Oper.IsHWIntrinsic && (use.User().AsHWIntrinsic().HWIntrinsicId is NI_Vector_ToScalar));
        var multiplyId = baseType is TYP_DOUBLE
            ? size == 8 ? NI_AdvSimd_MultiplyScalar : NI_AdvSimd_Arm64_Multiply
            : NI_AdvSimd_Multiply;
        assert(!varTypeIsLong(baseType));

        GenTree current = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, multiplyId, baseType,
            size, node.GetOp(1), node.GetOp(2));
        BlockRange().InsertBefore(node, current);
        _ = LowerNode(current);

        GenTree DuplicateCurrent()
        {
            node.SetOp(1, current);
            _ = ReplaceWithLclVar(new LIR.Use(BlockRange(), ref node.GetOpRef(1), node));
            current = node.GetOp(1);
            var duplicate = CompilerInstance.gtClone(current) ??
                throw new InvalidOperationException("ARM64 dot-product local could not be cloned.");
            BlockRange().InsertAfter(current, duplicate);
            return duplicate;
        }

        if (varTypeIsFloating(baseType))
        {
            if ((size != 8) || (baseType is TYP_FLOAT))
            {
                var duplicate = DuplicateCurrent();
                var pairwise = size == 8 ? NI_AdvSimd_AddPairwise : NI_AdvSimd_Arm64_AddPairwise;
                current = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, pairwise, baseType, size, current, duplicate);
                BlockRange().InsertAfter(duplicate, current);
                _ = LowerNode(current);

                if ((size == 16) && (baseType is TYP_FLOAT))
                {
                    // The first reduction yields <a+b,c+d,a+b,c+d>; the second sums all four lanes.
                    duplicate = DuplicateCurrent();
                    current = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AdvSimd_Arm64_AddPairwise,
                        baseType, size, current, duplicate);
                    BlockRange().InsertAfter(duplicate, current);
                    _ = LowerNode(current);
                }
            }
        }
        else if ((size == 8) && (baseType is TYP_INT or TYP_UINT))
        {
            var duplicate = DuplicateCurrent();
            current = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AdvSimd_AddPairwise,
                baseType, size, current, duplicate);
            BlockRange().InsertAfter(duplicate, current);
            _ = LowerNode(current);
        }
        else
        {
            assert(varTypeIsIntegral(baseType));
            var sum = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD8, NI_AdvSimd_Arm64_AddAcross,
                baseType, size, current);
            BlockRange().InsertAfter(current, sum);
            _ = LowerNode(sum);
            current = sum;
            if (needsBroadcast)
            {
                var index = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                BlockRange().InsertAfter(current, index);
                var duplicateId = size == 8
                    ? NI_AdvSimd_DuplicateSelectedScalarToVector64 : NI_AdvSimd_DuplicateSelectedScalarToVector128;
                current = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, duplicateId, baseType,
                    (byte)current.Type.Size, current, index);
                BlockRange().InsertAfter(index, current);
                _ = LowerNode(current);
            }
        }

        if (BlockRange().TryGetUse(node, out use))
        {
            use.ReplaceWith(current);
        }
        else
        {
            current.IsUnusedValue = true;
        }

        BlockRange().Remove(node);
        return current.Next;
    }
}
#endif
