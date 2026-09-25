// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerSwitch(GenTree node)
    {
#if WINDOWS_AMD64_ABI
        assert(node.Oper is GT_SWITCH);
        var compiler = CompilerInstance;
        var original = _block;
        assert(original is not null);
        var descriptor = original.SwitchTargets;
        var cases = descriptor.Cases;
        var jumpCount = cases.Length;
        var successors = descriptor.Succs;
        var targetCount = successors.Length;
#if DEBUG
        assert(!original.TryGetUse(node, out _));
#endif
        JITDUMP($"Lowering switch {FMT_BB(original.bbNum)}, {jumpCount} cases\n");

        if (targetCount == 1)
        {
            JITDUMP($"Lowering switch {FMT_BB(original.bbNum)}: single target; converting to BBJ_ALWAYS\n");
            noway_assert(compiler.opts.OptimizationDisabled);
            original.SetKindAndTargetEdge(BBJ_ALWAYS, cases[0]);
            var extraReferences = original.TargetEdge.DupCount - 1;
            original.TargetEdge.decrementDupCount(extraReferences);
            original.Target.bbRefs -= extraReferences;

            // Retain evaluation of the index, including its side effects.
            var value = node.AsUnOp().Op1;
            var localNumber = compiler.lvaGrabTemp(shortLifetime: true, "Lowering is creating a new local variable");
            compiler.lvaTable[localNumber].Type = value.Type;
            var store = compiler.gtNewStoreLclVarNode(localNumber, value);
            original.InsertAfter(node, store);
            original.Remove(node);

            return store;
        }

        noway_assert(jumpCount >= 2);
        var use = new LIR.Use(original, ref node.AsUnOp().Op1Ref, node);
        _ = ReplaceWithLclVar(use);
        var indexLocal = node.AsUnOp().Op1;
        assert(indexLocal.Oper is GT_LCL_VAR);
        var indexNumber = indexLocal.AsLclVarCommon().LclNum;
        var indexType = indexLocal.Type;
        var defaultBlock = cases[jumpCount - 1].DestinationBlock;
        var followingBlock = original.Next;

        var minimumTableCases = 2;
        if ((followingBlock == cases[0].DestinationBlock) || (followingBlock == defaultBlock))
        {
            minimumTableCases++;
        }

        // Match Windows native argument construction order and dump-visible IDs.
        var defaultLimit = compiler.gtNewIconNode(indexType.ActualType, jumpCount - 2);
        var defaultCondition = new GenTreeOp(GT_GT, TYP_INT, compiler.gtNewLclvNode(indexType, indexNumber), defaultLimit) {
            IsUnsigned = true,
        };
        var defaultJump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, defaultCondition) {
            Flags = node.Flags,
        };
        var conditionRange = LIR.SeqTree(compiler, defaultJump);
        original.InsertAtEnd(conditionRange);
        var switchBlock = compiler.fgSplitBlockAfterNode(original, defaultJump);
        assert(original.Kind is BBJ_ALWAYS);
        assert(original.Target == switchBlock && original.JumpsToNext);
        assert(switchBlock.Kind is BBJ_SWITCH);
        assert(switchBlock.SwitchTargets.HasDefaultCase && switchBlock.IsEmpty);

        // A default edge can also represent ordinary cases. Peel one duplicate
        // and its proportional likelihood, rather than removing the whole edge.
        var defaultEdge = cases[jumpCount - 1];
        var defaultLikelihood = defaultEdge.Likelihood / defaultEdge.DupCount;
        compiler.fgRemoveRefPred(defaultEdge);
        var defaultBranch = compiler.fgAddRefPred(defaultBlock, original);
        defaultBranch.Likelihood = defaultLikelihood;
        defaultEdge.Likelihood -= defaultLikelihood;
        var switchBranch = original.TargetEdge;
        var switchLikelihood = 1.0 - defaultLikelihood;
        switchBranch.Likelihood = switchLikelihood;
        original.SetCond(defaultBranch, switchBranch);
        switchBlock.inheritWeight(original);
        switchBlock.scaleBBWeight(switchLikelihood);

        var useJumpSequence = jumpCount < minimumTableCases;
        if ((targetCount == 2) && (defaultEdge.DupCount == 0))
        {
            var remaining = successors[0] == defaultEdge ? successors[1] : successors[0];
            assert(remaining != defaultEdge);
            switchBlock.SetKindAndTargetEdge(BBJ_ALWAYS, remaining);
            var extraReferences = remaining.DupCount - 1;
            remaining.decrementDupCount(extraReferences);
            remaining.DestinationBlock.bbRefs -= extraReferences;
        }
        else if (useJumpSequence || compiler.compStressCompile(Compiler.STRESS_SWITCH_CMP_BR_EXPANSION, 50))
        {
            JITDUMP($"Lowering switch {FMT_BB(original.bbNum)}: using compare/branch expansion\n");
            var usedSwitchBlock = false;
            var current = switchBlock;
            var anyTargetFollows = false;
            var testedLikelihood = defaultLikelihood;

            for (var index = 0; index < jumpCount - 1; index++)
            {
                var oldEdge = cases[index];
                var target = oldEdge.DestinationBlock;
                var edgeLikelihood = oldEdge.Likelihood;
                var caseLikelihood = edgeLikelihood / oldEdge.DupCount;
                var unlikelyToReachCase = Compiler.fgProfileWeightsEqual(testedLikelihood, 1.0, 0.001);
                var adjustedLikelihood = unlikelyToReachCase ? 0.5 : double.Min(1.0, caseLikelihood / (1.0 - testedLikelihood));
                compiler.fgRemoveRefPred(oldEdge);
                oldEdge.Likelihood = edgeLikelihood - caseLikelihood;

                if (target == followingBlock)
                {
                    anyTargetFollows = true;
                    continue;
                }

                if (usedSwitchBlock)
                {
                    var next = compiler.fgNewBBafter(BBJ_ALWAYS, current, true);
                    var falseEdge = compiler.fgAddRefPred(next, current);
                    var falseLikelihood = 1.0 - current.TrueEdge.Likelihood;
                    falseEdge.Likelihood = falseLikelihood;
                    current.FalseEdge = falseEdge;
                    next.inheritWeight(current);
                    next.scaleBBWeight(falseLikelihood);
                    current = next;
                }
                else
                {
                    assert(current == switchBlock);
                    // Do not share the first new branch's edge with the switch
                    // whose remaining duplicate cases are still being peeled.
                    if (oldEdge.DupCount > 0)
                    {
                        var next = compiler.fgNewBBafter(BBJ_ALWAYS, current, true);
                        var toNext = compiler.fgAddRefPred(next, current);
                        next.inheritWeight(current);
                        current = next;
                        switchBlock.SetKindAndTargetEdge(BBJ_ALWAYS, toNext);
                    }
                    usedSwitchBlock = true;
                }

                testedLikelihood += caseLikelihood;
                var newEdge = compiler.fgAddRefPred(target, current, oldEdge);
                assert(newEdge.DupCount == 1);
                if (!anyTargetFollows && (index == jumpCount - 2))
                {
                    current.SetKindAndTargetEdge(BBJ_ALWAYS, newEdge);
                }
                else
                {
                    // Native SetCond permits a null false edge while a branch
                    // chain is under construction. Initialize the target union
                    // before changing an initially targetless BBJ_ALWAYS's kind.
                    current.bbTrueEdge = newEdge;
                    current.Kind = BBJ_COND;
                    current.bbFalseEdge = null;
                    newEdge.Likelihood = adjustedLikelihood;
                    var caseValue = compiler.gtNewIconNode(indexType.ActualType, index);
                    var condition = new GenTreeOp(GT_EQ, TYP_INT, compiler.gtNewLclvNode(indexType, indexNumber), caseValue);
                    var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
                    current.InsertAtEnd(LIR.SeqTree(compiler, jump));
                }
            }

            if (anyTargetFollows)
            {
                var next = current.Next;
                assert(next is not null);
                var falseEdge = compiler.fgAddRefPred(next, current);
                current.FalseEdge = falseEdge;
                falseEdge.Likelihood = 1.0 - current.TrueEdge.Likelihood;
            }

            if (!usedSwitchBlock)
            {
                JITDUMP($"Lowering switch {FMT_BB(original.bbNum)}: all switch cases were fall-through\n");
                assert(current == switchBlock && current.Kind is BBJ_SWITCH);
                var next = current.Next;
                assert(next is not null);
                var edge = compiler.fgAddRefPred(next, current);
                current.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
                current.RemoveFlags(BBF_DONT_REMOVE);
                _ = compiler.fgRemoveBlock(current, unreachable: false);
            }

            if (switchBlock.hasProfileWeight)
            {
                var inconsistent = false;
                for (var index = 0; index < targetCount; index++)
                {
                    var target = successors[index].DestinationBlock;
                    target.setBBProfileWeight(target.computeIncomingWeight());
                    inconsistent |= target.NumSucc > 0;
                }
                if (inconsistent)
                {
                    JITDUMP($"Switch lowering: Flow out of {FMT_BB(switchBlock.bbNum)} needs to be propagated. Data {(compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                    compiler.fgPgoConsistent = false;
                }
            }
        }
        else
        {
            GenTree switchValue = compiler.gtNewLclvNode(indexType, indexNumber);
            switchBlock.InsertAtEnd(switchValue);
            if (!TryLowerSwitchToBitTest(cases, jumpCount, targetCount, switchBlock, switchValue, defaultLikelihood))
            {
                JITDUMP($"Lowering switch {FMT_BB(original.bbNum)}: using jump table expansion\n");
                if (indexType is not TYP_I_IMPL)
                {
                    switchValue = compiler.gtNewCastNode(TYP_I_IMPL, switchValue, true, TYP_U_IMPL);
                    switchBlock.InsertAtEnd(switchValue);
                }
                var table = new GenTree(GT_JMPTABLE, TYP_I_IMPL);
                var jump = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID, switchValue, table);
                switchBlock.InsertAfter(switchValue, table, jump);

                if (defaultEdge.DupCount == 0)
                {
                    for (var index = 0; index < targetCount; index++)
                    {
                        if (successors[index] == defaultEdge)
                        {
                            switchBlock.SwitchTargets.RemoveSucc(index);
                            break;
                        }
                    }
                    assert(targetCount == switchBlock.SwitchTargets.Succs.Length + 1);
                    targetCount--;
                }
                switchBlock.SwitchTargets.RemoveDefaultCase();

                if (Compiler.fgProfileWeightsEqual(defaultLikelihood, 1.0, 0.001))
                {
                    JITDUMP($"Zero weight switch block {FMT_BB(switchBlock.bbNum)}, distributing likelihoods equally per case\n");
                    var likelihood = 1.0 / (jumpCount - 1);
                    var inconsistent = false;
                    for (var index = 0; index < targetCount; index++)
                    {
                        var edge = successors[index];
                        edge.Likelihood = likelihood * edge.DupCount;
                        if (switchBlock.hasProfileWeight)
                        {
                            var target = edge.DestinationBlock;
                            target.setBBProfileWeight(target.computeIncomingWeight());
                            inconsistent |= target.NumSucc > 0;
                        }
                    }
                    if (inconsistent)
                    {
                        JITDUMP($"Switch lowering: Flow out of {FMT_BB(switchBlock.bbNum)} needs to be propagated. Data {(compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                        compiler.fgPgoConsistent = false;
                    }
                }
                else
                {
                    var scale = 1.0 / (1.0 - defaultLikelihood);
                    JITDUMP($"Scaling switch block {FMT_BB(switchBlock.bbNum)} likelihoods by {FMT_WT(scale)}\n");
                    for (var index = 0; index < targetCount; index++)
                    {
                        var edge = successors[index];
                        var likelihood = scale * edge.Likelihood;
                        if (likelihood > 1.0)
                        {
                            assert(Compiler.fgProfileWeightsEqual(likelihood, 1.0, 0.001));
                            likelihood = 1.0;
                        }
                        edge.Likelihood = likelihood;
                    }
                }
            }
        }

        var followingNode = node.Next;
        original.Remove(node.AsUnOp().Op1);
        original.Remove(node);
        compiler.fgInvalidateDfsTree();

        return followingNode;
#else
        throw new NotImplementedException("Switch lowering outside Windows AMD64 is not ported.");
#endif
    }

    private bool TryLowerSwitchToBitTest(ReadOnlySpan<FlowEdge> jumpTable, int jumpCount, int targetCount,
        BasicBlock switchBlock, GenTree switchValue, double defaultLikelihood)
    {
#if WINDOWS_AMD64_ABI
        assert(jumpCount >= 2);
        assert(targetCount >= 2);
        assert(switchBlock.Kind is BBJ_SWITCH);
        assert(switchValue.Oper is GT_LCL_VAR);

        // The distinct-target count still includes the peeled default case.
        // Reject a third non-default target while constructing the bit table.
        if (targetCount > 3)
        {
            return false;
        }

        var bitCount = jumpCount - 1;
        if (bitCount > TYP_I_IMPL.Size * BITS_PER_BYTE)
        {
            return false;
        }

        FlowEdge? case0Edge = null;
        var case1Edge = jumpTable[0];
        var bitTable = 1UL;
        for (var bitIndex = 1; bitIndex < bitCount; bitIndex++)
        {
            if (jumpTable[bitIndex] == case1Edge)
            {
                bitTable |= 1UL << bitIndex;
            }
            else if (case0Edge is null)
            {
                case0Edge = jumpTable[bitIndex];
            }
            else if (jumpTable[bitIndex] != case0Edge)
            {
                assert(targetCount == 3);
                return false;
            }
        }

        // LowerSwitch eliminates the single non-default target case before
        // calling this helper, so both target edges must have been found.
        assert(case0Edge is not null);
        if (~bitTable <= uint.MaxValue)
        {
            // Loading a 32-bit immediate zero-extends it on x64. Invert a table
            // with all upper bits set to avoid loading an eight-byte immediate.
            bitTable = ~bitTable;
            (case0Edge, case1Edge) = (case1Edge, case0Edge);
        }

        var case0Block = case0Edge.DestinationBlock;
        var case1Block = case1Edge.DestinationBlock;
        JITDUMP($"Lowering switch {FMT_BB(switchBlock.bbNum)} to bit test\n");

        case0Block.bbRefs -= case0Edge.DupCount - 1;
        case1Block.bbRefs -= case1Edge.DupCount - 1;
        case0Edge.decrementDupCount(case0Edge.DupCount - 1);
        case1Edge.decrementDupCount(case1Edge.DupCount - 1);

        if (!Compiler.fgProfileWeightsEqual(defaultLikelihood, 1.0, 0.001))
        {
            var scale = 1.0 / (1.0 - defaultLikelihood);
            case0Edge.Likelihood = double.Min(1.0, scale * case0Edge.Likelihood);
            case1Edge.Likelihood = double.Min(1.0, scale * case1Edge.Likelihood);
        }
        else
        {
            case0Edge.Likelihood = 0.5;
            case1Edge.Likelihood = 0.5;
        }

        switchBlock.SetCond(case1Edge, case0Edge);
        if (switchBlock.hasProfileWeight)
        {
            case0Block.setBBProfileWeight(case0Block.computeIncomingWeight());
            case1Block.setBBProfileWeight(case1Block.computeIncomingWeight());

            if ((case0Block.NumSucc > 0) || (case1Block.NumSucc > 0))
            {
                var consistency = CompilerInstance.fgPgoConsistent ? "is now" : "was already";
                JITDUMP($"TryLowerSwitchToBitTest: Flow out of {FMT_BB(switchBlock.bbNum)} needs to be propagated. Data {consistency} inconsistent.\n");
                CompilerInstance.fgPgoConsistent = false;
            }
        }

        var tableType = bitCount <= TYP_INT.Size * BITS_PER_BYTE ? TYP_INT : TYP_LONG;
        var table = CompilerInstance.gtNewIconNode(tableType, unchecked((nint)bitTable));
        var bitTest = new GenTreeOp(GT_BT, TYP_VOID, table, switchValue);
        bitTest.Flags |= GTF_SET_FLAGS;
        var jump = CompilerInstance.gtNewCC(GT_JCC, TYP_VOID, new GenCondition(GenCondition.C));
        switchBlock.InsertAfter(switchValue, table, bitTest, jump);

        return true;
#else
        throw new NotImplementedException("Switch bit-test lowering outside Windows AMD64 is not ported.");
#endif
    }
}
