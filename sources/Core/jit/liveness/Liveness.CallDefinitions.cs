// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    internal bool IsTrackedCallDefinition(LIR.Range range, GenTree node)
    {
        assert(node.Oper is GT_LCL_ADDR);
        if ((node.Flags & GTF_VAR_DEF) == 0)
        {
            return false;
        }

        ref var descriptor = ref _compiler.lvaGetDesc(node.AsLclVarCommon().LclNum);
        if (!descriptor.lvTracked)
        {
            return false;
        }

        var current = node;
        do
        {
            if (!range.TryGetUse(current, out var use))
            {
                return false;
            }

            current = use.User();
            if (current.Oper is GT_CALL)
            {
                return current.VisitPhysicalLocalDefNodes(_compiler, definition =>
                    ReferenceEquals(node, definition) ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue)
                    is GenTree.VisitResult.Abort;
            }
        }
        while ((current.Oper is GT_FIELD_LIST) || current.Oper.IsPutArg);

        return false;
    }
}
