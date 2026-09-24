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

        if (CheckImmedAndMakeContained(cmp, op2))
        {
            if (op1Type == op2Type)
            {
                TryMakeSrcContainedOrRegOptional(cmp, op1);
            }
        }
        else if (op1Type == op2Type)
        {
            // TEST has no r,rm encoding, but the emitter maps it to rm,r.
            if (IsContainableMemoryOp(op2) && IsSafeToContainMem(cmp, op2))
            {
                MakeSrcContained(cmp, op2);
            }

            if (!op2.IsContained && IsContainableMemoryOp(op1) && IsSafeToContainMem(cmp, op1))
            {
                MakeSrcContained(cmp, op1);
            }

            if (!op1.IsContained && !op2.IsContained)
            {
                var candidate = op1.Oper.IsCnsIntOrI ? op2 : PreferredRegOptionalOperand(op1, op2);
                if (IsSafeToMarkRegOptional(cmp, candidate))
                {
                    MakeSrcRegOptional(cmp, candidate);
                }
            }
        }
#else
        throw new System.NotImplementedException("Non-xarch comparison containment is not ported.");
#endif
    }
}
