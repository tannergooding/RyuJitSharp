// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class MergedReturnsTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public static void ReturnsAtTheLimitAreUnchanged(int limit)
    {
        WithCompiler(compiler => {
            var merger = new MergedReturns(compiler);
            merger.SetMaxReturns(limit);
            for (var i = 0; i < limit; i++)
            {
                merger.Record(AppendReturn(compiler, i));
            }

            Assert.That(compiler.fgReturnCount, Is.EqualTo(limit));
            Assert.That(compiler.genReturnBB, Is.Null);
            Assert.That(merger.PlaceReturns(), Is.False);
            Assert.That(compiler.Blocks.All(block => block.Kind == BBJ_RETURN), Is.True);
        });
    }

    [Test]
    public static void ConstantGroupsKeepACommonSlotAndMoveAfterTheirLastSource()
    {
        WithCompiler(compiler => {
            int[] values = [0, 1, 0, -1, int.MaxValue, 1, 42, 0];
            var originals = values.Select(value => AppendReturn(compiler, value)).ToArray();
            var constants = originals.Select(block => block.LastStmt?.RootNode.AsUnOp().Op1).ToArray();
            var merger = new MergedReturns(compiler);
            merger.SetMaxReturns(MergedReturns.ReturnCountHardLimit);

            for (var i = 0; i < originals.Length; i++)
            {
                originals[i].setBBProfileWeight(i + 1);
                merger.Record(originals[i]);
            }

            Assert.That(compiler.fgReturnCount, Is.EqualTo(4));
            var general = compiler.genReturnBB ?? throw new InvalidOperationException();
            Assert.That(general.HasFlag(BasicBlockFlags.BBF_DONT_REMOVE), Is.True);
            Assert.That(originals[4].Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(originals[6].Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(general.bbRefs, Is.Zero);
            Assert.That(merger.PlaceReturns(), Is.True);

            var zero = originals[0].Target;
            var one = originals[1].Target;
            var minusOne = originals[3].Target;
            Assert.Multiple(() => {
                Assert.That(zero, Is.SameAs(originals[2].Target));
                Assert.That(zero, Is.SameAs(originals[7].Target));
                Assert.That(one, Is.SameAs(originals[5].Target));
                Assert.That(zero?.bbWeight, Is.EqualTo(12));
                Assert.That(one?.bbWeight, Is.EqualTo(8));
                Assert.That(minusOne?.bbWeight, Is.EqualTo(4));
                Assert.That(originals[7].Next, Is.SameAs(zero));
                Assert.That(originals[5].Next, Is.SameAs(one));
                Assert.That(originals[3].Next, Is.SameAs(minusOne));
                Assert.That(zero?.LastStmt?.RootNode.AsUnOp().Op1, Is.SameAs(constants[0]));
                Assert.That(one?.LastStmt?.RootNode.AsUnOp().Op1, Is.SameAs(constants[1]));
                Assert.That(compiler.fgLastBB, Is.SameAs(general));
            });

            foreach (var block in originals.Where(block => block.Kind == BBJ_ALWAYS))
            {
                Assert.That(block.LastStmt, Is.Null);
                Assert.That(block.Target?.LastStmt?.RootNode.Flags & GTF_RET_MERGED, Is.EqualTo(GTF_RET_MERGED));
            }

            foreach (var block in compiler.Blocks)
            {
                Assert.That(block.Next?.Prev, block.Next is null ? Is.Null : Is.SameAs(block));
            }
        });
    }

    [TestCase(false, 1)]
    [TestCase(true, 4)]
    public static void SingleEpilogOrDebugCodeDefersAllRewrites(bool debugCode, int limit)
    {
        WithCompiler(compiler => {
            compiler.opts.compDbgCode = debugCode;
            var returns = Enumerable.Range(0, limit + 1).Select(value => AppendReturn(compiler, value)).ToArray();
            var merger = new MergedReturns(compiler);
            merger.SetMaxReturns(limit);
            foreach (var block in returns)
            {
                merger.Record(block);
            }

            Assert.That(compiler.fgReturnCount, Is.EqualTo(1));
            Assert.That(compiler.genReturnBB, Is.Not.Null);
            Assert.That(returns.All(block => block.Kind == BBJ_RETURN), Is.True);
            Assert.That(merger.PlaceReturns(), Is.True);
        });
    }

    [TestCase(TYP_VOID, false, TYP_VOID)]
    [TestCase(TYP_BYTE, false, TYP_INT)]
    [TestCase(TYP_DOUBLE, false, TYP_DOUBLE)]
    [TestCase(TYP_STRUCT, true, TYP_BYREF)]
    public static void EagerReturnUsesTheMethodReturnContract(var_types returnType, bool hiddenBuffer, var_types expected)
    {
        WithCompiler(compiler => {
            _ = AppendReturn(compiler, 0);
            compiler.info.compRetType = returnType;
            compiler.info.compRetBuffArg = hiddenBuffer ? 0 : BAD_VAR_NUM;
            var merger = new MergedReturns(compiler);
            merger.SetMaxReturns(1);
            var block = merger.EagerCreate();
            var node = block.LastStmt?.RootNode ?? throw new InvalidOperationException();

            Assert.That(block, Is.SameAs(compiler.genReturnBB));
            Assert.That(node.Type, Is.EqualTo(expected));
            Assert.That(node.Flags & GTF_RET_MERGED, Is.EqualTo(GTF_RET_MERGED));
            Assert.That(compiler.fgReturnCount, Is.EqualTo(1));
            if (expected == TYP_VOID)
            {
                Assert.That(compiler.genReturnLocal, Is.EqualTo(BAD_VAR_NUM));
                Assert.That(node.AsUnOp().Op1, Is.Null);
            }
            else
            {
                Assert.That(compiler.lvaTable[compiler.genReturnLocal].Type, Is.EqualTo(expected));
                Assert.That(node.AsUnOp().Op1.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_DONT_CSE));
                Assert.That(compiler.compFloatingPointUsed, Is.EqualTo(expected == TYP_DOUBLE));
            }
        });
    }

    private static BasicBlock AppendReturn(Compiler compiler, int value)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.bbRefs = 0;
        if (compiler.fgLastBB is BasicBlock last)
        {
            last.Next = block;
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;
        compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(compiler.gtNewUnaryNode(genTreeOps.GT_RETURN, TYP_INT,
            compiler.gtNewIconNode(TYP_INT, value))));
        return block;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaTable = [];
        compiler.fgPredsComputed = true;
        compiler.info.compRetType = TYP_INT;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.genReturnLocal = BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
