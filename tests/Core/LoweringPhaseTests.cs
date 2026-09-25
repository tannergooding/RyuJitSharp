// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
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

    [Test]
    public static void LifetimeEnabledModeRejectsBeforeMutatingLocalsBlocksOrPInvokeState()
    {
        WithCompiler((compiler, allocator) => {
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = entry;
            var marker = new GenTree(GT_NO_OP, TYP_VOID);
            entry.InsertAtEnd(marker);
            var dfs = compiler._dfsTree = compiler.fgComputeDfs();

            var exception = Assert.Throws<FatalJitException>(() => DoPhase(new Lowering(compiler, allocator)));

            Assert.That(exception!.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.False);
            Assert.That(entry.FirstNode, Is.SameAs(marker));
            Assert.That(entry.LastNode, Is.SameAs(marker));
            Assert.That(compiler._dfsTree, Is.SameAs(dfs));
            Assert.That(compiler.compCurBB, Is.Null);
        }, minOpts: false);
    }

    [Test]
    public static void UnfinishedAllocationReturnsExplicitSkip()
    {
        WithCompiler((compiler, allocator) => {
            var exception = Assert.Throws<FatalJitException>(() => allocator.DoRegisterAllocation());

            Assert.That(exception!.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.False);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DoPhase")]
    private static extern PhaseStatus DoPhase(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? RegAlloc(Compiler compiler);

    private static void WithCompiler(Action<Compiler, LinearScan> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[0].lvImplicitlyReferenced = true;
        compiler.compRationalIRForm = true;
        compiler.fgNodeThreading = NodeThreading.LIR;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitConfig = new JitConfigValues();
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
