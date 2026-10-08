// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.Collections.Generic;
using System.IO;
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

    [TestCase(false)]
    [TestCase(true)]
    public static void DiffableAddressesPreserveNullAndObscureOnlyNonzeroAddresses(bool diffable)
    {
        var disassembler = default(Disassembler);
        Diffable(ref disassembler) = diffable;
        nuint address = 0x1234;
        var expected = diffable ? (nuint)0xD1FFAB1E : address;

        Assert.That(DisplayAddress(ref disassembler, 0), Is.EqualTo((nuint)0));
        Assert.That(DisplayAddress(ref disassembler, address), Is.EqualTo(expected));
        Assert.That(DisplayPointer(ref disassembler, null) == null, Is.True);
        Assert.That((nuint)DisplayPointer(ref disassembler, (void*)address), Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RegisterCallbackDeclinesAnnotationAndClearsDeferredName(bool hasName)
    {
        var disassembler = default(Disassembler);
        HasName(ref disassembler) = hasName;
        var output = '?';

        var result = RegisterCallback(ref disassembler, null, int.MaxValue, &output, 1);

        Assert.That(result, Is.EqualTo((nuint)0));
        Assert.That(HasName(ref disassembler), Is.False);
        Assert.That(output, Is.EqualTo('?'));
    }

    [Test]
    public static void InitializationResetsMapsAndLabelsWithoutClosingBorrowedOutput()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            using var stream = new MemoryStream();
            using var output = new StreamWriter(stream, leaveOpen: true);
            var disassembler = codeGen.Disassembler;
            HasName(ref disassembler) = true;
            Labels(ref disassembler) = [1];
            MethodMap(ref disassembler) = [];
            HelperMap(ref disassembler) = [];
            Relocations(ref disassembler) = [];
            Diffable(ref disassembler) = true;
            Output(ref disassembler) = output;
#if USE_COREDISTOOLS
            Decoder(ref disassembler) = 1;
#endif

            disassembler.disInit(compiler);
            Assert.That(HasName(ref disassembler), Is.False);
            Assert.That(Labels(ref disassembler), Is.Null);
            Assert.That(MethodMap(ref disassembler), Is.Null);
            Assert.That(HelperMap(ref disassembler), Is.Null);
            Assert.That(Relocations(ref disassembler), Is.Null);
            Assert.That(Diffable(ref disassembler), Is.False);
            Assert.That(Output(ref disassembler), Is.Null);
#if USE_COREDISTOOLS
            Assert.That(Decoder(ref disassembler), Is.EqualTo((nuint)0));
#endif
            disassembler.disDone();
            disassembler.disDone();

            output.Write("still borrowed");
            output.Flush();
            Assert.That(stream.Length, Is.GreaterThan(0));
        });
    }

#if USE_COREDISTOOLS
    [Test]
    public static void CleanupRejectsAnUnportedLiveDecoderInsteadOfDiscardingItsHandle()
    {
        var disassembler = default(Disassembler);
        Decoder(ref disassembler) = 1;

        var exception = Assert.Throws<FatalJitException>(() => disassembler.disDone());
        Assert.That(exception?.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(exception?.Message, Does.Contain("FinishDisasm"));
        Assert.That(Decoder(ref disassembler), Is.EqualTo((nuint)1));
    }
#endif

    [Test]
    public static void RequestedLateDisassemblyFailsClosedWithoutDecoding()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.doLateDisasm = true;
            codeGen.Disassembler.disInit(compiler);
            var hot = stackalloc byte[1] { 0x90 };
            var hotRW = stackalloc byte[1] { 0xC3 };
            var cold = stackalloc byte[1] { 0xCC };
            var coldRW = stackalloc byte[1] { 0xC3 };
            HotCodeBlock(ref codeGen.Disassembler) = 0x1234;
            ColdCodeBlock(ref codeGen.Disassembler) = 0x5678;
            HotCodeSize(ref codeGen.Disassembler) = 3;
            ColdCodeSize(ref codeGen.Disassembler) = 2;

            void Request()
            {
                codeGen.Disassembler.disAsmCode(hot, hotRW, 1, cold, coldRW, 1);
            }

            var exception = Assert.Throws<FatalJitException>(Request);
            Assert.That(exception?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(exception?.Message, Does.Contain("CoreDisTools callback ABI"));
            Assert.That(*hot, Is.EqualTo(0x90));
            Assert.That(*hotRW, Is.EqualTo(0xC3));
            Assert.That(*cold, Is.EqualTo(0xCC));
            Assert.That(*coldRW, Is.EqualTo(0xC3));
            Assert.That(HotCodeBlock(ref codeGen.Disassembler), Is.EqualTo((nuint)0x1234));
            Assert.That(ColdCodeBlock(ref codeGen.Disassembler), Is.EqualTo((nuint)0x5678));
            Assert.That(HotCodeSize(ref codeGen.Disassembler), Is.EqualTo((nuint)3));
            Assert.That(ColdCodeSize(ref codeGen.Disassembler), Is.EqualTo((nuint)2));
            Assert.That(Labels(ref codeGen.Disassembler), Is.Null);
            Assert.That(Output(ref codeGen.Disassembler), Is.Null);
            codeGen.Disassembler.disDone();
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hasName")]
    private static extern ref bool HasName(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_labels")]
    private static extern ref byte[]? Labels(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_addressToMethodHandleMap")]
    private static extern ref Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>>? MethodMap(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_helperAddressToMethodHandleMap")]
    private static extern ref Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>>? HelperMap(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_relocationMap")]
    private static extern ref Dictionary<nuint, nuint>? Relocations(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_diffable")]
    private static extern ref bool Diffable(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_disAsmFile")]
    private static extern ref StreamWriter? Output(ref Disassembler disassembler);

#if USE_COREDISTOOLS
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_corDisasm")]
    private static extern ref nuint Decoder(ref Disassembler disassembler);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dspAddr")]
    private static extern nuint DisplayAddress(ref Disassembler disassembler, nuint address);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dspAddr")]
    private static extern void* DisplayPointer(ref Disassembler disassembler, void* address);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "disCchRegMember")]
    private static extern nuint RegisterCallback(ref Disassembler disassembler, void* decoder,
        int register, char* output, nuint capacity);

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
