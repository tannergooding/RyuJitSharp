// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterDataSectionOutputTests
{
    [Test]
    public static void RawSectionsCopyToWritableBlocksWithoutWritingExecutableAliases()
    {
        WithEmitter((_, emitter) =>
        {
            Assert.That(emitter.emitDataConst([0x12, 0x34, 0x56, 0x78], 4, TYP_INT), Is.Zero);
            Assert.That(emitter.emitDataConst([1, 2, 3, 4, 5, 6, 7, 8], 8, TYP_DOUBLE), Is.EqualTo(8u));
            var chunks = stackalloc AllocMemChunk[2];
            var readonlyFirst = stackalloc byte[4];
            var writableFirst = stackalloc byte[4];
            var readonlySecond = stackalloc byte[8];
            var writableSecond = stackalloc byte[8];
            new Span<byte>(readonlyFirst, 4).Fill(0xCC);
            new Span<byte>(readonlySecond, 8).Fill(0xCC);
            chunks[0] = new AllocMemChunk { size = 4, block = readonlyFirst, blockRW = writableFirst };
            chunks[1] = new AllocMemChunk { size = 8, block = readonlySecond, blockRW = writableSecond };

            emitter.emitOutputDataSec(emitter.emitConsDsc, chunks);

            Assert.That(new ReadOnlySpan<byte>(writableFirst, 4).ToArray(),
                Is.EqualTo(Convert.FromHexString("12345678")));
            Assert.That(new ReadOnlySpan<byte>(writableSecond, 8).ToArray(),
                Is.EqualTo(Convert.FromHexString("0102030405060708")));
            Assert.That(new ReadOnlySpan<byte>(readonlyFirst, 4).ToArray(),
                Is.EqualTo(Convert.FromHexString("CCCCCCCC")));
            Assert.That(new ReadOnlySpan<byte>(readonlySecond, 8).ToArray(),
                Is.EqualTo(Convert.FromHexString("CCCCCCCCCCCCCCCC")));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void AbsoluteLabelTableWritesHotAndColdAddressesAndRelocatesOnlyMatchedVm(
        bool relocatable, bool matchedVm)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = relocatable;
            compiler.info.compMatchedVM = matchedVm;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            emitter.emitCmpHandle = &context.JitInfo;

            var first = new BasicBlock(null, null) { bbEmitCookie = new insGroup { igOffs = 8 } };
            var cold = new BasicBlock(null, null) { bbEmitCookie = new insGroup { igOffs = 72 } };
            compiler.fgFirstBB = first;
            Assert.That(emitter.emitBBTableDataGenBeg(2, false), Is.Zero);
            emitter.emitDataGenData(0, first);
            emitter.emitDataGenData(1, cold);
            emitter.emitDataGenEnd();

            var hotCode = stackalloc byte[64];
            var coldCode = stackalloc byte[32];
            var readonlyBlock = stackalloc byte[16];
            var writableBlock = stackalloc byte[16];
            emitter.emitCodeBlock = hotCode;
            emitter.emitColdCodeBlock = coldCode;
            emitter.emitTotalHotCodeSize = 64;
            emitter.emitTotalColdCodeSize = 32;
            var chunk = new AllocMemChunk { size = 16, block = readonlyBlock, blockRW = writableBlock };

            emitter.emitOutputDataSec(emitter.emitConsDsc, &chunk);

            Assert.That(((nuint*)writableBlock)[0], Is.EqualTo((nuint)(hotCode + 8)));
            Assert.That(((nuint*)writableBlock)[1], Is.EqualTo((nuint)(coldCode + 8)));
            Assert.That(context.Calls, Is.EqualTo(relocatable && matchedVm ? 2 : 0));
            if (relocatable && matchedVm)
            {
                Assert.That((nuint)context.FirstLocation, Is.EqualTo((nuint)writableBlock));
                Assert.That((nuint)context.FirstTarget, Is.EqualTo((nuint)(hotCode + 8)));
                Assert.That((nuint)context.SecondLocation, Is.EqualTo((nuint)(writableBlock + 8)));
                Assert.That((nuint)context.SecondTarget, Is.EqualTo((nuint)(coldCode + 8)));
                Assert.That(context.FirstKind, Is.EqualTo(CorInfoReloc.DIRECT));
                Assert.That(context.SecondKind, Is.EqualTo(CorInfoReloc.DIRECT));
            }
        });
    }

    [Test]
    public static void RelativeLabelTableRetainsUnsignedNegativeDeltaWithoutRelocation()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = true;
            var first = new BasicBlock(null, null) { bbEmitCookie = new insGroup { igOffs = 72 } };
            var earlier = new BasicBlock(null, null) { bbEmitCookie = new insGroup { igOffs = 8 } };
            compiler.fgFirstBB = first;
            Assert.That(emitter.emitBBTableDataGenBeg(2, true), Is.Zero);
            emitter.emitDataGenData(0, first);
            emitter.emitDataGenData(1, earlier);
            emitter.emitDataGenEnd();
            var readonlyBlock = stackalloc byte[8];
            var writableBlock = stackalloc byte[8];
            var chunk = new AllocMemChunk { size = 8, block = readonlyBlock, blockRW = writableBlock };

            emitter.emitOutputDataSec(emitter.emitConsDsc, &chunk);

            Assert.That(((uint*)writableBlock)[0], Is.Zero);
            Assert.That(((uint*)writableBlock)[1], Is.EqualTo(0xFFFFFFC0u));
        });
    }

    [Test]
    public static void AsyncEntriesPreserveNullRemovedLocationsAndRelocateOnlyLiveDiagnosticIp()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = true;
            compiler.info.compMatchedVM = true;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            emitter.emitCmpHandle = &context.JitInfo;
            StubEntryPoint(emitter) = (void*)0x1234;

            var group = new insGroup { igOffs = 12 };
#if DEBUG
            group.igSelf = group;
#endif
            var section = new Emitter.dataSection
            {
                dsType = Emitter.dataSection.sectionType.asyncResumeInfo,
                dsSize = 2 * (uint)sizeof(CORINFO_AsyncResumeInfo),
                Locations = [new emitLocation(group), default],
            };
            var descriptor = new Emitter.dataSecDsc
            {
                dsdList = section,
                dsdLast = section,
                dsdOffs = section.dsSize,
            };
            var code = stackalloc byte[64];
            emitter.emitCodeBlock = code;
            emitter.emitTotalHotCodeSize = 64;
            var readonlyBlock = stackalloc byte[32];
            var writableBlock = stackalloc byte[32];
            var chunk = new AllocMemChunk { size = 32, block = readonlyBlock, blockRW = writableBlock };

            emitter.emitOutputDataSec(descriptor, &chunk);

            var entries = (CORINFO_AsyncResumeInfo*)writableBlock;
            Assert.That(entries[0].Resume, Is.EqualTo((nint)0x1234));
            Assert.That(entries[0].DiagnosticIP, Is.EqualTo((nint)(code + 12)));
            Assert.That(entries[1].Resume, Is.EqualTo((nint)0x1234));
            Assert.That(entries[1].DiagnosticIP, Is.EqualTo((nint)0));
            Assert.That(context.Calls, Is.EqualTo(3));
            Assert.That(context.FirstLocation == &entries[0].Resume, Is.True);
            Assert.That(context.SecondLocation == &entries[0].DiagnosticIP, Is.True);
            Assert.That(context.ThirdLocation == &entries[1].Resume, Is.True);
            Assert.That((nuint)context.SecondTarget, Is.EqualTo((nuint)(code + 12)));
        });
    }

    [Test]
    public static void DataDumpPrintsLabelAndTypedRawValuesBeforeCopy()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.disAsm = true;
            _ = emitter.emitDataConst([0x01, 0x02, 0x03, 0x04], 4, TYP_INT);
            var readonlyBlock = stackalloc byte[4];
            var writableBlock = stackalloc byte[4];
            var chunk = new AllocMemChunk { size = 4, block = readonlyBlock, blockRW = writableBlock };
            var output = CaptureOutput(emitter, &chunk);

            Assert.That(output, Is.EqualTo($"{Environment.NewLine}RWD00  \tdd\t04030201h{Environment.NewLine}"));
            Assert.That(new ReadOnlySpan<byte>(writableBlock, 4).ToArray(),
                Is.EqualTo(Convert.FromHexString("01020304")));
        });
    }

    [TestCase(true, "\nRWD00  \tdd\tG_M000_IG02 - G_M000_IG01\n")]
    [TestCase(false, "\nRWD00  \tdd\t00000010h ; case G_M000_IG02\n")]
    public static void RelativeTableDumpPreservesDiffableAndNumericForms(bool diffable, string expected)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.disAsm = true;
            compiler.opts.disDiffable = diffable;
            var firstGroup = new insGroup { igOffs = 8 };
            var secondGroup = new insGroup { igOffs = 24 };
            firstGroup.InitializeNum(1);
            secondGroup.InitializeNum(2);
            var first = new BasicBlock(null, null) { bbEmitCookie = firstGroup };
            var second = new BasicBlock(null, null) { bbEmitCookie = secondGroup };
            compiler.fgFirstBB = first;
            Assert.That(emitter.emitBBTableDataGenBeg(1, true), Is.Zero);
            emitter.emitDataGenData(0, second);
            emitter.emitDataGenEnd();
            var readonlyBlock = stackalloc byte[4];
            var writableBlock = stackalloc byte[4];
            var chunk = new AllocMemChunk { size = 4, block = readonlyBlock, blockRW = writableBlock };
            var output = CaptureOutput(emitter, &chunk);

            Assert.That(output, Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
            Assert.That(*(uint*)writableBlock, Is.EqualTo(16u));
        });
    }

    [Test]
    public static void FloatingDataDumpRetainsBitPatternsAndDecimalComments()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.disAsm = true;
            _ = emitter.emitDataConst(BitConverter.GetBytes(1.5f), 4, TYP_FLOAT);
            _ = emitter.emitDataConst(BitConverter.GetBytes(1.5), 8, TYP_DOUBLE);
            var chunks = stackalloc AllocMemChunk[2];
            var floatData = stackalloc byte[4];
            var doubleData = stackalloc byte[8];
            chunks[0] = new AllocMemChunk { size = 4, block = floatData, blockRW = floatData };
            chunks[1] = new AllocMemChunk { size = 8, block = doubleData, blockRW = doubleData };

            var output = CaptureOutput(emitter, chunks);

            Assert.That(output, Is.EqualTo(
                $"{Environment.NewLine}RWD00  \tdd\t3FC00000h\t;       1.5{Environment.NewLine}" +
                $"RWD08  \tdq\t3FF8000000000000h\t;          1.5{Environment.NewLine}"));
        });
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX,
            (compiler, codeGen, _) => action(compiler, codeGen.Emitter));
    }

    private static string CaptureOutput(Emitter emitter, AllocMemChunk* chunks)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            emitter.emitOutputDataSec(emitter.emitConsDsc, chunks);
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAsyncResumeStubEntryPoint")]
    private static extern ref void* StubEntryPoint(Emitter emitter);

    private struct RelocationContext
    {
        public ICorJitInfo JitInfo;
        public void* FirstLocation;
        public void* FirstTarget;
        public CorInfoReloc FirstKind;
        public void* SecondLocation;
        public void* SecondTarget;
        public CorInfoReloc SecondKind;
        public void* ThirdLocation;
        public int Calls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* info, void* location, void* locationRW,
        void* target, CorInfoReloc kind, int delta)
    {
        var context = (RelocationContext*)info;
        if (context->Calls == 0)
        {
            context->FirstLocation = location;
            context->FirstTarget = target;
            context->FirstKind = kind;
        }
        else if (context->Calls == 1)
        {
            context->SecondLocation = location;
            context->SecondTarget = target;
            context->SecondKind = kind;
        }
        else if (context->Calls == 2)
        {
            context->ThirdLocation = location;
        }
        context->Calls++;
    }
}
