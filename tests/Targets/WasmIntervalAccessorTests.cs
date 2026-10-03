#if TARGET_WASM
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;

namespace RyuJitSharp.Target.UnitTests;

internal static unsafe class WasmIntervalAccessorTests
{
    [Test]
    public static void IntervalAccessorsExposeBoundaryAndKindMetadata()
    {
        var block = new WasmInterval(2, 8, WasmInterval.Kind.Block);
        var loop = new WasmInterval(3, 7, WasmInterval.Kind.Loop);
        var tryInterval = new WasmInterval(4, 6, WasmInterval.Kind.Try);

        Assert.That(block.Start(), Is.EqualTo(2u));
        Assert.That(block.End(), Is.EqualTo(8u));
        Assert.That(block.ChainEnd(), Is.EqualTo(8u));
        Assert.That(block.IsBlock(), Is.True);
        Assert.That(block.IsLoop(), Is.False);
        Assert.That(block.IsTry(), Is.False);
        Assert.That(block.IsExnRefWrapper(), Is.False);

        Assert.That(loop.IsLoop(), Is.True);
        Assert.That(tryInterval.IsTry(), Is.True);
    }

    [Test]
    public static void ExceptionWrapperFactoryCreatesABlockInterval()
    {
        var start = new BasicBlock(null, null) { bbPreorderNum = 3 };
        var end = new BasicBlock(null, null) { bbPreorderNum = 9 };
        var wrapper = WasmInterval.NewExnRefWrapper(start, end);

        Assert.That(wrapper.Start(), Is.EqualTo(3u));
        Assert.That(wrapper.End(), Is.EqualTo(9u));
        Assert.That(wrapper.IsBlock(), Is.True);
        Assert.That(wrapper.IsExnRefWrapper(), Is.True);
    }

    [Test]
    public static void ChainedIntervalsPropagateTheFarthestEndAndCompress()
    {
        var first = new WasmInterval(3, 5, WasmInterval.Kind.Block);
        var second = new WasmInterval(4, 8, WasmInterval.Kind.Loop);
        var root = new WasmInterval(1, 12, WasmInterval.Kind.Try);

        first.SetChain(second);
        second.SetChain(root);

        Assert.That(root.ChainEnd(), Is.EqualTo(12u));
        Assert.That(first.FetchAndUpdateChain(), Is.SameAs(root));
        Assert.That(first.Chain(), Is.SameAs(root));
    }

    [Test]
    public static void JumpElisionPreservesWasmIntervalBoundaries()
    {
        WithCompiler(compiler => {
            var jump = NewBlock(compiler, BBJ_ALWAYS);
            var target = NewBlock(compiler, BBJ_RETURN);
            var otherTarget = NewBlock(compiler, BBJ_RETURN);
            jump.bbPreorderNum = 0;
            target.bbPreorderNum = 1;
            otherTarget.bbPreorderNum = 2;
            jump.Next = target;
            compiler.fgFirstBB = jump;
            compiler.fgLastBB = target;
            jump.TargetEdge = compiler.fgAddRefPred(target, jump);
            var intervals = new List<WasmInterval> {
                WasmInterval.NewBlock(jump, target)
            };
            compiler.fgWasmIntervals = intervals;

            Assert.That(jump.CanRemoveJumpToTarget(target, compiler), Is.True);

            intervals[0] = WasmInterval.NewLoop(jump, target);
            Assert.That(jump.CanRemoveJumpToTarget(target, compiler), Is.True);

            intervals[0] = WasmInterval.NewTry(jump, target);
            Assert.That(jump.CanRemoveJumpToTarget(target, compiler), Is.False);

            intervals[0] = WasmInterval.NewExnRefWrapper(jump, target);
            Assert.That(jump.CanRemoveJumpToTarget(target, compiler), Is.False);

            intervals[0] = WasmInterval.NewTry(jump, otherTarget);
            Assert.That(jump.CanRemoveJumpToTarget(target, compiler), Is.True);
        });
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;
        return block;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.fgPredsComputed = true;
        compiler.fgWasmIntervals = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
