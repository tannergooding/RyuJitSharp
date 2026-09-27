// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool TryLowerAddForPossibleContainment(GenTreeOp node, out GenTree? next)
    {
        assert(node.Oper is GT_ADD);
        next = null;
        if (!CompilerInstance.opts.OptimizationEnabled || node.IsContained || !varTypeIsIntegral(node.Type) ||
            ((node.Flags & GTF_SET_FLAGS) != 0) || node.HasOverflowCheck)
        {
            return false;
        }

        var op1 = node.Op1;
        var op2 = node.Op2;
        if (IsContainableImmed(node, op2))
        {
            return false;
        }

        var multiply = op1.Oper is GT_MUL ? op1 : op2;
        var addValue = op1.Oper is GT_MUL ? op2 : op1;
        if ((multiply.Oper is GT_MUL) && ((multiply.Flags & GTF_SET_FLAGS) == 0) &&
            varTypeIsIntegral(multiply.Type) && !multiply.HasOverflowCheck &&
            !multiply.IsContained && !addValue.IsContained)
        {
            var a = multiply.AsOp().Op1;
            var b = multiply.AsOp().Op2;
            if ((a.Oper is GT_NEG) && ((a.Flags & GTF_SET_FLAGS) == 0) && (b.Oper is not GT_NEG) &&
                !a.IsContained && !a.AsUnOp().Op1.IsContained)
            {
                multiply.AsOp().Op1 = a.AsUnOp().Op1;
                BlockRange().Remove(a);
                node.Op1 = addValue;
                node.Op2 = multiply;
                node.SetOper(GT_SUB);
                node.Flags &= GTF_COMMON_MASK;
                ContainCheckBinary(node);
                next = node.Next;
                return true;
            }
            else if ((b.Oper is GT_NEG) && ((b.Flags & GTF_SET_FLAGS) == 0) && (a.Oper is not GT_NEG) &&
                !b.IsContained && !b.AsUnOp().Op1.IsContained)
            {
                multiply.AsOp().Op2 = b.AsUnOp().Op1;
                BlockRange().Remove(b);
                node.Op1 = addValue;
                node.Op2 = multiply;
                node.SetOper(GT_SUB);
                node.Flags &= GTF_COMMON_MASK;
                ContainCheckBinary(node);
                next = node.Next;
                return true;
            }
            else if (op1.Oper is GT_MUL)
            {
                node.Op1 = addValue;
                node.Op2 = multiply;
                ContainCheckBinary(node);
                next = node.Next;
                return true;
            }
        }

        return false;
    }

    private GenTree? LowerNeg(GenTreeUnOp node)
    {
        if (TryLowerNegToMulLongOp(node, out var next))
        {
            return next;
        }

        ContainCheckNeg(node);
        return node.Next;
    }

    private bool TryLowerNegToMulLongOp(GenTreeUnOp op, out GenTree? next)
    {
        assert(op.Oper is GT_NEG);
        next = null;
        if (!CompilerInstance.opts.OptimizationEnabled || op.IsContained || !varTypeIsIntegral(op.Type) ||
            ((op.Flags & GTF_SET_FLAGS) != 0))
        {
            return false;
        }

        var op1 = op.Op1;
        if (op1.Oper is not GT_MUL_LONG)
        {
            return false;
        }

        var multiply = op1.AsOp();
        if ((multiply.Op1.Type.ActualType is not TYP_INT) || (multiply.Op2.Type.ActualType is not TYP_INT))
        {
            return false;
        }

        if (!IsInvariantInRange(multiply, op))
        {
            return false;
        }

        var result = CompilerInstance.gtNewScalarHWIntrinsicNode(TYP_LONG, NI_ArmBase_Arm64_MultiplyLongNeg,
            multiply.Op1, multiply.Op2);
        result.SimdBaseType = multiply.IsUnsigned ? TYP_ULONG : TYP_LONG;
        BlockRange().InsertAfter(op, result);
        if (BlockRange().TryGetUse(op, out var use))
        {
            use.ReplaceWith(result);
        }
        else
        {
            result.IsUnusedValue = true;
        }

        BlockRange().Remove(multiply);
        BlockRange().Remove(op);
        JITDUMP("Converted to HW_INTRINSIC 'NI_ArmBase_Arm64_MultiplyLongNeg'.\n");
        JITDUMP(":\n");
        DISPTREERANGE(BlockRange(), result);
        JITDUMP("\n");
        next = result;
        return true;
    }

    private bool TryLowerAndRshToBFX(GenTreeOp tree, out GenTree? next)
    {
        assert(tree.Oper is GT_AND);
        next = null;
        if (!CompilerInstance.opts.OptimizationEnabled)
        {
            return false;
        }

        var shift = tree.Op1;
        var andConst = tree.Op2;
        if ((shift.Oper is not (GT_RSH or GT_RSZ)) || !andConst.Oper.IsIntegralConst)
        {
            return false;
        }

        var shiftVar = shift.AsOp().Op1;
        var shiftConst = shift.AsOp().Op2;
        if (!shiftConst.Oper.IsIntegralConst)
        {
            return false;
        }

        var type = tree.Type.ActualType;
        if (!varTypeIsIntegral(type))
        {
            return false;
        }

        var mask = unchecked((ulong)andConst.AsIntConCommon().IntegralValue);
        var shiftVal = unchecked((ulong)shiftConst.AsIntConCommon().IntegralValue);
        if ((mask == 0) || ((mask & unchecked(mask + 1)) != 0))
        {
            return false;
        }

        var width = (ulong)BitOperations.PopCount(mask);
        var offset = shiftVal;
        var bitWidth = (ulong)type.Size * BITS_PER_BYTE;
        if ((width > bitWidth) || (offset >= bitWidth) || ((offset + width) > bitWidth))
        {
            return false;
        }

        if ((width == 8) || (width == 16) || ((bitWidth == 64) && (width == 32)))
        {
            return false;
        }

        var bfm = CompilerInstance.gtNewBfxNode(type, shiftVar, (uint)offset, (uint)width);
        // Native ContainCheckNode has no action for GT_BFX.
        BlockRange().InsertBefore(tree, bfm);
        if (BlockRange().TryGetUse(tree, out var use))
        {
            use.ReplaceWith(bfm);
        }
        else
        {
            bfm.IsUnusedValue = true;
        }

        BlockRange().Remove(shiftConst);
        BlockRange().Remove(shift);
        BlockRange().Remove(andConst);
        BlockRange().Remove(tree);
        next = bfm.Next;
        return true;
    }

    private bool TryLowerAddSubToMulLongOp(GenTreeOp op, out GenTree? next)
    {
        assert(op.Oper is GT_ADD or GT_SUB);
        next = null;
        if (!CompilerInstance.opts.OptimizationEnabled || op.IsContained || !varTypeIsIntegral(op.Type) ||
            ((op.Flags & GTF_SET_FLAGS) != 0) || op.HasOverflowCheck)
        {
            return false;
        }

        var op1 = op.Op1;
        var op2 = op.Op2;
        GenTreeOp multiply;
        GenTree addValue;
        if (op1.Oper is GT_MUL_LONG)
        {
            if (op.Oper is GT_SUB)
            {
                return false;
            }

            multiply = op1.AsOp();
            addValue = op2;
        }
        else if (op2.Oper is GT_MUL_LONG)
        {
            multiply = op2.AsOp();
            addValue = op1;
        }
        else
        {
            return false;
        }

        if (addValue.Type is not TYP_LONG)
        {
            return false;
        }

        if ((multiply.Op1.Type.ActualType is not TYP_INT) || (multiply.Op2.Type.ActualType is not TYP_INT))
        {
            return false;
        }

        if (!IsInvariantInRange(multiply, op))
        {
            return false;
        }

        var intrinsicId = op.Oper is GT_ADD ? NI_ArmBase_Arm64_MultiplyLongAdd : NI_ArmBase_Arm64_MultiplyLongSub;
        var result = CompilerInstance.gtNewScalarHWIntrinsicNode(TYP_LONG, intrinsicId,
            multiply.Op1, multiply.Op2, addValue);
        result.SimdBaseType = multiply.IsUnsigned ? TYP_ULONG : TYP_LONG;
        BlockRange().InsertAfter(op, result);
        if (BlockRange().TryGetUse(op, out var use))
        {
            use.ReplaceWith(result);
        }
        else
        {
            result.IsUnusedValue = true;
        }

        BlockRange().Remove(multiply);
        BlockRange().Remove(op);
        JITDUMP("Converted to HW_INTRINSIC 'NI_ArmBase_Arm64_MultiplyLong[Add/Sub]'.\n");
        JITDUMP(":\n");
        DISPTREERANGE(BlockRange(), result);
        JITDUMP("\n");
        next = result;
        return true;
    }
}
#endif
