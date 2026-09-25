// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe GenTree? LowerHWIntrinsicWithElement(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        var intrinsicId = node.HWIntrinsicId;
        var simdType = node.Type;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;

        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);

        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);

        if (!op2.Oper.IsConst)
        {
            op2 = NormalizeIndexToNativeSized(op2);
            node.SetOp(2, op2);
            ContainCheckHWIntrinsic(node);
            return node.Next;
        }

        // The bounds check guards invalid indices; nevertheless code generation needs
        // a valid immediate. Preserve the native byte truncation before the modulo.
        var elemSize = simdBaseType.Size;
        var count = simdSize / elemSize;
        var imm8 = unchecked((byte)op2.AsIntCon().IconValue) % count;
        var simd16Cnt = 16 / elemSize;
        var simd16Idx = imm8 / simd16Cnt;
        assert((0 <= imm8) && (imm8 < count));

        BlockRange().Remove(op2);
        GenTree idx;
        GenTree tmp1;
        var result = node;

        if (simdType is TYP_SIMD64)
        {
            assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX512));

            // Extract one 128-bit lane from a materialized vector and reinsert the updated
            // lane afterward. The clone belongs before the scalar value, not at the root.
            result = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsicId, simdBaseType, 16, op1, op2, op3);
            BlockRange().InsertBefore(node, result);
            var op1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
            _ = ReplaceWithLclVar(op1Use);
            var tmp64 = node.GetOp(1);
            var clone = CompilerInstance.gtClone(tmp64);
            assert(clone is not null);
            op1 = clone;
            BlockRange().InsertBefore(op3, op1);

            if (simd16Idx == 0)
            {
                tmp1 = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_GetLower128,
                    simdBaseType, simdSize, op1);
                BlockRange().InsertAfter(op1, tmp1);
                _ = LowerNode(tmp1);
            }
            else
            {
                assert((simd16Idx >= 1) && (simd16Idx <= 3));
                imm8 -= simd16Idx * simd16Cnt;
                idx = CompilerInstance.gtNewIconNode(TYP_INT, simd16Idx);
                BlockRange().InsertAfter(op1, idx);
                _ = LowerNode(idx);
                tmp1 = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX512_ExtractVector128,
                    simdBaseType, simdSize, op1, idx);
                BlockRange().InsertAfter(idx, tmp1);
                _ = LowerNode(tmp1);
            }

            op1 = tmp1;
            idx = CompilerInstance.gtNewIconNode(TYP_INT, simd16Idx);
            BlockRange().InsertBefore(node, idx);
            _ = LowerNode(idx);
            node.ResetHWIntrinsicId(NI_AVX512_InsertVector128, tmp64, result, idx);
        }
        else if (simdType is TYP_SIMD32)
        {
            assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX));
            result = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsicId, simdBaseType, 16, op1, op2, op3);
            BlockRange().InsertBefore(node, result);
            var op1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
            _ = ReplaceWithLclVar(op1Use);
            var tmp32 = node.GetOp(1);
            var clone = CompilerInstance.gtClone(tmp32);
            assert(clone is not null);
            op1 = clone;
            BlockRange().InsertBefore(op3, op1);

            if (simd16Idx == 0)
            {
                tmp1 = CompilerInstance.gtNewSimdGetLowerNode(TYP_SIMD16, op1, simdBaseType, simdSize);
                BlockRange().InsertAfter(op1, tmp1);
                _ = LowerNode(tmp1);
            }
            else
            {
                assert(simd16Idx == 1);
                imm8 -= count / 2;
                tmp1 = CompilerInstance.gtNewSimdGetUpperNode(TYP_SIMD16, op1, simdBaseType, simdSize);
                BlockRange().InsertAfter(op1, tmp1);
                _ = LowerNode(tmp1);
            }

            op1 = tmp1;
            node.ResetHWIntrinsicId((simd16Idx == 0) ? NI_Vector_WithLower : NI_Vector_WithUpper, tmp32, result);
        }
        else
        {
            assert(simd16Idx == 0);
        }

        switch (simdBaseType)
        {
            case TYP_LONG:
            case TYP_ULONG:
            {
                assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_X86Base_X64));
                idx = CompilerInstance.gtNewIconNode(TYP_INT, imm8);
                BlockRange().InsertBefore(result, idx);
                result.ChangeHWIntrinsicId(NI_X86Base_X64_Insert, op1, op3, idx);
                break;
            }

            case TYP_FLOAT:
            {
                tmp1 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op3, TYP_FLOAT, 16);
                imm8 *= 16;
                op3 = tmp1;
                goto case TYP_BYTE;
            }

            case TYP_BYTE:
            case TYP_UBYTE:
            case TYP_INT:
            case TYP_UINT:
            case TYP_SHORT:
            case TYP_USHORT:
            {
                idx = CompilerInstance.gtNewIconNode(TYP_INT, imm8);
                BlockRange().InsertBefore(result, idx);
                result.ChangeHWIntrinsicId(NI_X86Base_Insert, op1, op3, idx);
                break;
            }

            case TYP_DOUBLE:
            {
                tmp1 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op3, TYP_DOUBLE, 16);
                result.ResetHWIntrinsicId((imm8 == 0) ? NI_X86Base_MoveScalar : NI_X86Base_UnpackLow, op1, tmp1);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(result.HWIntrinsicId != intrinsicId);
        var nextNode = LowerNode(result);

        if (simdType is TYP_SIMD64)
        {
            assert(node.HWIntrinsicId is NI_AVX512_InsertVector128);
            assert(node != result);
            nextNode = LowerNode(node);
        }
        else if (simdType is TYP_SIMD32)
        {
            assert(node.HWIntrinsicId is NI_Vector_WithLower or NI_Vector_WithUpper);
            assert(node != result);
            nextNode = LowerNode(node);
        }
        else
        {
            assert(node == result);
        }

        return nextNode;
#else
        throw new NotImplementedException("Non-xarch vector WithElement lowering is not ported.");
#endif
    }
}
