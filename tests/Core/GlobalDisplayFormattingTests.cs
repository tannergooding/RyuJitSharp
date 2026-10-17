// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GlobalDisplayFormattingTests
{
    [Test]
    public static void DisplayHelpersPreserveNativeZeroAndReleaseIdentityBehavior()
    {
        var previousCompiler = JitTls.Compiler;
        using var tls = TestConfiguration.CreateJitTls();

        if (!TestConfiguration.IsDebug)
        {
            JitTls.Compiler = null;
        }

        try
        {
            Assert.That(JitTls.Compiler, Is.Null);
            Assert.That(Globals.dspOffset(0), Is.EqualTo((nint)0));
            Assert.That(Globals.dspPtr(null), Is.EqualTo((nint)0));

            if (!TestConfiguration.IsDebug)
            {
                var offset = (nint)0x1234;
                var pointer = (void*)0x1234;
                Assert.That(Globals.dspOffset(offset), Is.EqualTo(offset));
                Assert.That(Globals.dspPtr(pointer), Is.EqualTo((nint)pointer));
            }
        }
        finally
        {
            if (!TestConfiguration.IsDebug)
            {
                JitTls.Compiler = previousCompiler;
            }
        }
    }

    [Test]
    public static void PureCompilerDiagnosticFormattersRetainTheirNativeText()
    {
        Assert.That(Globals.DevirtualizationDetailToString(
            CORINFO_DEVIRTUALIZATION_DETAIL.CORINFO_DEVIRTUALIZATION_SUCCESS), Is.EqualTo("success"));
        Assert.That(Globals.refCntWtd2str(Globals.BB_UNITY_WEIGHT), Is.EqualTo("1"));
        Assert.That(GenTree.gtGetHandleKindString(Globals.GTF_ICON_CLASS_HDL),
            Is.EqualTo(nameof(Globals.GTF_ICON_CLASS_HDL)));
    }

    [Test]
    public static void InvalidPointSentinelsUseNativeIntWidth()
    {
        var expectedCc = IntPtr.Size == 8
            ? unchecked((nint)(long)0xCCCCCCCC_CCCCCCCC)
            : unchecked((nint)unchecked((int)0xCCCCCCCC));
        var expectedCd = IntPtr.Size == 8
            ? unchecked((nint)(long)0xCDCDCDCD_CDCDCDCD)
            : unchecked((nint)unchecked((int)0xCDCDCDCD));

        Assert.That(Globals.INVALID_POINT_CC, Is.EqualTo(expectedCc));
        Assert.That(Globals.INVALID_POINT_CD, Is.EqualTo(expectedCd));
    }

    [Test]
    public static void PointerFormattersUseNativeAddressWidth()
    {
        var address = (void*)(nuint)0x12345678u;
        var expectedDebugAddress = IntPtr.Size == 8 ? " 00000000`12345678 " : " 12345678 ";
        var expectedPointer = IntPtr.Size == 8 ? "0000000012345678" : "12345678";
        var expectedDisplayPointer = IntPtr.Size == 8 ? "0000000000000000" : "00000000";

        Assert.That(Globals.FMT_DBG_ADDR(address), Is.EqualTo(expectedDebugAddress));
        Assert.That(Globals.FMT_DSP_PTR(null), Is.EqualTo(expectedDisplayPointer));
        Assert.That(Globals.FMT_PTR(address), Is.EqualTo(expectedPointer));
    }
}
