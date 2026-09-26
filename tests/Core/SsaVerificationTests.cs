// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SsaVerificationTests
{
    [Test]
    public static void InvalidSsaDoesNotRequireADfsTree()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            compiler.verbose = true;

            Assert.That(CodeGenLifeTransitionTests.Capture(compiler.fgDebugCheckSsa), Is.Empty);
        });
    }

    [Test]
    public static void ValidNamesAndDescriptorsProduceSuccess()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            ref var local = ref compiler.lvaTable[0];
            local.lvInSsa = true;
            var ssaNum = local.lvPerSsaData.AllocSsaNum();
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
            store.SsaNum = ssaNum;
            local.GetPerSsaData(ssaNum) = new LclSsaVarDsc(block, store);
            _ = AddStatement(compiler, block, store);
            var use = new GenTreeLclVar(TYP_INT, 0) { SsaNum = ssaNum };
            _ = AddStatement(compiler, block, use);
            local.GetPerSsaData(ssaNum).AddUse(block);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(compiler.fgDebugCheckSsa);

            Assert.That(text, Is.EqualTo($"SSA checks completed successfully{Environment.NewLine}"));
        });
    }

    [Test]
    public static void OverestimatedUsesAndFlagsRetainNativeNoticesInOrder()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            var otherBlock = BasicBlock.New(compiler, BBJ_RETURN);
            ref var local = ref compiler.lvaTable[0];
            local.lvInSsa = true;
            var ssaNum = local.lvPerSsaData.AllocSsaNum();
            var use = new GenTreeLclVar(TYP_INT, 0) { SsaNum = ssaNum };
            _ = AddStatement(compiler, block, use);
            ref var descriptor = ref local.GetPerSsaData(ssaNum);
            descriptor = new LclSsaVarDsc(block);
            descriptor.AddPhiUse(otherBlock);
            descriptor.AddUse(block);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(compiler.fgDebugCheckSsa);

            Assert.That(text, Is.EqualTo(
                $"[info] NumUses overestimated for V00.1: IR 1 SSA 2{Environment.NewLine}" +
                $"[info] HasPhiUse overestimated for V00.1{Environment.NewLine}" +
                $"[info] HasGlobalUse overestimated for V00.1{Environment.NewLine}" +
                $"SSA checks completed successfully{Environment.NewLine}"));
        });
    }

    [TestCase(true, "[error] Missing SSA number on def")]
    [TestCase(false, "[error] Missing SSA number on use")]
    public static void MissingNumbersFailWithNativeDiagnostics(bool definition, string message)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            ref var local = ref compiler.lvaTable[0];
            local.lvInSsa = true;
            _ = local.lvPerSsaData.AllocSsaNum();
            var tree = definition
                ? compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1))
                : new GenTreeLclVar(TYP_INT, 0);
            _ = AddStatement(compiler, block, tree);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.StartWith($"fgDebugCheckSsa: errors found{Environment.NewLine}"));
            Assert.That(text, Does.Contain($"{message} [{tree.TreeId:D6}] (V00)"));
            Assert.That(text, Does.Not.Contain("SSA checks completed successfully"));
        });
    }

    [Test]
    public static void UnderestimatedUsesAndGlobalFlagFailAfterCrossCheck()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var first = AddBlock(compiler);
            var second = AddBlock(compiler);
            var edge = new FlowEdge(first, second, null);
            second.bbPreds = edge;
            first.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
            ref var local = ref compiler.lvaTable[0];
            local.lvInSsa = true;
            var ssaNum = local.lvPerSsaData.AllocSsaNum();
            var def = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
            def.SsaNum = ssaNum;
            local.GetPerSsaData(ssaNum) = new LclSsaVarDsc(first, def);
            _ = AddStatement(compiler, first, def);
            var use = new GenTreeLclVar(TYP_INT, 0) { SsaNum = ssaNum };
            _ = AddStatement(compiler, second, use);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.Contain($"[error] NumUses underestimated for V00.1: IR 1 SSA 0{Environment.NewLine}"));
            Assert.That(text, Does.Contain($"[error] HasGlobalUse underestimated for V00.1{Environment.NewLine}"));
            Assert.That(text.Split("fgDebugCheckSsa: errors found", StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(text, Does.Not.Contain("SSA checks completed successfully"));
        });
    }

    [Test]
    public static void DuplicateDefinitionFailsEvenWithAdequateUseCounts()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            ref var local = ref compiler.lvaTable[0];
            local.lvInSsa = true;
            var ssaNum = local.lvPerSsaData.AllocSsaNum();
            local.GetPerSsaData(ssaNum) = new LclSsaVarDsc(block);
            for (var index = 0; index < 2; index++)
            {
                var def = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, index));
                def.SsaNum = ssaNum;
                _ = AddStatement(compiler, block, def);
            }
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.Contain($"[error] HasMultipleDef for V00.1{Environment.NewLine}"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void UnexpectedNamesOnNonSsaLocalsFail(bool definition)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            GenTreeLclVarCommon tree = definition
                ? compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1))
                : new GenTreeLclVar(TYP_INT, 0);
            tree.SsaNum = 1;
            _ = AddStatement(compiler, block, tree);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.Contain(definition
                ? $"[error] Unexpected SSA number on def [{tree.TreeId:D6}] (V00)"
                : $"[error] Unexpected SSA number on [{tree.TreeId:D6}] (V00)"));
        });
    }

    [Test]
    public static void CompositeUseFailsBeforeDescriptorLookup()
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var block = AddBlock(compiler);
            ref var parent = ref compiler.lvaTable[0];
            parent.lvPromoted = true;
            parent.lvFieldCnt = 1;
            parent.lvFieldLclStart = 1;
            var use = new GenTreeLclVar(TYP_STRUCT, 0);
            use.SetSsaNum(compiler, 0, 1);
            _ = AddStatement(compiler, block, use);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.Contain($"[error] Composite SSA number on use [{use.TreeId:D6}] (V00)"));
        });
    }

    [Test]
    public static void PhiUseAndWrongDefinitionBlockReportIndependentErrors()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var first = AddBlock(compiler);
            var second = AddBlock(compiler);
            var edge = new FlowEdge(first, second, null);
            second.bbPreds = edge;
            first.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
            ref var local = ref compiler.lvaTable[0];
            local.lvInSsa = true;
            var sourceNum = local.lvPerSsaData.AllocSsaNum();
            var phiNum = local.lvPerSsaData.AllocSsaNum();
            var source = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
            source.SsaNum = sourceNum;
            local.GetPerSsaData(sourceNum) = new LclSsaVarDsc(second);
            local.GetPerSsaData(sourceNum).AddUse(second);
            _ = AddStatement(compiler, first, source);
            var phi = new GenTreePhi(TYP_INT)
            {
                FirstUse = new GenTreePhi.Use(new GenTreePhiArg(TYP_INT, 0, sourceNum, first)),
            };
            var phiDef = compiler.gtNewStoreLclVarNode(0, phi);
            phiDef.SsaNum = phiNum;
            local.GetPerSsaData(phiNum) = new LclSsaVarDsc(second);
            _ = AddStatement(compiler, second, phiDef);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.Contain($"[error] Wrong def block for V00.1 : IR " +
                                           $"{FMT_BB(first.bbNum)} SSA {FMT_BB(second.bbNum)}"));
            Assert.That(text, Does.Contain("[error] HasPhiUse underestimated for V00.1"));
            Assert.That(text, Does.Contain("[error] HasGlobalUse underestimated for V00.1"));
        });
    }

    [Test]
    public static void PhiAfterNonPhiIsRejected()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            var ordinary = AddStatement(compiler, block, new GenTreeLclVar(TYP_INT, 0));
            var phiDef = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, new GenTreePhi(TYP_INT)));
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.Contain($"[error] {FMT_BB(block.bbNum)} PhiDef " +
                                           $"{FMT_STMT(phiDef.Id)} appears after non-PhiDef {FMT_STMT(ordinary.Id)}"));
        });
    }

    [Test]
    public static void HandlerPhiAllowsRepeatedPredecessor()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var predecessor = AddBlock(compiler);
            var handler = AddBlock(compiler);
            var edge = new FlowEdge(predecessor, handler, null);
            handler.bbPreds = edge;
            predecessor.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = predecessor,
                    ebdTryLast = predecessor,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            var phi = new GenTreePhi(TYP_INT)
            {
                FirstUse = new GenTreePhi.Use(
                    new GenTreePhiArg(TYP_INT, 0, SsaConfig.RESERVED_SSA_NUM, predecessor),
                    new GenTreePhi.Use(
                        new GenTreePhiArg(TYP_INT, 0, SsaConfig.RESERVED_SSA_NUM, predecessor))),
            };
            _ = AddStatement(compiler, handler, compiler.gtNewStoreLclVarNode(0, phi));
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(compiler.fgDebugCheckSsa);

            Assert.That(text, Is.EqualTo($"SSA checks completed successfully{Environment.NewLine}"));
        });
    }

    [Test]
    public static void UnreachableBlocksAreNotChecked()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            _ = AddBlock(compiler);
            var unreachable = AddBlock(compiler);
            compiler.lvaTable[0].lvInSsa = true;
            _ = compiler.lvaTable[0].lvPerSsaData.AllocSsaNum();
            _ = AddStatement(compiler, unreachable, new GenTreeLclVar(TYP_INT, 0));
            EnableVerification(compiler);

            var dfsTree = compiler._dfsTree ?? throw new InvalidOperationException("Missing DFS tree.");
            Assert.That(dfsTree.Contains(unreachable), Is.False);
            var text = CodeGenLifeTransitionTests.Capture(compiler.fgDebugCheckSsa);

            Assert.That(text, Is.EqualTo($"SSA checks completed successfully{Environment.NewLine}"));
        });
    }

    [Test]
    public static void PhiChecksRejectWrongLocalAndDuplicatePredecessor()
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var block = AddBlock(compiler);
            var phi = new GenTreePhi(TYP_INT)
            {
                FirstUse = new GenTreePhi.Use(
                    new GenTreePhiArg(TYP_INT, 1, SsaConfig.RESERVED_SSA_NUM, block),
                    new GenTreePhi.Use(new GenTreePhiArg(TYP_INT, 0, SsaConfig.RESERVED_SSA_NUM, block))),
            };
            var definition = compiler.gtNewStoreLclVarNode(0, phi);
            _ = AddStatement(compiler, block, definition);
            EnableVerification(compiler);

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckSsa(), Throws.Exception));

            Assert.That(text, Does.Contain("[error] Wrong local V01 in PhiArg"));
            Assert.That(text, Does.Contain("multiple PhiArgs for predBlock"));
            Assert.That(text, Does.Contain("stale PhiArg"));
        });
    }

    private static void EnableVerification(Compiler compiler)
    {
        compiler._dfsTree = compiler.fgComputeDfs();
        compiler.fgSsaPassesCompleted = 1;
        compiler.fgSsaValid = true;
        compiler.verbose = true;
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = new Statement(root, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        return statement;
    }

    private static BasicBlock AddBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        if (compiler.fgLastBB is BasicBlock previous)
        {
            previous.Next = block;
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;
        return block;
    }
}
#endif
