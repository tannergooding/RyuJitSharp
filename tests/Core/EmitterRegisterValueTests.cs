// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterRegisterValueTests
{
    [TestCase(REG_X64_XMM0)]
    [TestCase(REG_X64_XMM7)]
    [TestCase(REG_X64_K0)]
    [TestCase(REG_X64_K7)]
    [TestCase(REG_X64_XMM15)]
    public static void ImmediateRegistersRoundTripWithoutChangingTheRegisterNumber(regNumber reg)
    {
        Assume.That(Configuration.TARGET_XARCH);
        if (reg == REG_X64_XMM15)
        {
            Assume.That(Configuration.TARGET_AMD64);
        }

        var value = Encode(null, reg);
        Assert.That(value, Is.EqualTo(unchecked((sbyte)reg)));
        Assert.That(Decode(null, value), Is.EqualTo(reg));
    }

    [TestCase(REG_X64_RAX, 0u)]
    [TestCase(REG_X64_RDI, 7u)]
    [TestCase(REG_X64_XMM0, 0u)]
    [TestCase(REG_X64_XMM7, 7u)]
    [TestCase(REG_X64_K0, 0u)]
    [TestCase(REG_X64_K7, 7u)]
    [TestCase(REG_X64_R24, 24u)]
    [TestCase(REG_X64_XMM15, 15u)]
    [TestCase(REG_X64_XMM16, 16u)]
    [TestCase(REG_X64_XMM31, 31u)]
    public static void RegisterClassesUseNativeAbsoluteAndOpcodeBits(regNumber reg, uint number)
    {
        Assume.That(Configuration.TARGET_XARCH);
        if (reg is REG_X64_R24 or REG_X64_XMM15 or REG_X64_XMM16 or REG_X64_XMM31)
        {
            Assume.That(Configuration.TARGET_AMD64);
        }

        Assert.That((uint)AbsRegNumber(reg), Is.EqualTo(number));
        Assert.That(RegEncoding(reg), Is.EqualTo(number & 7));
    }

    [TestCase(-129, 2)]
    [TestCase(-128, 1)]
    [TestCase(0, 1)]
    [TestCase(127, 1)]
    [TestCase(128, 2)]
    [TestCase(256, 2)]
    public static void InvalidImmediateRegistersRetainNativeDecodeAssertions(int value, int expected)
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.TARGET_XARCH);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);

        var reg = Decode(null, value);

        Assert.That(reg, Is.EqualTo(unchecked((regNumber)value)));
        Assert.That(context.Assertions, Is.EqualTo(expected));
    }

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        ((AssertionContext*)self)->Assertions++;
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "encodeRegAsIval")]
    private static extern sbyte Encode(Emitter? emitter, regNumber reg);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "decodeRegFromIval")]
    private static extern regNumber Decode(Emitter? emitter, nint value);
}
