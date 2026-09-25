// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private GenTree? LowerHWIntrinsicCmpOp(GenTreeHWIntrinsic node, genTreeOps cmpOp)
    {
        var intrinsicId = node.HWIntrinsicId;
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        var simdType = Compiler.GetSimdTypeForSize(size);
        assert(intrinsicId is NI_Vector_op_Equality or NI_Vector_op_Inequality);
        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(baseType));
        assert(size != 0 && node.Type is TYP_INT);
        assert(cmpOp is GT_EQ or GT_NE);

        var first = node.GetOp(1);
        var firstMask = first;
        var second = node.GetOp(2);
        var condition = new GenCondition(cmpOp is GT_EQ ? GenCondition.EQ : GenCondition.NE);
        var maskBaseType = baseType;

        if (firstMask is GenTreeHWIntrinsic conversion &&
            conversion.HWIntrinsicId is NI_AVX512_ConvertMaskToVector)
        {
            firstMask = conversion.GetOp(1);
            assert(varTypeIsMask(firstMask.Type));
            maskBaseType = conversion.SimdBaseType;
        }

        if (!varTypeIsFloating(baseType) && (size != 64) && !varTypeIsMask(firstMask.Type) &&
            (second.IsVectorZero || second.IsVectorAllBitsSet))
        {
            var skipReplacement = false;
            if (second.IsVectorAllBitsSet)
            {
                condition = new GenCondition(cmpOp is GT_EQ ? GenCondition.C : GenCondition.NC);
                skipReplacement = true;
            }
            else if (first is GenTreeHWIntrinsic operation && operation.Operands.Length == 2)
            {
                var nestedFirst = operation.GetOp(1);
                var nestedSecond = operation.GetOp(2);
                assert(!nestedFirst.IsContained);
                var embeddedBroadcast = nestedSecond.IsContained && nestedSecond is GenTreeHWIntrinsic;
                var binaryOper = operation.GetOperForHWIntrinsicId(out _);

                if (!embeddedBroadcast && binaryOper is (GT_AND or GT_AND_NOT))
                {
                    if (binaryOper is GT_AND_NOT)
                    {
                        condition = new GenCondition(cmpOp is GT_EQ ? GenCondition.C : GenCondition.NC);
                    }
                    node.SetOp(1, nestedFirst);
                    node.SetOp(2, nestedSecond);
                    BlockRange().Remove(first);
                    BlockRange().Remove(second);
                    skipReplacement = true;
                }
            }

            if (!skipReplacement)
            {
                assert(second.IsVectorZero);
                BlockRange().Remove(second);
                var firstUse = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                _ = ReplaceWithLclVar(firstUse);
                first = node.GetOp(1);
                second = CompilerInstance.gtClone(first) ??
                    throw new System.InvalidOperationException("PTEST local could not be cloned.");
                BlockRange().InsertAfter(first, second);
                node.SetOp(2, second);
            }

            LowerHWIntrinsicCC(node, size is 32 ? NI_AVX_PTEST : NI_X86Base_PTEST, condition);
            return LowerNode(node);
        }

        if (CompilerInstance.canUseEvexEncoding())
        {
            return LowerHWIntrinsicCmpOpEvex(node, cmpOp, baseType, maskBaseType,
                simdType, size, first, firstMask, second, condition);
        }

        assert(size != 64);
        NamedIntrinsic comparison;
        NamedIntrinsic extract;
        var_types comparisonType;
        var_types extractType;
        int expectedMask;

        switch (baseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            case TYP_SHORT:
            case TYP_USHORT:
            case TYP_INT:
            case TYP_UINT:
            case TYP_LONG:
            case TYP_ULONG:
            {
                comparisonType = baseType;
                extractType = TYP_UBYTE;
                comparison = size is 32 ? NI_AVX2_CompareEqual : NI_X86Base_CompareEqual;
                extract = size is 32 ? NI_AVX2_MoveMask : NI_X86Base_MoveMask;
                expectedMask = size is 32 ? -1 : 0xFFFF;
                break;
            }

            case TYP_FLOAT:
            case TYP_DOUBLE:
            {
                comparisonType = baseType;
                extractType = baseType;
                comparison = size is 32 ? NI_AVX_CompareEqual : NI_X86Base_CompareEqual;
                extract = size is 32 ? NI_AVX_MoveMask : NI_X86Base_MoveMask;
                expectedMask = baseType is TYP_FLOAT
                    ? (size is 32 ? 0xFF : 0xF)
                    : (size is 32 ? 0xF : 0x3);
                break;
            }

            default:
            {
                throw new System.InvalidOperationException($"Unexpected vector comparison base type: {baseType}.");
            }
        }

        var vectorComparison = CompilerInstance.gtNewSimdHWIntrinsicNode(simdType, comparison,
            comparisonType, size, first, second);
        BlockRange().InsertBefore(node, vectorComparison);
        _ = LowerNode(vectorComparison);

        var mask = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_INT, extract,
            extractType, size, vectorComparison);
        BlockRange().InsertAfter(vectorComparison, mask);
        _ = LowerNode(mask);

        var constant = CompilerInstance.gtNewIconNode(TYP_INT, expectedMask);
        BlockRange().InsertAfter(mask, constant);

        // The native node changes its union type in place. Managed tree classes retain their
        // concrete type, so replace the whole node while transferring its logical identity.
        var compare = new GenTreeOp(cmpOp, TYP_INT, mask, constant, node, NodeThreading.LIR) {
            Flags = node.Flags,
        };
        BlockRange().ReplaceNode(node, compare);
        _ = LowerNodeCC(compare, condition);
        compare.Type = TYP_VOID;
        compare.IsUnusedValue = false;
        return LowerNode(compare);
    }
#endif
}
