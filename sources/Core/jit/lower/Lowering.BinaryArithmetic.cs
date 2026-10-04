// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerBinaryArithmetic(GenTreeOp binOp)
    {
#if TARGET_XARCH
        if (CompilerInstance.opts.OptimizationEnabled && varTypeIsIntegral(binOp.Type) &&
            (binOp.Oper is GT_OR or GT_XOR or GT_AND))
        {
            var replacementNode = TryLowerBitwiseOpToBitOp(binOp);
            if (replacementNode is not null)
            {
                return replacementNode.Next;
            }
        }

#if FEATURE_HW_INTRINSICS
        if (CompilerInstance.opts.OptimizationEnabled && varTypeIsIntegral(binOp.Type))
        {
            if (binOp.Oper is GT_AND)
            {
                var replacementNode = TryLowerAndOpToAndNot(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }

                replacementNode = TryLowerAndOpToResetLowestSetBit(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }

                replacementNode = TryLowerAndOpToExtractLowestSetBit(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }

                replacementNode = TryLowerAndOpToZeroHighBits(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }
            }
            else if (binOp.Oper is GT_XOR)
            {
                var replacementNode = TryLowerXorOpToGetMaskUpToLowestSetBit(binOp);
                if (replacementNode is not null)
                {
                    return replacementNode.Next;
                }
            }
        }
#endif

        ContainCheckBinary(binOp);
#if TARGET_AMD64
        if (CompilerInstance.canUseApxEvexEncoding() && (JitConfig.EnableApxConditionalChaining != 0))
        {
            if ((binOp.Oper is GT_AND or GT_OR) && TryLowerAndOrToCCMP(binOp, out var next))
            {
                return next;
            }
        }
#endif

        return binOp.Next;
#elif TARGET_ARM64
        if (CompilerInstance.opts.OptimizationEnabled)
        {
            if (binOp.Oper is GT_AND)
            {
                GenTree? opNode = null;
                GenTree? notNode = null;
                if (binOp.Op1.Oper is GT_NOT)
                {
                    notNode = binOp.Op1;
                    opNode = binOp.Op2;
                }
                else if (binOp.Op2.Oper is GT_NOT)
                {
                    notNode = binOp.Op2;
                    opNode = binOp.Op1;
                }

                if (notNode is not null)
                {
                    assert(opNode is not null);
                    binOp.Op1 = opNode;
                    binOp.Op2 = notNode.AsUnOp().Op1;
                    binOp.SetOper(GT_AND_NOT);
                    binOp.Flags &= GTF_COMMON_MASK;
                    BlockRange().Remove(notNode);
                }
            }

            if (binOp.Oper is GT_AND or GT_OR)
            {
                if (TryLowerAndOrToCCMP(binOp, out var next))
                {
                    return next;
                }

                if ((binOp.Oper is GT_AND) && TryLowerAndRshToBFX(binOp, out next))
                {
                    return next;
                }
            }

            if ((binOp.Oper is GT_SUB) && TryLowerAddSubToMulLongOp(binOp, out var multiplyNext))
            {
                return multiplyNext;
            }

            if (binOp.Oper is GT_OR or GT_XOR)
            {
                GenTree? opNode = null;
                GenTree? notNode = null;
                if (binOp.Op1.Oper is GT_NOT)
                {
                    notNode = binOp.Op1;
                    opNode = binOp.Op2;
                }
                else if (binOp.Op2.Oper is GT_NOT)
                {
                    notNode = binOp.Op2;
                    opNode = binOp.Op1;
                }

                if (notNode is not null)
                {
                    assert(opNode is not null);
                    binOp.Op1 = opNode;
                    binOp.Op2 = notNode.AsUnOp().Op1;
                    binOp.SetOper(binOp.Oper is GT_OR ? GT_OR_NOT : GT_XOR_NOT);
                    binOp.Flags &= GTF_COMMON_MASK;
                    BlockRange().Remove(notNode);
                }
            }
        }

        ContainCheckBinary(binOp);
        return binOp.Next;
#elif TARGET_RISCV64
        var op1 = binOp.Op1;
        var op2 = binOp.Op2;
        var isOp1Negated = op1.Oper is GT_NOT;
        var isOp2Negated = op2.Oper is GT_NOT;

        ContainCheckBinary(binOp);
        if (!CompilerInstance.opts.OptimizationEnabled)
        {
            return binOp.Next;
        }

        if (CompilerInstance.compOpportunisticallyDependsOn(CORINFO_InstructionSet.InstructionSet_Zbs) &&
            (binOp.Oper is GT_OR or GT_XOR or GT_AND))
        {
            if (op2.Oper.IsIntegralConst)
            {
                var constant = op2.AsIntConCommon();
                var bit = unchecked((ulong)constant.IntegralValue);
                if (binOp.Oper is GT_AND)
                {
                    bit = ~bit;
                }

                if (!op2.IsContained && BitOperations.IsPow2(bit) &&
                    ((op1.Type is not TYP_INT) || (BitOperations.Log2(bit) != 31)))
                {
                    var singleBitOper = binOp.Oper switch {
                        GT_OR => GT_BIT_SET,
                        GT_XOR => GT_BIT_INVERT,
                        GT_AND => GT_BIT_CLEAR,
                        _ => throw new System.InvalidOperationException(),
                    };
                    binOp.SetOper(singleBitOper);
                    bit = (uint)BitOperations.Log2(bit);
                    assert(bit >= 11);
                    constant.IntegralValue = unchecked((long)bit);
                    constant.IsContained = true;
                }
            }
            else if (TryLowerBitwiseOpToBitOp(binOp) is not null)
            {
                op1 = binOp.Op1;
                op2 = binOp.Op2;
                if (op1.Type is TYP_INT)
                {
                    var mask = CompilerInstance.gtNewIconNode(TYP_INT, 0x1F);
                    mask.IsContained = true;
                    BlockRange().InsertAfter(op2, mask);
                    var maskedIndex = CompilerInstance.gtNewBinaryNode(GT_AND, op2.Type, op2, mask);
                    BlockRange().InsertAfter(mask, maskedIndex);
                    binOp.Op2 = maskedIndex;
                }
            }
        }

        op1 = binOp.Op1;
        op2 = binOp.Op2;
        if ((binOp.Oper is GT_AND or GT_OR or GT_XOR) && (isOp1Negated || isOp2Negated) &&
            ((isOp1Negated && isOp2Negated) ||
                CompilerInstance.compOpportunisticallyDependsOn(CORINFO_InstructionSet.InstructionSet_Zbb)))
        {
            if (isOp1Negated)
            {
                var operand = op1.AsUnOp().Op1;
                BlockRange().Remove(op1);
                binOp.Op1 = operand;
                op1 = operand;
            }

            if (isOp2Negated)
            {
                var operand = op2.AsUnOp().Op1;
                BlockRange().Remove(op2);
                binOp.Op2 = operand;
                op2 = operand;
            }

            if (isOp1Negated != isOp2Negated)
            {
                assert(CompilerInstance.compOpportunisticallyDependsOn(
                    CORINFO_InstructionSet.InstructionSet_Zbb));
                op2.IsContained = false;
                if (isOp1Negated)
                {
                    (op1, op2) = (op2, op1);
                    (binOp.Op1, binOp.Op2) = (op1, op2);
                }

                var operNot = binOp.Oper switch {
                    GT_AND => GT_AND_NOT,
                    GT_OR => GT_OR_NOT,
                    GT_XOR => GT_XOR_NOT,
                    _ => throw new System.InvalidOperationException(),
                };
                binOp.SetOper(operNot);
            }
            else if (binOp.Oper is GT_AND or GT_OR)
            {
                assert(isOp1Negated && isOp2Negated);
                var reverseOper = binOp.Oper is GT_AND ? GT_OR : GT_AND;
                binOp.SetOper(reverseOper);
                if (BlockRange().TryGetUse(binOp, out var use))
                {
                    var negation = CompilerInstance.gtNewUnaryNode(GT_NOT, binOp.Type, binOp);
                    BlockRange().InsertAfter(binOp, negation);
                    use.ReplaceWith(negation);
                }
                else
                {
                    binOp.IsUnusedValue = true;
                }
            }
        }

        return binOp.Next;
#elif TARGET_LOONGARCH64
        if (CompilerInstance.opts.OptimizationEnabled && (binOp.Oper is GT_AND))
        {
            GenTree? opNode = null;
            GenTree? notNode = null;
            if (binOp.Op1.Oper is GT_NOT)
            {
                notNode = binOp.Op1;
                opNode = binOp.Op2;
            }
            else if (binOp.Op2.Oper is GT_NOT)
            {
                notNode = binOp.Op2;
                opNode = binOp.Op1;
            }

            if (notNode is not null)
            {
                assert(opNode is not null);
                binOp.Op1 = opNode;
                binOp.Op2 = notNode.AsUnOp().Op1;
                binOp.SetOper(GT_AND_NOT);
                binOp.Flags &= GTF_COMMON_MASK;
                BlockRange().Remove(notNode);
            }
        }

        ContainCheckBinary(binOp);
        return binOp.Next;
#elif TARGET_WASM
        ContainCheckBinary(binOp);
        if (binOp.HasOverflowCheckEx)
        {
            SetMultiplyUsed(binOp.Op1
#if DEBUG
                , "LowerBinaryArithmetic op1 (overflow exception)"
#endif
            );
            SetMultiplyUsed(binOp.Op2
#if DEBUG
                , "LowerBinaryArithmetic op2 (overflow exception)"
#endif
            );
        }

        return binOp.Next;
#else
        throw new NotImplementedException("Binary arithmetic lowering is not ported for this target.");
#endif
    }
}
