// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.insCFlags;

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if TARGET_AMD64
    private static insCFlags TruthifyingFlags(GenCondition condition)
    {
        return condition.Code switch {
            GenCondition.EQ => INS_FLAGS_ZF,
            GenCondition.NE => INS_FLAGS_NONE,
            GenCondition.SGE => INS_FLAGS_NONE,
            GenCondition.SGT => INS_FLAGS_NONE,
            GenCondition.SLE => INS_FLAGS_ZF,
            GenCondition.SLT => INS_FLAGS_SF,
            GenCondition.UGE => INS_FLAGS_NONE,
            GenCondition.UGT => INS_FLAGS_NONE,
            GenCondition.ULE => INS_FLAGS_ZF,
            GenCondition.ULT => INS_FLAGS_CF,
            _ => throw new InvalidOperationException("Unexpected condition type."),
        };
    }

    private bool CanConvertOpToCCMP(GenTree operand, GenTree tree)
    {
        return (operand.Oper is GT_EQ or GT_NE or GT_LT or GT_LE or GT_GE or GT_GT) &&
            varTypeIsIntegralOrI(operand.AsOp().Op1.Type) && IsInvariantInRange(operand, tree);
    }

    private bool TryLowerAndOrToCCMP(GenTreeOp tree, out GenTree? next)
    {
        assert(tree.Oper is GT_AND or GT_OR);
        next = null;
        if (!CompilerInstance.opts.OptimizationEnabled)
        {
            return false;
        }

        var op1 = tree.Op1;
        var op2 = tree.Op2;
        if (((op1.Oper is GT_EQ or GT_NE or GT_LT or GT_LE or GT_GE or GT_GT) &&
                varTypeIsIntegralOrI(op1.AsOp().Op1.Type)) ||
            ((op2.Oper is GT_EQ or GT_NE or GT_LT or GT_LE or GT_GE or GT_GT) &&
                varTypeIsIntegralOrI(op2.AsOp().Op1.Type)))
        {
#if DEBUG
            JITDUMP($"[{tree.TreeId:D6}] is a potential candidate for CCMP:\n");
#endif
            DISPTREERANGE(BlockRange(), tree);
            JITDUMP("\n");
        }

        var canConvertOp2ToCCMP = CanConvertOpToCCMP(op2, tree);
        var canConvertOp1ToCCMP = CanConvertOpToCCMP(op1, tree);

        // Only the leading comparison can keep a contained memory operand. Prefer
        // a clean CCMP side, but retain the native both-dirty fallback rather than
        // abandon chaining when both relops require uncontainment.
        static bool CcmpSideOperandsAreClean(GenTree relop)
        {
            var relopOp1 = relop.AsOp().Op1;
            var relopOp2 = relop.AsOp().Op2;
            return (relopOp1.Oper.IsIntegralConst || !relopOp1.IsContained) &&
                ((relopOp2 is null) || relopOp2.Oper.IsIntegralConst || !relopOp2.IsContained);
        }

        if (canConvertOp2ToCCMP && (!canConvertOp1ToCCMP || CcmpSideOperandsAreClean(op2)) &&
            TryLowerConditionToFlagsNode(tree, op1, out var cond1, allowMultipleFlagsChecks: false))
        {
            // Convert op2 without swapping the input order.
        }
        else if (canConvertOp1ToCCMP && CcmpSideOperandsAreClean(op1) &&
            TryLowerConditionToFlagsNode(tree, op2, out cond1, allowMultipleFlagsChecks: false))
        {
            (op1, op2) = (op2, op1);
        }
        else if (canConvertOp2ToCCMP &&
            TryLowerConditionToFlagsNode(tree, op1, out cond1, allowMultipleFlagsChecks: false))
        {
            // Preserve the leading comparison's containment when both sides are dirty.
        }
        else if (canConvertOp1ToCCMP &&
            TryLowerConditionToFlagsNode(tree, op2, out cond1, allowMultipleFlagsChecks: false))
        {
            (op1, op2) = (op2, op1);
        }
        else
        {
#if DEBUG
            JITDUMP($"  ..could not turn [{op1.TreeId:D6}] or [{op2.TreeId:D6}] into a def of flags, bailing\n");
#endif
            return false;
        }

        BlockRange().Remove(op2);
        BlockRange().InsertBefore(tree, op2);
        var relop = op2.AsOp();
        var cond2 = GenCondition.FromRelop(relop);
        relop.Op1.IsContained = false;
        relop.Op2.IsContained = false;

        var condition = tree.Oper is GT_AND ? cond1 : GenCondition.Reverse(cond1);
        var flagsValue = TruthifyingFlags(tree.Oper is GT_AND ? GenCondition.Reverse(cond2) : cond2);
        var ccmp = new GenTreeCCMP(TYP_VOID, condition, relop.Op1, relop.Op2, flagsValue, relop, NodeThreading.LIR) {
            Flags = relop.Flags | GTF_SET_FLAGS,
        };
        ccmp._vnPair.SetBoth(ValueNumStore.NoVN);
        BlockRange().ReplaceNode(relop, ccmp);
        ContainCheckConditionalCompare(ccmp);

        // The flags-producing comparison is not a value edge of SETCC. Replace the
        // boolean tree as well, preserving its owning use and logical identity.
        var setcc = new GenTreeCC(GT_SETCC, tree.Type, cond2, tree, NodeThreading.LIR) {
            Flags = tree.Flags,
        };
        setcc._vnPair.SetBoth(ValueNumStore.NoVN);
        BlockRange().ReplaceNode(tree, setcc);

        JITDUMP("Conversion was legal. Result:\n");
        DISPTREERANGE(BlockRange(), setcc);
        JITDUMP("\n");
        next = setcc.Next;
        return true;
    }

    private void ContainCheckConditionalCompare(GenTreeCCMP compare)
    {
        var operand = compare.Op2;
        if (operand.Oper.IsCnsIntOrI && !operand.AsIntCon().ImmedValNeedsReloc(CompilerInstance) &&
            Emitter.emitIns_valid_imm_for_ccmp(operand.AsIntCon().IconValue))
        {
            MakeSrcContained(compare, operand);
        }
    }
#endif
}
