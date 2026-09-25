// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private GenTree? LowerHWIntrinsicCndSel(GenTreeHWIntrinsic node)
    {
        var compiler = CompilerInstance;
        var simdType = node.Type;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);

        var newNodes = new LIR.Range(null, null);
        GenTree? result = null;
        var condition = node.GetOp(1);
        var selectTrue = node.GetOp(2);
        var selectFalse = node.GetOp(3);

        if (condition is GenTreeHWIntrinsic conversion &&
            conversion.HWIntrinsicId is NI_AVX512_ConvertMaskToVector)
        {
            var mask = conversion.GetOp(1);
            BlockRange().Remove(conversion);
            condition = mask;
            simdBaseType = conversion.SimdBaseType;
            result = compiler.gtNewSimdHWIntrinsicNode(simdType, NI_AVX512_BlendVariableMask,
                simdBaseType, simdSize, selectFalse, selectTrue, condition);
        }
        else if (selectFalse.IsVectorZero)
        {
            BlockRange().Remove(selectFalse);
            result = compiler.gtNewSimdBinOpNode(GT_AND, simdType, condition, selectTrue, simdBaseType, simdSize);
        }
        else if (selectTrue.IsVectorZero)
        {
            BlockRange().Remove(selectTrue);
            result = compiler.gtNewSimdBinOpNode(GT_AND_NOT, simdType, selectFalse, condition, simdBaseType, simdSize);
        }
        else if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            var control = compiler.gtNewIconNode(TYP_INT, 0xCA);
            newNodes.InsertAtEnd(control);
            result = compiler.gtNewSimdHWIntrinsicNode(simdType, NI_AVX512_TernaryLogic,
                simdBaseType, simdSize, condition, selectTrue, selectFalse, control);
        }
        else if (condition.IsVectorPerElementMask(compiler, TYP_BYTE, simdSize))
        {
            var blendId = NI_Illegal;
            if (varTypeIsFloating(simdBaseType) &&
                !condition.IsVectorPerElementMask(compiler, simdBaseType, simdSize))
            {
                simdBaseType = TYP_BYTE;
            }
            if (simdSize is 32)
            {
                if (varTypeIsFloating(simdBaseType))
                {
#if DEBUG
                    assert(compiler.compSupportsHWIntrinsic(InstructionSet_AVX));
#endif
                    blendId = NI_AVX_BlendVariable;
                }
                else if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    blendId = NI_AVX2_BlendVariable;
                }
            }
            else
            {
                blendId = NI_X86Base_BlendVariable;
            }
            if (blendId is not NI_Illegal)
            {
                result = compiler.gtNewSimdHWIntrinsicNode(simdType, blendId,
                    simdBaseType, simdSize, selectFalse, selectTrue, condition);
            }
        }

        if (result is null)
        {
            assert(simdSize is not 64);
            if (condition.Oper is not GT_LCL_VAR)
            {
                LIR.Use.MakeDummyUse(newNodes, condition, out var conditionUse);
                _ = conditionUse.ReplaceWithLclVar(compiler);
                condition = conditionUse.Def();
            }

            var copy = compiler.gtClone(condition) ??
                throw new System.InvalidOperationException("Conditional-select mask local could not be cloned.");
            var truePart = compiler.gtNewSimdBinOpNode(GT_AND,
                simdType, condition, selectTrue, simdBaseType, simdSize);
            var falsePart = compiler.gtNewSimdBinOpNode(GT_AND_NOT,
                simdType, selectFalse, copy, simdBaseType, simdSize);
            result = compiler.gtNewSimdBinOpNode(GT_OR,
                simdType, truePart, falsePart, simdBaseType, simdSize);
            newNodes.InsertAtEnd(copy);
            newNodes.InsertAtEnd(truePart);
            newNodes.InsertAtEnd(falsePart);
        }

        newNodes.InsertAtEnd(result);
        var firstNewNode = newNodes.FirstNode;
        var lastNewNode = newNodes.LastNode;
        BlockRange().InsertBefore(node, newNodes);

        if (BlockRange().TryGetUse(node, out var use))
        {
            use.ReplaceWith(result);
        }
        else
        {
            result.IsUnusedValue = true;
        }
        BlockRange().Remove(node);
        var next = result.Next;
        LowerRange(firstNewNode, lastNewNode);
        return next;
    }
#endif
}
