// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RangeCheckCloningTests
{
    private delegate void CompilerTest(Compiler compiler, BasicBlock entry, BasicBlock body, JitFlags* flags);

    private static PhaseStatus Clone(Compiler compiler)
        => compiler.optRangeCheckCloning();

    [Test]
    public static void FourChecksProduceFastAndFallbackReturnPaths()
    {
        WithCompiler((compiler, entry, block, flags) => {
            var checks = AddChecks(compiler, block, [0, 1, 2, 3]);

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Kind, Is.EqualTo(BBJ_ALWAYS));
            var previous = entry.Target;
            var lower = previous.Next!;
            var upper = lower.Next!;
            var fallback = upper.Next!;
            var fast = fallback.Next!;
            Assert.That(fallback.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(fast.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(previous.Target, Is.SameAs(lower));
            Assert.That(lower.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(lower.Target, Is.SameAs(upper));
            Assert.That(upper.TrueTarget, Is.SameAs(fast));
            Assert.That(upper.FalseTarget, Is.SameAs(fallback));
            Assert.That(upper.LastStmt!.RootNode.AsUnOp().Op1.AsOp().Oper, Is.EqualTo(GT_GT));
            Assert.That(upper.LastStmt.RootNode.AsUnOp().Op1.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)3));

            foreach (var check in checks)
            {
                Assert.That(fast.Statements, Has.None.Matches<Statement>(stmt =>
                    stmt.TreeList.Any(node => node == check)));
            }
            Assert.That(fallback.LastStmt!.TreeList.Count(node => node.Oper is GT_BOUNDS_CHECK),
                Is.EqualTo(4));
        });
    }

    [Test]
    public static void VariableIndexOffsetsUseBaseVNAndGuardMaximumOffset()
    {
        WithCompiler((compiler, entry, block, flags) => {
            var checks = AddChecks(compiler, block, [1, 2, 3, 4], variableIndex: true);

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var lower = entry.Target.Next!;
            var upper = lower.Next!;
            var fallback = upper.Next!;
            var fast = fallback.Next!;
            Assert.That(lower.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(lower.TrueTarget, Is.SameAs(fallback));
            Assert.That(lower.FalseTarget, Is.SameAs(upper));
            Assert.That(upper.TrueTarget, Is.SameAs(fast));
            Assert.That(upper.FalseTarget, Is.SameAs(fallback));
            Assert.That(upper.LastStmt!.RootNode.AsUnOp().Op1.AsOp().Oper, Is.EqualTo(GT_LT));
            Assert.That(upper.LastStmt.RootNode.AsUnOp().Op1.AsOp().Op2.AsOp().Op2.AsIntCon().IconValue,
                Is.EqualTo((nint)(-4)));
            Assert.That(fallback.LastStmt!.TreeList.Count(node => node.Oper is GT_BOUNDS_CHECK), Is.EqualTo(4));
            Assert.That(fast.LastStmt!.TreeList.Any(node => node.Oper is GT_BOUNDS_CHECK), Is.False);
            Assert.That(checks.All(check => fallback.LastStmt.TreeList.All(node => node != check)), Is.True);
        });
    }

    [Test]
    public static void NonReturnBlockRejoinsAfterClonedStatements()
    {
        WithCompiler((compiler, entry, block, flags) => {
            _ = AddChecks(compiler, block, [0, 1, 2, 3], returnRoot: false);
            var next = compiler.fgNewBBafter(BBJ_RETURN, block, false);
            block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(next, block));

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var lower = entry.Target.Next!;
            var upper = lower.Next!;
            var fallback = upper.Next!;
            var fast = fallback.Next!;
            Assert.That(fallback.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(fast.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(fallback.Target, Is.SameAs(fast.Target));
            Assert.That(fast.Target.Target, Is.SameAs(next));
            Assert.That(fallback.LastStmt!.TreeList.Count(node => node.Oper is GT_BOUNDS_CHECK), Is.EqualTo(4));
            Assert.That(fast.LastStmt!.TreeList.Any(node => node.Oper is GT_BOUNDS_CHECK), Is.False);
        });
    }

    [Test]
    public static void SameStatementGroupsSelectNativeHashOrder()
    {
        WithCompiler((compiler, entry, block, flags) => {
            var checks = AddChecks(compiler, block, [0, 1, 2, 3, 0, 1, 2, 8], alternateLengths: true);
            var firstLength = checks[0].ArrayLength._vnPair.Conservative;
            var secondLength = checks[4].ArrayLength._vnPair.Conservative;
            var baseIndex = compiler.vnStore!.VNZeroForType(TYP_INT);
            var firstBucket = (uint)(baseIndex ^ (firstLength << 16)) % 9;
            var secondBucket = (uint)(baseIndex ^ (secondLength << 16)) % 9;
            var selectFirst = firstBucket >= secondBucket;

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var upper = entry.Target.Next!.Next!;
            var fallback = upper.Next!;
            var maximum = upper.LastStmt!.RootNode.AsUnOp().Op1.AsOp().Op2.AsIntCon().IconValue;
            Assert.That(maximum, Is.EqualTo((nint)(selectFirst ? 3 : 8)));
            Assert.That(fallback.LastStmt!.TreeList.Count(node => node.Oper is GT_BOUNDS_CHECK), Is.EqualTo(8));
        });
    }

    [TestCase(new[] { 0, 1, 2 })]
    [TestCase(new[] { 0, 1, 2, -3 })]
    public static void IneligibleGroupsDoNotClone(int[] indices)
    {
        WithCompiler((compiler, entry, block, flags) => {
            var checks = AddChecks(compiler, block, indices);
            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(entry.Target, Is.SameAs(block));
            Assert.That(block.LastStmt!.TreeList.Count(node => node.Oper is GT_BOUNDS_CHECK),
                Is.EqualTo(checks.Length));
        });
    }

    [Test]
    public static void SizePreferenceAndMethodFlagPreserveOriginalBlock()
    {
        WithCompiler((compiler, entry, block, flags) => {
            _ = AddChecks(compiler, block, [0, 1, 2, 3]);

            compiler.MethodHasBoundsChecks = false;
            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            compiler.MethodHasBoundsChecks = true;
            flags->Set(JitFlags.JIT_FLAG_SIZE_OPT);
            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(entry.Target, Is.SameAs(block));
            Assert.That(block.LastStmt!.TreeList.Count(node => node.Oper is GT_BOUNDS_CHECK), Is.EqualTo(4));
        });
    }

    [Test]
    public static void SharedReturnBlockIsNotDuplicated()
    {
        WithCompiler((compiler, entry, block, flags) => {
            _ = AddChecks(compiler, block, [0, 1, 2, 3]);
            compiler.genReturnBB = block;

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(entry.Target, Is.SameAs(block));
            Assert.That(block.LastStmt!.TreeList.Count(node => node.Oper is GT_BOUNDS_CHECK), Is.EqualTo(4));
        });
    }

    private static GenTreeBoundsChk[] AddChecks(Compiler compiler, BasicBlock block, int[] indices,
        bool variableIndex = false, bool returnRoot = true, bool alternateLengths = false)
    {
        var store = compiler.vnStore ?? throw new InvalidOperationException("A VN store is required.");
        var lengthVN = store.VNForExpr(null, TYP_INT);
        var otherLengthVN = alternateLengths ? store.VNForExpr(null, TYP_INT) : lengthVN;
        var indexVN = variableIndex ? store.VNForExpr(null, TYP_INT) : ValueNumStore.NoVN;
        var checks = new GenTreeBoundsChk[indices.Length];
        GenTree result = compiler.gtNewIconNode(TYP_INT, 7);
        for (var i = indices.Length - 1; i >= 0; i--)
        {
            GenTree index;
            if (variableIndex)
            {
                var baseIndex = compiler.gtNewLclvNode(TYP_INT, 1);
                baseIndex._vnPair.SetBoth(indexVN);
                index = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, baseIndex,
                    compiler.gtNewIconNode(TYP_INT, indices[i]));
                index._vnPair.SetBoth(store.VNForFunc(TYP_INT, VNFunc.VNF_ADD,
                    indexVN, store.VNForIntCon(indices[i])));
            }
            else
            {
                index = compiler.gtNewIconNode(TYP_INT, indices[i]);
                index._vnPair.SetBoth(store.VNForIntCon(indices[i]));
            }
            var useOtherLength = alternateLengths && (i >= indices.Length / 2);
            var length = compiler.gtNewLclvNode(TYP_INT, useOtherLength ? 2 : 0);
            length._vnPair.SetBoth(useOtherLength ? otherLengthVN : lengthVN);
            checks[i] = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            result = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, checks[i], result);
        }

        var statement = compiler.gtNewStmt(returnRoot ? compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, result) : result);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        compiler.fgInsertStmtAtEnd(block, statement);
        block.SetFlags(BBF_MAY_HAVE_BOUNDS_CHECKS);
        return checks;
    }

    private static void WithCompiler(CompilerTest test)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
#if DEBUG
        compiler.info.compFullName = nameof(RangeCheckCloningTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        compiler.vnStore = new ValueNumStore(compiler);
        compiler.MethodHasBoundsChecks = true;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
        compiler.lvaTable[2].Type = TYP_INT;
        var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
        var body = BasicBlock.New(compiler, BBJ_RETURN);
        entry.bbRefs = 1;
        entry.Next = body;
        body.Prev = entry;
        compiler.fgFirstBB = entry;
        compiler.fgLastBB = body;
        compiler.fgPredsComputed = true;
        JitTls.Compiler = compiler;
        entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(body, entry));
        try
        {
            test(compiler, entry, body, &flags);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
