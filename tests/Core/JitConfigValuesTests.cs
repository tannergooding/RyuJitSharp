// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class JitConfigValuesTests
{
    [Test]
    public static void DebugConfigurationValuesBindUniversallyButRemainDormantInRelease()
    {
        var debugRangeValue = stackalloc byte[1];
        debugRangeValue[0] = 0;

        ICorJitHost.Vtbl vtable = new()
        {
            getIntConfigValue = &GetIntConfigValue,
            getStringConfigValue = &GetStringConfigValue,
            freeStringConfigValue = &FreeStringConfigValue,
        };
        HostContext context = new()
        {
            Host = new ICorJitHost { lpVtbl = &vtable },
            DebugRangeValue = debugRangeValue,
        };

        var config = default(JitConfigValues);
        config.initialize(&context.Host);
        try
        {
            Assert.That(config.JitEnableNoWayAssert, Is.EqualTo(TestConfiguration.IsDebug ? 1 : 0));

            if (TestConfiguration.IsDebug)
            {
                Assert.That(config.JitStress, Is.Zero);
                Assert.That(context.DebugIntegerReads, Is.EqualTo(1));
                Assert.That(context.DebugRangeReads, Is.EqualTo(1));
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitStress);
                Assert.That(context.DebugIntegerReads, Is.Zero);
                Assert.That(context.DebugRangeReads, Is.Zero);
            }
        }
        finally
        {
            config.destroy(&context.Host);
        }

        Assert.That(context.DebugRangeFrees, Is.EqualTo(TestConfiguration.IsDebug ? 1 : 0));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HostContext
    {
        public ICorJitHost Host;
        public byte* DebugRangeValue;
        public int DebugIntegerReads;
        public int DebugRangeReads;
        public int DebugRangeFrees;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetIntConfigValue(ICorJitHost* host, byte* name, int defaultValue)
    {
        var context = (HostContext*)host;
        if (IsConfigName(name, "JitStress"u8))
        {
            context->DebugIntegerReads++;
        }

        return defaultValue;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetStringConfigValue(ICorJitHost* host, byte* name)
    {
        var context = (HostContext*)host;
        if (IsConfigName(name, "JitRange"u8))
        {
            context->DebugRangeReads++;
            return context->DebugRangeValue;
        }

        return null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void FreeStringConfigValue(ICorJitHost* host, byte* value)
    {
        var context = (HostContext*)host;
        if (value == context->DebugRangeValue)
        {
            context->DebugRangeFrees++;
        }
    }

    private static bool IsConfigName(byte* name, ReadOnlySpan<byte> expected)
    {
        var length = 0;
        while (name[length] != 0)
        {
            length++;
        }

        return new ReadOnlySpan<byte>(name, length).SequenceEqual(expected);
    }
}
