// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class IfConversionTests
{
#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDoIfConversion")]
    private static extern ref int IfConversionEnabled(ref JitConfigValues config);
#endif

    [Test]
    public static void SimpleStoreCreatesSelectAndRemovesSingleArm()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, final) = CreateIf(compiler, withElse: false);
            // Keep RISC-V on the select path instead of its small-immediate branch fast path.
            var thenStore = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 2048));
            var original = thenStore.RootNode;
            var edge = start.TrueEdge;

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(start.TargetEdge, Is.SameAs(edge));
            Assert.That(start.Target, Is.SameAs(final));
            Assert.That(start.LastStmt, Is.SameAs(thenStore));
            Assert.That(start.FirstStmt, Is.SameAs(thenStore));
            Assert.That(start.Next, Is.SameAs(final));
            Assert.That(thenBlock.FirstStmt, Is.Null);
            Assert.That(final!.bbRefs, Is.EqualTo(1));
            Assert.That(thenStore.RootNode, Is.SameAs(original));
            var select = original.AsLclVar().Data.AsConditional();
            Assert.That(select.Oper, Is.EqualTo(GT_SELECT));
            Assert.That(select.Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(select.Op1.AsLclVar().LclNum, Is.Zero);
            Assert.That(select.Op2.IsIntegralConst(2048), Is.True);
            Assert.That(thenStore.TreeListBegin, Is.Not.Null);
            Assert.That(thenStore.RootNode.Next, Is.Null);
        });
    }

    [Test]
    public static void DiamondStoreKeepsOperandAndConditionEvaluationOrder()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, join) = CreateIf(compiler, withElse: true);
            var elseBlock = start.TrueTarget;
            var thenStore = Store(compiler, thenBlock, 0, compiler.gtNewLclvNode(TYP_INT, 1));
            var elseStore = Store(compiler, elseBlock, 0, compiler.gtNewLclvNode(TYP_INT, 2));
            var condition = start.LastStmt!.RootNode.AsUnOp().Op1;

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(start.Next, Is.SameAs(join));
            Assert.That(start.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(start.Target, Is.SameAs(join));
            Assert.That(join!.bbRefs, Is.EqualTo(1));
            Assert.That(elseBlock.FirstStmt, Is.Null);
            Assert.That(thenStore.RootNode.AsLclVar().Data.AsConditional(), Is.TypeOf<GenTreeConditional>());
            var select = thenStore.RootNode.AsLclVar().Data.AsConditional();
            Assert.That(select.Cond, Is.SameAs(condition));
            Assert.That(select.Op1, Is.SameAs(elseStore.RootNode.AsLclVar().Data));
            Assert.That(select.Op2.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(select.Op1.Next, Is.SameAs(select.Op2));
            Assert.That(select.Op2.Next, Is.SameAs(select));
        });
    }

    [TestCase(0, 1, GT_EQ)]
    [TestCase(1, 0, GT_NE)]
    public static void BooleanArmsFoldToCompareWithoutSelect(int thenValue, int elseValue, genTreeOps result)
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            _ = Store(compiler, start.TrueTarget, 0, compiler.gtNewIconNode(TYP_INT, elseValue));
            var thenStore = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, thenValue));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(thenStore.RootNode.AsLclVar().Data.Oper, Is.EqualTo(result));
        });
    }

    [Test]
    public static void TwoReturnArmsBecomeSingleReturnWithoutMerge()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true, returns: true);
            var thenReturn = Add(compiler, thenBlock, new GenTreeUnOp(
                GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 5)));
            _ = Add(compiler, start.TrueTarget, new GenTreeUnOp(
                GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 9)));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(start.LastStmt, Is.SameAs(thenReturn));
            var select = thenReturn.RootNode.AsUnOp().Op1.AsConditional();
            Assert.That(select.Op1.IsIntegralConst(9), Is.True);
            Assert.That(select.Op2.IsIntegralConst(5), Is.True);
            Assert.That(compiler.fgLastBB, Is.SameAs(start));
            Assert.That(compiler.optReachableBitVecTraits, Is.Not.Null);
            Assert.That(compiler.optReachableBitVec, Is.Not.Null);
        });
    }

    [TestCase(GT_ADD)]
    [TestCase(GT_SUB)]
    public static void ConvertedReturnSelectRetainsCalculatedCostsAndLevel(genTreeOps operation)
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true, returns: true);
            var thenValue = compiler.gtNewBinaryNode(operation, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewLclvNode(TYP_INT, 2));
            var elseValue = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 2), compiler.gtNewLclvNode(TYP_INT, 3));
            var thenReturn = Add(compiler, thenBlock, new GenTreeUnOp(GT_RETURN, TYP_INT, thenValue));
            _ = Add(compiler, start.TrueTarget, new GenTreeUnOp(GT_RETURN, TYP_INT, elseValue));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var result = thenReturn.RootNode;
            var select = result.AsUnOp().Op1.AsConditional();
            var expectedLevel = int.Max(compiler.gtSetEvalOrder(select.Cond),
                int.Max(compiler.gtSetEvalOrder(select.Op1), compiler.gtSetEvalOrder(select.Op2)));
            var expectedEx = select.Cond.CostEx + select.Op1.CostEx + select.Op2.CostEx + 1;
            var expectedSz = select.Cond.CostSz + select.Op1.CostSz + select.Op2.CostSz + 1;

            Assert.That(expectedLevel, Is.GreaterThan(0));
            Assert.That(expectedEx, Is.GreaterThan(1));
            Assert.That(expectedSz, Is.GreaterThan(1));
            Assert.That(compiler.gtSetEvalOrder(result), Is.EqualTo(expectedLevel));
            Assert.That(select.CostEx, Is.EqualTo(expectedEx));
            Assert.That(select.CostSz, Is.EqualTo(expectedSz));
            Assert.That(result.CostEx, Is.EqualTo(expectedEx + 1));
            Assert.That(result.CostSz, Is.EqualTo(expectedSz + 1));
        });
    }

    [Test]
    public static void NullMergeClearsReachabilityScratchWithoutSpendingBudget()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true, returns: true);
            _ = Add(compiler, thenBlock, new GenTreeUnOp(
                GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 5)));
            _ = Add(compiler, start.TrueTarget, new GenTreeUnOp(
                GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 9)));

            var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
            var scratch = BitVecOps.MakeEmpty(traits);
            Assert.That(BitVecOps.TryAddElemD(traits, scratch, start.bbNum), Is.True);
            compiler.optReachableBitVecTraits = traits;
            compiler.optReachableBitVec = scratch;
            var budget = new int[1];
            budget[0] = 7;
            var descriptor = new Compiler.OptIfConversionDsc(compiler, start);

            Assert.That(descriptor.optIfConvert(budget), Is.True);
            Assert.That(start.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(compiler.optReachableBitVecTraits, Is.SameAs(traits));
            Assert.That(compiler.optReachableBitVec, Is.SameAs(scratch));
            Assert.That(BitVecOps.IsMember(traits, scratch, start.bbNum), Is.False);
            Assert.That(budget[0], Is.EqualTo(7));
        });
    }

    [Test]
    public static void SourceStoreInBranchBlockIsSunkOnlyForInvariantValue()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: false);
            var previous = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 22));
            var previousStmt = compiler.gtNewStmt(previous);
            compiler.fgInsertStmtBefore(start, start.LastStmt!, previousStmt);
            Prepare(compiler, previousStmt);
            var thenStore = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 33));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(start.FirstStmt, Is.SameAs(thenStore));
            Assert.That(thenStore.RootNode.AsLclVar().Data.AsConditional().Op1, Is.SameAs(previous.Data));
            Assert.That(start.LastStmt, Is.SameAs(thenStore));
        });
    }

    [TestCase(GTF_VAR_EXPLICIT_INIT)]
    [TestCase(GTF_EMPTY)]
    public static void UnsafePriorStoreRemainsAheadOfSelect(GenTreeFlags flag)
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: false);
            var previous = compiler.gtNewStoreLclVarNode(0,
                flag is GTF_EMPTY ? compiler.gtNewLclvNode(TYP_INT, 1) : compiler.gtNewIconNode(TYP_INT, 4));
            previous.Flags |= flag;
            var previousStmt = compiler.gtNewStmt(previous);
            compiler.fgInsertStmtBefore(start, start.LastStmt!, previousStmt);
            Prepare(compiler, previousStmt);
            // Keep RISC-V from preferring the small-immediate branch over a Zicond select.
            _ = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 2048));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(start.FirstStmt, Is.SameAs(previousStmt));
            Assert.That(start.LastStmt!.RootNode.AsLclVar().Data.AsConditional().Op1.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [TestCase(GTF_SIDE_EFFECT, false)]
    [TestCase(GTF_ORDER_SIDEEFF, false)]
    [TestCase(GTF_ORDER_SIDEEFF, true)]
    public static void SourceAndConditionEffectsRestrictSpeculation(GenTreeFlags flag, bool onCondition)
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: false);
            var source = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 3));
            if (onCondition)
            {
                start.LastStmt!.RootNode.AsUnOp().Op1.Flags |= flag;
            }
            else
            {
                source.Flags |= flag;
            }
            _ = Store(compiler, thenBlock, 0, source);

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    [Test]
    public static void WeightAndLoopReachabilityPreventConversion()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, join) = CreateIf(compiler, withElse: false);
            _ = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 3));
            start.bbWeight = BB_UNITY_WEIGHT * 2;
            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));

            start.bbWeight = BB_UNITY_WEIGHT;
            join!.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(start, join));
            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    public static void ReachabilityBudgetIsSharedAndBlocksMutationOnExhaustion(
        int initialBudget, bool converted)
    {
        WithCompiler(compiler => {
            var (start, thenBlock, join) = CreateIf(compiler, withElse: false);
            // Keep RISC-V on the select path instead of its small-immediate branch fast path.
            _ = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 2048));
            var tail = BasicBlock.New(compiler, BBJ_RETURN);
            tail.bbRefs = 0;
            compiler.fgLastBB!.Next = tail;
            compiler.fgLastBB = tail;
            join!.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(tail, join));
            var budget = new[] { initialBudget };
            var descriptor = new Compiler.OptIfConversionDsc(compiler, start);

            Assert.That(descriptor.optIfConvert(budget), Is.EqualTo(converted));
            Assert.That(start.Kind, Is.EqualTo(converted ? BBJ_ALWAYS : BBJ_COND));
            Assert.That(budget[0], Is.EqualTo(initialBudget == 0 ? 0 : initialBudget - 1));
        });
    }

    [Test]
    public static void DifferentEhRegionAndMultipleStatementsRejectConversion()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: false);
            _ = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 3));
            thenBlock.TryIndex = 0;
            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            thenBlock.bbTryIndex = 0;
            _ = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 4));
            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    [Test]
    public static void AdditionalPredecessorAndUnequalStoreDestinationsRejectConversion()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            _ = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 3));
            _ = Store(compiler, start.TrueTarget, 1, compiler.gtNewIconNode(TYP_INT, 4));
            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
        });

        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: false);
            _ = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 3));
            var extra = BasicBlock.New(compiler, BBJ_RETURN);
            extra.bbRefs = 0;
            compiler.fgLastBB!.Next = extra;
            compiler.fgLastBB = extra;
            extra.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(thenBlock, extra));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    [Test]
    public static void HighCostOperandDoesNotExecuteUnconditionally()
    {
        WithCompiler(compiler => {
            var (start, thenBlock, _) = CreateIf(compiler, withElse: false);
            GenTree value = compiler.gtNewLclvNode(TYP_INT, 1);
            for (var index = 0; index < 12; index++)
            {
                value = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                    value, compiler.gtNewLclvNode(TYP_INT, 2));
            }
            _ = Store(compiler, thenBlock, 0, value);
            Assert.That(value.CostEx, Is.GreaterThan(7));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    [Test]
    public static void SixtyFourBitBooleanSelectZeroExtendsComparison()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_LONG;
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            _ = Store(compiler, start.TrueTarget, 0, compiler.gtNewLconNode(1));
            var store = Store(compiler, thenBlock, 0, compiler.gtNewLconNode(0));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var cast = store.RootNode.AsLclVar().Data.AsCast();
            Assert.That(cast.Type, Is.EqualTo(TYP_LONG));
            Assert.That(cast.IsUnsigned, Is.True);
            Assert.That(cast.CastOp.Oper, Is.EqualTo(GT_EQ));
        });
    }

    [Test]
    public static void FloatSelectAbortsBeforeChangingControlFlow()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_FLOAT;
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            _ = Store(compiler, thenBlock, 0, compiler.gtNewDconNode(TYP_FLOAT, 3.0));
            _ = Store(compiler, start.TrueTarget, 0, compiler.gtNewDconNode(TYP_FLOAT, 4.0));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(thenBlock.FirstStmt!.RootNode.AsLclVar().Data.Oper, Is.EqualTo(GT_CNS_DBL));
        });
    }

    [Test]
    public static void DoubleNaNArmsDoNotSpeculateSelect()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            _ = Store(compiler, thenBlock, 0, compiler.gtNewDconNode(TYP_DOUBLE, double.NaN));
            _ = Store(compiler, start.TrueTarget, 0, compiler.gtNewDconNode(TYP_DOUBLE, -0.0));

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(thenBlock.FirstStmt!.RootNode.AsLclVar().Data.AsDblCon().DconVal,
                Is.NaN);
        });
    }

#if TARGET_RISCV64
    [TestCase(-2048L, 1)]
    [TestCase(2047L, 1)]
    [TestCase(-2049L, 2)]
    [TestCase(2048L, 2)]
    [TestCase(4096L, 1)]
    [TestCase(0x80000000L, 2)]
    [TestCase(long.MinValue, 2)]
    public static void RiscVImmediateLoadCountsMatchInstructionSelection(long immediate, int expectedCount)
    {
        var instructionCount = Emitter.emitLoadImmediate(
            false, EA_PTRSIZE, REG_NA, unchecked((nint)immediate));
        Assert.That(instructionCount, Is.EqualTo(expectedCount));
    }

    [TestCase(6L, 5L, GT_ADD, false)]
    [TestCase(8L, 4L, GT_LSH, false)]
    [TestCase(8L, 0L, GT_LSH, true)]
    public static void RiscVConstantSelectArithmeticUsesCondition(
        long trueValue, long falseValue, genTreeOps expectedOper, bool usesBitIndex)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_LONG;
            EnableZicond(compiler);
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            _ = Store(compiler, start.TrueTarget, 0, compiler.gtNewLconNode(trueValue));
            var store = Store(compiler, thenBlock, 0, compiler.gtNewLconNode(falseValue));
            var condition = start.LastStmt!.RootNode.AsUnOp().Op1;

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var result = store.RootNode.AsLclVar().Data.AsOp();
            Assert.That(result.Oper, Is.EqualTo(expectedOper));
            Assert.That(result.Type, Is.EqualTo(TYP_LONG));
            if (usesBitIndex)
            {
                Assert.That(result.Op1, Is.SameAs(condition));
                Assert.That(result.Op2.IsIntegralConst((nint)3), Is.True);
            }
            else if (expectedOper is GT_ADD)
            {
                Assert.That(
                    result.Op1.IsIntegralConst(unchecked((nint)falseValue)) ||
                    result.Op2.IsIntegralConst(unchecked((nint)falseValue)),
                    Is.True);
                Assert.That(
                    ReferenceEquals(result.Op1, condition) || ReferenceEquals(result.Op2, condition),
                    Is.True);
            }
            else
            {
                Assert.That(result.Op1.IsIntegralConst(unchecked((nint)falseValue)), Is.True);
                Assert.That(result.Op2, Is.SameAs(condition));
            }
        });
    }

    [Test]
    public static void RiscVSelectLocalOperationReusesAndUpdatesNode()
    {
        WithCompiler(compiler => {
            EnableZicond(compiler);
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            var local = compiler.gtNewLclvNode(TYP_INT, 1);
            var operation = compiler.gtNewBinaryNode(
                GT_ADD, TYP_INT, local, compiler.gtNewIconNode(TYP_INT, -1));
            _ = Store(compiler, start.TrueTarget, 0, operation);
            var store = Store(compiler, thenBlock, 0, compiler.gtNewLclvNode(TYP_INT, 1));
            var condition = start.LastStmt!.RootNode.AsUnOp().Op1;

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var result = store.RootNode.AsLclVar().Data.AsOp();
            Assert.That(result, Is.SameAs(operation));
            Assert.That(result.Oper, Is.EqualTo(GT_SUB));
            Assert.That(result.Op1, Is.SameAs(local));
            Assert.That(result.Op2, Is.SameAs(condition));
        });
    }

    [Test]
    public static void RiscVSelectConditionOperationReusesAndUpdatesNode()
    {
        WithCompiler(compiler => {
            EnableZicond(compiler);
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            var expression = compiler.gtNewIconNode(TYP_INT, 2);
            var operation = compiler.gtNewBinaryNode(
                GT_LSH, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1), expression);
            _ = Store(compiler, start.TrueTarget, 0, operation);
            var store = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 0));
            var condition = start.LastStmt!.RootNode.AsUnOp().Op1;

            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var result = store.RootNode.AsLclVar().Data.AsOp();
            Assert.That(result, Is.SameAs(operation));
            Assert.That(result.Oper, Is.EqualTo(GT_LSH));
            Assert.That(result.Op1, Is.SameAs(condition));
            Assert.That(result.Op2, Is.SameAs(expression));
        });
    }

    [Test]
    public static void RiscVSelectRequiresZicond()
    {
        WithCompiler(compiler => {
            compiler.opts.compSupportsISA = default;
            compiler.opts.compSupportsISAExactly = default;
            compiler.opts.compSupportsISAReported = default;
            var (start, thenBlock, _) = CreateIf(compiler, withElse: true);
            var elseBlock = start.TrueTarget;
            var thenStore = Store(compiler, thenBlock, 0, compiler.gtNewIconNode(TYP_INT, 17));
            _ = Store(compiler, elseBlock, 0, compiler.gtNewIconNode(TYP_INT, 23));

            _ = compiler.optIfConversion();

            Assert.That(start.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(thenBlock.FirstStmt, Is.SameAs(thenStore));
            Assert.That(elseBlock.FirstStmt, Is.Not.Null);
        });
    }
#endif

    [Test]
    public static void DisabledPhaseDoesNotClearReachabilityTraits()
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            Assert.That(compiler.optIfConversion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        }, optimized: false);
    }

    private static (BasicBlock Start, BasicBlock Then, BasicBlock? Final) CreateIf(
        Compiler compiler, bool withElse, bool returns = false)
    {
        var start = BasicBlock.New(compiler, BBJ_RETURN);
        var thenBlock = BasicBlock.New(compiler, BBJ_RETURN);
        var elseBlock = withElse ? BasicBlock.New(compiler, BBJ_RETURN) : null;
        var final = returns ? null : BasicBlock.New(compiler, BBJ_RETURN);
        start.Next = thenBlock;
        thenBlock.Next = elseBlock ?? final;
        elseBlock?.Next = final;
        compiler.fgFirstBB = start;
        compiler.fgLastBB = final ?? elseBlock ?? thenBlock;
        compiler.fgPredsComputed = true;
        start.bbRefs = 1;
        thenBlock.bbRefs = 0;
        elseBlock?.bbRefs = 0;
        final?.bbRefs = 0;

        var trueTarget = elseBlock ?? final!;
        start.SetCond(compiler.fgAddRefPred(trueTarget, start),
            compiler.fgAddRefPred(thenBlock, start));
        if (!returns)
        {
            thenBlock.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(final!, thenBlock));
            elseBlock?.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(final!, elseBlock));
        }
        var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 3), compiler.gtNewIconNode(TYP_INT, 0));
        comparison.Flags |= GTF_RELOP_JMP_USED;
        _ = Add(compiler, start, new GenTreeUnOp(GT_JTRUE, TYP_VOID, comparison));
        return (start, thenBlock, final);
    }

    private static Statement Store(Compiler compiler, BasicBlock block, int local, GenTree source)
    {
        return Add(compiler, block, compiler.gtNewStoreLclVarNode(local, source));
    }

    private static Statement Add(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtAtEnd(block, statement);
        Prepare(compiler, statement);
        return statement;
    }

    private static void Prepare(Compiler compiler, Statement statement)
    {
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
    }

#if TARGET_RISCV64
    private static void EnableZicond(Compiler compiler)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zicond);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zicond);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zicond);
    }
#endif

    private static void WithCompiler(Action<Compiler> action, bool optimized = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(!optimized);
        compiler.compHndBBtab = [];
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_INT }
        ];
        compiler.lvaCount = 4;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.fgCalledCount = BB_UNITY_WEIGHT;
#if TARGET_RISCV64
        EnableZicond(compiler);
#endif
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        IfConversionEnabled(ref JitConfig) = 1;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }
}
