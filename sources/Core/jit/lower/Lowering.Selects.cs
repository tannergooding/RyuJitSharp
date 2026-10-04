// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerSelect(GenTreeConditional select)
    {
        var condition = select.Cond;
#if TARGET_ARM64
        var trueValue = select.Op1;
        var falseValue = select.Op2;
#endif
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

#if TARGET_ARM64
        GenTreeOp result = newSelect is not null ? newSelect : select;
        if ((trueValue.Oper is GT_NOT or GT_NEG or GT_ADD) || (falseValue.Oper is GT_NOT or GT_NEG or GT_ADD))
        {
            TryLowerCselToCSOp(result, condition);
        }
        else if (trueValue.Oper.IsCnsIntOrI || falseValue.Oper.IsCnsIntOrI)
        {
            TryLowerCnsIntCselToCinc(result, condition);
        }
#endif

        return newSelect is not null ? newSelect.Next : select.Next;
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
#elif TARGET_ARM64
        if (select.Op1.IsIntegralConst(0))
        {
            MakeSrcContained(select, select.Op1);
        }
        if (select.Op2.IsIntegralConst(0))
        {
            MakeSrcContained(select, select.Op2);
        }
#elif TARGET_RISCV64
        assert(select.Oper is GT_SELECT);
        assert(CompilerInstance.compOpportunisticallyDependsOn(CORINFO_InstructionSet.InstructionSet_Zicond));
        assert(varTypeIsIntegralOrI(select.Type));

        var conditional = select.AsConditional();
        var condition = conditional.Cond;
        if (condition.Oper.IsCompare)
        {
            var relop = condition.AsOp();
            if (relop.Oper is GT_EQ or GT_NE)
            {
                var relopOp2 = relop.Op2;
                if (relopOp2.IsIntegralConst(0))
                {
                    conditional.CondRef = relop.Op1;
                    if (relop.Oper is GT_EQ)
                    {
                        (conditional.Op1, conditional.Op2) = (conditional.Op2, conditional.Op1);
                    }

                    BlockRange().Remove(relopOp2);
                    BlockRange().Remove(relop);
                }
            }
            else if ((relop.Oper is GT_GE or GT_LE) ||
                (varTypeIsFloating(relop.Op1.Type) && ((relop.Flags & GTF_RELOP_NAN_UN) != 0)))
            {
                var reversed = CompilerInstance.gtTryReverseCond(condition);
                assert(reversed);
                (conditional.Op1, conditional.Op2) = (conditional.Op2, conditional.Op1);
            }
        }

        if (conditional.Op1.IsIntegralConst(0))
        {
            MakeSrcContained(select, conditional.Op1);
        }
        if (conditional.Op2.IsIntegralConst(0))
        {
            MakeSrcContained(select, conditional.Op2);
        }
#elif TARGET_WASM
        // Wasm select operands remain on the stack in their existing order.
#elif TARGET_LOONGARCH64
        assert(false, "GT_SELECT nodes are not supported on LoongArch64.");
#else
        throw new System.NotImplementedException("Select containment is not ported for this target.");
#endif
    }
}
