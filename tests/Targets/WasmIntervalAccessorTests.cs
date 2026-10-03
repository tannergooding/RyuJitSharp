#if TARGET_WASM
using NUnit.Framework;

namespace RyuJitSharp.Target.UnitTests;

internal static class WasmIntervalAccessorTests
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
}
#endif
