// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool TryFoldBinop(GenTreeOp node)
    {
        if ((node.Flags & GTF_SET_FLAGS) != 0)
        {
            return false;
        }

        var first = node.Op1;
        var second = node.Op2;
        if (first.Oper.IsIntegralConst && second.Oper.IsIntegralConst)
        {
            if (TryFoldLirConst(node) is null)
            {
                return false;
            }

            BlockRange().Remove(first);
            BlockRange().Remove(second);
            return true;
        }

        if (((node.Oper is GT_LSH or GT_RSH or GT_RSZ or GT_ROL or GT_ROR) && second.IsIntegralConst(0)) ||
            ((node.Oper is GT_OR or GT_XOR) && (first.IsIntegralConst(0) || second.IsIntegralConst(0))))
        {
            var zero = second.IsIntegralConst(0) ? second : first;
            var other = zero == first ? second : first;
            if (BlockRange().TryGetUse(node, out var use))
            {
                use.ReplaceWith(other);
            }
            else
            {
                other.IsUnusedValue = true;
            }

            BlockRange().Remove(node);
            BlockRange().Remove(zero);
            return true;
        }

        return false;
    }

    // The folding factories accept only unthreaded source nodes. The probe has
    // the original's logical identity, but only Range.ReplaceNode changes LIR.
    // Callers retain their native eligibility gates and dispose of old operands.
    private GenTree? TryFoldLirConst(GenTree node)
    {
        if ((node is GenTreeOp) && ((node.Flags & GTF_SET_FLAGS) != 0))
        {
            return null;
        }

        GenTree probe = node switch {
            GenTreeCast cast => new GenTreeCast(cast.Type, cast.CastOp, cast.CastType, cast, NodeThreading.LIR),
            GenTreeOp binary when binary.Oper.IsBinary =>
                new GenTreeOp(binary.Oper, binary.Type, binary.Op1, binary.Op2, binary, NodeThreading.LIR),
            _ => throw new InvalidOperationException("Only unary casts and binary operations can be folded in LIR."),
        };

        // The source-aware unary constructor deliberately resets VN and masks
        // operator-specific flags for rewrites; folding observes the original.
        probe.Flags = node.Flags;
        probe._vnPair = node._vnPair;

        var folded = CompilerInstance.gtFoldExprConst(probe);
        if (!folded.Oper.IsConst)
        {
            assert(folded == probe);
            return null;
        }

        BlockRange().ReplaceNode(node, folded);
        return folded;
    }
}
