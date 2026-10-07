// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86 && JIT32_GCENCODER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Jit32GcInfoTests
{
    [TestCase(false, true, false, true, false, false)]
    [TestCase(false, true, false, false, false, true)]
    [TestCase(false, false, false, false, false, false)]
    [TestCase(true, true, false, true, false, true)]
    [TestCase(true, true, true, true, false, false)]
    [TestCase(true, true, true, false, false, true)]
    [TestCase(true, false, true, true, false, false)]
    [TestCase(true, false, true, true, true, true)]
    public static void UntrackedLocalClassificationMatchesJit32Rules(
        bool isParameter, bool onFrame, bool isRegisterArgument, bool tracked, bool usesJmp, bool expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.lvaCount = 1;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_REF,
                lvIsParam = isParameter,
                lvOnFrame = onFrame,
                lvIsRegArg = isRegisterArgument,
                lvTracked = tracked,
            },
        ];
        compiler.compJmpOpUsed = usesJmp;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        JitTls.Compiler = compiler;

        try
        {
            Assert.That(codeGen.GCInfo.gcIsUntrackedLocalOrNonEnregisteredArg(0), Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void HeaderCountsOnlyUntrackedRootsAndNonemptyLifetimes()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.lvaCount = 3;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_REF,
                lvOnFrame = true,
            },
            new LclVarDsc
            {
                Type = TYP_BYREF,
                lvIsParam = true,
                lvTracked = true,
            },
            new LclVarDsc
            {
                Type = TYP_INT,
                lvOnFrame = true,
            },
        ];
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        JitTls.Compiler = compiler;

        try
        {
            codeGen.GCInfo.gcVarPtrList = new GCInfo.varPtrDsc
            {
                vpdBegOfs = 4,
                vpdEndOfs = 4,
                vpdNext = new GCInfo.varPtrDsc
                {
                    vpdBegOfs = 8,
                    vpdEndOfs = 12,
                },
            };

            codeGen.GCInfo.gcCountForHeader(out var untrackedCount, out var varPtrTableSize,
                out var noGCRegionCount);

            Assert.That(untrackedCount, Is.EqualTo(1));
            Assert.That(varPtrTableSize, Is.EqualTo(1));
            Assert.That(noGCRegionCount, Is.EqualTo(0));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void HeaderEncoderUsesExactShortcutWhenAvailable()
    {
        var header = new GCInfo.InfoHdr
        {
            prologSize = 0,
            epilogSize = 1,
            epilogCount = 1,
            revPInvokeOffset = uint.MaxValue,
        };
        var cached = -1;
        var more = -1;

        var encoding = GCInfo.EncodeHeaderFirst(in header, out var state, ref more, ref cached);

        Assert.That(encoding, Is.EqualTo(0));
        Assert.That(more, Is.Zero);
        Assert.That(cached, Is.Zero);
        Assert.That(state.IsHeaderMatch(in header), Is.True);
    }

    [Test]
    public static void HeaderEncoderAdjustsWideValuesAndOptionalFields()
    {
        var header = new GCInfo.InfoHdr
        {
            prologSize = 51,
            epilogSize = 17,
            epilogCount = 2,
            epilogAtEnd = 1,
            ediSaved = 1,
            esiSaved = 1,
            ebxSaved = 1,
            ebpSaved = 1,
            ebpFrame = 1,
            interruptible = 1,
            doubleAlign = 1,
            security = 1,
            handlers = 1,
            localloc = 1,
            editNcontinue = 1,
            varargs = 1,
            profCallbacks = 1,
            genericsContext = 1,
            genericsContextIsMethodDesc = 1,
            returnKind = 3,
            isAsync = 1,
            argCount = 0x123,
            frameSize = 0x12345,
            untrackedCnt = 5,
            varPtrTableSize = 2,
            gsCookieOffset = 8,
            syncStartOffset = 4,
            syncEndOffset = 8,
            revPInvokeOffset = 12,
            noGCRegionCnt = 8,
        };
        var cached = -1;
        var more = -1;
        var encoding = GCInfo.EncodeHeaderFirst(in header, out var state, ref more, ref cached);
        var usedSecondCodeSet = false;
        var steps = 0;

        while ((encoding & 0x80) != 0)
        {
            Assert.That(steps++, Is.LessThan(64));
            encoding = GCInfo.EncodeHeaderNext(in header, ref state, out var codeSet);
            usedSecondCodeSet |= codeSet == 2;
        }

        Assert.That(state.IsHeaderMatch(in header), Is.True);
        Assert.That(usedSecondCodeSet, Is.True);
    }

    [Test]
    public static void HeaderSaveMeasurementMatchesSerializedSize()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        var returnTypeDesc = new ReturnTypeDesc();
        returnTypeDesc.InitializeReturnType(compiler, TYP_REF, null, default);
        compiler.compRetTypeDesc = returnTypeDesc;
        compiler.lvaCount = 0;
        compiler.lvaTable = [];
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerUsed = false;
        JitTls.Compiler = compiler;
        codeGen.RegSet.rsClearRegsModified();

        try
        {
#pragma warning disable IDE0007
            byte* destination = stackalloc byte[64];
#pragma warning restore IDE0007
            var header = default(GCInfo.InfoHdr);
            var cached = -1;
            var measuredSize = codeGen.GCInfo.gcInfoBlockHdrSave(destination, 0, 0x123, 0, 0,
                ref header, ref cached);
            var serializedSize = codeGen.GCInfo.gcInfoBlockHdrSave(destination, -1, 0x123, 0, 0,
                ref header, ref cached);

            Assert.That(serializedSize, Is.EqualTo(measuredSize));
            Assert.That(header.returnKind, Is.EqualTo(1));
#if VERIFY_GC_TABLES
            Assert.That(*(ushort*)destination, Is.EqualTo(0xFEEF));
            Assert.That(destination[2], Is.EqualTo(0x82));
            Assert.That(destination[3], Is.EqualTo(0x23));
#else
            Assert.That(destination[0], Is.EqualTo(0x82));
            Assert.That(destination[1], Is.EqualTo(0x23));
#endif
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void EmptyPointerTableMeasurementMatchesSerialization()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.lvaCount = 0;
        compiler.lvaTable = [];
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerUsed = true;
        JitTls.Compiler = compiler;

        try
        {
            var header = default(GCInfo.InfoHdr);
            nuint argTabOffset = 0;
            var measuredSize = codeGen.GCInfo.gcPtrTableSize(header, 0, ref argTabOffset);
#pragma warning disable IDE0007
            byte* destination = stackalloc byte[32];
#pragma warning restore IDE0007
            var end = codeGen.GCInfo.gcPtrTableSave(destination, header, 0, ref argTabOffset);

            Assert.That((nuint)(end - destination), Is.EqualTo(measuredSize));
#if VERIFY_GC_TABLES
            Assert.That(argTabOffset, Is.EqualTo((nuint)4));
            Assert.That(*(ushort*)destination, Is.EqualTo(0xBEEF));
            Assert.That(*(ushort*)(destination + 2), Is.EqualTo(0xCAFE));
            Assert.That(*(ushort*)(destination + 4), Is.EqualTo(0xBABE));
            Assert.That(destination[6], Is.EqualTo(0xFF));
            Assert.That(*(ushort*)(destination + 7), Is.EqualTo(0xBEEB));
#else
            Assert.That(argTabOffset, Is.EqualTo((nuint)0));
            Assert.That(destination[0], Is.EqualTo(0xFF));
#endif
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void PointerTableEncodesTrackedLifetimeAndArgumentOffset()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.lvaCount = 0;
        compiler.lvaTable = [];
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerUsed = true;
        JitTls.Compiler = compiler;

        try
        {
            codeGen.GCInfo.gcVarPtrList = new GCInfo.varPtrDsc
            {
                vpdVarNum = 4,
                vpdBegOfs = 3,
                vpdEndOfs = 7,
            };
            var header = new GCInfo.InfoHdr { varPtrTableSize = 1 };
            nuint argTabOffset = 0;
            var measuredSize = codeGen.GCInfo.gcPtrTableSize(header, 0, ref argTabOffset);
#if VERIFY_GC_TABLES
            Assert.That(argTabOffset, Is.EqualTo((nuint)7));
#else
            Assert.That(argTabOffset, Is.EqualTo((nuint)3));
#endif
#pragma warning disable IDE0007
            byte* destination = stackalloc byte[32];
#pragma warning restore IDE0007
            var end = codeGen.GCInfo.gcPtrTableSave(destination, header, 0, ref argTabOffset);

            Assert.That((nuint)(end - destination), Is.EqualTo(measuredSize));
#if VERIFY_GC_TABLES
            Assert.That(argTabOffset, Is.EqualTo((nuint)8));
#else
            Assert.That(argTabOffset, Is.EqualTo((nuint)4));
#endif
#if VERIFY_GC_TABLES
            Assert.That(destination[0], Is.EqualTo(7));
            Assert.That(*(ushort*)(destination + 1), Is.EqualTo(0xBEEF));
            Assert.That(*(ushort*)(destination + 3), Is.EqualTo(0xCAFE));
            Assert.That(destination[5], Is.EqualTo(4));
            Assert.That(destination[6], Is.EqualTo(3));
            Assert.That(destination[7], Is.EqualTo(4));
            Assert.That(*(ushort*)(destination + 8), Is.EqualTo(0xBABE));
            Assert.That(destination[10], Is.EqualTo(0xFF));
            Assert.That(*(ushort*)(destination + 11), Is.EqualTo(0xBEEB));
#else
            Assert.That(destination[0], Is.EqualTo(3));
            Assert.That(destination[1], Is.EqualTo(4));
            Assert.That(destination[2], Is.EqualTo(3));
            Assert.That(destination[3], Is.EqualTo(4));
            Assert.That(destination[4], Is.EqualTo(0xFF));
#endif
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

#if DUMP_GC_TABLES
#if DEBUG
    private const bool VerifyDumpMarkers = true;
#else
    private const bool VerifyDumpMarkers = false;
#endif

    [TestCase(0u)]
    [TestCase(0x7Fu)]
    [TestCase(0x80u)]
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public static void DumpUnsignedDecoderPreservesThirtyTwoBitValues(uint expected)
    {
#pragma warning disable IDE0007 // Unsafe stackalloc requires an explicit pointer local.
        byte* buffer = stackalloc byte[8];
#pragma warning restore IDE0007
        var written = GCInfo.encodeUnsigned(buffer, expected);
        var read = GCInfo.Jit32GCDump.decodeUnsigned(buffer, out var actual);

        Assert.That(read, Is.EqualTo((nuint)written));
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(0)]
    [TestCase(63)]
    [TestCase(-63)]
    [TestCase(64)]
    [TestCase(-64)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public static void DumpSignedDecoderPreservesSignAndOverflow(int expected)
    {
#pragma warning disable IDE0007 // Unsafe stackalloc requires an explicit pointer local.
        byte* buffer = stackalloc byte[8];
#pragma warning restore IDE0007
        var written = GCInfo.encodeSigned(buffer, expected);
        var read = GCInfo.Jit32GCDump.decodeSigned(buffer, out var actual);

        Assert.That(read, Is.EqualTo((nuint)written));
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public static void DumpDeltaDecoderWrapsAtUnsignedBoundary()
    {
#pragma warning disable IDE0007 // Unsafe stackalloc requires an explicit pointer local.
        byte* buffer = stackalloc byte[1];
#pragma warning restore IDE0007
        *buffer = 2;
        var size = GCInfo.Jit32GCDump.decodeUDelta(buffer, out var value, uint.MaxValue);

        Assert.That(size, Is.EqualTo((nuint)1));
        Assert.That(value, Is.EqualTo(1u));
    }

    [TestCase(false)]
#if DEBUG
    [TestCase(true)]
#endif
    public static void DumpHeaderDecodesWideFieldsOptionalPayloadAndEpilogDeltas(bool verify)
    {
        var header = new GCInfo.InfoHdr
        {
            prologSize = 51,
            epilogSize = 17,
            epilogCount = 2,
            ediSaved = 1,
            esiSaved = 1,
            ebxSaved = 1,
            ebpSaved = 1,
            ebpFrame = 1,
            interruptible = 1,
            doubleAlign = 1,
            security = 1,
            handlers = 1,
            localloc = 1,
            editNcontinue = 1,
            varargs = 1,
            profCallbacks = 1,
            genericsContext = 1,
            genericsContextIsMethodDesc = 1,
            returnKind = 3,
            isAsync = 1,
            argCount = 0x123,
            frameSize = 0x12345,
            untrackedCnt = 5,
            varPtrTableSize = 2,
            gsCookieOffset = 8,
            syncStartOffset = 4,
            syncEndOffset = 8,
            revPInvokeOffset = 12,
            noGCRegionCnt = 8,
        };
        var buffer = new byte[256];
        fixed (byte* table = buffer)
        {
            var end = WriteDumpHeader(table, header, 0x200, [0x100, 0x180], verify);
            var output = new StringBuilder();
            var dump = new GCInfo.Jit32GCDump(5, encBytes: false) { gcPrintf = text => output.Append(text) };
            var size = dump.DumpInfoHdr(table, out var actual, out var methodSize, verify);

            Assert.That(size, Is.EqualTo((nuint)(end - table)));
            Assert.That(methodSize, Is.EqualTo(0x200u));
            Assert.That(actual.IsHeaderMatch(in header), Is.True);
            Assert.That(actual.returnKind, Is.EqualTo(3));
            Assert.That(actual.isAsync, Is.EqualTo(1));
            var lines = output.ToString();
            Assert.That(lines, Does.StartWith("    method      size   = 0200\n"));
            Assert.That(lines, Does.Contain("    callee-saved regs  = EDI ESI EBX EBP \n"));
            Assert.That(lines, Does.Contain("    arguments size     = 291 DWORDs\n"));
            Assert.That(lines, Does.Contain("    stack frame size   = 74565 DWORDs\n"));
            Assert.That(lines, Does.Contain(
                "    security check obj = yes\n" +
                "    exception handlers = yes\n" +
                "    localloc           = yes\n" +
                "    edit & continue    = yes\n" +
                "    profiler callbacks = yes\n" +
                "    varargs            = yes\n" +
                "    GuardStack cookie  = [EBP-8]\n" +
                "    Sync region = [4,8] ([0x4,0x8])\n" +
                "    no GC region count =  8 \n" +
                "    epilog # 0    at   0100\n" +
                "    epilog # 1    at   0180\n" +
                "    argTabOffset = 2a  \n"));
        }
    }

    private static IEnumerable<TestCaseData> PointerDumpCases()
    {
        yield return new(true, false, new byte[] { 0xBC, 0xBF, 0x41, 0x01, 0xFF },
            "0001        reg EAX becoming live 'this' (iptr)\n0002        reg EAX becoming dead\n\n");
        yield return new(true, false, new byte[] { 0xB0, 0x88, 0xC8, 0xC0, 0xFF },
            "0000        push non-ptr (1)\n0000        push ptr  1  (2)\n0000        pop  1 args (1)\n\n");
        yield return new(true, true, new byte[] { 0x80, 0xC8, 0xFF },
            "0000        push ptr  0\n0000        pop  1 ptrs\n\n");
        yield return new(true, false, new byte[] { 0xF0, 0xB8, 0x81, 0x00, 0xF9, 0x03, 0xF8, 0x04, 0xFC, 0x05, 0xFD, 0x01, 0xFF },
            "0088        push ptr  4  (5)\n0088        pop  5 args (0)\n0088        kill args  1\n\n");
        yield return new(false, true, new byte[] { 0x11, 0xFF },
            "0001        call [ EDI ] argMask=00\n\n");
        yield return new(false, true, new byte[] { 0x10, 0x20, 0x40, 0xFF },
            "            thisptr in EDI\n            thisptr in ESI\n            thisptr in EBX\n\n");
        yield return new(false, true, new byte[] { 0x81, 0x63, 0xFF },
            "0001        call [ EDI ESI ] argMask=03\n\n");
        yield return new(false, true, new byte[] { 0xFD, 0x01, 0x12, 0x23, 0xFF },
            "0032        call [ EDI ] argMask=101\n\n");
        yield return new(false, true, new byte[] { 0xF9, 0x04, 0x63, 0x21, 0xFF },
            "0004        call [ EDI'ESI ] argMask=03 (iargs=01)\n\n");
        yield return new(false, true, new byte[] { 0xFE, 0x03, 8, 0, 0, 0, 3, 0, 0, 0, 0xFF },
            "0008        call [ EDI ESI ] argMask=03\n\n");
        yield return new(false, true, new byte[] { 0xFA, 0x13, 8, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 0xFF },
            "0008        call [ EDI'ESI ] argMask=03 (iargs=01)\n\n");
        yield return new(false, true, new byte[] { 0xFB, 0x13, 0x20, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 4, 9, 0xFF },
            "0020        call [ EDI'ESI ] ptrArgs=[4 8i]\n\n");
        yield return new(false, false, new byte[] { 1, 0x20, 3, 0x51, 0xFF },
            "0001        push\n0001       push 3\n0002        pop 1\n\n");
        yield return new(false, false, new byte[] { 0x40, 5, 0xD1, 8, 0xFF },
            "000B        call 1 [ EDI ]\n\n");
        yield return new(false, false, new byte[] { 0x80, 0xFF },
            "000A        call 0 [ ESI ]\n\n");
        yield return new(false, false, new byte[] { 0x41, 0xE1, 2, 3, 0xFF },
            "0001        call 2 [ EDI ] argMask=03\n\n");
        yield return new(false, false, new byte[] { 0x40, 2, 0xF0, 0x21, 0xE1, 1, 1, 0xFF },
            "            iptrMask = 21\n0002        call 1 [ EDI'] argMask=01 (iargs=02)\n\n");
        yield return new(false, false, new byte[] { 0xF4, 0xF5, 0xF6, 0xF7, 0xFF },
            "            thisptr in EDI\n            thisptr in ESI\n            thisptr in EBX\n            thisptr in EBP\n\n");
        yield return new(false, false, new byte[] { 0xF8, 0x91, 4, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 8, 0x0D, 0xFF },
            "0004        call 2 [ EDI'] argOffs(2) =    8    D\n\n");
    }

    [TestCaseSource(nameof(PointerDumpCases))]
    public static void PointerDumpMatchesEveryNativeEncodingFamily(
        bool interruptible, bool ebpFrame, byte[] encoding, string expected)
    {
        var header = new GCInfo.InfoHdr
        {
            interruptible = interruptible ? (byte)1 : (byte)0,
            ebpFrame = ebpFrame ? (byte)1 : (byte)0,
        };
#if DEBUG
        bool[] verificationModes = [false, true];
#else
        bool[] verificationModes = [false];
#endif
        foreach (var verify in verificationModes)
        {
            var buffer = new byte[encoding.Length + 8];
            fixed (byte* table = buffer)
            {
                var end = table;
                if (verify)
                {
                    *(ushort*)end = 0xBEEF;
                    end += sizeof(ushort);
                    *(ushort*)end = 0xCAFE;
                    end += sizeof(ushort);
                    *(ushort*)end = 0xBABE;
                    end += sizeof(ushort);
                }
                encoding.CopyTo(new Span<byte>(end, encoding.Length));
                end += encoding.Length;
                if (verify)
                {
                    *(ushort*)end = 0xBEEB;
                    end += sizeof(ushort);
                }

                var output = new StringBuilder();
                var dump = new GCInfo.Jit32GCDump(5, encBytes: false) { gcPrintf = text => output.Append(text) };
                var size = dump.DumpGCTable(table, in header, 0x100, verify);

                Assert.That(size, Is.EqualTo((nuint)(end - table)));
                Assert.That(output.ToString(), Is.EqualTo(expected));
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PointerDumpPreservesNoGcRegionsStackDeltasFlagsAndLifetimes(bool doubleAlign)
    {
        var header = new GCInfo.InfoHdr
        {
            noGCRegionCnt = 1,
            untrackedCnt = 2,
            varPtrTableSize = 2,
            frameSize = 2,
            ediSaved = 1,
            doubleAlign = doubleAlign ? (byte)1 : (byte)0,
        };
        var buffer = new byte[64];
        fixed (byte* table = buffer)
        {
            var end = table;
            end += GCInfo.encodeUnsigned(end, 3);
            end += GCInfo.encodeUnsigned(end, 4);
            end += GCInfo.encodeSigned(end, doubleAlign ? -23 : 13);
            end += GCInfo.encodeSigned(end, doubleAlign ? 15 : -33);
            end += GCInfo.encodeUnsigned(end, 7);
            end += GCInfo.encodeUDelta(end, 2, 0);
            end += GCInfo.encodeUDelta(end, 6, 2);
            end += GCInfo.encodeUnsigned(end, 8);
            end += GCInfo.encodeUDelta(end, 4, 2);
            end += GCInfo.encodeUDelta(end, 9, 4);
            *end++ = 0xFF;
            var output = new StringBuilder();
            var dump = new GCInfo.Jit32GCDump(5, encBytes: false) { gcPrintf = text => output.Append(text) };
            var size = dump.DumpGCTable(table, in header, 0x20);

            Assert.That(size, Is.EqualTo((nuint)(end - table)));
            Assert.That(output.ToString(), Is.EqualTo(
                "[0003-0007) no GC region\n" +
                (doubleAlign
                    ? "            [EBP+08H] an untracked pinned byref local\n"
                    : "            [ESP-10H] an untracked pinned byref local\n") +
                (doubleAlign
                    ? "            [ESP+08H] an untracked  local\n"
                    : "            [ESP+14H] an untracked  local\n") +
                "0002..0006  [ESP+04H] a byref pinned pointer\n" +
                "0004..0009  [ESP+08H] a  pointer\n\n"));
        }
    }

    [Test]
    public static void EncodingDumpPreservesNativeUnsignedPaddingAndOffsetGuard()
    {
        byte[] buffer = [0xFF, 0xAA, 0xBB, 0xCC, 0xDD];
        fixed (byte* table = buffer)
        {
            var output = new StringBuilder();
            var dump = new GCInfo.Jit32GCDump(5) { gcPrintf = text => output.Append(text) };
            var size = dump.DumpGCTable(table, default, 0);

            Assert.That(size, Is.EqualTo((nuint)1));
            // The pinned DumpEncoding decrements size_t through zero.
            Assert.That(output.ToString(), Is.EqualTo("FF    BB CC ...| \n"));

            output.Length = 0;
            byte[] call = [0x11, 0xFF];
            fixed (byte* callTable = call)
            {
                var header = new GCInfo.InfoHdr { ebpFrame = 1 };
                dump = new GCInfo.Jit32GCDump(5, encBytes: false, dumpCodeOffs: false)
                {
                    gcPrintf = text => output.Append(text),
                };
                _ = dump.DumpGCTable(callTable, in header, 1);
                Assert.That(output.ToString(), Is.EqualTo("        call [ EDI ] argMask=00\n\n"));
            }
        }
    }

    [Test]
    public static void PointerDumpPreservesPrintfSignedWidthAndMinusSign()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        byte[] encoding = [0xE1, 0x88, 0x80, 0x80, 0x80, 0, 0, 0xFF];
        try
        {
            CultureInfo.CurrentCulture = culture;
            fixed (byte* table = encoding)
            {
                var output = new StringBuilder();
                var dump = new GCInfo.Jit32GCDump(5, encBytes: false) { gcPrintf = text => output.Append(text) };
                _ = dump.DumpGCTable(table, default, 0);

                Assert.That(output.ToString(), Is.EqualTo("0000        call -2147483648 [ EDI ]\n\n"));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestCase(1u, "prolog")]
    [TestCase(4u, "body")]
    [TestCase(0x1Eu, "epilog (00 bytes into it)")]
    [TestCase(0x1Fu, "epilog (01 bytes into it)")]
    [TestCase(0x20u, "body")]
    public static void FrameDumpClassifiesOffsetsWithoutInventingRootEnumeration(uint offset, string location)
    {
        var header = new GCInfo.InfoHdr
        {
            prologSize = 4,
            epilogSize = 2,
            epilogCount = 1,
            epilogAtEnd = 1,
            revPInvokeOffset = uint.MaxValue,
        };
        var buffer = new byte[64];
        fixed (byte* table = buffer)
        {
            _ = WriteDumpHeader(table, header, 0x20, [], verify: VerifyDumpMarkers);
            var output = new StringBuilder();
            var dump = new GCInfo.Jit32GCDump(5) { gcPrintf = text => output.Append(text) };
            dump.DumpPtrsInFrame(table, null, offset, verifyGCTables: VerifyDumpMarkers);

            Assert.That(output.ToString(), Is.EqualTo($"    Offset {offset:X4} is within the method's {location}\n"));
        }
    }

    [TestCase(10u, "epilog (00 bytes into it)")]
    [TestCase(14u, "body")]
    public static void FrameDumpPreservesPinnedNonaccumulatingEpilogDeltas(uint offset, string location)
    {
        var header = new GCInfo.InfoHdr
        {
            epilogSize = 2,
            epilogCount = 2,
            revPInvokeOffset = uint.MaxValue,
        };
        var buffer = new byte[64];
        fixed (byte* table = buffer)
        {
            _ = WriteDumpHeader(table, header, 0x20, [4, 14], verify: VerifyDumpMarkers);
            var output = new StringBuilder();
            var dump = new GCInfo.Jit32GCDump(5) { gcPrintf = text => output.Append(text) };
            dump.DumpPtrsInFrame(table, null, offset, verifyGCTables: VerifyDumpMarkers);

            Assert.That(output.ToString(), Is.EqualTo($"    Offset {offset:X4} is within the method's {location}\n"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public static void DumpEntryPointsPreserveStdoutBannersAndDebugLoggerRouting(bool noGcRegion)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var header = new GCInfo.InfoHdr
        {
            noGCRegionCnt = noGcRegion ? 1u : 0u,
            revPInvokeOffset = uint.MaxValue,
        };
        var buffer = new byte[256];
        fixed (byte* table = buffer)
        {
            var verify = false;
#if VERIFY_GC_TABLES
            verify = true;
#endif
            var pointerTable = WriteDumpHeader(table, header, 0x20, [], verify);
            var end = pointerTable;
            if (verify)
            {
                *(ushort*)end = 0xBEEF;
                end += sizeof(ushort);
            }
            if (noGcRegion)
            {
                end += GCInfo.encodeUnsigned(end, 1);
                end += GCInfo.encodeUnsigned(end, 2);
            }
            if (verify)
            {
                *(ushort*)end = 0xCAFE;
                end += sizeof(ushort);
                *(ushort*)end = 0xBABE;
                end += sizeof(ushort);
            }
            *end++ = 0xFF;
            if (verify)
            {
                *(ushort*)end = 0xBEEB;
                end += sizeof(ushort);
            }

            using var console = new StringWriter();
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previousConsole = Console.Out;
            var previousJitStdout = Globals.s_jitstdout;
            try
            {
                Console.SetOut(console);
                Globals.s_jitstdout = writer;
                GCInfo gcInfo = default;
                var decoded = default(GCInfo.InfoHdr);
                var headerSize = gcInfo.gcInfoBlockHdrDump(table, ref decoded, out var methodSize);
                var pointerSize = gcInfo.gcDumpPtrTable(table + headerSize, decoded, methodSize);
                gcInfo.gcFindPtrsInFrame(table, null, 1);
                writer.Flush();

                Assert.That(headerSize, Is.EqualTo((nuint)(pointerTable - table)));
                Assert.That(pointerSize, Is.EqualTo((nuint)(end - pointerTable)));
                var banner = noGcRegion ? "No GC regions and pointer table:\n" : "Pointer table:\n";
#if DEBUG
                Assert.That(console.ToString(), Is.EqualTo("Method info block:\n" + banner));
                var diagnostics = Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
#else
                var diagnostics = console.ToString();
                Assert.That(diagnostics, Does.StartWith("Method info block:\n"));
                Assert.That(diagnostics, Does.Contain(banner));
#endif
                Assert.That(diagnostics, Does.Contain("    method      size   = 0020\n"));
                if (noGcRegion)
                {
                    Assert.That(diagnostics, Does.Contain("[0001-0003) no GC region\n"));
                }
                Assert.That(diagnostics, Does.EndWith("    Offset 0001 is within the method's body\n"));
            }
            finally
            {
                Globals.s_jitstdout = previousJitStdout;
                Console.SetOut(previousConsole);
            }
        }
    }

    private static byte* WriteDumpHeader(byte* table, GCInfo.InfoHdr header, uint methodSize,
        uint[] epilogs, bool verify)
    {
        if (verify)
        {
            *(ushort*)table = 0xFEEF;
            table += sizeof(ushort);
        }
        table += GCInfo.encodeUnsigned(table, methodSize);
        var more = 0;
        var cached = -1;
        var encoding = GCInfo.EncodeHeaderFirst(in header, out var state, ref more, ref cached);
        *table++ = encoding;
        while ((encoding & 0x80) != 0)
        {
            encoding = GCInfo.EncodeHeaderNext(in header, ref state, out var codeSet);
            if (codeSet == 2)
            {
                *table++ = 0xCF;
            }
            *table++ = encoding;
        }
        if (header.untrackedCnt > 3)
        {
            table += GCInfo.encodeUnsigned(table, header.untrackedCnt);
        }
        if (header.varPtrTableSize != 0)
        {
            table += GCInfo.encodeUnsigned(table, header.varPtrTableSize);
        }
        if (header.gsCookieOffset != 0)
        {
            table += GCInfo.encodeUnsigned(table, header.gsCookieOffset);
        }
        if (header.syncStartOffset != 0)
        {
            table += GCInfo.encodeUnsigned(table, header.syncStartOffset);
            table += GCInfo.encodeUnsigned(table, header.syncEndOffset);
        }
        if (header.revPInvokeOffset != uint.MaxValue)
        {
            table += GCInfo.encodeUnsigned(table, header.revPInvokeOffset);
        }
        if (header.noGCRegionCnt > 4)
        {
            table += GCInfo.encodeUnsigned(table, header.noGCRegionCnt);
        }
        if ((header.epilogCount != 0) && ((header.epilogAtEnd == 0) || (header.epilogCount != 1)))
        {
            if (verify)
            {
                *(ushort*)table = 0xFACE;
                table += sizeof(ushort);
            }
            var previous = 0u;
            foreach (var offset in epilogs)
            {
                table += GCInfo.encodeUDelta(table, offset, previous);
                previous = offset;
            }
        }
        if ((header.untrackedCnt > 3) || (header.varPtrTableSize != 0) || (header.noGCRegionCnt > 0))
        {
            table += GCInfo.encodeUnsigned(table, 0x2A);
        }

        return table;
    }
#endif
}
#endif
