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
}
#endif
