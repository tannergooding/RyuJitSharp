// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Disassembler;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LateDisassemblyBackendTests
{
#if DEBUG
    [Test]
    public static void DebugLateDisassemblyOutputUsesUtf8PathAndAppendMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ryujitsharp-{Guid.NewGuid():N}-é.txt");
        var fileName = Encoding.UTF8.GetBytes(path + '\0');

        try
        {
            fixed (byte* fileNamePointer = fileName)
            {
                using (var output = OpenLateDisassemblyFile(fileNamePointer)
                    ?? throw new AssertionException("The late-disassembly output file could not be opened."))
                {
                    output.Write("first");
                }

                using (var output = OpenLateDisassemblyFile(fileNamePointer)
                    ?? throw new AssertionException("The late-disassembly output file could not be reopened."))
                {
                    output.Write("second");
                }
            }

            Assert.That(File.ReadAllText(path, new UTF8Encoding(false)), Is.EqualTo("firstsecond"));
        }
        finally
        {
            File.Delete(path);
        }
    }
#endif

#if USE_COREDISTOOLS
    private static readonly List<(nuint Address, nuint Bytes, nuint Size)> s_calls = [];
    private static bool s_failDecode;
    private static nuint s_finished;

    [TestCase(false)]
    [TestCase(true)]
    public static void CoreDisToolsVisitsWritableHotThenColdAtLinearExecutionAddresses(bool printit)
    {
        var hot = stackalloc byte[8];
        var cold = stackalloc byte[4];
        var disassembler = default(Disassembler);
        Hot(ref disassembler) = (nuint)hot;
        HotSize(ref disassembler) = 8;
        Cold(ref disassembler) = (nuint)cold;
        ColdSize(ref disassembler) = 4;
        Decoder(ref disassembler) = 123;
        s_calls.Clear();
        s_failDecode = false;
        var saved = s_PtrDumpInstruction;
        s_PtrDumpInstruction = &Dump;

        try
        {
            using var stream = new MemoryStream();
            using var output = new StreamWriter(stream);
            CoreBuffer(ref disassembler, output, printit);

            Assert.That(s_calls, Is.EqualTo([
                ((nuint)0, (nuint)hot, (nuint)8),
                ((nuint)4, (nuint)(hot + 4), (nuint)4),
                ((nuint)8, (nuint)cold, (nuint)4),
            ]));
        }
        finally
        {
            s_PtrDumpInstruction = saved;
        }
    }

    [Test]
    public static void CoreDisToolsFailurePolicyPreservesTargetRecoveryAndSharedErrorCount()
    {
        var hot = stackalloc byte[196];
        var cold = stackalloc byte[12];
        new Span<byte>(hot, 196).Fill(0xAB);
        new Span<byte>(cold, 12).Fill(0xCD);
        var disassembler = default(Disassembler);
        Hot(ref disassembler) = (nuint)hot;
        HotSize(ref disassembler) = 196;
        Cold(ref disassembler) = (nuint)cold;
        ColdSize(ref disassembler) = 12;
        s_calls.Clear();
        s_failDecode = true;
        var saved = s_PtrDumpInstruction;
        s_PtrDumpInstruction = &Dump;

        try
        {
            using var stream = new MemoryStream();
            using var output = new StreamWriter(stream, leaveOpen: true);
            CoreBuffer(ref disassembler, output, true);
            output.Flush();
            var text = Encoding.UTF8.GetString(stream.ToArray());
#if TARGET_ARM64
            Assert.That(s_calls.Count, Is.EqualTo(50));
            Assert.That(s_calls[^1], Is.EqualTo(((nuint)196, (nuint)cold, (nuint)12)));
            Assert.That(text, Does.StartWith("0: ab ab ab ab\n"));
            Assert.That(text, Does.EndWith("c4: cd cd cd cd\nToo many failures\n"));
#else
            Assert.That(s_calls.Count, Is.EqualTo(2));
            Assert.That(text, Is.Empty);
#endif
        }
        finally
        {
            s_PtrDumpInstruction = saved;
        }
    }

    [Test]
    public static void CoreDisToolsFinishClearsHandleAndIsIdempotent()
    {
        var disassembler = default(Disassembler);
        Decoder(ref disassembler) = 123;
        s_finished = 0;
        var saved = s_PtrFinishDisasm;
        s_PtrFinishDisasm = &Finish;
        try
        {
            disassembler.disDone();
            Assert.That(s_finished, Is.EqualTo((nuint)123));
            Assert.That(Decoder(ref disassembler), Is.EqualTo((nuint)0));
            s_finished = 0;
            disassembler.disDone();
            Assert.That(s_finished, Is.EqualTo((nuint)0));
        }
        finally
        {
            s_PtrFinishDisasm = saved;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nuint Dump(nuint decoder, byte* address, byte* bytes, nuint size)
    {
        s_calls.Add(((nuint)address, (nuint)bytes, size));

        return s_failDecode ? (nuint)0 : 4;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Finish(nuint decoder)
    {
        s_finished = decoder;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DisasmBufferCoredistools")]
    private static extern void CoreBuffer(ref Disassembler disassembler, StreamWriter output, bool printit);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_corDisasm")]
    private static extern ref nuint Decoder(ref Disassembler disassembler);
#endif

#if TARGET_XARCH || TARGET_ARM64
    [TestCase(false)]
    [TestCase(true)]
    public static void MsvcTwoPassWalkAssignsByteLabelsAndOnlyInstallsCallbacksForOutput(bool printit)
    {
        var hot = stackalloc byte[8];
        var disassembler = default(Disassembler);
        Hot(ref disassembler) = (nuint)hot;
        HotSize(ref disassembler) = 8;
        TotalSize(ref disassembler) = 8;
        Labels(ref disassembler) = new byte[8];
        Diffable(ref disassembler) = true;
        var decoder = new FakeMsvc
        {
#if TARGET_XARCH
            Kind = MsTermination.JmpNear,
#else
            Kind = MsTermination.Bra,
#endif
            BranchTarget = 4,
        };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, leaveOpen: true);
        MsBuffer(ref disassembler, decoder, output, printit);
        output.Flush();

        Assert.That(decoder.Addresses, Is.EqualTo(new ulong[] { 0, 4, 0, 4 }));
        List<string> events = [];
#if TARGET_64BIT
        events.Add("address64");
#endif
        events.Add("client");
        if (printit)
        {
            events.AddRange(["address", "fixup", "regrel", "reg"]);
        }
        events.Add("delete");
        Assert.That(decoder.Events, Is.EqualTo(events));
        Assert.That(Labels(ref disassembler), Is.EqualTo(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }));
        Assert.That(decoder.FormatCount, Is.EqualTo(2));
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()).Contains("L_01:\n", StringComparison.Ordinal), Is.EqualTo(printit));
    }

    [Test]
    public static void MsvcByteLabelNumberingWrapsAt256LikeNative()
    {
        var hot = stackalloc byte[1028];
        var disassembler = default(Disassembler);
        Hot(ref disassembler) = (nuint)hot;
        HotSize(ref disassembler) = 1028;
        TotalSize(ref disassembler) = 1028;
        Labels(ref disassembler) = new byte[1028];
        Array.Fill(Labels(ref disassembler) ?? throw new AssertionException("Missing labels"), (byte)1);
        var decoder = new FakeMsvc { Kind = MsTermination.Trap };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream);

        MsBuffer(ref disassembler, decoder, output, false);

        Assert.That(Labels(ref disassembler)?[254], Is.EqualTo(255));
        Assert.That(Labels(ref disassembler)?[255], Is.Zero);
        Assert.That(Labels(ref disassembler)?[256], Is.EqualTo(1));
    }

    [Test]
    public static void MsvcDecodeFailureReportsBytesEvenDuringLabelDiscovery()
    {
        var bytes = stackalloc byte[4] { 0x11, 0x22, 0x33, 0x44 };
        var disassembler = default(Disassembler);
        var decoder = new FakeMsvc { DecodedSize = 0 };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, leaveOpen: true);
        var size = MsInstruction(ref disassembler, decoder, 10, 0, bytes, 4, output, true, false, false, false);
        output.Flush();
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.That(text, Does.StartWith("MSVCDIS can't disassemble instruction @ offset 10 (0x0a)!!!\n"));
#if TARGET_ARM64
        Assert.That(size, Is.EqualTo((nuint)4));
        Assert.That(text, Does.EndWith("44332211h\n"));
#else
        Assert.That(size, Is.EqualTo((nuint)1));
        Assert.That(text, Does.EndWith("11h\n"));
#endif
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MsvcCallLabelsExcludeRelocations(bool relocated)
    {
        var bytes = stackalloc byte[8];
        var disassembler = default(Disassembler);
        Hot(ref disassembler) = (nuint)bytes;
        HotSize(ref disassembler) = 8;
        TotalSize(ref disassembler) = 8;
        Labels(ref disassembler) = new byte[8];
        Relocations(ref disassembler) = relocated ? new() { [(nuint)(bytes + 1)] = 0x1234 } : [];
        var decoder = new FakeMsvc
        {
#if TARGET_XARCH
            Kind = MsTermination.CallNear32,
#else
            Kind = MsTermination.Call,
#endif
            BranchTarget = 4,
        };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream);
        _ = MsInstruction(ref disassembler, decoder, 0, 0, bytes, 8, output, true, false, false, false);

        Assert.That(Labels(ref disassembler)?[4], Is.EqualTo(relocated ? 0 : 1));
    }

    [TestCase(0ul, 0)]
    [TestCase(ulong.MaxValue, 1)]
    public static void MsvcLabelDiscoveryUsesTheBackendNilSentinel(ulong nil, byte expected)
    {
        var bytes = stackalloc byte[4];
        var disassembler = default(Disassembler);
        TotalSize(ref disassembler) = 4;
        Labels(ref disassembler) = new byte[4];
        var decoder = new FakeMsvc
        {
#if TARGET_XARCH
            Kind = MsTermination.JmpNear,
#else
            Kind = MsTermination.Bra,
#endif
            Nil = nil,
            BranchTarget = 0,
        };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream);
        _ = MsInstruction(ref disassembler, decoder, 0, 0, bytes, 4, output, true, false, false, false);

        Assert.That(Labels(ref disassembler)?[0], Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MsvcAddressCallbackFormatsLocalLabelsWithoutDisplacement(bool diffable)
    {
        var disassembler = default(Disassembler);
        TotalSize(ref disassembler) = 8;
        Labels(ref disassembler) = [0, 0, 0, 0, 7, 0, 0, 0];
        Target(ref disassembler) = 4;
        Diffable(ref disassembler) = diffable;
        var decoder = new FakeMsvc
        {
#if TARGET_XARCH
            Kind = MsTermination.JmpShort,
#else
            Kind = MsTermination.Bra,
#endif
        };
        ulong displacement = 123;
        var text = stackalloc char[64];
        var result = AddressCallback(ref disassembler, decoder, 4, text, 64, &displacement);

        Assert.That(result, Is.EqualTo((nuint)1));
        Assert.That(displacement, Is.Zero);
#if TARGET_XARCH
        Assert.That(new string(text), Is.EqualTo("short L_07"));
#else
        Assert.That(new string(text), Is.EqualTo("L_07"));
#endif
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MsvcUnannotatedAddressPreservesDisplacementAndDiffableReturnValue(bool diffable)
    {
        var disassembler = default(Disassembler);
        Target(ref disassembler) = 9;
        TotalSize(ref disassembler) = 8;
        Diffable(ref disassembler) = diffable;
        var decoder = new FakeMsvc
        {
#if TARGET_XARCH
            Kind = MsTermination.JmpNear,
#else
            Kind = MsTermination.Bra,
#endif
        };
        ulong displacement = 123;
        var text = stackalloc char[64];
        text[0] = '?';
        text[1] = '\0';
        var result = AddressCallback(ref disassembler, decoder, 9, text, 64, &displacement);
        Assert.That(result, Is.EqualTo((nuint)0));
        Assert.That(displacement, Is.EqualTo(123));
        Assert.That(new string(text), Is.EqualTo(diffable
            ? ((nuint)0xD1FFAB1E).ToString($"X{sizeof(nuint) * 2}", CultureInfo.InvariantCulture)
            : "?"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MsvcFixupFormatsRelocatedImmediateAndClearsOnlySuccessfulDisplacement(bool diffable)
    {
        var bytes = stackalloc byte[8];
        var disassembler = default(Disassembler);
        Hot(ref disassembler) = (nuint)bytes;
        HotSize(ref disassembler) = 8;
        Diffable(ref disassembler) = diffable;
        Relocations(ref disassembler) = new() { [(nuint)(bytes + 1)] = 0xABCD };
        var decoder = new FakeMsvc { Kind = MsTermination.FallThrough };
        ulong displacement = 123;
        var text = stackalloc char[64];
        var result = FixupCallback(ref disassembler, decoder, 1, 4, text, 64, &displacement);

        Assert.That(result, Is.EqualTo((nuint)1));
        Assert.That(displacement, Is.Zero);
        Assert.That(new string(text), Is.EqualTo(diffable ? "D1FFAB1Eh" : "ABCDh"));
        displacement = 123;
        result = FixupCallback(ref disassembler, decoder, 2, 4, text, 64, &displacement);
        Assert.That(result, Is.EqualTo((nuint)0));
        Assert.That(displacement, Is.EqualTo(123));
    }

    [TestCase(127u)]
    [TestCase(128u)]
    [TestCase(0xFFFFFF80u)]
    [TestCase(0xFFFFFF7Fu)]
    public static void MsvcIndirectRegisterRelativeOperandDeclinesWithoutMutatingDisplacement(uint disp)
    {
        var disassembler = default(Disassembler);
        var decoder = new FakeMsvc { Kind = MsTermination.CallInd };
        uint displacement = 123;
        var text = '?';
        var result = RegisterRelativeCallback(ref disassembler, decoder, 0, disp, &text, 1, &displacement);

        Assert.That(result, Is.EqualTo((nuint)0));
        Assert.That(displacement, Is.EqualTo(123));
        Assert.That(text, Is.EqualTo('?'));
    }

    [TestCase(127u, false)]
    [TestCase(128u, true)]
    [TestCase(0xFFFFFF80u, false)]
    [TestCase(0xFFFFFF7Fu, true)]
    public static void MsvcDeferredNameIsReadOnlyForNonByteDisplacements(uint disp, bool annotated)
    {
        var disassembler = default(Disassembler);
        HasName(ref disassembler) = true;
        var decoder = new FakeMsvc { Kind = MsTermination.CallInd, DeferredName = "callee" };
        uint displacement = 123;
        var text = stackalloc char[64];
        text[0] = '?';
        text[1] = '\0';
        var result = RegisterRelativeCallback(ref disassembler, decoder, 0, disp, text, 64, &displacement);

        Assert.That(result, Is.EqualTo(annotated ? (nuint)1 : 0));
        Assert.That(displacement, Is.EqualTo(annotated ? 0 : 123));
        Assert.That(HasName(ref disassembler), Is.EqualTo(!annotated));
        Assert.That(new string(text), Is.EqualTo(annotated ? $"reg+{disp} 'callee'" : "?"));
    }

    [Test]
    public static void MsvcByteDisplayTruncationPreservesNativeNullInclusiveWidth()
    {
        var bytes = stackalloc byte[4];
        var disassembler = default(Disassembler);
        Labels(ref disassembler) = new byte[4];
        var decoder = new FakeMsvc { ByteText = new string('A', 40) };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, leaveOpen: true);
        _ = MsInstruction(ref disassembler, decoder, 0xA, 0, bytes, 4, output, false, true, true, true);
        output.Flush();
#if TARGET_ARM64
        const int indent = 8;
#elif TARGET_AMD64
        const int indent = 30;
#else
        const int indent = 24;
#endif
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo($"00A  {new string('A', indent - 4)}...  nop\n"));
    }

    [TestCase("AA BB CC DD EE FF GG HH")]
    [TestCase("AA BB CC DD EE FF GGGG HH II")]
    public static void MsvcByteWrappingPreservesNativeWholeRemainderLastSpaceSearch(string byteText)
    {
        var bytes = stackalloc byte[4];
        var decoder = new FakeMsvc { ByteText = byteText, BytesMax = 18 };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, leaveOpen: true);
        var count = MsWithBytes(default, decoder, 0x123, bytes, 4, output);
        output.Flush();
        var split = byteText[18] == ' ' ? 18 : byteText.LastIndexOf(' ', StringComparison.Ordinal);
        var expected = $"  123: {byteText[..split],-18} nop\n       {byteText[(split + 1)..]}\n";

        Assert.That(count, Is.EqualTo((nuint)4));
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
    }

#if TARGET_ARM64
    [TestCase(true)]
    [TestCase(false)]
    public static void MsvcArm64FallthroughUsesDecodedAddressOrExistingTarget(bool isAddress)
    {
        var bytes = stackalloc byte[8];
        var disassembler = default(Disassembler);
        TotalSize(ref disassembler) = 8;
        Labels(ref disassembler) = new byte[8];
        var decoder = new FakeMsvc
        {
            Kind = MsTermination.FallThrough,
            BranchTarget = 4,
            DecodedInstruction = new MsInstruction(isAddress, 2, true, true, 6),
        };
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream);
        _ = MsInstruction(ref disassembler, decoder, 0, 0, bytes, 8, output, true, false, false, false);

        Assert.That(Labels(ref disassembler)?[isAddress ? 6 : 4], Is.EqualTo(1));
    }
#endif

    private sealed class FakeMsvc : MsDisassembler
    {
        internal MsTermination Kind;
        internal ulong BranchTarget;
        internal nuint DecodedSize = 4;
        internal readonly List<ulong> Addresses = [];
        internal readonly List<string> Events = [];
        internal int FormatCount;
        internal string ByteText = "90";
        internal nuint BytesMax = 16;
        internal MsInstruction? DecodedInstruction { get; set; }
        internal ulong Nil = ulong.MaxValue;
        internal string? DeferredName;
        private ulong _address;

        internal override MsTermination Termination => Kind;
        internal override int NativeTermination => (int)Kind;
        internal override ulong Address => _address;
        internal override nuint InstructionSize => DecodedSize;
        internal override ulong Target => BranchTarget;
        internal override ulong NilAddress => Nil;
        internal override ulong AddressAddress(uint operand) => 1;
        internal override bool Decode(out MsInstruction instruction)
        {
            instruction = DecodedInstruction.GetValueOrDefault();

            return DecodedInstruction.HasValue;
        }

        internal override nuint Disassemble(ulong address, byte* bytes, nuint size)
        {
            _address = address;
            Addresses.Add(address);

            return DecodedSize;
        }

        internal override string FormatInstruction(nuint capacity)
        {
            FormatCount++;

            return "nop";
        }

        internal override string FormatBytes(nuint capacity, out nuint written)
        {
            written = (nuint)ByteText.Length;

            return ByteText;
        }
        internal override string FormatAddress(ulong address, nuint capacity) => address.ToString("X", CultureInfo.InvariantCulture);
        internal override nuint FormatBytesMax() => BytesMax;
        internal override void SetAddress64(bool enabled)
        {
            Assert.That(enabled, Is.True);
            Events.Add("address64");
        }
        internal override void SetClient(ref Disassembler client) => Events.Add("client");
        internal override void SetAddressCallback(ref Disassembler client) => Events.Add("address");
        internal override void SetFixupCallback(ref Disassembler client) => Events.Add("fixup");
        internal override void SetRegisterRelativeCallback(ref Disassembler client) => Events.Add("regrel");
        internal override void SetRegisterCallback(ref Disassembler client) => Events.Add("reg");
        internal override void Delete() => Events.Add("delete");
        internal override string RegisterName(uint register) => "reg";
        internal override string ReadDeferredFunctionName(ref Disassembler client) =>
            DeferredName ?? throw new NotSupportedException();
        internal override int WriteAddressPrefix(StreamWriter output, string formattedAddress)
        {
            var prefix = $"  {formattedAddress}: ";
            output.Write(prefix);

            return Encoding.UTF8.GetByteCount(prefix);
        }
        internal override void WriteCallbackText(char* output, nuint capacity, string text)
        {
            Assert.That((nuint)text.Length, Is.LessThan(capacity));
            text.AsSpan().CopyTo(new Span<char>(output, text.Length));
            output[text.Length] = '\0';
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DisasmBufferMsvc")]
    private static extern void MsBuffer(ref Disassembler disassembler, MsDisassembler decoder, StreamWriter output, bool printit);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "CbDisassemble")]
    private static extern nuint MsInstruction(ref Disassembler disassembler, MsDisassembler decoder, nuint offset,
        ulong address, byte* bytes, nuint size, StreamWriter output, bool findLabels, bool printit, bool offsets, bool codeBytes);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "CbDisassembleWithBytes")]
    private static extern nuint MsWithBytes(Disassembler disassembler, MsDisassembler decoder,
        ulong address, byte* bytes, nuint size, StreamWriter output);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "disCchAddrMember")]
    private static extern nuint AddressCallback(ref Disassembler disassembler, MsDisassembler decoder, ulong address,
        char* text, nuint capacity, ulong* displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "disCchFixupMember")]
    private static extern nuint FixupCallback(ref Disassembler disassembler, MsDisassembler decoder, ulong address,
        nuint size, char* text, nuint capacity, ulong* displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "disCchRegRelMember")]
    private static extern nuint RegisterRelativeCallback(ref Disassembler disassembler, MsDisassembler decoder, uint register,
        uint offset, char* text, nuint capacity, uint* displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_labels")]
    private static extern ref byte[]? Labels(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_diffable")]
    private static extern ref bool Diffable(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_relocationMap")]
    private static extern ref Dictionary<nuint, nuint>? Relocations(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_target")]
    private static extern ref nuint Target(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hasName")]
    private static extern ref bool HasName(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_totalCodeSize")]
    private static extern ref nuint TotalSize(ref Disassembler disassembler);
#endif

#if DEBUG
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hotCodeBlock")]
    private static extern ref nuint Hot(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_coldCodeBlock")]
    private static extern ref nuint Cold(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hotCodeSize")]
    private static extern ref nuint HotSize(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_coldCodeSize")]
    private static extern ref nuint ColdSize(ref Disassembler disassembler);
}
#endif
