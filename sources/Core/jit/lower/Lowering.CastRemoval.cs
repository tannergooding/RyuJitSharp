// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool TryRemoveCast(GenTreeCast node)
    {
        if (CompilerInstance.opts.OptimizationDisabled || node.HasOverflowCheck)
        {
            return false;
        }

        var operand = node.CastOp;
        if (!operand.Oper.IsConst)
        {
            return false;
        }

        if (TryFoldLirConst(node) is null)
        {
            return false;
        }

        operand.IsUnusedValue = true;
        return true;
    }
}
