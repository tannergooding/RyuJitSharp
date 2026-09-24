// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckBoundsChk(GenTreeBoundsChk node)
    {
#if TARGET_XARCH
        assert(node.Oper is GT_BOUNDS_CHECK);

        GenTree other;
        if (CheckImmedAndMakeContained(node, node.Index))
        {
            other = node.ArrayLength;
        }
        else if (CheckImmedAndMakeContained(node, node.ArrayLength))
        {
            other = node.Index;
        }
        else if (IsContainableMemoryOp(node.Index))
        {
            other = node.Index;
        }
        else
        {
            other = node.ArrayLength;
        }

        if (node.Index.Type == node.ArrayLength.Type)
        {
            TryMakeSrcContainedOrRegOptional(node, other);
        }
#else
        throw new System.NotImplementedException("Bounds-check containment outside xarch is not ported.");
#endif
    }
}
