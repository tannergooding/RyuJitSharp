// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LateDisassemblyTests
{
    [Test]
    public static void DisabledLateDisassemblyDoesNotAlterBuffersOrRequireCoreDisTools()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.doLateDisasm = false;
            codeGen.Disassembler.disInit(compiler);
            var hot = stackalloc byte[1] { 0x90 };
            var hotRW = stackalloc byte[1] { 0xC3 };
            var cold = stackalloc byte[1] { 0x90 };
            var coldRW = stackalloc byte[1] { 0xC3 };

            codeGen.Disassembler.disOpenForLateDisAsm("Method", "Class", default);
            codeGen.Disassembler.disAsmCode(hot, hotRW, 1, cold, coldRW, 1);
            codeGen.Disassembler.disDone();
            codeGen.Disassembler.disDone();

            Assert.That(*hot, Is.EqualTo(0x90));
            Assert.That(*hotRW, Is.EqualTo(0xC3));
            Assert.That(*cold, Is.EqualTo(0x90));
            Assert.That(*coldRW, Is.EqualTo(0xC3));
        });
    }

    [Test]
    public static void LateDisassemblerOpenRecordsMethodAndClassOnlyWhenEnabled()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var disassembler = codeGen.Disassembler;
            disassembler.disInit(compiler);
            compiler.opts.doLateDisasm = true;
            disassembler.disOpenForLateDisAsm("Method", "Class", default);
            Assert.That(CurrentMethodName(ref disassembler), Is.EqualTo("Method"));
            Assert.That(CurrentClassName(ref disassembler), Is.EqualTo("Class"));

            compiler.opts.doLateDisasm = false;
            disassembler.disOpenForLateDisAsm("DisabledMethod", "DisabledClass", default);
            Assert.That(CurrentMethodName(ref disassembler), Is.EqualTo("Method"));
            Assert.That(CurrentClassName(ref disassembler), Is.EqualTo("Class"));
        });
    }

    [Test]
    public static void LinearAddressAndRemainingSizeSpanHotAndColdCodeBuffers()
    {
        var hot = stackalloc byte[3] { 0x10, 0x11, 0x12 };
        var cold = stackalloc byte[2] { 0x20, 0x21 };
        var disassembler = default(Disassembler);
        HotCodeBlock(ref disassembler) = (nuint)hot;
        ColdCodeBlock(ref disassembler) = (nuint)cold;
        HotCodeSize(ref disassembler) = 3;
        ColdCodeSize(ref disassembler) = 2;

        Assert.That(LinearAddress(ref disassembler, 0) == hot, Is.True);
        Assert.That(LinearAddress(ref disassembler, 2) == hot + 2, Is.True);
        Assert.That(LinearAddress(ref disassembler, 3) == cold, Is.True);
        Assert.That(LinearAddress(ref disassembler, 4) == cold + 1, Is.True);
        Assert.That(RemainingBufferSize(ref disassembler, 0), Is.EqualTo((nuint)3));
        Assert.That(RemainingBufferSize(ref disassembler, 2), Is.EqualTo((nuint)1));
        Assert.That(RemainingBufferSize(ref disassembler, 3), Is.EqualTo((nuint)2));
        Assert.That(RemainingBufferSize(ref disassembler, 4), Is.EqualTo((nuint)1));
    }

    [Test]
    public static void RequestedLateDisassemblyFailsClosedWithoutDecoding()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.doLateDisasm = true;
            codeGen.Disassembler.disInit(compiler);
            var hot = stackalloc byte[1] { 0x90 };
            var hotRW = stackalloc byte[1] { 0xC3 };

            void Request()
            {
                codeGen.Disassembler.disAsmCode(hot, hotRW, 1, null, null, 0);
            }

            var exception = Assert.Throws<FatalJitException>(Request);
            Assert.That(exception?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(exception?.Message, Does.Contain("CoreDisTools callback ABI"));
            Assert.That(*hot, Is.EqualTo(0x90));
            Assert.That(*hotRW, Is.EqualTo(0xC3));
            codeGen.Disassembler.disDone();
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_curMethodName")]
    private static extern ref string? CurrentMethodName(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_curClassName")]
    private static extern ref string? CurrentClassName(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hotCodeBlock")]
    private static extern ref nuint HotCodeBlock(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_coldCodeBlock")]
    private static extern ref nuint ColdCodeBlock(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hotCodeSize")]
    private static extern ref nuint HotCodeSize(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_coldCodeSize")]
    private static extern ref nuint ColdCodeSize(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "disGetLinearAddr")]
    private static extern byte* LinearAddress(ref Disassembler disassembler, nuint offset);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "disGetBufferSize")]
    private static extern nuint RemainingBufferSize(ref Disassembler disassembler, nuint offset);
}
#endif
