// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SsaResetTests
{
    [Test]
    public static void ResetPreservesNativeDeepAndPhiOnlyContracts(
        [Values] bool deepClean, [Values(0, 2)] int phiCount, [Values(0, 2)] int bodyCount)
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var phis = new List<Statement>();
            var body = new List<Statement>();
            for (var index = 0; index < phiCount; index++)
            {
                var phi = new GenTreePhi(TYP_INT);
                phis.Add(AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(index, phi)));
            }
            for (var index = 0; index < bodyCount; index++)
            {
                var value = new GenTreeLclVar(TYP_INT, 1) { SsaNum = 3 };
                body.Add(AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(0, value)));
            }

            for (var index = 0; index < compiler.lvaCount; index++)
            {
                _ = compiler.lvaTable[index].lvPerSsaData.AllocSsaNum();
            }
            _ = MemoryDefinitions(compiler).AllocSsaNum();
            var outlined = new List<int>(16) { 128, 129 };
            compiler._outlinedCompositeSsaNums = outlined;
            var key = compiler.gtNewIconNode(TYP_INT, 17);
            var map = new Dictionary<GenTree, int> { [key] = 1 };
            foreach (var kind in new AllMemoryKinds())
            {
                compiler._memorySsaMap[(int)kind] = map;
                block.bbMemorySsaPhiFunc[(int)kind] = new BasicBlock.MemoryPhiArg(3);
            }
            compiler.fgSsaPassesCompleted = 3;
            compiler.fgSsaValid = true;
            compiler.fgStmtRemoved = false;

#if DEBUG
            compiler.verbose = true;
            var text = CodeGenLifeTransitionTests.Capture(() => compiler.fgResetForSsa(deepClean));
#else
            compiler.fgResetForSsa(deepClean);
#endif

            Assert.That(block.FirstStmt, Is.SameAs(bodyCount == 0 ? null : body[0]));
            Assert.That(block.LastStmt, Is.SameAs(bodyCount == 0 ? null : body[^1]));
            if (bodyCount != 0)
            {
                Assert.That(body[0].PrevStmt, Is.SameAs(body[^1]));
                Assert.That(body[^1].NextStmt, Is.Null);
                Assert.That(body[0].NextStmt, Is.SameAs(body[1]));
            }
            foreach (var statement in body)
            {
                foreach (var tree in statement.TreeList)
                {
                    if (tree.Oper.IsAnyLocal)
                    {
                        Assert.That(tree.AsLclVarCommon().SsaNum,
                            Is.EqualTo(deepClean ? SsaConfig.RESERVED_SSA_NUM : 3));
                    }
                }
            }
            foreach (var statement in phis)
            {
                Assert.That(statement.RootNode.AsLclVarCommon().SsaNum, Is.EqualTo(3));
            }
            for (var index = 0; index < compiler.lvaCount; index++)
            {
                Assert.That(compiler.lvaTable[index].lvPerSsaData.Count, Is.EqualTo(deepClean ? 0 : 1));
                if (deepClean)
                {
                    Assert.That(compiler.lvaTable[index].lvPerSsaData.AllocSsaNum(), Is.EqualTo(SsaConfig.FIRST_SSA_NUM));
                }
            }
            Assert.That(MemoryDefinitions(compiler).Count, Is.EqualTo(deepClean ? 0 : 1));
            foreach (var kind in new AllMemoryKinds())
            {
                Assert.That(block.bbMemorySsaPhiFunc[(int)kind], Is.Null);
                Assert.That(compiler._memorySsaMap[(int)kind], Is.SameAs(deepClean ? null : map));
            }
            Assert.That(map, Has.Count.EqualTo(1));
            Assert.That(compiler._outlinedCompositeSsaNums, Is.SameAs(outlined));
            Assert.That(outlined.Count, Is.EqualTo(deepClean ? 0 : 2));
            Assert.That(outlined.Capacity, Is.EqualTo(16));
            Assert.That(compiler.fgSsaPassesCompleted, Is.EqualTo(3));
            Assert.That(compiler.fgSsaValid, Is.True);
            Assert.That(compiler.fgStmtRemoved, Is.False);
#if DEBUG
            Assert.That(text, Is.EqualTo($"Removing {(deepClean ? "all SSA artifacts" : "PHI functions")}{Environment.NewLine}"));
#endif
        });
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTreeLclVar store)
    {
        store.SsaNum = 3;
        var statement = new Statement(store, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.fgSetStmtSeq(statement);

        return statement;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "lvMemoryPerSsaData")]
    private static extern ref SsaDefArray<SsaMemDef> MemoryDefinitions(Compiler compiler);
}
