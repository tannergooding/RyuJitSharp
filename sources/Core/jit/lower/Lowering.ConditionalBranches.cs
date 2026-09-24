// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerJTrue(GenTreeUnOp branch)
    {
#if TARGET_XARCH
        var condition = branch.Op1;
        JITDUMP("Lowering JTRUE:\n");
        DISPTREERANGE(BlockRange(), branch);
        JITDUMP("\n");

        GenTree result = branch;
        if (TryLowerConditionToFlagsNode(branch, condition, out var code))
        {
            result = new GenTreeCC(GT_JCC, branch.Type, code, branch, NodeThreading.LIR) {
                Flags = branch.Flags,
            };
            result._vnPair.SetBoth(ValueNumStore.NoVN);
            BlockRange().ReplaceNode(branch, result);
        }

        JITDUMP("Lowering JTRUE Result:\n");
        DISPTREERANGE(BlockRange(), result);
        JITDUMP("\n");
        return null;
#else
        throw new System.NotImplementedException("Non-xarch conditional branch lowering is not ported.");
#endif
    }
}
