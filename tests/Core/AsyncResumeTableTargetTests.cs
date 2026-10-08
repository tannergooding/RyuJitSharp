// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncResumeTableTargetTests
{
    private const uint EntrySize = 2 * TARGET_POINTER_SIZE;

    [Test]
    public static void EntryLayoutUsesTwoTargetSizedUnsignedFields()
    {
        CORINFO_AsyncResumeInfo entry = default;
        Assert.That(sizeof(CORINFO_AsyncResumeInfo), Is.EqualTo(EntrySize));
        Assert.That(Marshal.OffsetOf<CORINFO_AsyncResumeInfo>(nameof(CORINFO_AsyncResumeInfo.Resume)),
            Is.EqualTo((nint)0));
        Assert.That(Marshal.OffsetOf<CORINFO_AsyncResumeInfo>(nameof(CORINFO_AsyncResumeInfo.DiagnosticIP)),
            Is.EqualTo((nint)TARGET_POINTER_SIZE));
#if TARGET_64BIT
        Assert.That(entry.Resume, Is.TypeOf<ulong>());
        Assert.That(entry.DiagnosticIP, Is.TypeOf<ulong>());
#else
        Assert.That(sizeof(CORINFO_AsyncResumeInfo), Is.EqualTo(8));
        Assert.That(sizeof(nint), Is.EqualTo(8), "This selection must exercise a 32-bit target on the 64-bit test host.");
        Assert.That(entry.Resume, Is.TypeOf<uint>());
        Assert.That(entry.DiagnosticIP, Is.TypeOf<uint>());
#endif
    }

    [TestCase(0u, 0u)]
    [TestCase(1u, 0u)]
    [TestCase(3u, 0u)]
    [TestCase(0u, 4u)]
    [TestCase(1u, 4u)]
    [TestCase(3u, 12u)]
    public static void AllocationRetainsTargetAlignmentOffsetsOrderingAndStubCache(uint count, uint prefix)
    {
        WithEmitter((compiler, emitter, context) =>
        {
            if (prefix != 0)
            {
                _ = emitter.emitDataGenBeg(prefix, 4, TYP_INT);
            }
            var binary = emitter.emitDataSecCur;
            var previous = emitter.emitConsDsc.dsdLast;
            var expectedOffset = (prefix + TARGET_POINTER_SIZE - 1) & ~(TARGET_POINTER_SIZE - 1);

            emitter.emitAsyncResumeTable(count, out var offset, out var table);

            Assert.That(offset, Is.EqualTo(expectedOffset));
            Assert.That(table.dsOffset, Is.EqualTo(offset));
            Assert.That(table.dsSize, Is.EqualTo(count * EntrySize));
            Assert.That(table.dsAlignment, Is.EqualTo(TARGET_POINTER_SIZE));
            Assert.That(table.dsDataType, Is.EqualTo(TYP_UNKNOWN));
            Assert.That(table.dsType, Is.EqualTo(Emitter.dataSection.sectionType.asyncResumeInfo));
            Assert.That(table.Locations, Has.Length.EqualTo(count));
            foreach (var location in table.Locations)
            {
                Assert.That(location.Valid(), Is.False);
            }
            Assert.That(emitter.emitDataSecCur, Is.SameAs(binary));
            Assert.That(previous is null ? emitter.emitConsDsc.dsdList : previous.dsNext, Is.SameAs(table));
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(offset + (count * EntrySize)));
            Assert.That(context->Queries, Is.EqualTo(1));

            emitter.emitAsyncResumeTable(1, out var nextOffset, out var next);

            Assert.That(nextOffset, Is.EqualTo(offset + (count * EntrySize)));
            Assert.That(table.dsNext, Is.SameAs(next));
            Assert.That(emitter.emitConsDsc.dsdLast, Is.SameAs(next));
            Assert.That(next.dsNext, Is.Null);
            Assert.That(next.Locations, Is.Not.SameAs(table.Locations));
            Assert.That(context->Queries, Is.EqualTo(1));
        });
    }

    [Test]
    public static void SectionEndRetainsNativeUnsignedWraparound()
    {
        WithEmitter((_, emitter, _) =>
        {
            emitter.emitConsDsc.dsdOffs = 0xFFFFFFF8;

            emitter.emitAsyncResumeTable(1, out var offset, out var table);

            Assert.That(offset, Is.EqualTo(0xFFFFFFF8u));
            Assert.That(table.dsSize, Is.EqualTo(EntrySize));
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(unchecked(0xFFFFFFF8u + EntrySize)));
        });
    }

#if TARGET_WASM
    [Test]
    public static void WasmSerializationUsesTargetWidthAndMethodRelativeDiagnosticRelocations()
    {
        WithEmitter((compiler, emitter, context) =>
        {
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.info.compMatchedVM = true;
            flags.Set(JitFlags.JIT_FLAG_AOT);
            compiler.opts.compReloc = true;

            emitter.emitAsyncResumeTable(2, out var offset, out var table);
            var locationGroup = Group(12, 1);
            table.Locations[0] = new emitLocation(locationGroup);

            var code = stackalloc byte[32];
            emitter.emitCodeBlock = code;
            var executable = stackalloc byte[16];
            var writable = stackalloc byte[16];
            new Span<byte>(executable, 16).Fill(0xCC);
            new Span<byte>(writable, 16).Fill(0xCC);
            var chunk = new AllocMemChunk { size = 16, block = executable, blockRW = writable };
            var section = new Emitter.dataSecDsc
            {
                dsdList = table,
                dsdLast = table,
                dsdOffs = offset + table.dsSize,
            };

            emitter.emitOutputDataSec(section, &chunk);

            var expected = new byte[16];
            WritePointer(expected, 0, (nuint)context->EntryPoint);
            WritePointer(expected, (int)TARGET_POINTER_SIZE, (nuint)code);
            WritePointer(expected, (int)EntrySize, (nuint)context->EntryPoint);
            Assert.That(new ReadOnlySpan<byte>(writable, 16).ToArray(), Is.EqualTo(expected));
            Assert.That(context->Queries, Is.EqualTo(1));
            Assert.That(context->Calls, Is.EqualTo(3));
            Assert.That(context->Locations[0], Is.EqualTo((ulong)(nuint)writable));
            Assert.That(context->Targets[0], Is.EqualTo((ulong)(nuint)context->EntryPoint));
            Assert.That(context->Kinds[0], Is.EqualTo((int)CorInfoReloc.DIRECT));
            Assert.That(context->Locations[1], Is.EqualTo((ulong)(nuint)(writable + TARGET_POINTER_SIZE)));
            Assert.That(context->Targets[1], Is.EqualTo((ulong)(nuint)code));
            Assert.That(context->Kinds[1], Is.EqualTo((int)CorInfoReloc.WASM_METHOD_RELATIVE_VIRTUAL_IP_I32));
            Assert.That(context->Locations[2], Is.EqualTo((ulong)(nuint)(writable + EntrySize)));
            Assert.That(context->Targets[2], Is.EqualTo((ulong)(nuint)context->EntryPoint));
            Assert.That(context->Kinds[2], Is.EqualTo((int)CorInfoReloc.DIRECT));
        });
    }
#endif

#if !TARGET_WASM
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void OutputUsesTargetStrideWritableAliasesAndNativeRelocationOrder(bool relocatable, bool matchedVm)
    {
        WithEmitter((compiler, emitter, context) =>
        {
            compiler.opts.compReloc = relocatable;
            compiler.info.compMatchedVM = matchedVm;
            emitter.emitAsyncResumeTable(3, out _, out var table);
            var hotGroup = Group(12, 1);
            var coldGroup = Group(72, 2);
            table.Locations[0] = new emitLocation(hotGroup);
            table.Locations[1] = new emitLocation(coldGroup);

            var hot = stackalloc byte[64];
            var cold = stackalloc byte[32];
            emitter.emitCodeBlock = hot;
            emitter.emitColdCodeBlock = cold;
            emitter.emitTotalHotCodeSize = 64;
            emitter.emitTotalColdCodeSize = 32;
            var size = checked((int)table.dsSize);
            var executable = stackalloc byte[size + 8];
            var writable = stackalloc byte[size + 8];
            new Span<byte>(executable, size + 8).Fill(0xCC);
            new Span<byte>(writable, size + 8).Fill(0xCC);
            var chunk = new AllocMemChunk { size = size, block = executable, blockRW = writable };

            emitter.emitOutputDataSec(emitter.emitConsDsc, &chunk);

            var expected = new byte[size];
            WritePointer(expected, 0, (nuint)context->EntryPoint);
            WritePointer(expected, (int)TARGET_POINTER_SIZE, (nuint)(hot + 12));
            WritePointer(expected, (int)EntrySize, (nuint)context->EntryPoint);
            WritePointer(expected, (int)(EntrySize + TARGET_POINTER_SIZE), (nuint)(cold + 8));
            WritePointer(expected, (int)(2 * EntrySize), (nuint)context->EntryPoint);
            Assert.That(new ReadOnlySpan<byte>(writable, size).ToArray(), Is.EqualTo(expected));
            Assert.That(new ReadOnlySpan<byte>(writable + size, 8).ToArray(), Is.All.EqualTo(0xCC));
            Assert.That(new ReadOnlySpan<byte>(executable, size + 8).ToArray(), Is.All.EqualTo(0xCC));
            Assert.That(context->Calls, Is.EqualTo(relocatable && matchedVm ? 5 : 0));
            if (relocatable && matchedVm)
            {
                var targets = new nuint[]
                {
                    (nuint)context->EntryPoint, (nuint)(hot + 12),
                    (nuint)context->EntryPoint, (nuint)(cold + 8), (nuint)context->EntryPoint,
                };
                for (var index = 0; index < targets.Length; index++)
                {
                    Assert.That(context->Locations[index],
                        Is.EqualTo((ulong)(nuint)(writable + (index * TARGET_POINTER_SIZE))));
                    Assert.That(context->Targets[index], Is.EqualTo((ulong)targets[index]));
                    Assert.That(context->Kinds[index], Is.EqualTo((int)CorInfoReloc.DIRECT));
                }
            }
        });
    }
#endif

#if !TARGET_WASM
    [TestCase(1u)]
    [TestCase(3u)]
    [TestCase(4u)]
    public static void DiagnosticsPreserveOracleHostLocationDivisorDqAndTargetEntryLabels(uint count)
    {
        WithEmitter((compiler, emitter, context) =>
        {
            _ = emitter.emitDataConst(new byte[4], 4, TYP_INT);
            emitter.emitAsyncResumeTable(count, out var offset, out var table);
            var group = Group(12, 1);
            group.igInsCnt = 1;
            group.igSize = 4;
            for (var index = 0; index < count; index++)
            {
                table.Locations[index] = index == 0
                    ? new emitLocation(group)
                    : new emitLocation(group, 1);
            }
            var section = new Emitter.dataSecDsc { dsdList = table, dsdLast = table, dsdOffs = offset + table.dsSize };
            var chunk = new AllocMemChunk();
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                emitter.emitDispDataSec(section, &chunk);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            var expected = new StringBuilder($"\n{$"RWD{offset:D2}",-7}");
            var diagnosticCount = table.dsSize / (2u * (uint)sizeof(nint));
            for (var index = 0u; index < diagnosticCount; index++)
            {
                if (index != 0)
                {
                    _ = expected.Append(CultureInfo.InvariantCulture, $"{ $"RWD{offset + (index * EntrySize):D2}",-7}");
                }
                _ = expected.Append("\tdq\tCORINFO_HELP_THROW\n");
                _ = expected.Append(index == 0 ? "\tdq\tG_M000_IG01\n" : "\tdq\tG_M000_IG01 + 4\n");
            }
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                Is.EqualTo(expected.ToString().Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
        });
    }
#endif

    private static void WritePointer(byte[] bytes, int offset, nuint value)
    {
        for (var index = 0; index < TARGET_POINTER_SIZE; index++)
        {
            bytes[offset + index] = unchecked((byte)(value >> (index * 8)));
        }
    }

    private static insGroup Group(uint offset, uint number)
    {
        var group = new insGroup { igOffs = offset };
        group.InitializeNum(number);
#if DEBUG
        group.igSelf = group;
#endif

        return group;
    }

    private static void WithEmitter(EmitterTest action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = codeGen.Emitter;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.getAsyncResumptionStub = &GetAsyncResumptionStub;
        vtable.recordRelocation = &RecordRelocation;
        var context = new ResumeContext
        {
            JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            EntryPoint = (void*)unchecked((nuint)0x1234567887654321UL),
        };
        emitter.emitBegCG(compiler, &context.JitInfo);
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, emitter, &context);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private delegate void EmitterTest(Compiler compiler, Emitter emitter, ResumeContext* context);

    private struct ResumeContext
    {
        public ICorJitInfo JitInfo;
        public void* EntryPoint;
        public int Queries;
        public int Calls;
        public fixed ulong Locations[5];
        public fixed ulong Targets[5];
        public fixed int Kinds[5];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* GetAsyncResumptionStub(ICorJitInfo* self, void** entryPoint)
    {
        var context = (ResumeContext*)self;
        context->Queries++;
        *entryPoint = context->EntryPoint;

        return (CORINFO_METHOD_STRUCT_*)(((nint)CorInfoHelpFunc.CORINFO_HELP_THROW << 2) | 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* self, void* location, void* locationRW,
        void* target, CorInfoReloc kind, int delta)
    {
        var context = (ResumeContext*)self;
        var index = context->Calls++;
        context->Locations[index] = (nuint)location;
        context->Targets[index] = (nuint)target;
        context->Kinds[index] = (int)kind;
    }
}
