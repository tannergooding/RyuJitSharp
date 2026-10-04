// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerAdd(GenTreeOp node)
    {
        if (varTypeIsIntegralOrI(node.Type))
        {
            var op1 = node.Op1;
            var op2 = node.Op2;

            if (op2.IsIntegralConst(0))
            {
                JITDUMP("Lower: optimize val + 0: ");
                DISPNODE(node);
                JITDUMP("Replaced with: ");
                DISPNODE(op1);
                if (BlockRange().TryGetUse(node, out var zeroUse))
                {
                    zeroUse.ReplaceWith(op1);
                }
                else
                {
                    op1.IsUnusedValue = true;
                }

                var next = node.Next;
                BlockRange().Remove(op2);
                BlockRange().Remove(node);
#if DEBUG
                JITDUMP($"Remove [{op2.TreeId:D6}], [{node.TreeId:D6}]\n");
#endif
                return next;
            }

            if (CompilerInstance.opts.OptimizationEnabled)
            {
                while ((op1.Oper is GT_ADD) && op2.Oper.IsIntegralConst &&
                    op1.AsOp().Op2.Oper.IsIntegralConst && !node.HasOverflowCheck && !op1.HasOverflowCheck)
                {
                    var first = op1.AsOp().Op2.AsIntConCommon();
                    var second = op2.AsIntConCommon();
                    if (first.ImmedValNeedsReloc(CompilerInstance) || second.ImmedValNeedsReloc(CompilerInstance) ||
                        varTypeIsGC(first.Type) || (first.Type != second.Type))
                    {
                        break;
                    }

                    JITDUMP("Folding (x + c1) + c2. Before:\n");
                    DISPTREERANGE(BlockRange(), node);

                    var result = (node.Type.Size == sizeof(long))
                        ? unchecked(first.IntegralValue + second.IntegralValue)
                        : unchecked((int)first.IntegralValue + (int)second.IntegralValue);
                    second.IntegralValue = result;
                    node.Op1 = op1.AsOp().Op1;
                    BlockRange().Remove(first);
                    BlockRange().Remove(op1);
                    op1 = node.Op1;
                    op1.IsRegOptional = false;
                    op1.IsContained = false;

                    JITDUMP("\nAfter:\n");
                    DISPTREERANGE(BlockRange(), node);
                }

                if (op1.Oper.IsCnsIntOrI && op2.Oper.IsCnsIntOrI && !node.HasOverflowCheck &&
                    (op1.AsIntCon().IsIconHandle(GTF_ICON_OBJ_HDL) || op2.AsIntCon().IsIconHandle(GTF_ICON_OBJ_HDL)) &&
                    !op1.AsIntCon().ImmedValNeedsReloc(CompilerInstance) &&
                    !op2.AsIntCon().ImmedValNeedsReloc(CompilerInstance))
                {
                    assert(node.Type is TYP_I_IMPL or TYP_BYREF);
                    var value = unchecked(op1.AsIntCon().IconValue + op2.AsIntCon().IconValue);
                    BlockRange().Remove(op1);
                    BlockRange().Remove(op2);
                    var constant = new GenTreeIntCon(node.Type, value, null, node, NodeThreading.LIR);
                    constant._vnPair.SetBoth(ValueNumStore.NoVN);
                    BlockRange().ReplaceNode(node, constant);
                    return constant.Next;
                }
            }

#if TARGET_XARCH
            if (BlockRange().TryGetUse(node, out var use))
            {
                var parent = use.User();
                if ((!parent.Oper.IsIndir || parent.Oper.IsAtomic) && (parent.Oper is not GT_ADD))
                {
                    GenTree address = node;
                    if (TryCreateAddrMode(ref address, false, parent))
                    {
                        return address.Next;
                    }
                }
            }
#endif
        }

#if TARGET_ARM64
        if (node.Oper is GT_ADD)
        {
            if (TryLowerAddForPossibleContainment(node, out var next))
            {
                return next;
            }

            if (TryLowerAddSubToMulLongOp(node, out next))
            {
                return next;
            }
        }
#endif

#if TARGET_RISCV64
        if (CompilerInstance.compOpportunisticallyDependsOn(CORINFO_InstructionSet.InstructionSet_Zba))
        {
            if (TryLowerShiftAddToShxadd(node, out var next))
            {
                return next;
            }
            else if (TryLowerZextAddToAddUw(node, out next))
            {
                return next;
            }
        }
#endif

#if TARGET_WASM
        if (node.Oper is GT_ADD)
        {
            _ = LowerBinaryArithmetic(node);
        }
#endif
        if (node.Oper is GT_ADD)
        {
            ContainCheckBinary(node);
        }
        return null;
    }

#if TARGET_RISCV64
    private bool TryLowerShiftAddToShxadd(GenTreeOp node, out GenTree? next)
    {
        next = null;
        if (CompilerInstance.opts.OptimizationDisabled)
        {
            return false;
        }

        if (node.IsContained || (node.Oper is not GT_ADD) || node.HasOverflowCheck ||
            ((node.Type.EmitActualSize != EA_8BYTE) && (node.Type.EmitActualSize != EA_BYREF)))
        {
            return false;
        }

        GenTree baseNode;
        GenTree shift;
        if (node.Op1.Oper is GT_LSH or GT_MUL or GT_SLLI_UW)
        {
            shift = node.Op1;
            baseNode = node.Op2;
        }
        else if (node.Op2.Oper is GT_LSH or GT_MUL or GT_SLLI_UW)
        {
            shift = node.Op2;
            baseNode = node.Op1;
        }
        else
        {
            return false;
        }

        var isSlliUw = shift.Oper is GT_SLLI_UW;
        var index = shift.AsOp().Op1;
        var scale = shift.ScaledIndex;
        if (scale == 0)
        {
            return false;
        }

        assert(baseNode.IsValue && index.IsValue);
        if (baseNode.IsContained || index.IsContained || !varTypeIsIntegralOrI(baseNode.Type) ||
            !varTypeIsIntegralOrI(index.Type) || baseNode.Oper.IsCnsIntOrI || index.Oper.IsCnsIntOrI)
        {
            return false;
        }

        var shiftAmount = shift.AsOp().Op2;
        JITDUMP("Removing unused node:\n  ");
        DISPNODE(shiftAmount);
        BlockRange().Remove(shiftAmount);

        JITDUMP("Removing unused node:\n  ");
        DISPNODE(shift);
        BlockRange().Remove(shift);

        var shiftCount = BitOperations.TrailingZeroCount(scale);
        node.Op1 = index;
        node.Op2 = baseNode;
        node.SetOper(GetShxaddOp((uint)shiftCount, isSlliUw));

        JITDUMP("Base:\n  ");
        DISPNODE(node.Op2);
        JITDUMP("Index:\n  ");
        DISPNODE(node.Op1);
        JITDUMP("New SHXADD node:\n  ");
        DISPNODE(node);
        JITDUMP("\n");

        if (index is GenTreeCast cast && IsIntZeroExtCast(cast))
        {
            var source = cast.CastOp;
            JITDUMP("Removing unused node:\n  ");
            DISPNODE(cast);
            BlockRange().Remove(cast);

            node.Op1 = source;
            node.SetOper(GetShxaddOp((uint)shiftCount, true));

            JITDUMP("Index:\n  ");
            DISPNODE(node.Op1);
            JITDUMP("Transformed SH(X)ADD node to SH(X)ADD_UW node:\n  ");
            DISPNODE(node);
            JITDUMP("\n");
        }

        next = node.Next;
        return true;
    }

    private bool TryLowerZextAddToAddUw(GenTreeOp node, out GenTree? next)
    {
        next = null;
        if (CompilerInstance.opts.OptimizationDisabled)
        {
            return false;
        }

        if (node.IsContained || (node.Oper is not GT_ADD) || node.HasOverflowCheck ||
            ((node.Type.EmitActualSize != EA_8BYTE) && (node.Type.EmitActualSize != EA_BYREF)))
        {
            return false;
        }

        GenTree baseNode;
        GenTree index;
        if (node.Op1.Oper is GT_CAST)
        {
            index = node.Op1;
            baseNode = node.Op2;
        }
        else if (node.Op2.Oper is GT_CAST)
        {
            index = node.Op2;
            baseNode = node.Op1;
        }
        else
        {
            return false;
        }

        assert(baseNode.IsValue && index.IsValue);
        if (baseNode.IsContained || index.IsContained || !varTypeIsIntegralOrI(baseNode.Type) ||
            !varTypeIsIntegralOrI(index.Type) || baseNode.Oper.IsCnsIntOrI || index.Oper.IsCnsIntOrI)
        {
            return false;
        }

        var cast = index.AsCast();
        var source = cast.CastOp;
        if (!IsIntZeroExtCast(cast))
        {
            return false;
        }

        JITDUMP("Removing unused node:\n  ");
        DISPNODE(cast);
        BlockRange().Remove(cast);

        node.Op1 = source;
        node.Op2 = baseNode;
        node.SetOper(GT_ADD_UW);

        JITDUMP("Base:\n  ");
        DISPNODE(node.Op2);
        JITDUMP("Index:\n  ");
        DISPNODE(node.Op1);
        JITDUMP("New ADD_UW node:\n  ");
        DISPNODE(node);
        JITDUMP("\n");

        next = node.Next;
        return true;
    }

    private static bool IsIntZeroExtCast(GenTreeCast cast)
    {
        var sourceType = genActualType(cast.CastOp.Type);
        var castType = cast.CastType;
        var sourceUnsigned = cast.IsUnsigned;
        var castUnsigned = varTypeIsUnsigned(castType);
        return varTypeIsIntegralOrI(sourceType) && varTypeIsIntegralOrI(castType) &&
            (sourceType.Size == 4) && (castType.Size == 8) && (castUnsigned || sourceUnsigned);
    }

    private static genTreeOps GetShxaddOp(uint shiftAmount, bool isUnsigned)
    {
        return (shiftAmount, isUnsigned) switch {
            (1, false) => GT_SH1ADD,
            (2, false) => GT_SH2ADD,
            (3, false) => GT_SH3ADD,
            (1, true) => GT_SH1ADD_UW,
            (2, true) => GT_SH2ADD_UW,
            (3, true) => GT_SH3ADD_UW,
            _ => throw new System.InvalidOperationException(),
        };
    }
#endif

#if TARGET_XARCH
    private GenTreeOp? TryLowerMulWithConstant(GenTreeOp node)
    {
        assert(node.Oper is GT_MUL);
        if (CompilerInstance.opts.MinOpts || !varTypeIsIntegral(node.Type) || node.HasOverflowCheck)
        {
            return null;
        }

        var op1 = node.Op1;
        var op2 = node.Op2;
        if (op1.IsContained || op2.IsContained || !op2.Oper.IsCnsIntOrI)
        {
            return null;
        }

        var constant = op2.AsIntConCommon();
        var value = constant.IconValue;
        if (value is 3 or 5 or 9)
        {
            return null;
        }

        if (nint.IsPow2(value))
        {
            constant.IconValue = (nint)BitOperations.Log2(unchecked((ulong)(nuint)value));
            node.SetOper(GT_LSH);
            ContainCheckShiftRotate(node);
            return node;
        }

#if TARGET_X86
        return null;
#else
        var plusOne = unchecked(value + 1);
        var minusOne = unchecked(value - 1);
        var useSub = nint.IsPow2(plusOne);
        if (!useSub && !nint.IsPow2(minusOne))
        {
            return null;
        }

        var operandUse = new LIR.Use(BlockRange(), ref node.Op1Ref, node);
        op1 = ReplaceWithLclVar(operandUse);
        value = useSub ? plusOne : minusOne;
        node.SetOper(useSub ? GT_SUB : GT_ADD);
        constant.IconValue = (nint)BitOperations.Log2(unchecked((ulong)(nuint)value));

        node.Op1 = CompilerInstance.gtNewBinaryNode(GT_LSH, node.Type, op1, constant);
        var clone = CompilerInstance.gtClone(op1);
        assert(clone is not null);
        node.Op2 = clone;

        BlockRange().Remove(op1);
        BlockRange().Remove(constant);
        BlockRange().InsertBefore(node, clone);
        BlockRange().InsertBefore(node, constant);
        BlockRange().InsertBefore(node, op1);
        BlockRange().InsertBefore(node, node.Op1);

        ContainCheckBinary(node);
        ContainCheckShiftRotate(node.Op1.AsOp());
        return node;
#endif
    }
#endif

    private void ContainCheckMul(GenTreeOp node)
    {
#if TARGET_XARCH
#if TARGET_X86
        assert(node.Oper is GT_MUL or GT_MULHI or GT_MUL_LONG);
#else
        assert(node.Oper is GT_MUL or GT_MULHI);
#endif
        if (varTypeIsFloating(node.Type))
        {
            ContainCheckFloatBinary(node);
            return;
        }

        var op1 = node.Op1;
        var op2 = node.Op2;
        var safeOp1 = true;
        var safeOp2 = true;
        var impliedFirstOperand = (node.IsUnsigned && node.HasOverflowCheckEx) || (node.Oper is GT_MULHI);
        var useLeaEncoding = false;
        GenTreeIntConCommon? immediate = null;
        GenTree? other = null;
        GenTree? memory = null;
        var nodeType = node.Type;
#if TARGET_X86
        if (node.Oper is GT_MUL_LONG)
        {
            impliedFirstOperand = true;
            nodeType = TYP_INT;
        }
#endif
        assert(!varTypeIsSmall(node.Type));
        if (!impliedFirstOperand && (IsContainableImmed(node, op2) || IsContainableImmed(node, op1)))
        {
            if (IsContainableImmed(node, op2))
            {
                immediate = op2.AsIntConCommon();
                other = op1;
            }
            else
            {
                immediate = op1.AsIntConCommon();
                other = op2;
            }

            var value = immediate.IconValue;
            useLeaEncoding = !node.HasOverflowCheckEx && (value is 3 or 5 or 9);
            MakeSrcContained(node, immediate);
            if (IsContainableMemoryOp(other))
            {
                memory = other;
            }
        }

        if (memory is null)
        {
            if ((op2.Type == nodeType) && IsContainableMemoryOp(op2))
            {
                safeOp2 = IsSafeToContainMem(node, op2);
                if (safeOp2)
                {
                    memory = op2;
                }
            }
            if ((memory is null) && (op1.Type == nodeType) && IsContainableMemoryOp(op1))
            {
                safeOp1 = IsSafeToContainMem(node, op1);
                if (safeOp1)
                {
                    memory = op1;
                }
            }
        }
        else if (memory.Type != nodeType)
        {
            memory = null;
        }
        else if (!IsSafeToContainMem(node, memory))
        {
            if (memory == op1)
            {
                safeOp1 = false;
            }
            else
            {
                safeOp2 = false;
            }
            memory = null;
        }

        if (!useLeaEncoding)
        {
            if (memory is not null)
            {
                MakeSrcContained(node, memory);
            }
            else
            {
                if (immediate is not null)
                {
                    assert(other is not null);
                    safeOp1 = (other == op1) && IsSafeToMarkRegOptional(node, op1);
                    safeOp2 = (other == op2) && IsSafeToMarkRegOptional(node, op2);
                }
                else if (impliedFirstOperand)
                {
                    safeOp1 = false;
                    safeOp2 = safeOp2 && IsSafeToMarkRegOptional(node, op2);
                }
                else
                {
                    safeOp1 = safeOp1 && IsSafeToMarkRegOptional(node, op1);
                    safeOp2 = safeOp2 && IsSafeToMarkRegOptional(node, op2);
                }
                SetRegOptionalForBinOp(node, safeOp1, safeOp2);
            }
        }
#elif TARGET_ARM64
        ContainCheckBinary(node);
#elif TARGET_RISCV64
        ContainCheckBinary(node);
#elif TARGET_LOONGARCH64
        ContainCheckBinary(node);
#elif TARGET_WASM
        // Wasm multiplication does not contain operands.
#else
        throw new NotImplementedException("Multiply containment is not ported for this target.");
#endif
    }

    private GenTree? LowerMul(GenTreeOp mul)
    {
#if TARGET_XARCH
#if TARGET_X86
        assert(mul.Oper is GT_MUL or GT_MULHI or GT_MUL_LONG);
#else
        assert(mul.Oper is GT_MUL or GT_MULHI);
#endif
        if (mul.Oper is GT_MUL)
        {
            var replacement = TryLowerMulWithConstant(mul);
            if (replacement is not null)
            {
                return replacement.Next;
            }
        }

        ContainCheckMul(mul);
        return mul.Next;
#elif TARGET_ARM64
        assert(mul.Oper is GT_MUL or GT_MULHI or GT_MUL_LONG);
        if (CompilerInstance.opts.OptimizationEnabled && (mul.Oper is GT_MUL) && mul.IsValidLongMul())
        {
            var op1 = mul.Op1.AsCast();
            var op2 = mul.Op2;
            mul.Flags &= ~(GTF_OVERFLOW | GTF_UNSIGNED);
            if (op1.IsUnsigned)
            {
                mul.Flags |= GTF_UNSIGNED;
            }

            op1.CastOp.IsContained = false;
            mul.Op1 = op1.CastOp;
            BlockRange().Remove(op1);
            if (op2 is GenTreeCast cast)
            {
                cast.CastOp.IsContained = false;
                mul.Op2 = cast.CastOp;
                BlockRange().Remove(op2);
            }
            else
            {
                assert(op2.Oper.IsIntegralConst);
                assert(FitsIn(TYP_INT, op2.AsIntConCommon().IntegralValue));
                op2.Type = TYP_INT;
            }

            mul.SetOper(GT_MUL_LONG);
            mul.Flags &= GTF_COMMON_MASK;
        }

        ContainCheckMul(mul);
        return mul.Next;
#elif TARGET_RISCV64
        assert(mul.Oper.IsMul);
        ContainCheckMul(mul);
        return mul.Next;
#elif TARGET_LOONGARCH64
        assert(mul.Oper.IsMul);
        ContainCheckMul(mul);
        return mul.Next;
#elif TARGET_WASM
        assert(mul.Oper is GT_MUL);
        _ = LowerBinaryArithmetic(mul);
        ContainCheckMul(mul);
        return mul.Next;
#else
        throw new NotImplementedException("Multiply lowering is not ported for this target.");
#endif
    }
}
