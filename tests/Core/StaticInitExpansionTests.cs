// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using unsafe CORINFO_CLASS_HANDLE = RyuJitSharp.CORINFO_CLASS_STRUCT_*;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class StaticInitExpansionTests
{
    private static bool s_rejectFlag;
    private static bool s_rejectBase;
    private static bool s_sameAddress;
    private static bool s_indirectBase;
    private static bool s_requestedGc;
    private static bool s_nativeAot;
    private static int s_flagRequests;
    private static int s_baseRequests;

    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void StaticBaseCreatesNativeAbiGuardAndPreservesFallback(
        bool nativeAot, bool gcBase, bool indirectBase)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            s_indirectBase = indirectBase;
            var call = NewCall(compiler, gcBase);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasStaticInit = true;
            body.setBBProfileWeight(100);

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_flagRequests, Is.EqualTo(1));
            Assert.That(s_baseRequests, Is.EqualTo(1));
            Assert.That(s_requestedGc, Is.EqualTo(gcBase));
            Assert.That(call._initClsHnd == null, Is.True);

            var conditionBlock = compiler.Blocks.Single(block => block.Kind is BBJ_COND);
            var fallback = conditionBlock.FalseTarget;
            var remainder = conditionBlock.TrueTarget;
            var guard = conditionBlock.LastStmt!.RootNode.AsUnOp().Op1.AsOp();

            Assert.That(conditionBlock.TrueEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(conditionBlock.FalseEdge.Likelihood, Is.EqualTo(0.0));
            Assert.That(fallback.bbWeight, Is.EqualTo(0));
            Assert.That(fallback.LastStmt!.RootNode, Is.SameAs(call));
            Assert.That(fallback.Target, Is.SameAs(remainder));
            Assert.That(remainder.FirstStmt, Is.SameAs(original));
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);
            Assert.That(remainder.bbCodeOffsEnd, Is.EqualTo(BAD_IL_OFFSET));
            Assert.That(BasicBlock.sameEHRegion(conditionBlock, fallback), Is.True);
            Assert.That(BasicBlock.sameEHRegion(conditionBlock, remainder), Is.True);

            Assert.That(guard.Oper, Is.EqualTo(GT_EQ));
            Assert.That(guard.Flags & GTF_RELOP_JMP_USED, Is.Not.EqualTo(GTF_EMPTY));
            var actual = guard.Op1;
            if (nativeAot)
            {
                Assert.That(guard.Op2.AsIntCon().IconValue, Is.EqualTo((nint)0));
                Assert.That(actual.Oper, Is.EqualTo(GT_IND));
                Assert.That(actual.Flags & (GTF_IND_VOLATILE | GTF_IND_NONFAULTING),
                    Is.EqualTo(GTF_IND_VOLATILE | GTF_IND_NONFAULTING));
                var offset = actual.AsIndir().Addr.AsOp();
                Assert.That(offset.Oper, Is.EqualTo(GT_ADD));
                Assert.That(offset.Op2.AsIntCon().IconValue, Is.EqualTo((nint)(-8)));
                Assert.That(offset.Op1.AsIntCon().IconValue, Is.EqualTo((nint)0x4000));
            }
            else
            {
                Assert.That(guard.Op2.AsIntCon().IconValue, Is.EqualTo((nint)1));
                Assert.That(actual.Oper, Is.EqualTo(GT_AND));
                Assert.That(actual.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)1));
                Assert.That(actual.AsOp().Op1.Flags & (GTF_IND_VOLATILE | GTF_ORDER_SIDEEFF),
                    Is.EqualTo(GTF_IND_VOLATILE | GTF_ORDER_SIDEEFF));
            }

            var replacement = original.RootNode.AsLclVar().Data;
            if (indirectBase)
            {
                Assert.That(replacement.Oper, Is.EqualTo(GT_IND));
                Assert.That(replacement.AsIndir().Addr.AsIntCon().IconValue, Is.EqualTo((nint)0x5000));
            }
            else
            {
                Assert.That(replacement.AsIntCon().IconValue, Is.EqualTo((nint)0x5000));
                Assert.That(replacement.AsIntCon().IsIconHandle(GTF_ICON_STATIC_HDL), Is.True);
            }

            Assert.That(entry.Target, Is.SameAs(body));
        });
    }

    [Test]
    public static void NativeAotSharesIdenticalFlagAndStaticBaseAddress()
    {
        WithCompiler(true, (compiler, _, body) =>
        {
            s_sameAddress = true;
            var call = NewCall(compiler, false);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasStaticInit = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var guard = compiler.Blocks.Single(block => block.Kind is BBJ_COND)
                .LastStmt!.RootNode.AsUnOp().Op1.AsOp();
            var baseAddress = guard.Op1.AsIndir().Addr.AsOp().Op1.AsOp();
            Assert.That(baseAddress.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(baseAddress.Op1.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(original.RootNode.AsLclVar().Data.AsLclVarCommon().LclNum,
                Is.EqualTo(baseAddress.Op2.AsLclVarCommon().LclNum));
            Assert.That(guard.Op1.AsIndir().Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)(-8)));
        });
    }

    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    public static void MissingMethodOrEEDataLeavesCallAndGraphIntact(
        bool nativeAot, bool noMethodFlag, bool rejectFlag)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            s_rejectFlag = rejectFlag;
            var call = NewCall(compiler, false);
            var statement = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasStaticInit = !noMethodFlag;
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_flagRequests, Is.EqualTo(noMethodFlag ? 0 : 1));
            Assert.That(s_baseRequests, Is.Zero);
            Assert.That(compiler.Blocks.Count(), Is.EqualTo(2));
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(body.FirstStmt, Is.SameAs(statement));
            Assert.That(statement.RootNode.AsLclVar().Data, Is.SameAs(call));
            Assert.That(call._initClsHnd != null, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MissingStaticBaseLeavesOriginalCallAndGraph(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            s_rejectBase = true;
            var call = NewCall(compiler, false);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasStaticInit = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_flagRequests, Is.EqualTo(1));
            Assert.That(s_baseRequests, Is.EqualTo(1));
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(compiler.Blocks.Count(), Is.EqualTo(2));
            Assert.That(body.LastStmt!.RootNode.AsLclVar().Data, Is.SameAs(call));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void RareBlockPolicyMatchesTargetAbi(bool nativeAot, bool expands)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            body.setBBProfileWeight(0);
            var call = NewCall(compiler, false);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasStaticInit = true;

            Assert.That(Expand(compiler), Is.EqualTo(expands
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_flagRequests, Is.EqualTo(expands ? 1 : 0));
            Assert.That(entry.Target, Is.SameAs(body));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void DisabledOptimizationsAreBypassedOnlyOnNativeAot(bool nativeAot, bool expands)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            var call = NewCall(compiler, false);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasStaticInit = true;

            Assert.That(Expand(compiler), Is.EqualTo(expands
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_flagRequests, Is.EqualTo(expands ? 1 : 0));
            Assert.That(entry.Target, Is.SameAs(body));
        }, minOpts: true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NestedCallRetainsEarlierEffectsAndMovesOnlyTheHelper(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            var call = NewCall(compiler, false);
            var effect = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_I_IMPL, 17));
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, call, compiler.gtNewIconNode(TYP_I_IMPL, 5));
            var expression = compiler.gtNewBinaryNode(GT_COMMA, TYP_I_IMPL, effect, sum);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, expression));
            compiler.MethodHasStaticInit = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Target.Statements.Any(stmt => stmt.RootNode is GenTreeLclVar store
                && store.Oper is GT_STORE_LCL_VAR && store.LclNum == 0), Is.True);
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);
            Assert.That(original.TreeList.Any(node => node.Oper is GT_ADD), Is.True);
            var guard = compiler.Blocks.Single(block => block.Kind is BBJ_COND);
            Assert.That(guard.FalseTarget.LastStmt!.RootNode, Is.SameAs(call));
            Assert.That(guard.TrueTarget.FirstStmt, Is.SameAs(original));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TwoStaticHelpersInOneStatementExpandInEvaluationOrder(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, _, body) =>
        {
            var first = NewCall(compiler, false);
            var second = NewCall(compiler, true);
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, first, second);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, sum));
            compiler.MethodHasStaticInit = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_flagRequests, Is.EqualTo(2));
            Assert.That(s_baseRequests, Is.EqualTo(2));
            var guards = compiler.Blocks.Where(block => block.Kind is BBJ_COND).ToArray();
            Assert.That(guards, Has.Length.EqualTo(2));
            Assert.That(guards[0].FalseTarget.LastStmt!.RootNode, Is.SameAs(first));
            Assert.That(guards[1].FalseTarget.LastStmt!.RootNode, Is.SameAs(second));
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, first) || ReferenceEquals(node, second)), Is.False);
            Assert.That(original.RootNode.AsLclVar().Data.Oper, Is.EqualTo(GT_ADD));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IneligibleHelperIsNotQueriedOrRewritten(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            var call = compiler.gtNewHelperCallNode(TYP_I_IMPL, CORINFO_HELP_RUNTIMEHANDLE_CLASS,
                compiler.gtNewIconNode(TYP_I_IMPL, 0x1234));
            call._initClsHnd = (CORINFO_CLASS_HANDLE)0x9000;
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasStaticInit = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_flagRequests, Is.Zero);
            Assert.That(s_baseRequests, Is.Zero);
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(body.FirstStmt, Is.SameAs(original));
            Assert.That(original.RootNode.AsLclVar().Data, Is.SameAs(call));
        });
    }

    private static PhaseStatus Expand(Compiler compiler)
        => compiler.fgExpandStaticInit();

    private static GenTreeCall NewCall(Compiler compiler, bool gcBase)
    {
        var helper = gcBase ? CORINFO_HELP_GET_GCSTATIC_BASE : CORINFO_HELP_GET_NONGCSTATIC_BASE;
        var call = compiler.gtNewHelperCallNode(TYP_I_IMPL, helper,
            compiler.gtNewIconNode(TYP_I_IMPL, 0x1234));
        call._initClsHnd = (CORINFO_CLASS_HANDLE)0x9000;
        return call;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var stmt = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtAtEnd(block, stmt);
        compiler.gtSetStmtInfo(stmt);
        compiler.fgSetStmtSeq(stmt);
        return stmt;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetFlag(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset)
    {
        s_flagRequests++;
        *offset = s_nativeAot ? -8 : 0;
        addr->accessType = IAT_VALUE;
        addr->addr = (void*)0x4000;
        return s_rejectFlag ? (byte)0 : (byte)1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetBase(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, byte isGc, CORINFO_CONST_LOOKUP* addr)
    {
        s_baseRequests++;
        s_requestedGc = isGc != 0;
        addr->accessType = s_indirectBase ? IAT_PVALUE : IAT_VALUE;
        addr->addr = (void*)(s_sameAddress ? 0x4000 : 0x5000);
        return s_rejectBase ? (byte)0 : (byte)1;
    }

    private static void WithCompiler(bool nativeAot, Action<Compiler, BasicBlock, BasicBlock> action, bool minOpts = false)
    {
        s_rejectFlag = false;
        s_rejectBase = false;
        s_sameAddress = false;
        s_indirectBase = false;
        s_requestedGc = false;
        s_nativeAot = nativeAot;
        s_flagRequests = 0;
        s_baseRequests = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getIsClassInitedFlagAddress = &GetFlag;
        vtable.Base.Base.getStaticBaseAddress =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool, CORINFO_CONST_LOOKUP*, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, byte, CORINFO_CONST_LOOKUP*, byte>)&GetBase;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(StaticInitExpansionTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compCompHnd = &jitInfo;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = nativeAot ? CORINFO_NATIVEAOT_ABI : CORINFO_CORECLR_ABI;
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
