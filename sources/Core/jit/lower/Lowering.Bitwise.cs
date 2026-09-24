// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if TARGET_XARCH || TARGET_RISCV64
    private GenTreeOp? TryLowerBitwiseOpToBitOp(GenTreeOp binOp)
    {
        assert(binOp.Oper is GT_OR or GT_XOR or GT_AND);

        if (binOp.Type is not (TYP_INT or TYP_LONG))
        {
            return null;
        }

        var op1 = binOp.Op1;
        var op2 = binOp.Op2;
        var isOp1Negated = op1.Oper is GT_NOT;
        var isOp2Negated = op2.Oper is GT_NOT;
        var wantNegated = binOp.Oper is GT_AND;
        var opp1 = isOp1Negated ? op1.AsUnOp().Op1 : op1;
        var opp2 = isOp2Negated ? op2.AsUnOp().Op1 : op2;

        var isOp1SingleBit = (isOp1Negated == wantNegated) && (opp1.Oper is GT_LSH) &&
            opp1.AsOp().Op1.IsIntegralConst(1);
        var isOp2SingleBit = (isOp2Negated == wantNegated) && (opp2.Oper is GT_LSH) &&
            opp2.AsOp().Op1.IsIntegralConst(1);

        if (!isOp1SingleBit && !isOp2SingleBit)
        {
            return null;
        }

        if (isOp1SingleBit)
        {
            (binOp.Op1, binOp.Op2) = (op2, op1);
            isOp2Negated = isOp1Negated;
        }

        var notNode = isOp2Negated ? binOp.Op2 : null;
        var lshNode = notNode is not null ? notNode.AsUnOp().Op1 : binOp.Op2;

        if (lshNode.Type != binOp.Type)
        {
            return null;
        }

        // xarch bit operations mask variable indices to the operand width.
        // RISC-V callers must mask 32-bit indices explicitly.
        var indexNode = lshNode.AsOp().Op2;
        if (indexNode.Oper.IsIntegralConst)
        {
            return null;
        }

        if (((binOp.Flags | lshNode.Flags | (notNode?.Flags ?? GTF_EMPTY)) & GTF_SET_FLAGS) != 0)
        {
            return null;
        }

        var newOper = binOp.Oper switch {
            GT_OR => GT_BIT_SET,
            GT_XOR => GT_BIT_INVERT,
            GT_AND => GT_BIT_CLEAR,
            _ => throw new System.InvalidOperationException(),
        };

        JITDUMP($"Lower: optimize {binOp.Oper.Name}(X, {(notNode is not null ? "NOT(LSH(1, Y))" : "LSH(1, Y)")})\n");
        DISPNODE(binOp);

        if (notNode is not null)
        {
            BlockRange().Remove(notNode);
        }
        BlockRange().Remove(lshNode.AsOp().Op1);
        BlockRange().Remove(lshNode);

        binOp.Op2 = indexNode;
        binOp.SetOper(newOper);
        binOp.Flags &= GTF_COMMON_MASK;

        JITDUMP("to:\n");
        DISPNODE(binOp);

        return binOp;
    }
#endif

    private bool TryLowerAndNegativeOne(GenTreeOp node, out GenTree? nextNode)
    {
        assert(node.Oper is GT_AND);
        nextNode = null;

        if (!varTypeIsIntegral(node.Type) || ((node.Flags & GTF_SET_FLAGS) != 0) || node.IsContained)
        {
            return false;
        }

        var op2 = node.Op2;
        if (!op2.IsIntegralConst(-1))
        {
            return false;
        }

#if LOWER_DECOMPOSE_LONGS
        assert(op2.Type is TYP_INT);
#endif
        var op1 = node.Op1;
        if (BlockRange().TryGetUse(node, out var use))
        {
            use.ReplaceWith(op1);
        }
        else
        {
            op1.IsUnusedValue = true;
        }

        nextNode = node.Next;
        BlockRange().Remove(op2);
        BlockRange().Remove(node);

        return true;
    }
}
