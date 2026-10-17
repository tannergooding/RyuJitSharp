// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class BlockDisplayTests
{
    [Test]
    public static unsafe void NewBlockVerboseDumpUsesStableDisplay()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        using var tls = TestConfiguration.CreateJitTlsWithCompiler(compiler);
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.compHndBBtab = [];
        TestConfiguration.EnableSafeBasicBlockCreation(compiler);
        TestConfiguration.EnableSafeFlowEdgeCreation(compiler);
        compiler.fgPredsComputed = true;
        TestConfiguration.EnableVerbose(compiler);

        var output = CodeGenLifeTransitionTests.Capture(
            () => BasicBlock.New(compiler, BBJ_RETURN));

        Assert.That(
            output,
            TestConfiguration.IsDebug
                ? Does.Contain("New Basic Block BB01 [0000] created.")
                : Is.Empty);
    }

    [TestCase(false, -2, 2)]
    [TestCase(false, 0, 0)]
    [TestCase(false, 2, 2)]
    [TestCase(true, -2, 2)]
    [TestCase(true, 0, 0)]
    [TestCase(true, 2, 2)]
    public static unsafe void TargetPaddingPreservesSignedPrintfWidth(bool conditional, int delta, int padding)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        using var tls = TestConfiguration.CreateJitTlsWithCompiler(compiler);
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.compHndBBtab = [];
        TestConfiguration.EnableSafeBasicBlockCreation(compiler);
        TestConfiguration.EnableSafeFlowEdgeCreation(compiler);
        compiler.fgPredsComputed = true;
        var block = BasicBlock.New(compiler, conditional ? BBJ_COND : BBJ_RETURN);
        compiler.fgFirstBB = block;
        var printedWidth = 9;
        var targetText = "";
        var kindText = " (return)";

        if (conditional)
        {
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBJ_RETURN);
            block.SetCond(compiler.fgAddRefPred(first, block), compiler.fgAddRefPred(second, block));
            if (TestConfiguration.IsDebug)
            {
                block.TrueEdge.Likelihood = 0.03125;
                block.FalseEdge.Likelihood = 0.96875;
            }

            targetText = "-> BB02(0.0312),BB03(0.969)";
            kindText = " ( cond )";
            printedWidth = targetText.Length + kindText.Length;
        }

        var range = CodeGenLifeTransitionTests.Capture(() => block.dspBlockILRange());
        var output = CodeGenLifeTransitionTests.Capture(
            () => compiler.fgTableDispBasicBlock(block, blockTargetFieldWidth: printedWidth + delta));

        if (!TestConfiguration.IsDebug)
        {
            Assert.That(range, Is.Empty);
            Assert.That(output, Is.Empty);
            return;
        }

        Assert.That(output, Does.Contain(range + targetText + new string(' ', padding) + kindText));
    }

    [TestCase((BasicBlockFlags)0, "")]
    [TestCase(BBF_IMPORTED, "i")]
    [TestCase(BBF_INTERNAL | BBF_COLD, "internal cold")]
    [TestCase(BBF_STALE_PREDICATE, "stale-pred")]
    [TestCase(BBF_IMPORTED | BBF_THROW_HELPER | BBF_STALE_PREDICATE, "i throw-hlpr stale-pred")]
    public static void FlagsMatchNativeOrderAndSeparators(BasicBlockFlags flags, string expected)
    {
        var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
        block.FlagsRaw = flags;

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previousWriter = Globals.s_jitstdout;

        try
        {
            Globals.s_jitstdout = writer;
            block.dspFlags();
            writer.Flush();
        }
        finally
        {
            Globals.s_jitstdout = previousWriter;
        }

        Assert.That(
            Encoding.UTF8.GetString(stream.ToArray()),
            Is.EqualTo(TestConfiguration.IsDebug ? expected : string.Empty));
    }

    [TestCase(0UL, "00000000.00000000")]
    [TestCase(0x80000000UL, "00000000.80000000")]
    [TestCase(0x100000000UL, "00000001.00000000")]
    [TestCase(0x8000000000000000UL, "80000000.00000000")]
    [TestCase(ulong.MaxValue, "ffffffff.ffffffff")]
    public static void HeaderPrintsBothFlagWordsWithoutCheckedOverflow(ulong bits, string expected)
    {
        var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
        block.FlagsRaw = unchecked((BasicBlockFlags)bits);

        var output = CodeGenLifeTransitionTests.Capture(
            () => block.dspBlockHeader(showKind: false, showFlags: true, showPreds: false));

        Assert.That(
            output,
            TestConfiguration.IsDebug
                ? Does.Contain($" flags=0x{expected}: ")
                : Is.Empty);
        Assert.That(block.FlagsRaw, Is.EqualTo(unchecked((BasicBlockFlags)bits)));
    }
}
