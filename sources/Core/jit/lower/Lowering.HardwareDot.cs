// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe GenTree? LowerHWIntrinsicDotInnerMulSum(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        var intrinsicId = node.HWIntrinsicId;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        var simdType = Compiler.GetSimdTypeForSize(simdSize);

        assert(intrinsicId is NI_Vector_Dot);
        assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX));
        assert(varTypeIsSimd(simdType));
        assert(varTypeIsFloating(simdBaseType));
        assert(simdSize is 16 or 32);

        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        GenTree idx;
        GenTree tmp1;
        GenTree? tmp2;
        GenTree? tmp3;

        // CreateScalarUnsafe elision can leave scalar-typed operands here, but Dot still
        // needs a vector multiply, not the scalar broadcast performed by gtNewSimdBinOpNode.
        var multiply = CompilerInstance.GetHWIntrinsicIdForBinOp(GT_MUL, op1, op2, simdBaseType, simdSize, isScalar: false);
        tmp1 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, multiply, simdBaseType, simdSize, op1, op2);
        BlockRange().InsertBefore(node, tmp1);
        _ = LowerNode(tmp1);

        switch (simdBaseType)
        {
            case TYP_FLOAT:
            {
                // Sum adjacent products, then pairs of pairs within each 128-bit lane.
                // Preserve the native addition operand order, including its reversal
                // between the first and second passes.
                node.SetOp(1, tmp1);
                var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                _ = ReplaceWithLclVar(tmp1Use);
                tmp1 = node.GetOp(1);

                tmp3 = CompilerInstance.gtClone(tmp1);
                assert(tmp3 is not null);
                BlockRange().InsertAfter(tmp1, tmp3);
                idx = CompilerInstance.gtNewIconNode(TYP_INT, 0xB1);
                BlockRange().InsertAfter(tmp3, idx);
                tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AVX_Permute, simdBaseType, simdSize, tmp1, idx);
                BlockRange().InsertAfter(idx, tmp2);
                _ = LowerNode(tmp2);

                tmp1 = CompilerInstance.gtNewSimdBinOpNode(GT_ADD, simdType, tmp2, tmp3, simdBaseType, simdSize);
                BlockRange().InsertAfter(tmp2, tmp1);
                _ = LowerNode(tmp1);

                node.SetOp(1, tmp1);
                var tmp1Use2 = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                _ = ReplaceWithLclVar(tmp1Use2);
                tmp1 = node.GetOp(1);
                tmp3 = CompilerInstance.gtClone(tmp1);
                assert(tmp3 is not null);
                BlockRange().InsertAfter(tmp1, tmp3);
                idx = CompilerInstance.gtNewIconNode(TYP_INT, 0x4E);
                BlockRange().InsertAfter(tmp3, idx);
                tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AVX_Permute, simdBaseType, simdSize, tmp1, idx);
                BlockRange().InsertAfter(idx, tmp2);
                _ = LowerNode(tmp2);

                tmp1 = CompilerInstance.gtNewSimdBinOpNode(GT_ADD, simdType, tmp3, tmp2, simdBaseType, simdSize);
                BlockRange().InsertAfter(tmp2, tmp1);
                break;
            }

            case TYP_DOUBLE:
            {
                idx = CompilerInstance.gtNewIconNode(TYP_INT, simdSize == 32 ? 0x5 : 0x1);
                BlockRange().InsertAfter(tmp1, idx);

                node.SetOp(1, tmp1);
                var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                _ = ReplaceWithLclVar(tmp1Use);
                tmp1 = node.GetOp(1);
                tmp2 = CompilerInstance.gtClone(tmp1);
                assert(tmp2 is not null);
                BlockRange().InsertAfter(idx, tmp2);

                tmp1 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AVX_Permute, simdBaseType, simdSize, tmp1, idx);
                BlockRange().InsertAfter(tmp2, tmp1);
                _ = LowerNode(tmp1);
                tmp3 = CompilerInstance.gtNewSimdBinOpNode(GT_ADD, simdType, tmp1, tmp2, simdBaseType, simdSize);
                BlockRange().InsertAfter(tmp1, tmp3);
                tmp1 = tmp3;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        if (simdSize == 32)
        {
            node.SetOp(1, tmp1);
            var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
            _ = ReplaceWithLclVar(tmp1Use);
            tmp1 = node.GetOp(1);
            tmp2 = CompilerInstance.gtClone(tmp1);
            assert(tmp2 is not null);
            BlockRange().InsertAfter(tmp1, tmp2);
            tmp3 = CompilerInstance.gtClone(tmp2);
            assert(tmp3 is not null);
            BlockRange().InsertAfter(tmp2, tmp3);
            idx = CompilerInstance.gtNewIconNode(TYP_INT, 0x01);
            BlockRange().InsertAfter(tmp3, idx);
            tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_AVX_Permute2x128,
                simdBaseType, simdSize, tmp2, tmp3, idx);
            BlockRange().InsertAfter(idx, tmp2);
            _ = LowerNode(tmp2);

            tmp1 = CompilerInstance.gtNewSimdBinOpNode(GT_ADD, simdType, tmp1, tmp2, simdBaseType, simdSize);
            BlockRange().InsertAfter(tmp2, tmp1);
        }

        if (!varTypeIsSimd(node.Type))
        {
            // Broadcast the result even for scalar consumers: partial writes would limit CSE.
            _ = LowerNode(tmp1);
            tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(node.Type, NI_Vector_ToScalar, simdBaseType, simdSize, tmp1);
            BlockRange().InsertAfter(tmp1, tmp2);
            tmp1 = tmp2;
        }

        var foundUse = BlockRange().TryGetUse(node, out var use);
        assert(foundUse);
        use.ReplaceWith(tmp1);
        BlockRange().Remove(node);
        return LowerNode(tmp1);
#else
        throw new NotImplementedException("Non-xarch Dot multiply/sum lowering is not ported.");
#endif
    }

    private unsafe GenTree? LowerHWIntrinsicDot(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        var intrinsicId = node.HWIntrinsicId;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        var simdType = Compiler.GetSimdTypeForSize(simdSize);
        var simd16Count = 16 / simdBaseType.Size;

        assert(intrinsicId is NI_Vector_Dot);
        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);
        assert(varTypeIsSimd(node.Type));

        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        GenTree idx;
        GenTree tmp1;
        GenTree? tmp2;
        GenTree? tmp3;
        var horizontalAdd = NI_Illegal;
        var shuffle = NI_Illegal;

        if (varTypeIsFloating(simdBaseType) && (simdSize is 16 or 32) &&
            CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX))
        {
            if (BlockRange().TryGetUse(node, out _))
            {
                return LowerHWIntrinsicDotInnerMulSum(node);
            }

            return node.Next;
        }

        if (simdSize == 32)
        {
            switch (simdBaseType)
            {
                case TYP_SHORT:
                case TYP_USHORT:
                case TYP_INT:
                case TYP_UINT:
                {
                    assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX2));
                    horizontalAdd = NI_AVX2_HorizontalAdd;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
        else
        {
            switch (simdBaseType)
            {
                case TYP_SHORT:
                case TYP_USHORT:
                case TYP_INT:
                case TYP_UINT:
                {
                    horizontalAdd = NI_X86Base_HorizontalAdd;
                    break;
                }

                case TYP_FLOAT:
                {
                    assert(simdSize == 16);
                    idx = CompilerInstance.gtNewIconNode(TYP_INT, 0xFF);
                    BlockRange().InsertBefore(node, idx);
                    if (varTypeIsSimd(node.Type))
                    {
                        node.ResetHWIntrinsicId(NI_X86Base_DotProduct, op1, op2, idx);
                    }
                    else
                    {
                        // Keep the result broadcast for CSE rather than introduce a partial write.
                        tmp3 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_X86Base_DotProduct,
                            simdBaseType, simdSize, op1, op2, idx);
                        BlockRange().InsertAfter(idx, tmp3);
                        _ = LowerNode(tmp3);
                        node.ResetHWIntrinsicId(NI_Vector_ToScalar, tmp3);
                    }

                    return LowerNode(node);
                }

                case TYP_DOUBLE:
                {
                    idx = CompilerInstance.gtNewIconNode(TYP_INT, 0x33);
                    BlockRange().InsertBefore(node, idx);
                    if (varTypeIsSimd(node.Type))
                    {
                        node.ResetHWIntrinsicId(NI_X86Base_DotProduct, op1, op2, idx);
                    }
                    else
                    {
                        tmp3 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_X86Base_DotProduct,
                            simdBaseType, simdSize, op1, op2, idx);
                        BlockRange().InsertAfter(idx, tmp3);
                        _ = LowerNode(tmp3);
                        node.ResetHWIntrinsicId(NI_Vector_ToScalar, tmp3);
                    }

                    return LowerNode(node);
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }

        tmp1 = CompilerInstance.gtNewSimdBinOpNode(GT_MUL, simdType, op1, op2, simdBaseType, simdSize);
        BlockRange().InsertBefore(node, tmp1);
        _ = LowerNode(tmp1);

        // HorizontalAdd combines pairs, requiring log2(elements per 128-bit lane) passes.
        var haddCount = BitOperations.Log2((uint)simd16Count);
        for (var i = 0; i < haddCount; i++)
        {
            node.SetOp(1, tmp1);
            var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
            _ = ReplaceWithLclVar(tmp1Use);
            tmp1 = node.GetOp(1);
            tmp2 = CompilerInstance.gtClone(tmp1);
            assert(tmp2 is not null);
            BlockRange().InsertAfter(tmp1, tmp2);

#pragma warning disable CA1508 // Retain the native shuffle fallback even though current selection always uses HorizontalAdd.
            if (shuffle is NI_Illegal)
            {
                tmp1 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, horizontalAdd, simdBaseType, simdSize, tmp1, tmp2);
            }
            else
            {
                var shuffleConst = 0x00;
                switch (i)
                {
                    case 0:
                    {
                        assert((simdBaseType is TYP_SHORT or TYP_USHORT) || varTypeIsFloating(simdBaseType));
                        // Exchange adjacent elements within each group of four.
                        shuffleConst = 0xB1;
                        break;
                    }

                    case 1:
                    {
                        assert(simdBaseType is TYP_SHORT or TYP_USHORT or TYP_FLOAT);
                        // Exchange pairs within each group of four.
                        shuffleConst = 0x4E;
                        break;
                    }

                    case 2:
                    {
                        assert(simdBaseType is TYP_SHORT or TYP_USHORT);
                        // Exchange the lower and upper groups of four 16-bit elements.
                        shuffleConst = 0x4E;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                idx = CompilerInstance.gtNewIconNode(TYP_INT, shuffleConst);
                BlockRange().InsertAfter(tmp2, idx);
                if (varTypeIsFloating(simdBaseType))
                {
                    node.SetOp(1, tmp2);
                    var tmp2Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                    _ = ReplaceWithLclVar(tmp2Use);
                    tmp2 = node.GetOp(1);
                    tmp3 = CompilerInstance.gtClone(tmp2);
                    assert(tmp3 is not null);
                    BlockRange().InsertAfter(tmp2, tmp3);
                    tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, shuffle, simdBaseType, simdSize, tmp2, tmp3, idx);
                }
                else
                {
                    assert(simdBaseType is TYP_SHORT or TYP_USHORT);
                    if (i < 2)
                    {
                        tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_X86Base_ShuffleLow,
                            simdBaseType, simdSize, tmp2, idx);
                        BlockRange().InsertAfter(idx, tmp2);
                        _ = LowerNode(tmp2);
                        idx = CompilerInstance.gtNewIconNode(TYP_INT, shuffleConst);
                        BlockRange().InsertAfter(tmp2, idx);
                        tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_X86Base_ShuffleHigh,
                            simdBaseType, simdSize, tmp2, idx);
                    }
                    else
                    {
                        assert(i == 2);
                        tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_X86Base_Shuffle,
                            TYP_INT, simdSize, tmp2, idx);
                    }
                }

                BlockRange().InsertAfter(idx, tmp2);
                _ = LowerNode(tmp2);
                tmp1 = CompilerInstance.gtNewSimdBinOpNode(GT_ADD, simdType, tmp1, tmp2, simdBaseType, simdSize);
            }

#pragma warning restore CA1508
            BlockRange().InsertAfter(tmp2, tmp1);
            _ = LowerNode(tmp1);
        }

        if (simdSize == 32)
        {
            assert(simdBaseType is not TYP_FLOAT);
            node.SetOp(1, tmp1);
            var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
            _ = ReplaceWithLclVar(tmp1Use);
            tmp1 = node.GetOp(1);
            tmp2 = CompilerInstance.gtClone(tmp1);
            assert(tmp2 is not null);
            BlockRange().InsertAfter(tmp1, tmp2);
            tmp3 = CompilerInstance.gtClone(tmp2);
            assert(tmp3 is not null);
            BlockRange().InsertAfter(tmp2, tmp3);
            idx = CompilerInstance.gtNewIconNode(TYP_INT, 0x01);
            BlockRange().InsertAfter(tmp3, idx);

            var permute2x128 = simdBaseType is TYP_DOUBLE ? NI_AVX_Permute2x128 : NI_AVX2_Permute2x128;
            tmp2 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, permute2x128, simdBaseType, simdSize, tmp2, tmp3, idx);
            BlockRange().InsertAfter(idx, tmp2);
            _ = LowerNode(tmp2);
            tmp1 = CompilerInstance.gtNewSimdBinOpNode(GT_ADD, simdType, tmp1, tmp2, simdBaseType, simdSize);
            BlockRange().InsertAfter(tmp2, tmp1);
            _ = LowerNode(tmp1);
        }

        if (BlockRange().TryGetUse(node, out var use))
        {
            use.ReplaceWith(tmp1);
        }
        else
        {
            tmp1.IsUnusedValue = true;
        }

        BlockRange().Remove(node);
        return tmp1.Next;
#else
        throw new NotImplementedException("Non-xarch vector Dot lowering is not ported.");
#endif
    }
}
