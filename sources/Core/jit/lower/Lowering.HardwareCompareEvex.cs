// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private GenTree? LowerHWIntrinsicCmpOpEvex(GenTreeHWIntrinsic node, genTreeOps cmpOp,
        var_types baseType, var_types maskBaseType, var_types simdType, byte size,
        GenTree first, GenTree firstMask, GenTree? second, GenCondition condition)
    {
        var compiler = CompilerInstance;
        GenTree maskNode = node;
        var next = node.Next;
        var maskIntrinsicId = NI_AVX512_CompareEqualMask;
        var count = size / maskBaseType.Size;

        if (varTypeIsMask(firstMask.Type) && second is not null &&
            (second.IsVectorZero || second.IsVectorAllBitsSet))
        {
            maskNode = firstMask;
            assert(maskNode.Type is TYP_MASK);
            var vector = second.AsVecCon();
            var handled = false;
            if (vector.IsZero)
            {
                handled = true;
            }
            else if (vector.IsAllBitsSet)
            {
                if (count < 8)
                {
                    assert(count is 1 or 2 or 4);
                    if (!TryInvertMask(maskNode, size, maskBaseType))
                    {
                        maskNode = compiler.gtNewSimdHWIntrinsicNode(TYP_MASK, NI_AVX512_NotMask,
                            maskBaseType, size, maskNode);
                        BlockRange().InsertBefore(node, maskNode);
                    }
                }
                else
                {
                    condition = new GenCondition(cmpOp is GT_EQ ? GenCondition.C : GenCondition.NC);
                }
                handled = true;
            }

            if (handled)
            {
                if (BlockRange().TryGetUse(node, out var use))
                {
                    use.ReplaceWith(maskNode);
                }
                else
                {
                    maskNode.IsUnusedValue = true;
                }
                BlockRange().Remove(second);
                if (!ReferenceEquals(firstMask, first))
                {
                    BlockRange().Remove(first);
                }
                BlockRange().Remove(node);
                second = null;
            }
        }

        if (!varTypeIsFloating(baseType) && (second is not null) && second.IsVectorZero)
        {
            var skipReplacement = false;
            if (first is GenTreeHWIntrinsic operation &&
                operation.GetOperForHWIntrinsicId(out _) is GT_AND)
            {
                var nestedFirst = operation.GetOp(1);
                var nestedSecond = operation.GetOp(2);
                if (nestedSecond.IsContained && nestedSecond is GenTreeHWIntrinsic broadcast &&
                    broadcast.HWIntrinsicId is (NI_X86Base_MoveAndDuplicate or
                        NI_AVX2_BroadcastScalarToVector128 or NI_AVX2_BroadcastScalarToVector256 or
                        NI_AVX512_BroadcastScalarToVector512))
                {
                    var broadcastOp = broadcast.GetOp(1);
                    var scalar = broadcastOp;
                    if (broadcastOp is GenTreeHWIntrinsic create &&
                        create.HWIntrinsicId is NI_Vector_CreateScalarUnsafe &&
                        broadcastOp.Type is TYP_SIMD16)
                    {
                        scalar = create.GetOp(1);
                    }
                    if (!broadcast.IsMemoryLoad() && scalar.Oper.IsConst)
                    {
                        var vector = compiler.gtNewSimdCreateBroadcastNode(simdType,
                            scalar, broadcast.SimdBaseType, size);
                        assert(vector is GenTreeVecCon);
                        BlockRange().InsertAfter(scalar, vector);
                        nestedSecond = vector;
                        if (!ReferenceEquals(scalar, broadcastOp))
                        {
                            BlockRange().Remove(broadcastOp);
                        }
                        BlockRange().Remove(scalar);
                        BlockRange().Remove(broadcast);
                    }
                }

                node.SetOp(1, nestedFirst);
                node.SetOp(2, nestedSecond);
                nestedSecond.IsContained = false;
                if (varTypeIsSmall(baseType))
                {
                    baseType = varTypeIsUnsigned(baseType) ? TYP_UINT : TYP_INT;
                    node.SimdBaseType = baseType;
                    maskBaseType = baseType;
                }
                BlockRange().Remove(first);
                BlockRange().Remove(second);
                skipReplacement = true;
            }

            if (!skipReplacement)
            {
                BlockRange().Remove(second);
                var firstUse = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                _ = ReplaceWithLclVar(firstUse);
                first = node.GetOp(1);
                second = compiler.gtClone(first) ??
                    throw new System.InvalidOperationException("EVEX mask local could not be cloned.");
                BlockRange().InsertAfter(first, second);
                node.SetOp(2, second);
            }

            node.Type = TYP_MASK;
            node.ChangeHWIntrinsicId(NI_AVX512_PTESTM);
            _ = LowerNode(node);
            maskNode = node;
        }

        if (maskNode.Type is not TYP_MASK)
        {
            assert(ReferenceEquals(node, maskNode));
            maskBaseType = baseType;
            count = size / maskBaseType.Size;

            if (count < 8)
            {
                assert(count is 1 or 2 or 4);
                maskIntrinsicId = NI_AVX512_CompareNotEqualMask;
            }
            else
            {
                assert(count is 8 or 16 or 32 or 64);
                if (cmpOp is GT_EQ)
                {
                    condition = new GenCondition(GenCondition.C);
                }
                else
                {
                    maskIntrinsicId = NI_AVX512_CompareNotEqualMask;
                }
            }

            node.Type = TYP_MASK;
            node.ChangeHWIntrinsicId(maskIntrinsicId);
            _ = LowerNode(node);
            maskNode = node;
        }

        if (BlockRange().TryGetUse(maskNode, out var maskUse))
        {
            var cc = compiler.gtNewSimdHWIntrinsicNode(simdType, NI_AVX512_KORTEST,
                maskBaseType, size, maskNode);
            if (next is not null)
            {
                BlockRange().InsertBefore(next, cc);
            }
            else
            {
                BlockRange().InsertAtEnd(cc);
            }
            maskUse.ReplaceWith(cc);
            LowerHWIntrinsicCC(cc, NI_AVX512_KORTEST, condition);
            next = cc.Next;
        }
        return next;
    }
#endif
}
