// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;

namespace RyuJitSharp.UnitTests;

#if DEBUG
[NonParallelizable]
internal static class BlockDisplayTests
{
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

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
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

        Assert.That(output, Does.Contain($" flags=0x{expected}: "));
        Assert.That(block.FlagsRaw, Is.EqualTo(unchecked((BasicBlockFlags)bits)));
    }
}
#endif
