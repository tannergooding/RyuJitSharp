// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerSelect(GenTreeConditional select)
    {
#if TARGET_XARCH
        var condition = select.Cond;
        JITDUMP("Lowering select:\n");
        DISPTREERANGE(BlockRange(), select);
        JITDUMP("\n");

        GenTreeOpCC? newSelect = null;
        // x86 decomposition can require SELECT itself to produce flags. Do not
        // turn that form into a consumer of the condition's flags.
        if (((select.Flags & GTF_SET_FLAGS) == 0) &&
            TryLowerConditionToFlagsNode(select, condition, out var selectCondition))
        {
            newSelect = new GenTreeOpCC(GT_SELECTCC, select.Type, selectCondition, select.Op1, select.Op2,
                select, NodeThreading.LIR) {
                Flags = select.Flags,
            };
            newSelect._vnPair.SetBoth(ValueNumStore.NoVN);
            BlockRange().ReplaceNode(select, newSelect);
            ContainCheckSelect(newSelect);

            JITDUMP("Converted to SELECTCC:\n");
            DISPTREERANGE(BlockRange(), newSelect);
            JITDUMP("\n");
        }
        else
        {
            ContainCheckSelect(select);
        }

        return newSelect is not null ? newSelect.Next : select.Next;
#else
        throw new System.NotImplementedException("Non-xarch select lowering is not ported.");
#endif
    }

    private void ContainCheckSelect(GenTreeOp select)
    {
#if TARGET_XARCH
        assert(select.Oper is GT_SELECT or GT_SELECTCC);
        if (select.Oper is GT_SELECTCC)
        {
            // These floating conditions require two CMOVs. Containing an operand
            // would repeat its memory access/address calculation; LSRA does not
            // yet support permitting just one memory operand for these cases.
            switch (select.AsOpCC().Condition.Code)
            {
                case GenCondition.FEQ:
                case GenCondition.FLT:
                case GenCondition.FLE:
                case GenCondition.FNEU:
                case GenCondition.FGEU:
                case GenCondition.FGTU:
                {
                    return;
                }
            }
        }

        var op1 = select.Op1;
        var op2 = select.Op2;
        var operSize = select.Type.Size;
        assert((operSize == 4) || (operSize == TARGET_POINTER_SIZE));

        // Each value has its own conditional instruction, so both can be memory
        // operands unless the condition required the two-CMOV sequence above.
        if (op1.Type.Size == operSize)
        {
            if (IsContainableMemoryOp(op1) && IsSafeToContainMem(select, op1))
            {
                MakeSrcContained(select, op1);
            }
            else if (IsSafeToMarkRegOptional(select, op1))
            {
                MakeSrcRegOptional(select, op1);
            }
        }

        if (op2.Type.Size == operSize)
        {
            if (IsContainableMemoryOp(op2) && IsSafeToContainMem(select, op2))
            {
                MakeSrcContained(select, op2);
            }
            else if (IsSafeToMarkRegOptional(select, op2))
            {
                MakeSrcRegOptional(select, op2);
            }
        }
#else
        throw new System.NotImplementedException("Non-xarch select containment is not ported.");
#endif
    }
}
