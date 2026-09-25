// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe GenTree? LowerHWIntrinsicCreate(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        var intrinsicId = node.HWIntrinsicId;
        var simdType = node.Type;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        simd_t simdVal = default;

        if ((simdSize == 8) && (simdType is TYP_DOUBLE))
        {
            // Struct retyping can leave the wrong type here.
            simdType = TYP_SIMD8;
        }

        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);

        var op1 = node.GetOp(1);
        var isConstant = GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref simdVal);
        var isCreateScalar = HWIntrinsicInfo.IsVectorCreateScalar(intrinsicId);
        var argCnt = node.Operands.Length;

        if (isConstant)
        {
            assert(simdSize is 16 or 32 or 64);
            foreach (var arg in node.Operands)
            {
#if !TARGET_64BIT
                if (arg.Oper.IsLong)
                {
                    BlockRange().Remove(arg.AsOp().Op1);
                    assert(arg.AsOp().Op2 is not null);
                    BlockRange().Remove(arg.AsOp().Op2);
                }
#endif
                BlockRange().Remove(arg);
            }

            var vecCon = CompilerInstance.gtNewVconNode(simdType);
            vecCon.SimdVal = simdVal;
            BlockRange().InsertBefore(node, vecCon);
            if (BlockRange().TryGetUse(node, out var use))
            {
                use.ReplaceWith(vecCon);
            }
            else
            {
                vecCon.IsUnusedValue = true;
            }

            BlockRange().Remove(node);
            return vecCon.Next;
        }

        GenTree tmp1;
        GenTree? tmp2;
        GenTree tmp3;
        GenTree idx;

        if (argCnt == 1)
        {
            if (isCreateScalar)
            {
                switch (simdBaseType)
                {
                    case TYP_BYTE:
                    case TYP_UBYTE:
                    case TYP_SHORT:
                    case TYP_USHORT:
                    {
                        // The smallest zeroing scalar SIMD load is 32 bits. Retype a same-width
                        // non-overflowing cast or memory load when safe; otherwise zero-extend
                        // explicitly so signed small values do not set the upper scalar bits.
                        node.SimdBaseType = TYP_INT;
                        var unsignedType = varTypeToUnsigned(simdBaseType);
                        if ((op1.Oper is GT_CAST) && !op1.HasOverflowCheck && !op1.AsCast().CastOp.IsContained &&
                            (op1.AsCast().CastType.Size == simdBaseType.Size))
                        {
                            op1.AsCast().CastType = unsignedType;
                        }
                        else if ((op1.Oper is GT_IND or GT_LCL_FLD) && (op1.Type.Size == simdBaseType.Size))
                        {
                            op1.Type = unsignedType;
                        }
                        else if ((op1.Oper is not GT_CAST) || (op1.AsCast().CastType != unsignedType))
                        {
                            tmp1 = CompilerInstance.gtNewCastNode(TYP_INT, op1, false, unsignedType);
                            node.SetOp(1, tmp1);
                            BlockRange().InsertAfter(op1, tmp1);
                            _ = LowerNode(tmp1);
                        }

                        break;
                    }
                }

                ContainCheckHWIntrinsic(node);
                return node.Next;
            }

            assert(intrinsicId is NI_Vector_Create);

            if (simdType is TYP_SIMD64)
            {
                assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX512));
                tmp1 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op1, simdBaseType, 16);
                node.ResetHWIntrinsicId(NI_AVX512_BroadcastScalarToVector512, tmp1);
                return LowerNode(node);
            }

            if (simdType is TYP_SIMD32)
            {
                if (CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    tmp1 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op1, simdBaseType, 16);
                    node.ResetHWIntrinsicId(NI_AVX2_BroadcastScalarToVector256, tmp1);
                    return LowerNode(node);
                }

                assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX));

                tmp1 = CompilerInstance.gtNewSimdCreateBroadcastNode(TYP_SIMD16, op1, simdBaseType, 16);
                BlockRange().InsertAfter(op1, tmp1);
                node.SetOp(1, tmp1);
                _ = LowerNode(tmp1);

                var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                _ = ReplaceWithLclVar(tmp1Use);
                tmp1 = node.GetOp(1);
                tmp2 = CompilerInstance.gtClone(tmp1);
                assert(tmp2 is not null);
                BlockRange().InsertAfter(tmp1, tmp2);

                tmp3 = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD32, NI_Vector_ToVector256Unsafe,
                    simdBaseType, 16, tmp2);
                BlockRange().InsertAfter(tmp2, tmp3);
                node.ResetHWIntrinsicId(NI_Vector_WithUpper, tmp3, tmp1);
                _ = LowerNode(tmp3);
                return LowerNode(node);
            }

            assert(simdType is TYP_SIMD16);
            tmp1 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op1, simdBaseType, 16);

            if ((simdBaseType is not TYP_DOUBLE) && CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
            {
                node.ChangeHWIntrinsicId(NI_AVX2_BroadcastScalarToVector128, tmp1);
                return LowerNode(node);
            }

            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    tmp2 = CompilerInstance.gtNewZeroConNode(simdType);
                    BlockRange().InsertAfter(tmp1, tmp2);
                    _ = LowerNode(tmp2);
                    node.ResetHWIntrinsicId(NI_X86Base_Shuffle, tmp1, tmp2);
                    break;
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    node.SetOp(1, tmp1);
                    var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                    _ = ReplaceWithLclVar(tmp1Use);
                    tmp1 = node.GetOp(1);
                    tmp2 = CompilerInstance.gtClone(tmp1);
                    assert(tmp2 is not null);
                    BlockRange().InsertAfter(tmp1, tmp2);

                    tmp1 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_X86Base_UnpackLow,
                        TYP_USHORT, simdSize, tmp1, tmp2);
                    BlockRange().InsertAfter(tmp2, tmp1);
                    _ = LowerNode(tmp1);
                    goto case TYP_INT;
                }

                case TYP_INT:
                case TYP_UINT:
                {
                    idx = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                    BlockRange().InsertAfter(tmp1, idx);
                    node.ResetHWIntrinsicId(NI_X86Base_Shuffle, tmp1, idx);
                    node.SimdBaseType = TYP_UINT;
                    break;
                }

                case TYP_FLOAT:
                {
                    if (CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX))
                    {
                        idx = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                        BlockRange().InsertAfter(tmp1, idx);
                        node.ResetHWIntrinsicId(NI_AVX_Permute, tmp1, idx);
                        break;
                    }

                    node.SetOp(1, tmp1);
                    var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                    _ = ReplaceWithLclVar(tmp1Use);
                    tmp1 = node.GetOp(1);
                    tmp2 = CompilerInstance.gtClone(tmp1);
                    assert(tmp2 is not null);
                    BlockRange().InsertAfter(tmp1, tmp2);
                    idx = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                    BlockRange().InsertAfter(tmp2, idx);
                    node.ResetHWIntrinsicId(NI_X86Base_Shuffle, tmp1, tmp2, idx);
                    break;
                }

                case TYP_LONG:
                case TYP_ULONG:
                case TYP_DOUBLE:
                {
                    if (IsContainableMemoryOp(op1) || (simdBaseType is TYP_DOUBLE))
                    {
                        node.ChangeHWIntrinsicId(NI_X86Base_MoveAndDuplicate, tmp1);
                        node.SimdBaseType = TYP_DOUBLE;
                        break;
                    }

                    node.SetOp(1, tmp1);
                    var tmp1Use = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                    _ = ReplaceWithLclVar(tmp1Use);
                    tmp1 = node.GetOp(1);
                    tmp2 = CompilerInstance.gtClone(tmp1);
                    assert(tmp2 is not null);
                    BlockRange().InsertAfter(tmp1, tmp2);
                    node.ResetHWIntrinsicId(NI_X86Base_UnpackLow, tmp1, tmp2);
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            return LowerNode(node);
        }

        assert(intrinsicId is NI_Vector_Create);

        if (simdType is TYP_SIMD32 or TYP_SIMD64)
        {
            assert(argCnt >= (simdSize / TYP_LONG.Size));
            assert(((simdSize == 64) && CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX512)) ||
                ((simdSize == 32) && CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_AVX)));

            // Split into two smaller Creates, placing each after its latest operand rather
            // than after its last argument: argument order need not be LIR execution order.
            var halfSize = (byte)(simdSize / 2);
            var halfType = Compiler.GetSimdTypeForSize(halfSize);
            var halfArgCnt = argCnt / 2;
            assert((halfArgCnt * 2) == argCnt);

            var loOperands = node.Operands[..halfArgCnt].ToArray();
            var hiOperands = node.Operands[halfArgCnt..].ToArray();
            var loInsertionPoint = LIR.LastNode(loOperands);
            var hiInsertionPoint = LIR.LastNode(hiOperands);
            var lo = CompilerInstance.gtNewSimdHWIntrinsicNode(halfType, NI_Vector_Create, simdBaseType, halfSize, loOperands);
            var hi = CompilerInstance.gtNewSimdHWIntrinsicNode(halfType, NI_Vector_Create, simdBaseType, halfSize, hiOperands);
            node.ResetHWIntrinsicId(NI_Vector_WithUpper, lo, hi);
            BlockRange().InsertAfter(loInsertionPoint, lo);
            BlockRange().InsertAfter(hiInsertionPoint, hi);
            _ = LowerNode(lo);
            _ = LowerNode(hi);
            return LowerNode(node);
        }

        assert(simdType is TYP_SIMD16);
        tmp1 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op1, simdBaseType, 16);

        switch (simdBaseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            case TYP_SHORT:
            case TYP_USHORT:
            case TYP_INT:
            case TYP_UINT:
            {
                var insIntrinsic = NI_X86Base_Insert;
                for (var n = 1; n < argCnt - 1; n++)
                {
                    var opN = node.GetOp(n + 1);
                    idx = CompilerInstance.gtNewIconNode(TYP_INT, n);
                    var insertionPoint = LIR.LastNode(tmp1, opN);
                    tmp1 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, insIntrinsic,
                        simdBaseType, simdSize, tmp1, opN, idx);
                    BlockRange().InsertAfter(insertionPoint, idx, tmp1);
                    _ = LowerNode(tmp1);
                }

                var lastOp = node.GetOp(argCnt);
                idx = CompilerInstance.gtNewIconNode(TYP_INT, argCnt - 1);
                BlockRange().InsertAfter(lastOp, idx);
                node.ResetHWIntrinsicId(insIntrinsic, tmp1, lastOp, idx);
                break;
            }

            case TYP_FLOAT:
            {
                assert(argCnt <= 4);
                var insertedNodes = new GenTree[4];
                for (var n = 1; n < argCnt - 1; n++)
                {
                    var opN = node.GetOp(n + 1);
                    tmp2 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, opN, simdBaseType, 16);
                    idx = CompilerInstance.gtNewIconNode(TYP_INT, n << 4);
                    var insertionPoint = LIR.LastNode(tmp1, tmp2);
                    tmp3 = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, NI_X86Base_Insert,
                        simdBaseType, simdSize, tmp1, tmp2, idx);
                    BlockRange().InsertAfter(insertionPoint, idx, tmp3);
                    insertedNodes[n] = tmp3;
                    tmp1 = tmp3;
                }

                tmp2 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, node.GetOp(argCnt), simdBaseType, 16);
                idx = CompilerInstance.gtNewIconNode(TYP_INT, (argCnt - 1) << 4);
                BlockRange().InsertAfter(tmp2, idx);
                node.ResetHWIntrinsicId(NI_X86Base_Insert, tmp1, tmp2, idx);

                // Insert lowering can merge neighboring inserts or remove them for zeros,
                // constants and special masks. Complete the dependent chain before lowering it.
                for (var n = 1; n < argCnt - 1; n++)
                {
                    _ = LowerNode(insertedNodes[n]);
                }

                break;
            }

            case TYP_LONG:
            case TYP_ULONG:
            case TYP_DOUBLE:
            {
                var op2 = node.GetOp(2);
                if (varTypeIsLong(simdBaseType) && CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_X86Base_X64))
                {
                    idx = CompilerInstance.gtNewIconNode(TYP_INT, 1);
                    BlockRange().InsertBefore(node, idx);
                    node.ResetHWIntrinsicId(NI_X86Base_X64_Insert, tmp1, op2, idx);
                    break;
                }

                tmp2 = InsertNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op2, simdBaseType, 16);
                node.ResetHWIntrinsicId(NI_X86Base_UnpackLow, tmp1, tmp2);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return LowerNode(node);
#else
        throw new NotImplementedException("Non-xarch vector Create lowering is not ported.");
#endif
    }
}
