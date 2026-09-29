// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterRegisterValueTests
{
    [TestCase(REG_XMM0)]
    [TestCase(REG_XMM7)]
    [TestCase(REG_K0)]
    [TestCase(REG_K7)]
#if TARGET_AMD64
    [TestCase(REG_XMM15)]
#endif
    public static void ImmediateRegistersRoundTripWithoutChangingTheRegisterNumber(regNumber reg)
    {
        var value = Encode(null, reg);
        Assert.That(value, Is.EqualTo(unchecked((sbyte)reg)));
        Assert.That(Decode(null, value), Is.EqualTo(reg));
    }

    [TestCase(REG_RAX, 0u)]
    [TestCase(REG_RDI, 7u)]
    [TestCase(REG_XMM0, 0u)]
    [TestCase(REG_XMM7, 7u)]
    [TestCase(REG_K0, 0u)]
    [TestCase(REG_K7, 7u)]
#if TARGET_AMD64
    [TestCase(REG_R24, 24u)]
    [TestCase(REG_XMM15, 15u)]
    [TestCase(REG_XMM16, 16u)]
    [TestCase(REG_XMM31, 31u)]
#endif
    public static void RegisterClassesUseNativeAbsoluteAndOpcodeBits(regNumber reg, uint number)
    {
        Assert.That((uint)AbsRegNumber(reg), Is.EqualTo(number));
        Assert.That(RegEncoding(reg), Is.EqualTo(number & 7));
    }

#if DEBUG
    [TestCase(-129, 2)]
    [TestCase(-128, 1)]
    [TestCase(0, 1)]
    [TestCase(127, 1)]
    [TestCase(128, 2)]
    [TestCase(256, 2)]
    public static void InvalidImmediateRegistersRetainNativeDecodeAssertions(int value, int expected)
    {
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
#endif

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "encodeRegAsIval")]
    private static extern sbyte Encode(Emitter? emitter, regNumber reg);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "decodeRegFromIval")]
    private static extern regNumber Decode(Emitter? emitter, nint value);
}
#endif
