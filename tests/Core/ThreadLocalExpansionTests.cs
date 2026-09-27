// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using unsafe CORINFO_GENERIC_HANDLE = RyuJitSharp.CORINFO_GENERIC_STRUCT_*;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.JitFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ThreadLocalExpansionTests
{
    private static int s_coreRequests;
    private static int s_aotRequests;
    private static nuint s_tlsIndex;
    private static int s_baseOffset;
    private static int s_storageOffset;

    [TestCase(CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED)]
    [TestCase(CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED)]
    public static void CoreClrChecksIndexAndCachedBlockBeforeOriginalHelper(CorInfoHelpFunc helper)
    {
        WithCompiler(false, (compiler, entry, body) =>
        {
            var call = NewCall(compiler, helper);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            body.setBBProfileWeight(100);

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_coreRequests, Is.EqualTo(1));
            Assert.That(s_aotRequests, Is.Zero);
            var maxCheck = body.Next!;
            var blockCheck = maxCheck.Next!;
            var fallback = blockCheck.Next!;
            var fast = fallback.Next!;
            var remainder = fast.Next!;

            Assert.That(body.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(body.Target, Is.SameAs(maxCheck));
            Assert.That(maxCheck.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(maxCheck.TrueTarget, Is.SameAs(fallback));
            Assert.That(maxCheck.FalseTarget, Is.SameAs(blockCheck));
            Assert.That(maxCheck.TrueEdge.Likelihood, Is.EqualTo(0.0));
            Assert.That(maxCheck.FalseEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(blockCheck.TrueTarget, Is.SameAs(fast));
            Assert.That(blockCheck.FalseTarget, Is.SameAs(fallback));
            Assert.That(blockCheck.TrueEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(blockCheck.FalseEdge.Likelihood, Is.EqualTo(0.0));
            Assert.That(fallback.Target, Is.SameAs(remainder));
            Assert.That(fast.Target, Is.SameAs(remainder));
            Assert.That(fallback.bbWeight, Is.Zero);
            Assert.That(fast.bbWeight, Is.EqualTo(100));
            Assert.That(remainder.FirstStmt, Is.SameAs(original));
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);
            Assert.That(fallback.FirstStmt!.RootNode.AsLclVar().Data, Is.SameAs(call));
            Assert.That(fast.FirstStmt!.RootNode.AsLclVar().Data.Type, Is.EqualTo(TYP_BYREF));

            var tlsLoad = maxCheck.FirstStmt!.RootNode.AsLclVar().Data.AsIndir();
            Assert.That(tlsLoad.Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)24));
            Assert.That(tlsLoad.Addr.AsOp().Op2.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL), Is.True);
#if DEBUG
            Assert.That(tlsLoad.Addr.AsOp().Op2.TreeId,
                Is.LessThan(tlsLoad.Addr.AsOp().Op1.AsIndir().Addr.TreeId));
#endif
            Assert.That(tlsLoad.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
                Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
            Assert.That(maxCheck.LastStmt!.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_LE));
            Assert.That(blockCheck.LastStmt!.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_NE));
#if DEBUG
            var blocksAddress = blockCheck.FirstStmt!.RootNode.AsLclVar().Data.AsIndir()
                .Addr.AsOp().Op1.AsIndir().Addr.AsOp();
            Assert.That(blocksAddress.Op2.TreeId, Is.LessThan(blocksAddress.Op1.TreeId));
#endif
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(BasicBlock.sameEHRegion(body, remainder), Is.True);
        });
    }

    [TestCase(4, 0x34L)]
    [TestCase(-1, 0x10000002FL)]
    public static void CoreClrOptimized2ComputesBaseWithoutFallback(int typeIndex, long expectedOffset)
    {
        WithCompiler(false, (compiler, _, body) =>
        {
            var call = NewCall(compiler, CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2,
                typeIndex);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_coreRequests, Is.EqualTo(1));
            var compute = body.Next!;
            Assert.That(compute.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(body.Target, Is.SameAs(compute));
            Assert.That(compute.Target, Is.SameAs(compute.Next));
            Assert.That(compiler.Blocks.Count(), Is.EqualTo(4));
            Assert.That(compiler.Blocks.Any(block => block.Kind is BBJ_COND), Is.False);
            Assert.That(compiler.Blocks.SelectMany(block => block.Statements)
                .Any(stmt => stmt.TreeList.Any(tree => ReferenceEquals(tree, call))), Is.False);
            Assert.That(original.TreeList.Any(tree => ReferenceEquals(tree, call)), Is.False);
            var baseAdd = compute.LastStmt!.RootNode.AsLclVar().Data.AsOp();
            Assert.That(baseAdd.Oper, Is.EqualTo(GT_ADD));
            Assert.That(baseAdd.Op2.AsIntCon().IconValue, Is.EqualTo((nint)expectedOffset));
        });
    }

    [Test]
    public static void CoreClrZeroTlsIndexDoesNotInsertModuleSlot()
    {
        WithCompiler(false, (compiler, entry, body) =>
        {
            s_tlsIndex = 0;
            var call = NewCall(compiler, CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var tlsLoad = body.Next!.FirstStmt!.RootNode.AsLclVar().Data.AsIndir();
            Assert.That(tlsLoad.Addr.Oper, Is.EqualTo(GT_IND));
            Assert.That(tlsLoad.Addr.AsIndir().Addr.AsIntCon().IconValue, Is.EqualTo((nint)0x58));
            Assert.That(entry.Target, Is.SameAs(body));
        });
    }

    [Test]
    public static void MetadataOffsetsUseNativeUnsignedWidth()
    {
        WithCompiler(false, (compiler, entry, body) =>
        {
            s_baseOffset = unchecked((int)0x80000030);
            var call = NewCall(compiler, CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var baseAddress = body.Next!.LastStmt!.RootNode.AsLclVar().Data.AsOp();
            Assert.That(baseAddress.Op2.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)0x80000034)));
            Assert.That(entry.Target, Is.SameAs(body));
        });

        WithCompiler(true, (compiler, entry, body) =>
        {
            s_storageOffset = unchecked((int)0x80000058);
            var call = NewCall(compiler, CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var tlsLoad = body.Next!.FirstStmt!.RootNode.AsLclVar().Data.AsOp().Op1.AsIndir();
            Assert.That(tlsLoad.Addr.AsOp().Op1.AsIndir().Addr.AsIntCon().IconValue,
                Is.EqualTo(unchecked((nint)0x80000058)));
            Assert.That(entry.Target, Is.SameAs(body));
        });
    }

    [Test]
    public static void NativeAotReadsRelocatableRootAndCallsSlowPathOnCacheMiss()
    {
        WithCompiler(true, (compiler, entry, body) =>
        {
            var call = NewCall(compiler, CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            body.setBBProfileWeight(100);

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_aotRequests, Is.EqualTo(1));
            Assert.That(s_coreRequests, Is.Zero);
            var check = body.Next!;
            var fallback = check.Next!;
            var fast = fallback.Next!;
            var remainder = fast.Next!;
            Assert.That(check.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(check.TrueTarget, Is.SameAs(fast));
            Assert.That(check.FalseTarget, Is.SameAs(fallback));
            Assert.That(check.TrueEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(check.FalseEdge.Likelihood, Is.EqualTo(0.0));
            Assert.That(fallback.bbWeight, Is.Zero);
            Assert.That(fast.bbWeight, Is.EqualTo(100));
            Assert.That(fallback.Target, Is.SameAs(remainder));
            Assert.That(fast.Target, Is.SameAs(remainder));
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);
            Assert.That(compiler.Blocks.SelectMany(bb => bb.Statements)
                .Any(statement => statement.TreeList.Any(node => ReferenceEquals(node, call))), Is.False);
            var slow = fallback.FirstStmt!.RootNode.AsLclVar().Data.AsCall();
            Assert.That(slow.IsHelperCall(), Is.False);
            Assert.That(slow.Type, Is.EqualTo(TYP_REF));
            Assert.That(slow.Args.CountUserArgs(), Is.EqualTo(1));
            Assert.That(slow.ControlExpr!.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL), Is.True);
            Assert.That(slow.ControlExpr.AsIntCon().IconValue, Is.EqualTo((nint)0x3300));

            var address = check.FirstStmt!.RootNode.AsLclVar().Data.AsOp();
            Assert.That(address.Op2.AsIntCon().Type, Is.EqualTo(TYP_INT));
            Assert.That(address.Op2.AsIntCon().Flags & GTF_ICON_SECREL_OFFSET,
                Is.EqualTo(GTF_ICON_SECREL_OFFSET));
            Assert.That(address.Op2.AsIntCon().IconValue, Is.EqualTo((nint)0x1100));
            Assert.That(check.Statements.Count(), Is.EqualTo(3));
#if DEBUG
            Assert.That(check.LastStmt!.Id, Is.LessThan(check.FirstStmt!.NextStmt!.Id));
#endif
            Assert.That(remainder.FirstStmt, Is.SameAs(original));
            Assert.That(entry.Target, Is.SameAs(body));
        });
    }

    [TestCase(false, true, false, false)]
    [TestCase(true, true, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, false, false, true)]
    [TestCase(true, false, true, true)]
    public static void PhaseGatesRejectWithoutChangingTheCall(
        bool nativeAot, bool missingMethodFlag, bool minOpts, bool sizeOpt)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            var helper = nativeAot ? CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR
                : CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED;
            var call = NewCall(compiler, helper);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = !missingMethodFlag;
            if (sizeOpt)
            {
                compiler.opts.jitFlags->Set(JIT_FLAG_SIZE_OPT);
            }

            var expected = nativeAot && !missingMethodFlag
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
            Assert.That(Expand(compiler), Is.EqualTo(expected));
            Assert.That(s_coreRequests, Is.Zero);
            Assert.That(s_aotRequests, Is.EqualTo(expected is PhaseStatus.MODIFIED_EVERYTHING ? 1 : 0));
            if (expected is PhaseStatus.MODIFIED_NOTHING)
            {
                Assert.That(entry.Target, Is.SameAs(body));
                Assert.That(body.FirstStmt, Is.SameAs(original));
                Assert.That(original.RootNode.AsLclVar().Data, Is.SameAs(call));
            }
        }, minOpts);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RareBlockPolicyDependsOnTargetAbi(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, _, body) =>
        {
            body.setBBProfileWeight(0);
            var helper = nativeAot ? CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR
                : CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED;
            var call = NewCall(compiler, helper);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            Assert.That(Expand(compiler), Is.EqualTo(nativeAot
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_coreRequests, Is.Zero);
            Assert.That(s_aotRequests, Is.EqualTo(nativeAot ? 1 : 0));
            if (!nativeAot)
            {
                Assert.That(original.RootNode.AsLclVar().Data, Is.SameAs(call));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnrecognizedHelperIsNotExpanded(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            var call = NewCall(compiler, CORINFO_HELP_LMUL);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_coreRequests, Is.Zero);
            Assert.That(s_aotRequests, Is.Zero);
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(original.RootNode.AsLclVar().Data, Is.SameAs(call));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OtherAbiTlsHelperRemainsUnchanged(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            var helper = nativeAot ? CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
                : CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR;
            var call = NewCall(compiler, helper);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasTlsFieldAccess = true;
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_coreRequests, Is.Zero);
            Assert.That(s_aotRequests, Is.Zero);
            Assert.That(entry.Target, Is.SameAs(body));
            Assert.That(original.RootNode.AsLclVar().Data, Is.SameAs(call));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NestedCallSpillsEarlierEffectsBeforeTlsGuard(bool nativeAot)
    {
        WithCompiler(nativeAot, (compiler, entry, body) =>
        {
            var helper = nativeAot ? CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR
                : CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED;
            var call = NewCall(compiler, helper);
            var effect = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
            var expression = compiler.gtNewBinaryNode(GT_COMMA, TYP_BYREF, effect, call);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, expression));
            compiler.MethodHasTlsFieldAccess = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Target.Statements.Any(statement => statement.RootNode is GenTreeLclVar store
                && store.Oper is GT_STORE_LCL_VAR && store.LclNum == 0), Is.True);
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);
            Assert.That(compiler.Blocks.SelectMany(bb => bb.Statements)
                .Any(statement => statement.RootNode is GenTreeLclVar store
                    && store.Oper is GT_STORE_LCL_VAR && ReferenceEquals(store.Data, call)),
                Is.EqualTo(!nativeAot));
        });
    }

    private static PhaseStatus Expand(Compiler compiler)
        => compiler.fgExpandThreadLocalAccess();

    private static GenTreeCall NewCall(Compiler compiler, CorInfoHelpFunc helper, int typeIndex = 4)
    {
        var returnType = helper is CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2
            ? TYP_I_IMPL : TYP_BYREF;
        return compiler.gtNewHelperCallNode(returnType, helper, compiler.gtNewIconNode(TYP_INT, typeIndex));
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
    private static void GetCoreInfo(ICorJitInfo* self, CORINFO_THREAD_STATIC_BLOCKS_INFO* info)
    {
        s_coreRequests++;
        info->tlsIndex.addr = (void*)s_tlsIndex;
        info->offsetOfThreadLocalStoragePointer = s_storageOffset;
        info->offsetOfMaxThreadStaticBlocks = 0x18;
        info->offsetOfThreadStaticBlocks = 0x20;
        info->offsetOfBaseOfThreadLocalData = s_baseOffset;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetAotInfo(ICorJitInfo* self, CORINFO_THREAD_STATIC_INFO_NATIVEAOT* info)
    {
        s_aotRequests++;
        info->offsetOfThreadLocalStoragePointer = s_storageOffset;
        info->tlsRootObject.handle = (CORINFO_GENERIC_HANDLE)0x1100;
        info->tlsIndexObject.handle = (CORINFO_GENERIC_HANDLE)0x2200;
        info->threadStaticBaseSlow.addr = (void*)0x3300;
    }

    private static void WithCompiler(bool nativeAot, Action<Compiler, BasicBlock, BasicBlock> action,
        bool minOpts = false)
    {
        s_coreRequests = 0;
        s_aotRequests = 0;
        s_tlsIndex = 3;
        s_baseOffset = 0x30;
        s_storageOffset = 0x58;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getThreadLocalStaticBlocksInfo = &GetCoreInfo;
        vtable.Base.Base.getThreadLocalStaticInfo_NativeAOT = &GetAotInfo;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(ThreadLocalExpansionTests);
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
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_BYREF;
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
