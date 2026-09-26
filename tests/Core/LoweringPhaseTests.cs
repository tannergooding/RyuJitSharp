// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoweringPhaseTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void LowersAllBlocksRemovesUnreachableCodeAndInvalidatesDfs(bool existingDfs)
    {
        WithCompiler((compiler, allocator) => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var dead = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = dead;
            entry.Next = dead;
            dead.bbRefs = 0;
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var keepAlive = compiler.gtNewUnaryNode(GT_KEEPALIVE, TYP_VOID, address);
            address.Next = keepAlive;
            keepAlive.Prev = address;
            entry.InsertAtEnd(new LIR.Range(address, keepAlive));
            var deadAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var deadKeepAlive = compiler.gtNewUnaryNode(GT_KEEPALIVE, TYP_VOID, deadAddress);
            deadAddress.Next = deadKeepAlive;
            deadKeepAlive.Prev = deadAddress;
            dead.InsertAtEnd(new LIR.Range(deadAddress, deadKeepAlive));
            if (existingDfs)
            {
                compiler._dfsTree = compiler.fgComputeDfs();
            }

            Assert.That(DoPhase(new Lowering(compiler, allocator)), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.True);
            Assert.That(address.IsRegOptional, Is.True);
            Assert.That(deadAddress.IsRegOptional, Is.True);
            Assert.That(compiler.compCurBB, Is.SameAs(dead));
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
            Assert.That(entry.Next, Is.Null);
            Assert.That(compiler.fgLastBB, Is.SameAs(entry));
            Assert.That(dead.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.True);
            Assert.That(compiler._dfsTree, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LifetimeEnabledModeRunsLivenessWithoutChangingTheFlowGraph(bool existingDfs)
    {
        WithCompiler((compiler, allocator) => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = entry;
            entry.bbRefs = 1;
            entry.SetFlags(BBF_IMPORTED);
            compiler.fgPredsComputed = true;
            var local = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var keepAlive = compiler.gtNewUnaryNode(GT_KEEPALIVE, TYP_VOID, local);
            local.Next = keepAlive;
            keepAlive.Prev = local;
            entry.InsertAtEnd(new LIR.Range(local, keepAlive));
            if (existingDfs)
            {
                compiler._dfsTree = compiler.fgComputeDfs();
            }

            Assert.That(DoPhase(new Lowering(compiler, allocator)), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            Assert.That(entry.FirstNode, Is.SameAs(local));
            Assert.That(entry.LastNode, Is.SameAs(keepAlive));
            Assert.That(local.IsRegOptional, Is.True);
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.fgBBVarSetsInited, Is.True);
            Assert.That(compiler.lvaTable[0].lvRefCnt(), Is.EqualTo(1));
            Assert.That(compiler._dfsTree, Is.Null);
        }, minOpts: false);
    }

    [Test]
    public static void LifetimeEnabledModeRefreshesDfsRerunsLivenessAndRecountsAfterGraphChanges()
    {
        WithCompiler((compiler, allocator) => {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var successor = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = successor;
            entry.Next = successor;
            successor.Prev = entry;
            entry.bbRefs = 1;
            successor.bbRefs = 0;
            entry.SetFlags(BBF_IMPORTED);
            successor.SetFlags(BBF_IMPORTED);
            compiler.fgPredsComputed = true;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(successor, entry));

            var unused = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            unused.IsUnusedValue = true;
            entry.InsertAtEnd(unused);
            compiler._dfsTree = compiler.fgComputeDfs();

#if DEBUG
            compiler.verbose = true;
            var output = CodeGenLifeTransitionTests.Capture(() => DoPhase(new Lowering(compiler, allocator)));
            Assert.That(Occurrences(output, "had to run another liveness pass:"), Is.EqualTo(1));
            Assert.That(Occurrences(output, "In Liveness::Init"), Is.EqualTo(2));
#else
            Assert.That(DoPhase(new Lowering(compiler, allocator)), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
#endif
            Assert.That(unused.Prev, Is.Null);
            Assert.That(entry.FirstNode, Is.Null);
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.lvaTable[0].lvRefCnt(), Is.Zero);
            Assert.That(compiler._dfsTree, Is.Null);
        }, minOpts: false);
    }

#if LATE_DISASM
    [Test]
    public static void UnsupportedLateDisassemblyReturnsExplicitSkipBeforeGeneration()
    {
        WithCompiler((compiler, allocator) => {
            compiler.opts.doLateDisasm = true;
            var exception = Assert.Throws<FatalJitException>(() => compiler.codeGen!.genGenerateCode(out _, out _));

            Assert.That(exception!.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.False);
        });
    }
#endif

#if FEATURE_LOOP_ALIGN
    [TestCase(false)]
    [TestCase(true)]
    public static void LoopAlignmentWithoutNaturalLoopsLeavesGraphUnchanged(bool enabled)
    {
        WithCompiler((compiler, allocator) => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgPredsComputed = true;
            compiler.codeGen!.ShouldAlignLoops = enabled;
            Assert.That(compiler.placeLoopAlignInstructions(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DoPhase")]
    private static extern PhaseStatus DoPhase(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? RegAlloc(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocalsToTrack(ref JitConfigValues config);

#if DEBUG
    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var position = text.IndexOf(value, StringComparison.Ordinal); position >= 0;
            position = text.IndexOf(value, position + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
#endif

    private static void WithCompiler(Action<Compiler, LinearScan> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaArg0Var = BAD_VAR_NUM;
        compiler.compHndBBtab = [];
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[0].lvImplicitlyReferenced = minOpts;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.compRationalIRForm = true;
        compiler.fgNodeThreading = NodeThreading.LIR;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitConfig = new JitConfigValues();
        MaxLocalsToTrack(ref JitConfig) = 1024;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var allocator = new LinearScan(compiler);
            RegAlloc(compiler) = allocator;
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
