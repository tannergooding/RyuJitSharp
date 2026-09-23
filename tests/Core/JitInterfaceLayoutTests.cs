// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class JitInterfaceLayoutTests
{
    [Test]
    public static unsafe void WindowsX64LayoutsMatchPinnedNativeHeaders()
    {
        if (!OperatingSystem.IsWindows() || (RuntimeInformation.ProcessArchitecture != Architecture.X64))
        {
            Assert.Ignore("These measurements are from the Windows-x64 native headers.");
        }

        // Measured with MSVC against corjit.h at 206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc.
        // Use raw managed sizes, not marshaller layouts: the JIT passes these through pointers.
        Assert.Multiple(() => {
            Assert.That(sizeof(ICorDebugInfo.VarLoc), Is.EqualTo(16));
            Assert.That(sizeof(ICorDebugInfo.NativeVarInfo), Is.EqualTo(32));
            Assert.That(sizeof(CORINFO_ASYNC_INFO), Is.EqualTo(128));
            Assert.That(sizeof(CORINFO_EE_INFO), Is.EqualTo(80));
            Assert.That(sizeof(CORINFO_CALL_INFO), Is.EqualTo(344));
            Assert.That(sizeof(CORINFO_WASM_WELLKNOWN_GLOBALS), Is.EqualTo(32));
        });

        ICorDebugInfo.NativeVarInfo info = default;
        Assert.That((byte*)&info.callReturnValueILOffset - (byte*)&info, Is.EqualTo(8));
        Assert.That((byte*)&info.loc - (byte*)&info, Is.EqualTo(16));
    }
}
