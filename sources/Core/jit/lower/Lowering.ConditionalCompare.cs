// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.insCFlags;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private static insCFlags TruthifyingFlags(GenCondition condition)
    {
        if (Configuration.TARGET_ARM64)
        {
            switch (condition.Code)
            {
                case GenCondition.EQ:
                    return INS_FLAGS_Z;
                case GenCondition.NE:
                    return INS_FLAGS_NONE;
                case GenCondition.SGE:
                    return INS_FLAGS_Z;
                case GenCondition.SGT:
                    return INS_FLAGS_NONE;
                case GenCondition.SLE:
                    return INS_FLAGS_NZC;
                case GenCondition.SLT:
                    return INS_FLAGS_NC;
                case GenCondition.UGE:
                    return INS_FLAGS_C;
                case GenCondition.UGT:
                    return INS_FLAGS_C;
                case GenCondition.ULE:
                    return INS_FLAGS_Z;
                case GenCondition.ULT:
                    return INS_FLAGS_NONE;
                default:
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnexpectedConditionType);
                    return default;
            }
        }

        if (Configuration.TARGET_AMD64)
        {
            switch (condition.Code)
            {
                case GenCondition.EQ:
                    return INS_FLAGS_ZF;
                case GenCondition.NE:
                    return INS_FLAGS_NONE;
                case GenCondition.SGE:
                    return INS_FLAGS_NONE;
                case GenCondition.SGT:
                    return INS_FLAGS_NONE;
                case GenCondition.SLE:
                    return INS_FLAGS_ZF;
                case GenCondition.SLT:
                    return INS_FLAGS_SF;
                case GenCondition.UGE:
                    return INS_FLAGS_NONE;
                case GenCondition.UGT:
                    return INS_FLAGS_NONE;
                case GenCondition.ULE:
                    return INS_FLAGS_ZF;
                case GenCondition.ULT:
                    return INS_FLAGS_CF;
                default:
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnexpectedConditionType);
                    return default;
            }
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.ConditionalCompareLoweringIsOnlySupportedForX64AndARM64
    );
        return default!;
    }

    private bool CanConvertOpToCCMP(GenTree operand, GenTree tree)
    {
        return (operand.Oper is GT_EQ or GT_NE or GT_LT or GT_LE or GT_GE or GT_GT) &&
            varTypeIsIntegralOrI(operand.AsOp().Op1.Type) && IsInvariantInRange(operand, tree);
    }

    private bool TryLowerAndOrToCCMP(GenTreeOp tree, out GenTree? next)
    {
        if (!Configuration.TARGET_AMD64 && !Configuration.TARGET_ARM64)
        {
            next = null;
            return false;
        }

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
            JITDUMP($"[{tree.TreeId:D6}] is a potential candidate for CCMP:\n");
            DISPTREERANGE(BlockRange(), tree);
            JITDUMP("\n");
        }

        var canConvertOp2ToCCMP = CanConvertOpToCCMP(op2, tree);
        var canConvertOp1ToCCMP = CanConvertOpToCCMP(op1, tree);

        // Only the leading comparison can keep a contained memory or shifted operand. Prefer
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
            op2 = op1;
        }
        else if (canConvertOp2ToCCMP &&
            TryLowerConditionToFlagsNode(tree, op1, out cond1, allowMultipleFlagsChecks: false))
        {
            // Preserve the leading comparison's containment when both sides are dirty.
        }
        else if (canConvertOp1ToCCMP &&
            TryLowerConditionToFlagsNode(tree, op2, out cond1, allowMultipleFlagsChecks: false))
        {
            op2 = op1;
        }
        else
        {
            JITDUMP($"  ..could not turn [{op1.TreeId:D6}] or [{op2.TreeId:D6}] into a def of flags, bailing\n");
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
        if (!Configuration.TARGET_AMD64 && !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ConditionalCompareContainmentIsNotPortedForThisTarget
    );
            return;
        }

        var operand = compare.Op2;
        if (operand.Oper.IsCnsIntOrI && !operand.AsIntCon().ImmedValNeedsReloc(CompilerInstance) &&
            Emitter.emitIns_valid_imm_for_ccmp(operand.AsIntCon().IconValue))
        {
            MakeSrcContained(compare, operand);
        }
    }
}
