// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void fgAssertionGen(GenTree tree)
    {
        assert(optLocalAssertionProp);
        assert(apTraits is not null);
        var traits = apTraits;
#if DEBUG
        var oldAssertionCount = optAssertionCount;
#endif
        optAssertionGen(tree);

        void Announce(AssertionIndex index, string condition)
        {
#if DEBUG
            if (verbose && (oldAssertionCount == optAssertionCount) && !BitVecOps.IsMember(traits, apLocal, index - 1))
            {
                jitprintf($"GenTreeNode creates {condition}assertion:\n");
                gtDispTree(tree, topOnly: true);
                assert(compCurBB is not null);
                jitprintf($"In {FMT_BB(compCurBB.bbNum)} New Local ");
                optPrintAssertion(optGetAssertion(index), index);
            }
#endif
        }

        void AddImpliedAssertions(AssertionIndex index, ASSERT_TP? assertions)
        {
            var assertion = optGetAssertion(index);
            if (assertion.KindIs(optAssertionKind.OAK_EQUAL) && assertion.Op1.KindIs(optOp1Kind.O1K_LCLVAR) &&
                assertion.Op2.KindIs(optOp2Kind.O2K_CONST_INT))
            {
                ref var local = ref lvaGetDesc(assertion.Op1.LclNum);
                if (varTypeIsIntegral(local.Type) && (assertion.Op2.IntConstant is 0 or 1))
                {
                    var range = new IntegralRange(SymbolicIntegerValue.Zero, SymbolicIntegerValue.One);
                    var extraIndex = optAddAssertion(AssertionDsc.CreateSubrange(this, assertion.Op1.LclNum, range));
                    if (extraIndex != NO_ASSERTION_INDEX)
                    {
                        BitVecOps.AddElemD(traits, assertions, extraIndex - 1);
                        Announce(extraIndex, "[bool range] ");
                    }
                }
            }
        }

        var makeCondAssertions = false;
        if (tree.Oper == GT_JTRUE)
        {
            assert(compCurBB is not null);
            makeCondAssertions = (compCurBB.Kind == BBJ_COND) && (compCurBB.NumSucc == 2);
        }

        if (makeCondAssertions)
        {
            apLocalIfTrue = BitVecOps.MakeCopy(traits, apLocal);
        }

        if (!tree.GeneratesAssertion)
        {
            return;
        }

        var assertionInfo = tree.AssertionInfo;
        if (makeCondAssertions)
        {
            assert(optCrossBlockLocalAssertionProp);
            var index = assertionInfo.AssertionIndex;
            var complement = optFindComplementary(index);
            var ifFalse = assertionInfo.AssertionHoldsOnFalseEdge ? index : complement;
            var ifTrue = assertionInfo.AssertionHoldsOnFalseEdge ? complement : index;
            if (ifTrue != NO_ASSERTION_INDEX)
            {
                Announce(ifTrue, "[if true] ");
                BitVecOps.AddElemD(traits, apLocalIfTrue, ifTrue - 1);
                AddImpliedAssertions(ifTrue, apLocalIfTrue);
            }

            if (ifFalse != NO_ASSERTION_INDEX)
            {
                Announce(ifFalse, "[if false] ");
                BitVecOps.AddElemD(traits, apLocal, ifFalse - 1);
                AddImpliedAssertions(ifFalse, apLocal);
            }
        }
        else
        {
            var index = assertionInfo.AssertionIndex;
            Announce(index, "");
            BitVecOps.AddElemD(traits, apLocal, index - 1);
            AddImpliedAssertions(index, apLocal);
        }
    }

    public void fgMorphTreeDone(GenTree tree) => fgMorphTreeDone(tree, optAssertionPropDone: false);

    public unsafe void fgMorphTreeDone(GenTree tree, bool optAssertionPropDone, int morphNum = 0)
    {
#if DEBUG
        if (verbose && treesBeforeAfterMorph)
        {
            jitprintf($"\nfgMorphTree (after {morphNum}):\n");
            gtDispTree(tree);
            jitstdout().Flush();
        }
#endif
        if (!fgGlobalMorph)
        {
            return;
        }

        tree.SetMorphed(this);
        if (tree.Oper.IsConst || !optLocalAssertionProp || optAssertionPropDone)
        {
            return;
        }

        if (optAssertionCount > 0)
        {
            _ = tree.VisitPhysicalLocalDefNodes(this, def => {
                fgKillDependentAssertions(def.AsLclVarCommon().LclNum, tree);
                return GenTree.VisitResult.Continue;
            });
        }

        fgAssertionGen(tree);
    }
}
