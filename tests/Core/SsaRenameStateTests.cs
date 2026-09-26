// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.MemoryKind;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SsaRenameStateTests
{
    [TestCase(0)]
    [TestCase(2)]
    public static void RepeatedLocalDefinitionsInOneBlockReplaceTheTop(int local)
    {
        SsaLivenessTests.WithCompiler(3, compiler => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var child = BasicBlock.New(compiler, BBJ_RETURN);
            var state = new SsaRenameState(compiler);

            state.Push(entry, local, 1);
            state.Push(entry, local, 2);
            Assert.That(state.Top(local), Is.EqualTo(2));

            state.Push(child, local, 3);
            state.Push(child, local, 4);
            Assert.That(state.Top(local), Is.EqualTo(4));
            state.PopBlockStacks(child);
            Assert.That(state.Top(local), Is.EqualTo(2));

            state.Push(child, local, 5);
            Assert.That(state.Top(local), Is.EqualTo(5));
            state.PopBlockStacks(child);
            Assert.That(state.Top(local), Is.EqualTo(2));
        });
    }

    [Test]
    public static void InterleavedLocalsAndMemoryPopInReversePushOrder()
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var child = BasicBlock.New(compiler, BBJ_RETURN);
            var grandchild = BasicBlock.New(compiler, BBJ_RETURN);
            var state = new SsaRenameState(compiler);

            state.Push(entry, 0, 10);
            state.PushMemory(ByrefExposed, entry, 20);
            state.Push(entry, 1, 30);
            state.PushMemory(GcHeap, entry, 40);

            state.PushMemory(GcHeap, child, 41);
            state.Push(child, 1, 31);
            state.Push(child, 0, 11);
            state.PushMemory(ByrefExposed, child, 21);
            state.Push(child, 0, 12);
            state.PushMemory(GcHeap, child, 42);

            state.Push(grandchild, 1, 32);
            state.PushMemory(ByrefExposed, grandchild, 22);
            Assert.That(state.Top(0), Is.EqualTo(12));
            Assert.That(state.Top(1), Is.EqualTo(32));
            Assert.That(state.TopMemory(ByrefExposed), Is.EqualTo(22));
            Assert.That(state.TopMemory(GcHeap), Is.EqualTo(42));

            state.PopBlockStacks(grandchild);
            Assert.That(state.Top(1), Is.EqualTo(31));
            Assert.That(state.TopMemory(ByrefExposed), Is.EqualTo(21));
            state.PopBlockStacks(child);
            Assert.That(state.Top(0), Is.EqualTo(10));
            Assert.That(state.Top(1), Is.EqualTo(30));
            Assert.That(state.TopMemory(ByrefExposed), Is.EqualTo(20));
            Assert.That(state.TopMemory(GcHeap), Is.EqualTo(40));

            state.PopBlockStacks(entry);
            _ = Assert.Throws<InvalidOperationException>(() => state.TopMemory(ByrefExposed));
            _ = Assert.Throws<InvalidOperationException>(() => state.TopMemory(GcHeap));
        });
    }

    [TestCase(ByrefExposed)]
    [TestCase(GcHeap)]
    public static void MemoryKindsRetainIndependentDefinitionsAndCallerChosenAlias(MemoryKind updated)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var child = BasicBlock.New(compiler, BBJ_RETURN);
            var state = new SsaRenameState(compiler);
            var other = updated == ByrefExposed ? GcHeap : ByrefExposed;

            state.PushMemory(ByrefExposed, entry, 1);
            state.PushMemory(GcHeap, entry, 1);
            state.PushMemory(updated, child, 2);
            state.PushMemory(updated, child, 3);
            Assert.That(state.TopMemory(updated), Is.EqualTo(3));
            Assert.That(state.TopMemory(other), Is.EqualTo(1));
            Assert.That(state.TopMemory(ByrefExposed), Is.EqualTo(updated == ByrefExposed ? 3 : 1));

            state.PopBlockStacks(child);
            Assert.That(state.TopMemory(updated), Is.EqualTo(1));
            Assert.That(state.TopMemory(other), Is.EqualTo(1));
        });
    }

    [Test]
    public static void PoppingACompletedBlockLeavesOuterDefinitionsUntouched()
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var child = BasicBlock.New(compiler, BBJ_RETURN);
            var sibling = BasicBlock.New(compiler, BBJ_RETURN);
            var state = new SsaRenameState(compiler);

            state.Push(entry, 0, 1);
            state.Push(child, 0, 2);
            state.PopBlockStacks(entry);
            Assert.That(state.Top(0), Is.EqualTo(2));

            state.PopBlockStacks(child);
            state.Push(sibling, 0, 3);
            state.Push(sibling, 1, 4);
            Assert.That(state.Top(0), Is.EqualTo(3));
            Assert.That(state.Top(1), Is.EqualTo(4));
            state.PopBlockStacks(sibling);
            Assert.That(state.Top(0), Is.EqualTo(1));
            state.PopBlockStacks(entry);
        });
    }

#if DEBUG
    [Test]
    public static void VerboseSsaDiagnosticsTrackLocalAndMemoryStacks()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var state = new SsaRenameState(compiler);
            compiler.verboseSsa = true;

            var output = CodeGenLifeTransitionTests.Capture(() => {
                state.Push(block, 0, 7);
                _ = state.Top(0);
                state.PushMemory(ByrefExposed, block, 8);
                state.PushMemory(GcHeap, block, 9);
                state.PopBlockStacks(block);
            });

            Assert.That(output, Does.Contain($"[SsaRenameState::Push] {Globals.FMT_BB(block.bbNum)}, V00, ssaNum = 7"));
            Assert.That(output, Does.Contain($"[SsaRenameState::Top] {Globals.FMT_BB(block.bbNum)}, V00, ssaNum = 7"));
            Assert.That(output, Does.Contain($"V00: <{Globals.FMT_BB(block.bbNum)}, 7>"));
            Assert.That(output, Does.Contain($"ByrefExposed: <{Globals.FMT_BB(block.bbNum)}, 8>"));
            Assert.That(output, Does.Contain($"GcHeap: <{Globals.FMT_BB(block.bbNum)}, 9>"));
            Assert.That(output, Does.Contain("[SsaRenameState::PopBlockStacks]"));
        });
    }
#endif
}
