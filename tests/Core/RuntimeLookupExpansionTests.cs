// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RuntimeLookupExpansionTests
{
    private static PhaseStatus Expand(Compiler compiler) => compiler.fgExpandRuntimeLookups();

    [TestCase(false)]
    [TestCase(true)]
    public static void DirectLookupBuildsGuardedFastPathAndHelperFallback(bool sizeCheck)
    {
        WithCompiler((compiler, entry, body) =>
        {
            var lookup = NewLookup((void*)0x4320, sizeCheck);
            var call = NewCall(compiler, lookup, CORINFO_HELP_RUNTIMEHANDLE_CLASS);
            var store = compiler.gtNewStoreLclVarNode(1, call);
            var original = Append(compiler, body, store);
            compiler.SignatureToLookupInfoMap[lookup.signature] = lookup;
            compiler.MethodHasExpRuntimeLookup = true;
            body.setBBProfileWeight(100);

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var previous = entry.Target;
            var size = sizeCheck ? previous.Next : null;
            var nullcheck = sizeCheck ? size!.Next! : previous.Next!;
            var fast = nullcheck.Next!;
            var fallback = fast.Next!;
            var remainder = fallback.Next!;

            Assert.That(previous.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(previous.Target, Is.SameAs(size ?? nullcheck));
            Assert.That(nullcheck.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(nullcheck.TrueTarget, Is.SameAs(fallback));
            Assert.That(nullcheck.FalseTarget, Is.SameAs(fast));
            Assert.That(nullcheck.TrueEdge.Likelihood, Is.EqualTo(0.2));
            Assert.That(nullcheck.FalseEdge.Likelihood, Is.EqualTo(0.8));
            Assert.That(fast.Target, Is.SameAs(remainder));
            Assert.That(fallback.Target, Is.SameAs(remainder));
            Assert.That(remainder.bbWeight, Is.EqualTo(100));
            Assert.That(nullcheck.bbWeight, Is.EqualTo(sizeCheck ? 80 : 100));
            Assert.That(fast.bbWeight, Is.EqualTo(sizeCheck ? 64 : 80));
            Assert.That(fallback.bbWeight, Is.EqualTo(sizeCheck ? 36 : 20));
            Assert.That(fast.hasProfileWeight, Is.True);
            Assert.That(fallback.hasProfileWeight, Is.True);
            Assert.That(previous, Is.SameAs(body));
            Assert.That(remainder, Is.Not.SameAs(body));
            Assert.That(remainder.FirstStmt, Is.Null);
            Assert.That(original.NextStmt, Is.Null);
            Assert.That(previous.HasFlag(BBF_IMPORTED), Is.True);
            foreach (var block in new[] { nullcheck, fast, fallback, remainder })
            {
                Assert.That(block.HasFlag(BBF_IMPORTED), Is.True);
                Assert.That(BasicBlock.sameEHRegion(previous, block), Is.True);
            }

            var nullcheckCondition = nullcheck.LastStmt!.RootNode.AsUnOp().Op1;
            Assert.That(nullcheckCondition.Oper, Is.EqualTo(GT_EQ));
            Assert.That(nullcheckCondition.Flags & GTF_RELOP_JMP_USED, Is.Not.EqualTo(GTF_EMPTY));
            var guardedSlot = nullcheckCondition.AsOp().Op1.AsOp();
            Assert.That(guardedSlot.Oper, Is.EqualTo(GT_COMMA));
            var slotLoad = guardedSlot.Op1.AsLclVar().Data;
            Assert.That(slotLoad.Oper, Is.EqualTo(GT_IND));
            Assert.That(slotLoad.Flags & GTF_IND_NONFAULTING, Is.Not.EqualTo(GTF_EMPTY));
            Assert.That(nullcheckCondition.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)0));
            Assert.That(fast.LastStmt!.RootNode.AsLclVar().LclNum, Is.EqualTo(1));
            Assert.That(fallback.LastStmt!.RootNode.AsLclVar().LclNum, Is.EqualTo(1));
            Assert.That(fallback.LastStmt.RootNode.AsLclVar().Data, Is.SameAs(call));
            Assert.That(fast.LastStmt.RootNode.AsLclVar().Data.Oper, Is.EqualTo(GT_LCL_VAR));
            if (size is not null)
            {
                Assert.That(size.Kind, Is.EqualTo(BBJ_COND));
                Assert.That(size.TrueTarget, Is.SameAs(fallback));
                Assert.That(size.FalseTarget, Is.SameAs(nullcheck));
                Assert.That(size.TrueEdge.Likelihood, Is.EqualTo(0.2));
                Assert.That(size.FalseEdge.Likelihood, Is.EqualTo(0.8));
                var condition = size.LastStmt!.RootNode.AsUnOp().Op1;
                Assert.That(condition.Oper, Is.EqualTo(GT_LE));
                Assert.That(condition.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)24));
                Assert.That(previous.Statements.Count(), Is.EqualTo(1));
                Assert.That(size.HasFlag(BBF_IMPORTED), Is.True);
            }
            else
            {
                Assert.That(previous.Statements, Is.Empty);
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void IndirectOffsetsSpillContextBeforeGuards(bool first, bool second)
    {
        WithCompiler((compiler, entry, body) =>
        {
            var lookup = NewLookup((void*)0x4330, sizeCheck: true);
            lookup.indirections = 3;
            lookup.offsets[2] = 32;
            lookup.indirectFirstOffset = first;
            lookup.indirectSecondOffset = second;
            var call = NewCall(compiler, lookup, CORINFO_HELP_RUNTIMEHANDLE_METHOD);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.SignatureToLookupInfoMap[lookup.signature] = lookup;
            compiler.MethodHasExpRuntimeLookup = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var previous = entry.Target;
            var spills = previous.Statements.ToArray();
            Assert.That(spills.Length, Is.EqualTo((first ? 1 : 0) + (second ? 1 : 0) + 1));
            Assert.That(spills.All(stmt => stmt.RootNode.Oper is GT_STORE_LCL_VAR), Is.True);
            Assert.That(spills[^1].RootNode.AsLclVar().Data.Oper,
                Is.EqualTo(second ? GT_ADD : GT_IND));
            if (!second)
            {
                Assert.That(spills[^1].RootNode.AsLclVar().Data.Flags & GTF_IND_NONFAULTING,
                    Is.Not.EqualTo(GTF_EMPTY));
                Assert.That(spills[^1].RootNode.AsLclVar().Data.Flags & GTF_IND_INVARIANT,
                    Is.EqualTo(GTF_EMPTY));
            }

            var size = previous.Next!;
            var comparison = size.LastStmt!.RootNode.AsUnOp().Op1.AsOp();
            Assert.That(comparison.Op1.AsIndir().Addr.AsOp().Op1.AsLclVarCommon().LclNum,
                Is.EqualTo(spills[^1].RootNode.AsLclVarCommon().LclNum));
            Assert.That(comparison.Op2.AsIntCon().IconValue, Is.EqualTo((nint)32));
        });
    }

    [Test]
    public static void NestedLookupKeepsOwningUseAndRetainsEarlierEffects()
    {
        WithCompiler((compiler, entry, body) =>
        {
            var lookup = NewLookup((void*)0x4340, sizeCheck: false);
            var call = NewCall(compiler, lookup, CORINFO_HELP_RUNTIMEHANDLE_METHOD);
            var effect = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_I_IMPL, 7));
            var nested = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, call, compiler.gtNewIconNode(TYP_I_IMPL, 5));
            var expression = compiler.gtNewBinaryNode(GT_COMMA, TYP_I_IMPL, effect, nested);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, expression));
            compiler.SignatureToLookupInfoMap[lookup.signature] = lookup;
            compiler.MethodHasExpRuntimeLookup = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Target.Statements.Any(stmt => stmt.RootNode is GenTreeLclVar node
                && node.Oper is GT_STORE_LCL_VAR && node.LclNum == 0), Is.True);
            Assert.That(entry.Target, Is.SameAs(body));
            var remainder = entry.Target.Next!.Next!.Next!.Next!;
            Assert.That(remainder.FirstStmt, Is.SameAs(original));
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);
            Assert.That(original.TreeList.Any(node => node.Oper is GT_ADD), Is.True);
            var fast = entry.Target.Next!.Next!;
            Assert.That(fast.LastStmt!.RootNode.AsLclVar().LclNum,
                Is.EqualTo(fast.Target.FirstStmt!.TreeList.First(node => node.Oper is GT_LCL_VAR)
                    .AsLclVarCommon().LclNum));
            Assert.That(fast.Next!.LastStmt!.RootNode.AsLclVar().Data, Is.SameAs(call));
        });
    }

    [Test]
    public static void TwoLookupsInOneStatementAreExpandedInEvaluationOrder()
    {
        WithCompiler((compiler, entry, body) =>
        {
            var firstLookup = NewLookup((void*)0x4370, sizeCheck: false);
            var secondLookup = NewLookup((void*)0x4380, sizeCheck: true);
            var firstCall = NewCall(compiler, firstLookup, CORINFO_HELP_RUNTIMEHANDLE_CLASS);
            var secondCall = NewCall(compiler, secondLookup, CORINFO_HELP_RUNTIMEHANDLE_METHOD);
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, firstCall, secondCall);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, sum));
            compiler.SignatureToLookupInfoMap[firstLookup.signature] = firstLookup;
            compiler.SignatureToLookupInfoMap[secondLookup.signature] = secondLookup;
            compiler.MethodHasExpRuntimeLookup = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var fallbackCalls = compiler.Blocks
                .SelectMany(block => block.Statements)
                .Where(stmt => stmt.RootNode is GenTreeLclVar store
                    && store.Oper is GT_STORE_LCL_VAR
                    && (ReferenceEquals(store.Data, firstCall) || ReferenceEquals(store.Data, secondCall)))
                .Select(stmt => stmt.RootNode.AsLclVar().Data)
                .ToArray();
            Assert.That(fallbackCalls, Has.Length.EqualTo(2));
            Assert.That(fallbackCalls[0], Is.SameAs(firstCall));
            Assert.That(fallbackCalls[1], Is.SameAs(secondCall));
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, firstCall)
                || ReferenceEquals(node, secondCall)), Is.False);
            Assert.That(original.RootNode.AsLclVar().Data.Oper, Is.EqualTo(GT_ADD));
            Assert.That(entry.Target, Is.SameAs(body));
        });
    }

    [Test]
    public static void GateAndNonRuntimeCallsRemainUnchanged()
    {
        WithCompiler((compiler, entry, body) =>
        {
            var other = compiler.gtNewHelperCallNode(TYP_I_IMPL, CORINFO_HELP_LMUL,
                compiler.gtNewIconNode(TYP_I_IMPL, 1), compiler.gtNewIconNode(TYP_I_IMPL, 2));
            var statement =             _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, other));
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            compiler.MethodHasExpRuntimeLookup = true;
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(body.FirstStmt, Is.SameAs(statement));
        });
    }

    [Test]
    public static void UnregisteredSignatureFailsBeforeSplittingBlock()
    {
        WithCompiler((compiler, entry, body) =>
        {
            var lookup = NewLookup((void*)0x4350, sizeCheck: false);
            var call = NewCall(compiler, lookup, CORINFO_HELP_RUNTIMEHANDLE_CLASS);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasExpRuntimeLookup = true;

            _ = Assert.Throws<FatalJitException>(() => Expand(compiler));
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(body.FirstStmt, Is.SameAs(original));
        });
    }

    [Test]
    public static void TailCallDoesNotExpand()
    {
        WithCompiler((compiler, entry, body) =>
        {
            var lookup = NewLookup((void*)0x4360, sizeCheck: false);
            var tail = NewCall(compiler, lookup, CORINFO_HELP_RUNTIMEHANDLE_CLASS);
            tail._callMoreFlags |= GTF_CALL_M_TAILCALL;
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, tail));
            compiler.MethodHasExpRuntimeLookup = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(body.Statements.Count(), Is.EqualTo(1));
        });
    }

    private static CORINFO_RUNTIME_LOOKUP NewLookup(void* signature, bool sizeCheck)
    {
        CORINFO_RUNTIME_LOOKUP lookup = new()
        {
            signature = signature,
            helper = CORINFO_HELP_RUNTIMEHANDLE_CLASS,
            indirections = 2,
            testForNull = true,
            sizeOffset = sizeCheck ? (ushort)4 : CORINFO_NO_SIZE_CHECK,
        };
        lookup.offsets[0] = 8;
        lookup.offsets[1] = 24;
        return lookup;
    }

    private static GenTreeCall NewCall(Compiler compiler, CORINFO_RUNTIME_LOOKUP lookup, CorInfoHelpFunc helper)
    {
        var context = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
        GenTree signature = compiler.gtNewIconNode(TYP_I_IMPL, (nint)lookup.signature);
        signature.Flags |= GTF_DONT_CSE;
        return compiler.gtNewHelperCallNode(TYP_I_IMPL, helper, context, signature);
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var stmt = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtAtEnd(block, stmt);
        compiler.gtSetStmtInfo(stmt);
        compiler.fgSetStmtSeq(stmt);
        return stmt;
    }

    private static void WithCompiler(Action<Compiler, BasicBlock, BasicBlock> action)
    {
#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(RuntimeLookupExpansionTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[1].Type = TYP_I_IMPL;
        compiler.lvaTable[2].Type = TYP_I_IMPL;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.codeGen = new CodeGen(compiler);
        var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
        var body = BasicBlock.New(compiler, BBJ_RETURN);
        body.RemoveFlags(BBF_INTERNAL);
        body.SetFlags(BBF_IMPORTED);
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
            action(compiler, entry, body);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
