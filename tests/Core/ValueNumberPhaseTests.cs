// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using AssertionOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class ValueNumberPhaseTests
{
    [Test]
    public static void SkippedSsaLeavesAssertionStateUnchanged()
    {
        WithCompiler(1, compiler => {
            var block = CreateGraph(compiler, [[]])[0];
            nint[] previousOut = [17];
            block.bbAssertionOut = previousOut;

            Assert.That(compiler.optAssertionPropMain(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.bbAssertionOut, Is.SameAs(previousOut));
            Assert.That(compiler.apTraits, Is.Null);
        });
    }

    [Test]
    public static void AssertionPhaseSubstitutesAValueNumberedLocalConstant()
    {
        WithCompiler(1, compiler => {
            var block = CreateGraph(compiler, [[]])[0];
            _ = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7)));
            var statement = AddStatement(compiler, block,
                new GenTreeUnOp(GT_RETURN, TYP_INT, new GenTreeLclVar(TYP_INT, 0)));
            PrepareAssertionPropagation(compiler);

            Assert.That(compiler.optAssertionPropMain(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var result = statement.RootNode.AsUnOp().Op1;
            Assert.That(result.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)7));
        });
    }

    [TestCase(0)]
    [TestCase(7)]
    public static void AssertionPhasePropagatesEqualityOnlyAlongTheTrueEdge(int constant)
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].lvIsParam = true;
            var blocks = CreateGraph(compiler, [[1, 2], [], []]);
            _ = AddStatement(compiler, blocks[0], new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(GT_EQ, TYP_INT, new GenTreeLclVar(TYP_INT, 0),
                    compiler.gtNewIconNode(TYP_INT, constant))));
            var trueReturn = AddStatement(compiler, blocks[1],
                new GenTreeUnOp(GT_RETURN, TYP_INT, new GenTreeLclVar(TYP_INT, 0)));
            var falseReturn = AddStatement(compiler, blocks[2],
                new GenTreeUnOp(GT_RETURN, TYP_INT, new GenTreeLclVar(TYP_INT, 0)));
            PrepareAssertionPropagation(compiler);

            Assert.That(compiler.optAssertionPropMain(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var result = trueReturn.RootNode.AsUnOp().Op1;
            Assert.That(result.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)constant));
            Assert.That(falseReturn.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(compiler.AssertionCount, Is.GreaterThan(0));
        });
    }

    [TestCase(GT_EQ, 2)]
    [TestCase(GT_NE, 3)]
    public static void AssertionPhaseRepairsOutgoingFactsAfterFoldingAnEdge(genTreeOps comparison, int retained)
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].lvIsParam = true;
            var blocks = CreateGraph(compiler, [[1, 4], [2, 3], [], [], []]);
            _ = AddStatement(compiler, blocks[0], new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(GT_EQ, TYP_INT, new GenTreeLclVar(TYP_INT, 0),
                    compiler.gtNewIconNode(TYP_INT, 7))));
            _ = AddStatement(compiler, blocks[1], new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(comparison, TYP_INT, new GenTreeLclVar(TYP_INT, 0),
                    compiler.gtNewIconNode(TYP_INT, 7))));
            for (var index = 2; index < blocks.Length; index++)
            {
                _ = AddStatement(compiler, blocks[index],
                    new GenTreeUnOp(GT_RETURN, TYP_INT, new GenTreeLclVar(TYP_INT, 0)));
            }

            PrepareAssertionPropagation(compiler);
            Assert.That(compiler.optAssertionPropMain(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(blocks[1].Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(blocks[1].UniqueSucc, Is.SameAs(blocks[retained]));
            var traits = compiler.apTraits ?? throw new InvalidOperationException("Assertion traits were not initialized.");
            Assert.That(AssertionOps.Equal(traits, blocks[1].bbAssertionOut,
                compiler.optGetEdgeAssertions(blocks[1], blocks[0])), Is.True);
        });
    }

    [Test]
    public static void AssertionPhaseWithNoFactsClearsStaleIncomingAndOutgoingSets()
    {
        WithCompiler(1, compiler => {
            var block = CreateGraph(compiler, [[]])[0];
            _ = AddStatement(compiler, block,
                new GenTreeUnOp(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 7)));
            PrepareAssertionPropagation(compiler);
            block.bbAssertionIn = [-1, -1, -1, -1];
            block.bbAssertionOut = [-1, -1, -1, -1];

            _ = compiler.optAssertionPropMain();
            Assert.That(compiler.AssertionCount, Is.Zero);
            var traits = compiler.apTraits ?? throw new InvalidOperationException("Assertion traits were not initialized.");
            Assert.That(AssertionOps.IsEmpty(traits, block.bbAssertionIn), Is.True);
            Assert.That(AssertionOps.MaybeUninit(block.bbAssertionOut), Is.True);
        });
    }

    private static void PrepareAssertionPropagation(Compiler compiler)
    {
        _ = ComputeDominators(compiler);
        Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
        PrepareValueNumbering(compiler);
        Assert.That(compiler.fgValueNumber(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
    }

    [Test]
    public static void SkippedSsaLeavesValueNumberStateUnchanged()
    {
        WithCompiler(1, compiler => {
            _ = CreateGraph(compiler, [[]]);

            Assert.That(compiler.fgValueNumber(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgVNPassesCompleted, Is.Zero);
            Assert.That(compiler.vnStore, Is.Null);
            Assert.That(compiler.vnState, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InitialSsaLocalsAndMemoryReceiveNativeInitialValues(bool initMem)
    {
        WithCompiler(3, compiler => {
            compiler.info.compInitMem = initMem;
            compiler.lvaTable[0].lvIsParam = true;
            var block = CreateGraph(compiler, [[]])[0];
            var value = new GenTreeOp(GT_ADD, TYP_INT,
                new GenTreeLclVar(TYP_INT, 0),
                new GenTreeOp(GT_ADD, TYP_INT,
                    new GenTreeLclVar(TYP_INT, 1), new GenTreeLclVar(TYP_INT, 2)));
            _ = AddStatement(compiler, block, new GenTreeUnOp(GT_RETURN, TYP_INT, value));
            _ = ComputeDominators(compiler);

            Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            PrepareValueNumbering(compiler);
            compiler.lvaTable[1].lvMustInit = true;
            Assert.That(compiler.lvaTable[0].lvInSsa, Is.True);
            Assert.That(compiler.lvaTable[1].lvInSsa, Is.True);
            Assert.That(compiler.lvaTable[2].lvInSsa, Is.True);
            Assert.That(compiler.lvaTable[1].lvMustInit, Is.True);
            Assert.That(SetOps.IsMember(compiler, block.bbLiveIn, compiler.lvaTable[2]._varIndex), Is.True);

            Assert.That(compiler.fgValueNumber(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var store = compiler.vnStore ?? throw new InvalidOperationException("Value numbering did not create a store.");
            for (var local = 0; local < 3; local++)
            {
                var expected = (local != 0 && initMem)
                    ? store.VNZeroForType(TYP_INT)
                    : store.VNForFunc(TYP_INT, VNF_InitVal, store.VNForIntCon(local));
                var definition = compiler.lvaTable[local].GetPerSsaData(SsaConfig.FIRST_SSA_NUM);
                Assert.That(definition._vnPair.Liberal, Is.EqualTo(expected));
                Assert.That(definition._vnPair.Conservative, Is.EqualTo(expected));
                Assert.That(definition.Block, Is.SameAs(block));
            }

            var initialMemory = store.VNForFunc(TYP_HEAP, VNF_InitVal, store.VNForIntCon(-1));
            var memoryDefinition = compiler.GetMemoryPerSsaData(SsaConfig.FIRST_SSA_NUM);
            Assert.That(memoryDefinition._vnPair.Liberal, Is.EqualTo(initialMemory));
            Assert.That(memoryDefinition._vnPair.Conservative, Is.EqualTo(initialMemory));
            Assert.That(compiler.fgVNPassesCompleted, Is.EqualTo(1));
            Assert.That(compiler.vnState, Is.Null);
        });
    }

    [Test]
    public static void SecondPassDiscardsStaleTreeAndMemoryNumbers()
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].lvIsParam = true;
            var block = CreateGraph(compiler, [[]])[0];
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            var value = new GenTreeOp(GT_ADD, TYP_INT, new GenTreeLclVar(TYP_INT, 0), constant);
            _ = AddStatement(compiler, block, new GenTreeUnOp(GT_RETURN, TYP_INT, value));
            _ = ComputeDominators(compiler);
            Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            PrepareValueNumbering(compiler);

            Assert.That(compiler.fgValueNumber(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var store = compiler.vnStore ?? throw new InvalidOperationException("Value numbering did not create a store.");
            Assert.That(constant._vnPair.Liberal, Is.EqualTo(store.VNForIntCon(7)));

            var stale = store.VNForIntCon(999);
            constant._vnPair.SetBoth(stale);
            compiler.GetMemoryPerSsaData(SsaConfig.FIRST_SSA_NUM)._vnPair.SetBoth(stale);

            Assert.That(compiler.fgValueNumber(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.vnStore, Is.SameAs(store));
            Assert.That(compiler.fgVNPassesCompleted, Is.EqualTo(2));
            Assert.That(constant._vnPair.Liberal, Is.EqualTo(store.VNForIntCon(7)));
            Assert.That(constant._vnPair.Conservative, Is.EqualTo(store.VNForIntCon(7)));
            var expectedMemory = store.VNForFunc(TYP_HEAP, VNF_InitVal, store.VNForIntCon(-1));
            Assert.That(compiler.GetMemoryPerSsaData(SsaConfig.FIRST_SSA_NUM)._vnPair.Liberal,
                Is.EqualTo(expectedMemory));
            Assert.That(compiler.vnState, Is.Null);
        });
    }

    [Test]
    public static void EqualPredecessorDefinitionsForwardTheSamePhiValue()
    {
        WithCompiler(2, compiler => {
            compiler.lvaTable[0].lvIsParam = true;
            var blocks = CreateGraph(compiler, [[1, 2], [3], [3], []]);
            _ = AddStatement(compiler, blocks[0], new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(GT_NE, TYP_INT, new GenTreeLclVar(TYP_INT, 0),
                    compiler.gtNewIconNode(TYP_INT, 0))));
            _ = AddStatement(compiler, blocks[1],
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 10)));
            _ = AddStatement(compiler, blocks[2],
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 10)));
            var result = new GenTreeLclVar(TYP_INT, 1);
            _ = AddStatement(compiler, blocks[3], new GenTreeUnOp(GT_RETURN, TYP_INT, result));
            _ = ComputeDominators(compiler);

            Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            PrepareValueNumbering(compiler);
            var phiStatement = blocks[3].FirstStmt
                ?? throw new InvalidOperationException("SSA did not insert the expected phi definition.");
            Assert.That(phiStatement.IsPhiDefnStmt, Is.True);
            var phi = phiStatement.RootNode.AsLclVar();

            Assert.That(compiler.fgValueNumber(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var store = compiler.vnStore ?? throw new InvalidOperationException("Value numbering did not create a store.");
            var expected = store.VNForIntCon(10);
            var definition = compiler.lvaTable[1].GetPerSsaData(phi.SsaNum);
            Assert.That(definition._vnPair.Liberal, Is.EqualTo(expected));
            Assert.That(definition._vnPair.Conservative, Is.EqualTo(expected));
            Assert.That(phi._vnPair.Liberal, Is.EqualTo(ValueNumStore.VNForVoid()));
            Assert.That(phi.Data._vnPair.Liberal, Is.EqualTo(expected));
            Assert.That(phi.Data._vnPair.Conservative, Is.EqualTo(expected));
            Assert.That(result._vnPair.Liberal, Is.EqualTo(expected));
        });
    }

    private static BasicBlock[] CreateGraph(Compiler compiler, int[][] successors)
    {
        var blocks = new BasicBlock[successors.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[i].bbRefs = 0;
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        for (var i = 0; i < blocks.Length; i++)
        {
            if (successors[i].Length == 1)
            {
                blocks[i].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[successors[i][0]], blocks[i]));
            }
            else if (successors[i].Length == 2)
            {
                blocks[i].SetCond(compiler.fgAddRefPred(blocks[successors[i][0]], blocks[i]),
                    compiler.fgAddRefPred(blocks[successors[i][1]], blocks[i]));
            }
        }

        return blocks;
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        if ((root.Oper is GT_JTRUE) && root.AsUnOp().Op1.Oper.IsCompare)
        {
            root.AsUnOp().Op1.Flags |= GenTreeFlags.GTF_RELOP_JMP_USED;
        }

        var statement = new Statement(root, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);

        return statement;
    }

    private static void PrepareValueNumbering(Compiler compiler)
    {
        compiler._dfsTree ??= compiler.fgComputeDfs();
        compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
    }

    private static void WithCompiler(int count, Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(count, compiler => {
            compiler.info.compInitMem = true;
            compiler.info.compRetType = TYP_INT;
            compiler.info.compRetNativeType = TYP_INT;
            compiler.fgPredsComputed = true;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaStubArgumentVar = BAD_VAR_NUM;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            action(compiler);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgComputeDominators")]
    private static extern PhaseStatus ComputeDominators(Compiler compiler);
}
