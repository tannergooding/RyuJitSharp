// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class FlowGraphCleanupTests
{
#if !JIT32_GCENCODER
    [TestCase(NodeThreading.None, false)]
    [TestCase(NodeThreading.AllTrees, false)]
    [TestCase(NodeThreading.AllLocals, false)]
    [TestCase(NodeThreading.AllTrees, true)]
    public static void ReturnNormalizationPreservesIdentityAndCreatesFalseThenTrueBlocks(NodeThreading threading, bool eh)
    {
        WithCompiler(threading, compiler => {
            compiler.info.compRetType = TYP_UBYTE;
            var block = NewBlock(compiler, BBJ_RETURN);
            var tail = NewBlock(compiler, BBJ_RETURN);
            block.Next = tail;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = tail;
            block.bbCodeOffs = 5;
            block.bbCodeOffsEnd = 12;
            block.setBBProfileWeight(81);
            var comparison = Comparison(compiler, 0, GT_LT, 10);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, comparison);
            var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
            var context = new InlineContext(strategy) { _ilSize = 20 };
#if DEBUG
            context._ilInstsSet = new BitArray(20, true);
#endif
            var debugInfo = new DebugInfo(context, new ILLocation(5, 0));
            var statement = compiler.fgNewStmtFromTree(ret, di: debugInfo);
            compiler.fgInsertStmtAtEnd(block, statement);
            var originalBegin = statement.TreeListBegin;
            var originalCount = compiler.fgBBcount;
            if (eh)
            {
                block.TryIndex = 0;
                compiler.compHndBBtab = [
                    new EHblkDsc { ebdTryBeg = block, ebdTryLast = block, ebdHndBeg = tail, ebdHndLast = tail }
                ];
                compiler.compHndBBtabCount = 1;
            }

            Assert.That(compiler.fgDedupReturnComparison(block), Is.True);
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            var jump = statement.RootNode;
            var trueBlock = block.TrueTarget;
            var falseBlock = block.FalseTarget;
            Assert.That(jump.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(jump.Type, Is.EqualTo(TYP_VOID));
            Assert.That(jump.AsUnOp().Op1, Is.SameAs(comparison));
            Assert.That(comparison.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE),
                Is.EqualTo(GTF_RELOP_JMP_USED | GTF_DONT_CSE));
            Assert.That(block.Next, Is.SameAs(falseBlock));
            Assert.That(falseBlock.Next, Is.SameAs(trueBlock));
            Assert.That(trueBlock.Next, Is.SameAs(tail));
            Assert.That(tail.Prev, Is.SameAs(trueBlock));
            Assert.That(compiler.fgBBcount, Is.EqualTo(originalCount + 2));
            Assert.That(compiler.fgLastBB, Is.SameAs(tail));
            Assert.That(block.TrueEdge.Likelihood, Is.EqualTo(0.5));
            Assert.That(block.FalseEdge.Likelihood, Is.EqualTo(0.5));
            BasicBlock[] successors = [trueBlock, falseBlock];
            foreach (var successor in successors)
            {
                Assert.That(successor.bbWeight, Is.EqualTo(40.5));
                Assert.That(successor.hasProfileWeight, Is.True);
                Assert.That(successor.HasFlag(BBF_INTERNAL), Is.True);
                Assert.That(successor.bbCodeOffs, Is.EqualTo(12));
                Assert.That(successor.bbCodeOffsEnd, Is.EqualTo(12));
                Assert.That(successor.bbRefs, Is.EqualTo(1));
                Assert.That(successor.GetUniquePred(compiler), Is.SameAs(block));
                var root = successor.LastStmt?.RootNode ?? throw new AssertionException("Missing return.");
                Assert.That(root.Oper, Is.EqualTo(GT_RETURN));
                Assert.That(root.AsUnOp().Op1.IsIntegralConst(successor == trueBlock ? 1 : 0), Is.True);
                Assert.That(successor.LastStmt?.DebugInfo.GetRoot().Location.Offset, Is.EqualTo(5));
            }

            Assert.That(ret.Prev is null && ret.Next is null, Is.True);
            Assert.That(statement.TreeListBegin, Is.SameAs(originalBegin));
            if (threading is NodeThreading.AllTrees)
            {
                Assert.That(comparison.Next, Is.SameAs(jump));
                Assert.That(jump.Prev, Is.SameAs(comparison));
                Assert.That(jump.Next, Is.Null);
            }

            if (eh)
            {
                Assert.That(trueBlock.TryIndex, Is.EqualTo(0));
                Assert.That(falseBlock.TryIndex, Is.EqualTo(0));
                Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(trueBlock));
            }
#if DEBUG
            Assert.That(jump.TreeId, Is.EqualTo(ret.TreeId));
            Assert.That(trueBlock.LastStmt?.RootNode.TreeId, Is.Not.EqualTo(jump.TreeId));
#endif
        });
    }

    [TestCase("type")]
    [TestCase("epilogue")]
    [TestCase("empty")]
    [TestCase("not-return")]
    [TestCase("not-compare")]
    [TestCase("test-compare")]
    public static void ReturnNormalizationRejectsNonComparisonReturns(string reason)
    {
        WithCompiler(NodeThreading.None, compiler => {
            compiler.info.compRetType = reason == "type" ? TYP_INT : TYP_UBYTE;
            var block = NewBlock(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            GenTree comparison = Comparison(compiler, 0, reason == "test-compare" ? GT_TEST_EQ : GT_EQ, 0);
            if (reason == "not-compare")
            {
                comparison = compiler.gtNewIconNode(TYP_INT, 1);
            }

            if (reason != "empty")
            {
                _ = Append(compiler, block, reason == "not-return"
                    ? comparison : new GenTreeUnOp(GT_RETURN, TYP_INT, comparison));
            }

            if (reason == "epilogue")
            {
                compiler.genReturnBB = block;
            }

            var count = compiler.fgBBcount;
            Assert.That(compiler.fgDedupReturnComparison(block), Is.False);
            Assert.That(block.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(compiler.fgBBcount, Is.EqualTo(count));
        });
    }
#else
    [Test]
    public static void LegacyEncoderDoesNotAddEpilogues()
    {
        WithCompiler(NodeThreading.None, compiler => {
            Assert.That(compiler.fgDedupReturnComparison(NewBlock(compiler, BBJ_RETURN)), Is.False);
        });
    }
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void NewBlockFromTreeHonorsSideEffectUpdateOption(bool update)
    {
        WithCompiler(NodeThreading.None, compiler => {
            var block = NewBlock(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            block.bbCodeOffsEnd = 15;
            var constant = compiler.gtNewIconNode(TYP_INT, 1);
            constant.Flags |= GTF_CALL;

            var next = compiler.fgNewBBFromTreeAfter(BBJ_RETURN, block, constant, default, update);

            Assert.That(next.FirstStmt?.RootNode, Is.SameAs(constant));
            Assert.That(next.bbCodeOffs, Is.EqualTo(15));
            Assert.That(next.bbCodeOffsEnd, Is.EqualTo(15));
            Assert.That(constant.Flags & GTF_CALL, Is.EqualTo(update ? GTF_EMPTY : GTF_CALL));
            Assert.That(compiler.fgLastBB, Is.SameAs(next));
        });
    }

    [TestCase(NodeThreading.None, false)]
    [TestCase(NodeThreading.AllTrees, false)]
    [TestCase(NodeThreading.AllLocals, false)]
    [TestCase(NodeThreading.AllTrees, true)]
    public static void TailDuplicationClonesNonPhisAndTransfersProfileProvenance(NodeThreading threading, bool intermediate)
    {
        WithCompiler(threading, compiler => {
            var (source, target, trueBlock, falseBlock) = CreateTailGraph(compiler, intermediate);
            var sourceLast = source.LastStmt;
            var targetStatements = target.Statements.ToArray();
            var incoming = source.TargetEdge;
            source.setBBProfileWeight(40);
            target.setBBProfileWeight(100);
            target.TrueEdge.Likelihood = 0.25;
            target.FalseEdge.Likelihood = 0.75;
            target.TrueEdge.isHeuristicBased = true;
            target.FalseEdge.isHeuristicBased = true;
            var phi = compiler.gtNewStmt(new GenTreeLclVar(TYP_INT, 2, new GenTreePhi(TYP_INT)));
            var first = target.FirstStmt ?? throw new AssertionException("Missing target statement.");
            compiler.fgInsertStmtBefore(target, first, phi);

            Assert.That(compiler.fgOptimizeUncondBranchToSimpleCond(source, target), Is.True);
            Assert.That(source.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(source.TrueEdge, Is.SameAs(incoming));
            Assert.That(source.TrueTarget, Is.SameAs(trueBlock));
            Assert.That(source.FalseTarget, Is.SameAs(falseBlock));
            Assert.That(source.TrueEdge.Likelihood, Is.EqualTo(0.25));
            Assert.That(source.FalseEdge.Likelihood, Is.EqualTo(0.75));
            Assert.That(source.TrueEdge.isHeuristicBased && source.FalseEdge.isHeuristicBased, Is.True);
            Assert.That(target.bbWeight, Is.EqualTo(60));
            Assert.That(target.bbRefs, Is.EqualTo(1));
            Assert.That(trueBlock.bbRefs, Is.EqualTo(2));
            Assert.That(falseBlock.bbRefs, Is.EqualTo(2));
            Assert.That(target.FirstStmt, Is.SameAs(phi));
            var clone = sourceLast?.NextStmt;
            foreach (var original in targetStatements)
            {
                Assert.That(clone, Is.Not.Null);
                var cloneStatement = clone ?? throw new AssertionException("Missing clone.");
                Assert.That(cloneStatement.RootNode, Is.Not.SameAs(original.RootNode));
                Assert.That(cloneStatement.RootNode.Oper, Is.EqualTo(original.RootNode.Oper));
                Assert.That(cloneStatement.TreeListBegin, Is.Null);
#if DEBUG
                Assert.That(cloneStatement.RootNode.TreeId, Is.Not.EqualTo(original.RootNode.TreeId));
#endif
                clone = cloneStatement.NextStmt;
            }

            Assert.That(clone, Is.Null);
            Assert.That(source.LastStmt?.RootNode.Oper, Is.EqualTo(GT_JTRUE));
        });
    }

    [TestCase("eh")]
    [TestCase("not-cond")]
    [TestCase("not-join")]
    [TestCase("self-edge")]
    [TestCase("too-many-statements")]
    [TestCase("no-information")]
    [TestCase("cold")]
    [TestCase("address-exposed")]
    [TestCase("third-statement")]
    public static void TailDuplicationRefusesUnprofitableOrUnsafeCandidates(string reason)
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (source, target, _, _) = CreateTailGraph(compiler, false);
            switch (reason)
            {
                case "eh":
                {
                    target.TryIndex = 0;
                    break;
                }

                case "not-cond":
                {
                    target.Kind = BBJ_RETURN;
                    break;
                }

                case "not-join":
                {
                    target.bbRefs = 1;
                    break;
                }

                case "self-edge":
                {
                    compiler.fgRedirectEdge(ref target.TrueEdgeRef, target);
                    break;
                }

                case "too-many-statements":
                {
                    var first = target.FirstStmt ?? throw new AssertionException("Missing target.");
                    compiler.fgInsertStmtBefore(target, first, compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1)));
                    compiler.fgInsertStmtBefore(target, first, compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 2)));
                    break;
                }

                case "no-information":
                {
                    source.FirstStmt = null;
                    break;
                }

                case "cold":
                {
                    source.bbWeight = 0;
                    break;
                }

                case "address-exposed":
                {
                    compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
                    break;
                }

                case "third-statement":
                {
                    _ = Append(compiler, source, compiler.gtNewIconNode(TYP_INT, 1));
                    _ = Append(compiler, source, compiler.gtNewIconNode(TYP_INT, 2));
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown refusal.");
                }
            }

            var edge = source.TargetEdge;
            var last = source.LastStmt;
            Assert.That(compiler.fgOptimizeUncondBranchToSimpleCond(source, target), Is.False);
            Assert.That(source.TargetEdge, Is.SameAs(edge));
            Assert.That(source.LastStmt, Is.SameAs(last));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FavorableStoreCanBePenultimateAndCanContainArrayLengthOrRelop(bool arrayLength)
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (source, target, _, _) = CreateTailGraph(compiler, false);
            var store = source.LastStmt?.RootNode.AsLclVar() ?? throw new AssertionException("Missing store.");
            compiler.lvaTable[2].Type = TYP_REF;
            GenTree value = arrayLength
                ? new GenTreeArrLen(TYP_INT, compiler.gtNewLclvNode(TYP_REF, 2), 8)
                : Comparison(compiler, 1, GT_EQ, 0);
            store.DataRef = value;
            _ = Append(compiler, source, compiler.gtNewIconNode(TYP_INT, 123));
            Assert.That(compiler.fgOptimizeUncondBranchToSimpleCond(source, target), Is.True);
        });
    }

    [TestCase(false, false, 100)]
    [TestCase(false, true, 100)]
    [TestCase(true, false, 100)]
    [TestCase(true, true, 60)]
    public static void TailDuplicationDebitsOnlyWhenBothWeightsAreProfileDerived(
        bool sourceProfile, bool targetProfile, int expected)
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (source, target, _, _) = CreateTailGraph(compiler, false);
            source.bbWeight = 40;
            target.bbWeight = 100;
            if (sourceProfile)
            {
                source.SetFlags(BBF_PROF_WEIGHT);
            }

            if (targetProfile)
            {
                target.SetFlags(BBF_PROF_WEIGHT);
            }

            Assert.That(compiler.fgOptimizeUncondBranchToSimpleCond(source, target), Is.True);
            Assert.That(target.bbWeight, Is.EqualTo(expected));
        });
    }

    [TestCase("casts", true, 0)]
    [TestCase("same-local", true, 0)]
    [TestCase("different-locals", false, -1)]
    [TestCase("constants", false, -1)]
    [TestCase("complex-left", false, -1)]
    [TestCase("complex-right", false, -1)]
    [TestCase("not-relop", false, -1)]
    [TestCase("not-jtrue", false, -1)]
    [TestCase("intermediate", true, 1)]
    [TestCase("intermediate-wrong-local", false, 0)]
    [TestCase("intermediate-unary", false, 0)]
    [TestCase("intermediate-distinct-locals", false, 0)]
    public static void CandidateRecognizesOnlyNativeLocalAndConstantShapes(string shape, bool accepted, int local)
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (_, target, _, _) = CreateTailGraph(compiler, false);
            var statement = target.LastStmt ?? throw new AssertionException("Missing target.");
            var condition = statement.RootNode.AsUnOp().Op1.AsOp();
            switch (shape)
            {
                case "casts":
                {
                    condition.Op1 = compiler.gtNewCastNode(TYP_LONG, condition.Op1, false, TYP_LONG);
                    condition.Op2 = compiler.gtNewCastNode(TYP_LONG, condition.Op2, false, TYP_LONG);
                    break;
                }

                case "same-local":
                {
                    condition.Op2 = compiler.gtNewLclvNode(TYP_INT, 0);
                    break;
                }

                case "different-locals":
                {
                    condition.Op2 = compiler.gtNewLclvNode(TYP_INT, 1);
                    break;
                }

                case "constants":
                {
                    condition.Op1 = compiler.gtNewIconNode(TYP_INT, 1);
                    break;
                }

                case "complex-left":
                {
                    condition.Op1 = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, condition.Op1);
                    break;
                }

                case "complex-right":
                {
                    condition.Op2 = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, condition.Op2);
                    break;
                }

                case "not-relop":
                {
                    statement.RootNode.AsUnOp().Op1 = compiler.gtNewLclvNode(TYP_INT, 0);
                    break;
                }

                case "not-jtrue":
                {
                    statement.RootNode = condition;
                    break;
                }

                default:
                {
                    GenTree data = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                        compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 1));
                    if (shape == "intermediate-unary")
                    {
                        data = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 1));
                    }
                    else if (shape == "intermediate-distinct-locals")
                    {
                        data.AsOp().Op2 = compiler.gtNewLclvNode(TYP_INT, 2);
                    }

                    var store = compiler.gtNewStoreLclVarNode(shape == "intermediate-wrong-local" ? 1 : 0, data);
                    compiler.fgInsertStmtBefore(target, statement, compiler.gtNewStmt(store));
                    break;
                }
            }

            Assert.That(IsCandidate(compiler, target, out var lclNum), Is.EqualTo(accepted));
            Assert.That(lclNum, Is.EqualTo(local));
        });
    }

    [TestCase(NodeThreading.None, false, TYP_INT, 5, 5, true)]
    [TestCase(NodeThreading.AllTrees, false, TYP_INT, 5, 6, false)]
    [TestCase(NodeThreading.AllLocals, true, TYP_INT, 5, 5, true)]
    [TestCase(NodeThreading.AllTrees, false, TYP_BYTE, 255, -1, true)]
    [TestCase(NodeThreading.AllTrees, true, TYP_UBYTE, 257, 1, true)]
    [TestCase(NodeThreading.None, false, TYP_SHORT, 65535, -1, true)]
    [TestCase(NodeThreading.AllTrees, false, TYP_USHORT, 65537, 1, true)]
    public static void ForwardSubstitutionFoldsEitherOperandAndTruncatesSmallStores(
        NodeThreading threading, bool localOnRight, var_types localType, int stored, int compared, bool expected)
    {
        WithCompiler(threading, compiler => {
            compiler.lvaTable[0].Type = localType;
            var (block, trueBlock, falseBlock) = CreateConditional(compiler);
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, stored));
            var storeStatement = Append(compiler, block, store);
            var condition = Comparison(compiler, 0, GT_EQ, compared);
            if (localOnRight)
            {
                (condition.Op1, condition.Op2) = (condition.Op2, condition.Op1);
            }

            var local = localOnRight ? condition.Op2 : condition.Op1;
            var originalData = store.Data;
            var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            _ = Append(compiler, block, jump);
            var retained = expected ? block.TrueEdge : block.FalseEdge;

            Assert.That(compiler.fgFoldSimpleCondByForwardSub(block), Is.True);
            Assert.That(block.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(block.Target, Is.SameAs(expected ? trueBlock : falseBlock));
            Assert.That(block.TargetEdge, Is.SameAs(retained));
            Assert.That(block.TargetEdge.Likelihood, Is.EqualTo(1));
            Assert.That(block.FirstStmt, Is.SameAs(storeStatement));
            Assert.That(block.LastStmt, Is.SameAs(storeStatement));
            Assert.That(store.Data, Is.SameAs(originalData));
            Assert.That(store.Data.IsIntegralConst(stored), Is.True);
            Assert.That(local.Prev is null && local.Next is null, Is.True);
            var folded = jump.Op1;
            Assert.That(folded.IsIntegralConst(expected ? 1 : 0), Is.True);
#if DEBUG
            Assert.That(folded.TreeId, Is.EqualTo(condition.TreeId));
#endif
        });
    }

    [TestCase("no-store")]
    [TestCase("different-local")]
    [TestCase("nonconstant")]
    [TestCase("type-mismatch")]
    [TestCase("not-relop")]
    [TestCase("two-locals")]
    [TestCase("intervening")]
    public static void ForwardSubstitutionLeavesNonmatchingDefinitionsAlone(string reason)
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (block, _, _) = CreateConditional(compiler);
            var store = compiler.gtNewStoreLclVarNode(reason == "different-local" ? 1 : 0,
                reason == "nonconstant" ? compiler.gtNewLclvNode(TYP_INT, 1) : compiler.gtNewIconNode(TYP_INT, 5));
            if (reason == "type-mismatch")
            {
                store.DataRef = compiler.gtNewIconNode(TYP_LONG, 5);
            }

            if (reason != "no-store")
            {
                _ = Append(compiler, block, store);
            }

            if (reason == "intervening")
            {
                _ = Append(compiler, block, compiler.gtNewIconNode(TYP_INT, 7));
            }

            GenTree condition = Comparison(compiler, 0, GT_EQ, 5);
            if (reason == "not-relop")
            {
                condition = compiler.gtNewLclvNode(TYP_INT, 0);
            }
            else if (reason == "two-locals")
            {
                condition.AsOp().Op2 = compiler.gtNewLclvNode(TYP_INT, 1);
            }

            var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            _ = Append(compiler, block, jump);
            Assert.That(compiler.fgFoldSimpleCondByForwardSub(block), Is.False);
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(jump.Op1, Is.SameAs(condition));
        });
    }

    [TestCase(NodeThreading.None)]
    [TestCase(NodeThreading.AllTrees)]
    [TestCase(NodeThreading.AllLocals)]
    public static void DisabledOptimizationStillSubstitutesButDoesNotChangeFlow(NodeThreading threading)
    {
        WithCompiler(threading, compiler => {
            var (block, _, _) = CreateConditional(compiler);
            _ = Append(compiler, block, compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 5)));
            var condition = Comparison(compiler, 0, GT_EQ, 5);
            var originalLocal = condition.Op1;
            var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            var statement = Append(compiler, block, jump);

            Assert.That(compiler.fgFoldSimpleCondByForwardSub(block), Is.False);
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(condition.Op1.IsIntegralConst(5), Is.True);
            Assert.That(condition.Op1, Is.Not.SameAs(originalLocal));
            Assert.That(originalLocal.Prev is null && originalLocal.Next is null, Is.True);
            if (threading is NodeThreading.AllTrees)
            {
                Assert.That(statement.TreeListBegin, Is.SameAs(condition.Op1));
                Assert.That(condition.Next, Is.SameAs(jump));
            }
            else if (threading is NodeThreading.AllLocals)
            {
                Assert.That(statement.TreeListBegin, Is.Null);
                Assert.That(statement.TreeListEnd, Is.Null);
            }
        }, minopts: true);
    }

    private static (BasicBlock Source, BasicBlock Target, BasicBlock TrueBlock, BasicBlock FalseBlock)
        CreateTailGraph(Compiler compiler, bool intermediate)
    {
        var source = NewBlock(compiler, BBJ_ALWAYS);
        var other = NewBlock(compiler, BBJ_ALWAYS);
        var target = NewBlock(compiler, BBJ_COND);
        var trueBlock = NewBlock(compiler, BBJ_RETURN);
        var falseBlock = NewBlock(compiler, BBJ_RETURN);
        source.Next = other;
        other.Next = target;
        target.Next = trueBlock;
        trueBlock.Next = falseBlock;
        compiler.fgFirstBB = source;
        compiler.fgLastBB = falseBlock;
        source.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, source));
        other.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, other));
        target.SetCond(compiler.fgAddRefPred(trueBlock, target), compiler.fgAddRefPred(falseBlock, target));
        target.TrueEdge.Likelihood = 0.5;
        target.FalseEdge.Likelihood = 0.5;
        _ = Append(compiler, source, compiler.gtNewStoreLclVarNode(intermediate ? 1 : 0,
            compiler.gtNewIconNode(TYP_INT, 1)));
        if (intermediate)
        {
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 1));
            _ = Append(compiler, target, compiler.gtNewStoreLclVarNode(0, sum));
        }

        _ = Append(compiler, target, new GenTreeUnOp(GT_JTRUE, TYP_VOID, Comparison(compiler, 0, GT_EQ, 0)));

        return (source, target, trueBlock, falseBlock);
    }

    private static (BasicBlock Block, BasicBlock TrueBlock, BasicBlock FalseBlock) CreateConditional(Compiler compiler)
    {
        var block = NewBlock(compiler, BBJ_COND);
        var trueBlock = NewBlock(compiler, BBJ_RETURN);
        var falseBlock = NewBlock(compiler, BBJ_RETURN);
        block.Next = trueBlock;
        trueBlock.Next = falseBlock;
        compiler.fgFirstBB = block;
        compiler.fgLastBB = falseBlock;
        block.SetCond(compiler.fgAddRefPred(trueBlock, block), compiler.fgAddRefPred(falseBlock, block));
        block.TrueEdge.Likelihood = 0.25;
        block.FalseEdge.Likelihood = 0.75;

        return (block, trueBlock, falseBlock);
    }

    private static GenTreeOp Comparison(Compiler compiler, int local, genTreeOps oper, int value)
    {
        var comparison = compiler.gtNewBinaryNode(oper, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, local), compiler.gtNewIconNode(TYP_INT, value));
        comparison.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;

        return comparison;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = compiler.fgNewStmtFromTree(tree);
        compiler.fgInsertStmtAtEnd(block, statement);

        return statement;
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;

        return block;
    }

    private static void WithCompiler(NodeThreading threading, Action<Compiler> action, bool minopts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        if (minopts)
        {
            flags.Set(JitFlags.JIT_FLAG_MIN_OPT);
        }

        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }
        ];
        compiler.lvaCount = 3;
        compiler.compHndBBtab = [];
        compiler.fgNodeThreading = threading;
        compiler.fgPredsComputed = true;
        compiler.fgPgoConsistent = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgBlockIsGoodTailDuplicationCandidate")]
    private static extern bool IsCandidate(Compiler compiler, BasicBlock target, out int lclNum);
}
