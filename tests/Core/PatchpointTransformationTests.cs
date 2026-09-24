// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class PatchpointTransformationTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_tC_OnStackReplacement_InitialCounter")]
    private static extern ref int InitialCounter(ref JitConfigValues config);

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(123)]
    public static void RegularPatchpointsShareOneCounterAndPreserveNativeFlow(int initialCounter)
    {
        WithCompiler(compiler => {
            InitialCounter(ref JitConfig) = initialCounter;
            var entry = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var entryStatement = compiler.gtNewStmt(compiler.gtNewNothingNode());
            compiler.fgInsertStmtAtEnd(entry, entryStatement);
            var first = AddBlock(compiler, entry, 10);
            var second = AddBlock(compiler, first, 20);
            first.SetFlags(BBF_OSR_PATCHPOINT | BBF_GC_SAFE_POINT);
            second.SetFlags(BBF_OSR_PATCHPOINT);
            first.setBBProfileWeight(100);
            second.setBBProfileWeight(200);
            var firstBody = compiler.gtNewStmt(compiler.gtNewNothingNode());
            var secondBody = compiler.gtNewStmt(compiler.gtNewNothingNode());
            compiler.fgInsertStmtAtEnd(first, firstBody);
            compiler.fgInsertStmtAtEnd(second, secondBody);

            Assert.That(compiler.fgTransformPatchpoints(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(compiler.lvaGetDesc(0).Type, Is.EqualTo(TYP_INT));
            Assert.That(compiler.lvaGetDesc(0).lvIsTemp, Is.True);
            var init = entry.FirstStmt ?? throw new InvalidOperationException();
            Assert.That(init.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(init.RootNode.AsLclVarCommon().LclNum, Is.Zero);
            Assert.That(init.RootNode.AsLclVarCommon().Data.AsIntCon().IconValue, Is.EqualTo((nint)Math.Max(initialCounter, 0)));
            Assert.That(init.NextStmt, Is.SameAs(entryStatement));
            Assert.That(compiler.fgBBcount, Is.EqualTo(7));

            foreach (var testBlock in new[] { first, second })
            {
                var helper = testBlock.FalseTarget;
                var remainder = testBlock.TrueTarget;
                var original = testBlock == first ? firstBody : secondBody;
                var ilOffset = testBlock == first ? 10 : 20;
                Assert.That(testBlock.Kind, Is.EqualTo(BBJ_COND));
                Assert.That(testBlock.HasFlag(BBF_INTERNAL), Is.True);
                Assert.That(testBlock.HasFlag(BBF_OSR_PATCHPOINT), Is.False);
                Assert.That(testBlock.TrueEdge.Likelihood, Is.EqualTo(0.99));
                Assert.That(testBlock.FalseEdge.Likelihood, Is.EqualTo(0.01));
                Assert.That(testBlock.Next, Is.SameAs(helper));
                Assert.That(helper.Next, Is.SameAs(remainder));
                Assert.That(helper.Target, Is.SameAs(remainder));
                Assert.That(helper.TargetEdge.Likelihood, Is.EqualTo(1));
                Assert.That(helper.bbWeight, Is.EqualTo(testBlock.bbWeight * 0.01));
                Assert.That(helper.HasFlag(BBF_BACKWARD_JUMP), Is.True);
                Assert.That(helper.HasFlag(BBF_IMPORTED), Is.True);
                Assert.That(remainder.bbWeight, Is.EqualTo(testBlock.bbWeight));
                Assert.That(remainder.FirstStmt, Is.SameAs(original));
                Assert.That(remainder.bbCodeOffs, Is.EqualTo(ilOffset));

                var update = testBlock.FirstStmt ?? throw new InvalidOperationException();
                var subtract = update.RootNode.AsLclVarCommon().Data.AsOp();
                Assert.That(update.RootNode.AsLclVarCommon().LclNum, Is.Zero);
                Assert.That(subtract.Oper, Is.EqualTo(GT_SUB));
                Assert.That(subtract.Op1.AsLclVar().LclNum, Is.Zero);
                Assert.That(subtract.Op2.AsIntCon().IconValue, Is.EqualTo((nint)1));
                var branch = (update.NextStmt ?? throw new InvalidOperationException()).RootNode;
                Assert.That(branch.Oper, Is.EqualTo(GT_JTRUE));
                var compare = branch.AsUnOp().Op1.AsOp();
                Assert.That(compare.Oper, Is.EqualTo(GT_GT));
                Assert.That(compare.Op1.AsLclVar().LclNum, Is.Zero);
                Assert.That(compare.Op2.AsIntCon().IconValue, Is.EqualTo((nint)0));

                var patchpoint = (helper.FirstStmt ?? throw new InvalidOperationException()).RootNode.AsOp();
                Assert.That(patchpoint.Oper, Is.EqualTo(GT_PATCHPOINT));
                Assert.That(patchpoint.Flags & GenTreeFlags.GTF_CALL, Is.EqualTo(GenTreeFlags.GTF_CALL));
                Assert.That(patchpoint.Op1.Oper, Is.EqualTo(GT_LCL_ADDR));
                Assert.That(patchpoint.Op1.AsLclVarCommon().LclNum, Is.Zero);
                Assert.That(patchpoint.Op2.AsIntCon().IconValue, Is.EqualTo((nint)ilOffset));
            }

            Assert.That(first.TrueTarget.HasFlag(BBF_GC_SAFE_POINT), Is.True);
            Assert.That(first.HasFlag(BBF_GC_SAFE_POINT), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(3)]
    public static void ForcedPatchpointsReplaceAllStatementsWithoutAllocatingCounter(int statements)
    {
        WithCompiler(compiler => {
            var entry = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var block = AddBlock(compiler, entry, 42);
            block.SetKindAndTargetEdge(BBJ_THROW, null);
            block.SetFlags(BBF_PARTIAL_COMPILATION_PATCHPOINT);
            block.setBBProfileWeight(12);

            for (var i = 0; i < statements; i++)
            {
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(compiler.gtNewNothingNode()));
            }

            Assert.That(compiler.fgTransformPatchpoints(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaCount, Is.Zero);
            Assert.That(entry.FirstStmt, Is.Null);
            Assert.That(block.Kind, Is.EqualTo(BBJ_THROW));
            Assert.That(block.HasFlag(BBF_PARTIAL_COMPILATION_PATCHPOINT), Is.False);
            Assert.That(block.bbWeight, Is.EqualTo(12));
            Assert.That(block.Statements.Count(), Is.EqualTo(1));
            var patchpoint = (block.FirstStmt ?? throw new InvalidOperationException()).RootNode;
            Assert.That(patchpoint.Oper, Is.EqualTo(GT_PATCHPOINT_FORCED));
            Assert.That(patchpoint.Flags & GenTreeFlags.GTF_CALL, Is.EqualTo(GenTreeFlags.GTF_CALL));
            Assert.That(patchpoint.AsUnOp().Op1.AsIntCon().IconValue, Is.EqualTo((nint)42));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GatingAndUnmarkedBlocksDoNotChangeTheGraph(bool methodHasPatchpoint)
    {
        WithCompiler(compiler => {
            var entry = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var block = AddBlock(compiler, entry, 10);
            compiler.MethodHasPatchpoint = methodHasPatchpoint;
            if (!methodHasPatchpoint)
            {
                block.SetFlags(BBF_OSR_PATCHPOINT);
            }

            Assert.That(compiler.fgTransformPatchpoints(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgBBcount, Is.EqualTo(2));
            Assert.That(compiler.lvaCount, Is.Zero);
            Assert.That(block.HasFlag(BBF_OSR_PATCHPOINT), Is.EqualTo(!methodHasPatchpoint));
        });
    }

    private static BasicBlock AddBlock(Compiler compiler, BasicBlock after, int offset)
    {
        var block = compiler.fgNewBBafter(BBJ_RETURN, after, true);
        block.SetFlags(BBF_IMPORTED);
        block.bbCodeOffs = offset;
        block.bbCodeOffsEnd = offset + 10;
        after.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(block, after));
        return block;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.lvaTable = [];
        compiler.info.compIsStatic = true;
        compiler.MethodHasPatchpoint = true;
        JitFlags flags = default;
        flags.Set(JitFlags.JIT_FLAG_TIER0);
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        using var jitTls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitTls.Compiler = compiler;
        JitConfig = new JitConfigValues();

        try
        {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            entry.bbRefs = 0;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = entry;
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }
}
