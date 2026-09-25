// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerRotate(GenTree tree)
    {
#if TARGET_XARCH
        ContainCheckShiftRotate(tree.AsOp());
#else
        throw new System.NotImplementedException("Non-xarch rotate lowering is not ported.");
#endif
    }

    private void ContainCheckShiftRotate(GenTreeOp node)
    {
#if TARGET_XARCH
        assert(node.Oper.IsShiftOrRotate);

        var source = node.Op1;
        var shiftBy = node.Op2;
#if TARGET_X86
        if (node.Oper.IsShiftLong)
        {
            assert(source.Oper is GT_LONG);
            MakeSrcContained(node, source);
        }
#endif
        if (IsContainableImmed(node, shiftBy) && (shiftBy.AsIntConCommon().IconValue <= 255) &&
            (shiftBy.AsIntConCommon().IconValue >= 0))
        {
            MakeSrcContained(node, shiftBy);
        }

        var canContainSource = !source.IsContained && (source.Type.Size >= node.Type.Size);
        if (canContainSource && ((node.Flags & GTF_SET_FLAGS) == 0) &&
            (shiftBy.IsContained != node.Oper.IsShift) &&
            CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            if (IsContainableMemoryOp(source) && IsSafeToContainMem(node, source))
            {
                MakeSrcContained(node, source);
            }
            else if (IsSafeToMarkRegOptional(node, source))
            {
                MakeSrcRegOptional(node, source);
            }
        }
#else
        throw new System.NotImplementedException("Non-xarch shift containment is not ported.");
#endif
    }
}
