// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree InsertNewSimdCreateScalarUnsafeNode(var_types simdType, GenTree operand, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(simdType));

#if TARGET_XARCH || TARGET_ARM64
        if (varTypeIsFloating(simdBaseType) && !operand.Oper.IsCnsFltOrDbl)
        {
            return operand;
        }
#endif

        var result = CompilerInstance.gtNewSimdCreateScalarUnsafeNode(simdType, operand, simdBaseType, simdSize);
        BlockRange().InsertAfter(operand, result);

        if (result.Oper.IsCnsVec)
        {
            BlockRange().Remove(operand);
        }

        _ = LowerNode(result);
        return result;
    }

    private GenTreeLclVar ReplaceWithLclVar(LIR.Use use, int tempNum = BAD_VAR_NUM)
    {
        var oldUseNode = use.Def();
        if ((oldUseNode.Oper is not GT_LCL_VAR) || (tempNum != BAD_VAR_NUM))
        {
            _ = use.ReplaceWithLclVar(CompilerInstance, tempNum, out var store);
            var newUseNode = use.Def();
            assert(oldUseNode.Next is not null);
            ContainCheckRange(oldUseNode.Next, newUseNode);

            // SIMD12 and other special local representations also require lowering the store and load.
            _ = LowerNode(store);
            _ = LowerNode(newUseNode);

            return newUseNode.AsLclVar();
        }

        return oldUseNode.AsLclVar();
    }
}
