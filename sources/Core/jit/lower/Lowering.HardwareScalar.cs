// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerHWIntrinsicToScalar(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        var baseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        var simdType = Compiler.GetSimdTypeForSize(simdSize);
        assert(node.HWIntrinsicId is NI_Vector_ToScalar);
        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(baseType));
        assert(simdSize != 0);

        var operand = node.GetOp(1);
        if (IsContainableMemoryOp(operand) && (!varTypeIsLong(baseType) || TargetArchitecture.Is64Bit))
        {
            // Expose scalar memory operands to consumers before containment. Long loads cannot
            // be introduced on 32-bit targets after DecomposeLongs.
            if (operand.Oper is GT_IND)
            {
                var indirection = operand.AsIndir();
                var load = CompilerInstance.gtNewIndir(node.SimdBaseTypeAsVarType, indirection.Addr,
                    indirection.Flags & GTF_IND_FLAGS);
                BlockRange().InsertBefore(node, load);
                if (BlockRange().TryGetUse(node, out var use))
                {
                    use.ReplaceWith(load);
                }
                else
                {
                    load.IsUnusedValue = true;
                }

                BlockRange().Remove(operand);
                BlockRange().Remove(node);
                return LowerNode(load);
            }

            if (operand.Oper is GT_LCL_VAR or GT_LCL_FLD)
            {
                var local = operand.AsLclVarCommon();
                ref var descriptor = ref CompilerInstance.lvaGetDesc(local.LclNum);
                if (descriptor.lvDoNotEnregister &&
                    ((uint)local.LclOffs + (uint)baseType.Size <= descriptor.lvValueSize.ExactSize))
                {
                    var load = CompilerInstance.gtNewLclFldNode(node.SimdBaseTypeAsVarType, local.LclNum, local.LclOffs);
                    BlockRange().InsertBefore(node, load);
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        use.ReplaceWith(load);
                    }
                    else
                    {
                        load.IsUnusedValue = true;
                    }

                    BlockRange().Remove(operand);
                    BlockRange().Remove(node);
                    return LowerNode(load);
                }
            }
        }

        ContainCheckHWIntrinsic(node);
        return node.Next;
#else
        throw new NotImplementedException("Non-xarch vector-to-scalar lowering is not ported.");
#endif
    }
}
