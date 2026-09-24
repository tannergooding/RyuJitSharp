// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool TryLowerConditionToFlagsNode(GenTree parent, GenTree condition, out GenCondition code,
        bool allowMultipleFlagsChecks = true)
    {
        code = default;
#if TARGET_XARCH
        JITDUMP("Lowering condition:\n");
        DISPTREERANGE(BlockRange(), condition);
        JITDUMP("\n");

        if (condition.Oper.IsCompare)
        {
            if (!IsInvariantInRange(condition, parent))
            {
                return false;
            }

            var relop = condition.AsOp();
            code = GenCondition.FromRelop(relop);
            var optimizing = CompilerInstance.opts.OptimizationEnabled;
            var op1 = relop.Op1;
            var op2 = relop.Op2;

            // x != x is a NaN check when both local reads denote the same value.
            if (optimizing && (code.Code is GenCondition.FNEU) && op1.Oper.IsLocal &&
                GenTree.Compare(op1, op2) && IsInvariantInRange(op1, relop) && IsInvariantInRange(op2, relop))
            {
                code = new GenCondition(GenCondition.P);
            }

            if (!allowMultipleFlagsChecks && (GenConditionDesc.Get(code).Oper is not GT_NONE))
            {
                return false;
            }

            relop.Type = TYP_VOID;
            relop.Flags |= GTF_SET_FLAGS;
            if (relop.Oper is GT_EQ or GT_NE or GT_LT or GT_LE or GT_GE or GT_GT)
            {
                relop.SetOper(GT_CMP);
                if (code.PreferSwap)
                {
                    (relop.Op1, relop.Op2) = (relop.Op2, relop.Op1);
                    code = GenCondition.Swap(code);
                }
            }
            else if (relop.Oper is GT_BITTEST_EQ or GT_BITTEST_NE)
            {
                relop.SetOper(GT_BT);
            }
            else
            {
                assert(relop.Oper is GT_TEST_EQ or GT_TEST_NE);
                relop.SetOper(GT_TEST);
            }

            if (relop.Next != parent)
            {
                BlockRange().Remove(relop);
                BlockRange().InsertBefore(parent, relop);
            }

            return true;
        }

        if (condition.Oper is GT_SETCC)
        {
            var flagsEnd = condition.Prev;
            assert((flagsEnd is not null) && ((flagsEnd.Flags & GTF_SET_FLAGS) != 0));
            var flagsDef = flagsEnd;
#if TARGET_AMD64
            // CCMP also consumes flags. Move its producer chain as one range, with
            // the native ten-node lookback bound preventing quadratic behavior.
            for (var i = 0; (i < 10) && (flagsDef.Oper is GT_CCMP); i++)
            {
                var previous = flagsDef.Prev;
                assert((previous is not null) && ((previous.Flags & GTF_SET_FLAGS) != 0));
                flagsDef = previous;
            }
#endif
            if (!IsRangeInvariantInRange(flagsDef, flagsEnd, parent, condition))
            {
                return false;
            }

            code = condition.AsCC().Condition;
            if (!allowMultipleFlagsChecks && (GenConditionDesc.Get(code).Oper is not GT_NONE))
            {
                return false;
            }

            var range = BlockRange().RemoveAndGetRange(flagsDef, flagsEnd);
            BlockRange().InsertBefore(parent, range);
            BlockRange().Remove(condition);

            return true;
        }

        return false;
#else
        throw new System.NotImplementedException("Non-xarch condition-to-flags lowering is not ported.");
#endif
    }
}
