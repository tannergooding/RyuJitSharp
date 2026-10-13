// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree InsertNewSimdCreateScalarUnsafeNode(var_types simdType, GenTree operand, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "SIMD lowering is unsupported on this target.");
        }

        assert(varTypeIsSimd(simdType));

        if ((Configuration.TARGET_XARCH || Configuration.TARGET_ARM64) &&
            varTypeIsFloating(simdBaseType) &&
            !operand.Oper.IsCnsFltOrDbl)
        {
            return operand;
        }

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
            if (Configuration.TARGET_ARM64)
            {
                WidenSIMD12IfNecessary(store.AsLclVarCommon());
                _ = LowerStoreLocCommon(store.AsLclVarCommon());
                LowerLclVar(newUseNode.AsLclVar());
            }
            else
            {
                _ = LowerNode(store);
                _ = LowerNode(newUseNode);
            }

            return newUseNode.AsLclVar();
        }

        return oldUseNode.AsLclVar();
    }
}
