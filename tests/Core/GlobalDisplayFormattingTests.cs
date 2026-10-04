// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GlobalDisplayFormattingTests
{
    [Test]
    public static void DisplayHelpersPreserveNativeZeroAndReleaseIdentityBehavior()
    {
#if DEBUG
        using var tls = new JitTls(null);
#else
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = null;
        try
        {
#endif
            Assert.That(JitTls.Compiler, Is.Null);
            Assert.That(Globals.dspOffset(0), Is.EqualTo((nint)0));
            Assert.That(Globals.dspPtr(null), Is.EqualTo((nint)0));

#if !DEBUG
            var offset = (nint)0x1234;
            var pointer = (void*)0x1234;
            Assert.That(Globals.dspOffset(offset), Is.EqualTo(offset));
            Assert.That(Globals.dspPtr(pointer), Is.EqualTo((nint)pointer));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
#endif
    }
}
