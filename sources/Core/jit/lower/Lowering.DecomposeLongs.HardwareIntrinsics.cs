// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LOWER_DECOMPOSE_LONGS && FEATURE_HW_INTRINSICS
using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private sealed partial class DecomposeLongs
    {
        private GenTree? DecomposeHWIntrinsic(ref LIR.Use use)
        {
            var tree = use.Def();
            assert(tree.Oper is GT_HWINTRINSIC);
            var node = tree.AsHWIntrinsic();

            return node.HWIntrinsicId switch {
                NI_Vector_GetElement => DecomposeHWIntrinsicGetElement(ref use, node),
                NI_Vector_ToScalar => DecomposeHWIntrinsicToScalar(ref use, node),
                NI_AVX512_MoveMask => DecomposeHWIntrinsicMoveMask(ref use, node),
                _ => throw new InvalidOperationException("Unexpected GT_HWINTRINSIC node in long decomposition."),
            };
        }

        private GenTree? DecomposeHWIntrinsicGetElement(ref LIR.Use use, GenTreeHWIntrinsic node)
        {
            assert(node == use.Def());
            assert(varTypeIsLong(node.Type));
            assert(HWIntrinsicInfo.IsVectorGetElement(node.HWIntrinsicId));
            var op1 = node.GetOp(1);
            var op2 = node.GetOp(2);
            var simdBaseType = node.SimdBaseType;
            var simdSize = node.SimdSize;
            assert(varTypeIsLong(simdBaseType));
            assert(varTypeIsSimd(op1.Type));
            assert(op2.Type is TYP_INT);

            var simdTmpVar = RepresentOpAsLocalVar(op1, node, ref node.GetOpRef(1));
            var simdTmpVarNum = simdTmpVar.AsLclVarCommon().LclNum;
            JITDUMP("[DecomposeHWIntrinsicGetElement]: Saving op1 tree to a temp var:\n");
            DISPTREERANGE(Range(), simdTmpVar);
            Range().Remove(simdTmpVar);

            GenTree indexTimesTwo;
            var indexIsConst = op2.Oper.IsConst;
            if (indexIsConst)
            {
                indexTimesTwo = op2;
                Range().Remove(op2);
                indexTimesTwo.AsIntCon().IconValue = unchecked(op2.AsIntCon().IconValue * 2);
                Range().InsertBefore(node, simdTmpVar, indexTimesTwo);
            }
            else
            {
                var one = _compiler.gtNewIconNode(TYP_INT, 1);
                indexTimesTwo = _compiler.gtNewBinaryNode(GT_LSH, TYP_INT, op2, one);
                Range().InsertBefore(node, simdTmpVar, one, indexTimesTwo);
            }

            var loResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_INT, node.HWIntrinsicId,
                TYP_INT, simdSize, simdTmpVar, indexTimesTwo);
            Range().InsertBefore(node, loResult);
            simdTmpVar = _compiler.gtNewLclvNode(simdTmpVar.Type, simdTmpVarNum);
            Range().InsertBefore(node, simdTmpVar);

            GenTree indexTimesTwoPlusOne;
            if (indexIsConst)
            {
                indexTimesTwoPlusOne = _compiler.gtNewIconNode(TYP_INT,
                    unchecked(indexTimesTwo.AsIntCon().IconValue + 1));
                Range().InsertBefore(node, indexTimesTwoPlusOne);
            }
            else
            {
                indexTimesTwo = RepresentOpAsLocalVar(indexTimesTwo, loResult, ref loResult.GetOpRef(2));
                var indexTimesTwoVarNum = indexTimesTwo.AsLclVarCommon().LclNum;
                JITDUMP("[DecomposeHWIntrinsicWithElement]: Saving indexTimesTwo tree to a temp var:\n");
                DISPTREERANGE(Range(), indexTimesTwo);

                indexTimesTwo = _compiler.gtNewLclvNode(indexTimesTwo.Type, indexTimesTwoVarNum);
                var one = _compiler.gtNewIconNode(TYP_INT, 1);
                indexTimesTwoPlusOne = _compiler.gtNewBinaryNode(GT_ADD, TYP_INT, indexTimesTwo, one);
                Range().InsertBefore(node, indexTimesTwo, one, indexTimesTwoPlusOne);
            }

            var hiResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_INT, node.HWIntrinsicId,
                TYP_INT, simdSize, simdTmpVar, indexTimesTwoPlusOne);
            Range().InsertBefore(node, hiResult);
            Range().Remove(node);

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }

        private GenTree? DecomposeHWIntrinsicToScalar(ref LIR.Use use, GenTreeHWIntrinsic node)
        {
            assert(node == use.Def());
            assert(varTypeIsLong(node.Type));
            assert(HWIntrinsicInfo.IsVectorToScalar(node.HWIntrinsicId));
            var op1 = node.GetOp(1);
            var simdBaseType = node.SimdBaseType;
            var simdSize = node.SimdSize;
            assert(varTypeIsLong(simdBaseType));
            assert(varTypeIsSimd(op1.Type));

            var simdTmpVar = RepresentOpAsLocalVar(op1, node, ref node.GetOpRef(1));
            var simdTmpVarNum = simdTmpVar.AsLclVarCommon().LclNum;
            JITDUMP("[DecomposeHWIntrinsicToScalar]: Saving op1 tree to a temp var:\n");
            DISPTREERANGE(Range(), simdTmpVar);

            var loResult = _compiler.gtNewSimdToScalarNode(TYP_INT, simdTmpVar, TYP_INT, simdSize);
            Range().InsertAfter(simdTmpVar, loResult);
            simdTmpVar = _compiler.gtNewLclvNode(simdTmpVar.Type, simdTmpVarNum);
            Range().InsertAfter(loResult, simdTmpVar);
            var one = _compiler.gtNewIconNode(TYP_INT, 1);
            var hiResult = _compiler.gtNewSimdGetElementNode(TYP_INT, simdTmpVar, one, TYP_INT, simdSize);
            Range().InsertAfter(simdTmpVar, one, hiResult);
            Range().Remove(node);

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }

        private GenTree? DecomposeHWIntrinsicMoveMask(ref LIR.Use use, GenTreeHWIntrinsic node)
        {
            assert(node == use.Def());
            assert(varTypeIsLong(node.Type));
            assert(node.HWIntrinsicId is NI_AVX512_MoveMask);
            var op1 = node.GetOp(1);
            var simdBaseType = node.SimdBaseType;
            var simdSize = node.SimdSize;
            assert(varTypeIsArithmetic(simdBaseType));
            assert(op1.Type is TYP_MASK);
            assert(simdSize == 64);

            GenTree loResult;
            GenTree hiResult;
            if (varTypeIsByte(simdBaseType))
            {
                var simdTmpVar = RepresentOpAsLocalVar(op1, node, ref node.GetOpRef(1));
                var simdTmpVarNum = simdTmpVar.AsLclVarCommon().LclNum;
                JITDUMP("[DecomposeHWIntrinsicMoveMask]: Saving op1 tree to a temp var:\n");
                DISPTREERANGE(Range(), simdTmpVar);
                Range().Remove(simdTmpVar);
                Range().InsertBefore(node, simdTmpVar);
                loResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_INT, NI_AVX512_MoveMask,
                    simdBaseType, 32, simdTmpVar);
                Range().InsertBefore(node, loResult);

                simdTmpVar = _compiler.gtNewLclvNode(simdTmpVar.Type, simdTmpVarNum);
                Range().InsertBefore(node, simdTmpVar);
                var shiftIcon = _compiler.gtNewIconNode(TYP_INT, 32);
                Range().InsertBefore(node, shiftIcon);
                simdTmpVar = _compiler.gtNewSimdHWIntrinsicNode(TYP_MASK, NI_AVX512_ShiftRightMask,
                    simdBaseType, 64, simdTmpVar, shiftIcon);
                Range().InsertBefore(node, simdTmpVar);
                hiResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_INT, NI_AVX512_MoveMask,
                    simdBaseType, 32, simdTmpVar);
                Range().InsertBefore(node, hiResult);
            }
            else
            {
                loResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_INT, NI_AVX512_MoveMask,
                    simdBaseType, simdSize, op1);
                Range().InsertBefore(node, loResult);
                hiResult = _compiler.gtNewZeroConNode(TYP_INT);
                Range().InsertBefore(node, hiResult);
            }

            Range().Remove(node);

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }
    }
}
#endif
