// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckCompare(GenTreeOp cmp)
    {
#if TARGET_XARCH
        assert(cmp.Oper.IsCompare || (cmp.Oper is GT_CMP or GT_TEST));
        var op1 = cmp.Op1;
        var op2 = cmp.Op2;
        var op1Type = op1.Type;
        var op2Type = op2.Type;

        if (varTypeIsFloating(op1Type))
        {
            assert(op1Type == op2Type);

            // UCOMIS[S/D] allows memory only in its second operand. Codegen swaps
            // operands for certain ordered/unordered conditions, not the LIR here.
            var otherOp = GenCondition.FromFloatRelop(cmp).PreferSwap ? op1 : op2;
            if (otherOp.IsCnsNonZeroFltOrDbl)
            {
                MakeSrcContained(cmp, otherOp);
            }
            else if (IsContainableMemoryOp(otherOp) && IsSafeToContainMem(cmp, otherOp))
            {
                MakeSrcContained(cmp, otherOp);
            }

            if (!otherOp.IsContained && IsSafeToMarkRegOptional(cmp, otherOp))
            {
                // This may use a spilled value even when containing the original
                // memory operation would move it across an interfering operation.
                MakeSrcRegOptional(cmp, otherOp);
            }

            return;
        }

        bool CanCompareAtMemoryWidth(GenTree memoryOp, GenTree otherOp)
        {
            if (memoryOp.Type == otherOp.Type)
            {
                return true;
            }

            if ((cmp.Oper is not (GT_EQ or GT_NE or GT_LT or GT_LE or GT_GE or GT_GT)) ||
                !CompilerInstance.opts.OptimizationEnabled ||
                !varTypeIsSmall(memoryOp.Type) || !varTypeIsIntegral(otherOp.Type))
            {
                return false;
            }

            var range = IntegralRange.ForType(memoryOp.Type);
            return otherOp.Oper.IsIntegralConst
                ? range.Contains(otherOp.AsIntConCommon().IntegralValue)
                : range.Contains(IntegralRange.ForNode(otherOp, CompilerInstance));
        }

        if (CheckImmedAndMakeContained(cmp, op2))
        {
            if (op1Type == op2Type)
            {
                TryMakeSrcContainedOrRegOptional(cmp, op1);
            }
            else if (IsContainableMemoryOp(op1) && CanCompareAtMemoryWidth(op1, op2) && IsSafeToContainMem(cmp, op1))
            {
                MakeSrcContained(cmp, op1);
            }
        }
        else
        {
            // TEST has no r,rm encoding, but the emitter maps it to rm,r.
            if (IsContainableMemoryOp(op2) && CanCompareAtMemoryWidth(op2, op1) && IsSafeToContainMem(cmp, op2))
            {
                MakeSrcContained(cmp, op2);
            }

            if (!op2.IsContained && IsContainableMemoryOp(op1) &&
                CanCompareAtMemoryWidth(op1, op2) && IsSafeToContainMem(cmp, op1))
            {
                MakeSrcContained(cmp, op1);
            }

            if ((op1Type == op2Type) && !op1.IsContained && !op2.IsContained)
            {
                var candidate = op1.Oper.IsCnsIntOrI ? op2 : PreferredRegOptionalOperand(op1, op2);
                if (IsSafeToMarkRegOptional(cmp, candidate))
                {
                    MakeSrcRegOptional(cmp, candidate);
                }
            }
        }

        // A contained memory operand bounds both values; otherwise small compares
        // have matching operand types.
        var rangeSource = op2.IsContained && !op2.Oper.IsCnsIntOrI ? op2 : op1;
        if (cmp.Oper.IsCompare && (cmp.GetCompareSize() < TYP_INT.Size) && varTypeIsUnsigned(rangeSource.Type))
        {
            cmp.IsUnsigned = true;
        }
#elif TARGET_ARM64
        var op1 = cmp.Op1;
        var op2 = cmp.Op2;
        if (CheckImmedAndMakeContained(cmp, op2))
        {
            return;
        }

        if (cmp.Oper.IsCompare && CheckImmedAndMakeContained(cmp, op1))
        {
            (cmp.Op1, cmp.Op2) = (cmp.Op2, cmp.Op1);
            cmp.SetOper(cmp.Oper.SwapRelop);
            return;
        }

        if (CompilerInstance.opts.OptimizationEnabled && (cmp.Oper.IsCompare || (cmp.Oper is GT_CMP)))
        {
            static void ForceCastOpInRegister(GenTree operand)
            {
                GenTreeCast? cast = operand as GenTreeCast;
                if ((operand.Oper is GT_NEG) && (operand.AsUnOp().Op1 is GenTreeCast negatedCast))
                {
                    cast = negatedCast;
                }

                cast?.CastOp.IsRegOptional = false;
            }

            if (IsContainableUnaryOrBinaryOp(cmp, op2))
            {
                if (cmp.Oper.IsCmpCompare)
                {
                    ForceCastOpInRegister(op2);
                }

                MakeSrcContained(cmp, op2);
                return;
            }

            if (IsContainableUnaryOrBinaryOp(cmp, op1))
            {
                if (cmp.Oper.IsCmpCompare)
                {
                    ForceCastOpInRegister(op1);
                }

                MakeSrcContained(cmp, op1);
                (cmp.Op1, cmp.Op2) = (cmp.Op2, cmp.Op1);
                if (cmp.Oper.IsCompare)
                {
                    cmp.SetOper(cmp.Oper.SwapRelop);
                }
                return;
            }
        }
#elif TARGET_RISCV64
        if (cmp.Op1.IsIntegralConst(0) && !cmp.Op1.AsIntCon().ImmedValNeedsReloc(CompilerInstance))
        {
            MakeSrcContained(cmp, cmp.Op1);
        }

        CheckImmedAndMakeContained(cmp, cmp.Op2);
#elif TARGET_WASM
        // Wasm compare containment remains an optimization opportunity.
#elif TARGET_LOONGARCH64
        CheckImmedAndMakeContained(cmp, cmp.Op2);
#else
        throw new System.NotImplementedException("Comparison containment is not ported for this target.");
#endif
    }
}
