#if TARGET_LOONGARCH64
using System;
#if DEBUG
using System.Buffers.Binary;
using System.IO;
using System.Text;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class LoongArchUnwindMatchTests
{
    [TestCase(new byte[] { 0xE1, 0xAA }, new byte[] { 0xAA }, 1)]
    [TestCase(new byte[] { 0xE2, 0x01, 0x02, 0xAA }, new byte[] { 0xAA }, 3)]
    public static void PrologFrameCodeMatchesTheCorrespondingEpilogTail(
        byte[] prologCodes,
        byte[] epilogCodes,
        int expectedMatchIndex)
    {
        var unwindPrologCodesType = typeof(UnwindInfo).GetNestedType("UnwindPrologCodes", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindPrologCodes type was not found.");
        var unwindEpilogCodesType = typeof(UnwindInfo).GetNestedType("UnwindEpilogCodes", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindEpilogCodes type was not found.");
        var unwindEpilogInfoType = typeof(UnwindInfo).GetNestedType("UnwindEpilogInfo", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindEpilogInfo type was not found.");
        var constructorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // Matching uses only these buffers; the compiler fields are not accessed.
        var unwindPrologCodes = Activator.CreateInstance(unwindPrologCodesType, constructorFlags, null, [null], null)
            ?? throw new InvalidOperationException("UnwindPrologCodes construction failed.");
        var epilogInfo = Activator.CreateInstance(unwindEpilogInfoType, constructorFlags, null, [null], null)
            ?? throw new InvalidOperationException("UnwindEpilogInfo construction failed.");
        var epilogStorage = unwindEpilogInfoType.GetField("epiCodes", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(epilogInfo)
            ?? throw new InvalidOperationException("UnwindEpilogInfo.epiCodes was not found.");
        var addCodeFlags = BindingFlags.Instance | BindingFlags.Public;
        var prologAddCode = unwindPrologCodesType.GetMethod(
            "AddCode", addCodeFlags, null, [typeof(byte)], null)
            ?? throw new InvalidOperationException("UnwindPrologCodes.AddCode was not found.");
        var epilogAddCode = unwindEpilogCodesType.GetMethod(
            "AddCode", addCodeFlags, null, [typeof(byte)], null)
            ?? throw new InvalidOperationException("UnwindEpilogCodes.AddCode was not found.");
        var finalizeCodes = unwindEpilogInfoType.GetMethod("FinalizeCodes", addCodeFlags)
            ?? throw new InvalidOperationException("UnwindEpilogInfo.FinalizeCodes was not found.");
        var match = unwindPrologCodesType.GetMethod(
            "Match", addCodeFlags, null, [unwindEpilogInfoType], null)
            ?? throw new InvalidOperationException("UnwindPrologCodes.Match was not found.");

        for (var index = prologCodes.Length - 1; index >= 0; index--)
        {
            _ = prologAddCode.Invoke(unwindPrologCodes, [prologCodes[index]]);
        }

        foreach (var code in epilogCodes)
        {
            _ = epilogAddCode.Invoke(epilogStorage, [code]);
        }

        _ = finalizeCodes.Invoke(epilogInfo, null);

        Assert.That(match.Invoke(unwindPrologCodes, [epilogInfo]), Is.EqualTo(expectedMatchIndex));
    }
}

internal static unsafe class LoongArchUnwindLifecycleTests
{
    [Test]
    public static void PrologRecordingPreservesLoongArchInstructionEncoding()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = new UnwindInfo() }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();
            Assert.That(compiler.generateCFIUnwindCodes(), Is.False);

            compiler.unwindBegProlog();
            compiler.unwindAllocStack(16);
            compiler.unwindAllocStack(512);
            compiler.unwindAllocStack(2048);
            compiler.unwindSetFrameReg(REG_FP, 0);
            compiler.unwindSetFrameReg(REG_FP, 8);
            compiler.unwindSaveReg(REG_RA, 8);
            compiler.unwindSaveReg(REG_F24, 8);
            compiler.unwindNop();
            compiler.unwindEndProlog();

            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            var unwindInfoType = typeof(UnwindInfo);
            var fragmentType = unwindInfoType.GetNestedType("UnwindFragmentInfo", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("UnwindFragmentInfo type was not found.");
            var prologType = unwindInfoType.GetNestedType("UnwindPrologCodes", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("UnwindPrologCodes type was not found.");
            var fragment = GetPrivateField<object>(unwindInfoType, unwindInfo, "uwiFragmentFirst");
            var prolog = GetPrivateField<object>(fragmentType, fragment, "ufiPrologCodes");
            var codeSlot = GetPrivateField<int>(prologType, prolog, "upcCodeSlot");
            var codes = GetPrivateField<byte[]>(prologType, prolog, "upcMem");

            Assert.That(codes[codeSlot..], Is.EqualTo(new byte[]
            {
                0xE3, 0xDC, 0x00, 0x01, 0xD0, 0x00, 0x01, 0xE2, 0x00, 0x01, 0xE1,
                0xE0, 0x00, 0x00, 0x80, 0xC0, 0x20, 0x01,
                0xE4, 0xE4, 0xE4, 0xE4,
            }));
        });
    }

    [Test]
    public static void SplitAddsFragmentsAtEmitterGroupBoundaries()
    {
        var previousConfig = Globals.JitConfig;
        Globals.JitConfig = default;
        try
        {
            WithCodeGen((compiler, codeGen) =>
            {
                const uint fragmentSize = 1u << 20;
                const int fullGroupCount = 16;
                const ushort fullGroupSize = ushort.MaxValue;
                var groups = new insGroup[fullGroupCount + 1];
                var offset = 0u;

                for (var index = 0; index < fullGroupCount; index++)
                {
                    groups[index] = CreateGroup(offset, fullGroupSize);
                    offset = unchecked(offset + fullGroupSize);
                    if (index > 0)
                    {
                        groups[index - 1].igNext = groups[index];
                    }
                }

                groups[^1] = CreateGroup(offset, 24);
                groups[^2].igNext = groups[^1];
                FirstGroup(codeGen.Emitter) = groups[0];
                codeGen.Emitter.emitCurIG = groups[^1];
                compiler.info.compTotalHotCodeSize = unchecked((int)(offset + 24));

                var unwindInfo = new UnwindInfo();
                unwindInfo.InitUnwindInfo(compiler, null, null);
                unwindInfo.Split();

                var unwindInfoType = typeof(UnwindInfo);
                var fragmentType = unwindInfoType.GetNestedType("UnwindFragmentInfo", BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("UnwindFragmentInfo type was not found.");
                var firstFragment = GetPrivateField<object>(unwindInfoType, unwindInfo, "uwiFragmentFirst");
                var secondFragment = GetPrivateNullableField(fragmentType, firstFragment, "ufiNext")
                    ?? throw new AssertionException("The split did not create a second fragment.");
                var secondLocation = GetPrivateField<emitLocation?>(fragmentType, secondFragment, "ufiEmitLoc")
                    ?? throw new AssertionException("The second fragment has no emitter location.");
                var splitOffset = secondLocation.CodeOffset(codeGen.Emitter);

                Assert.That(compiler.info.compTotalHotCodeSize, Is.GreaterThan(fragmentSize));
                Assert.That(splitOffset, Is.GreaterThan(0));
                Assert.That(splitOffset, Is.LessThanOrEqualTo(fragmentSize));
                Assert.That(GetPrivateNullableField(fragmentType, secondFragment, "ufiNext"), Is.Null);
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [Test]
    public static void PrologLifecycleCapturesStateAndCompletesWithoutInstructions()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = new UnwindInfo() }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();

            compiler.unwindBegProlog();
            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            Assert.That(unwindInfo.GetCurrentEmitterLocation().HasValue, Is.True);

            compiler.unwindPadding();

            compiler.unwindEndProlog();
            Assert.That(compiler.compGeneratingUnwindProlog, Is.False);

            codeGen.Emitter.emitCurIG = CreateGroup(flags: InsGroupFlags.Epilog);
            compiler.unwindBegEpilog();
            Assert.That(compiler.compGeneratingUnwindEpilog, Is.True);
            compiler.unwindEndEpilog();
            Assert.That(compiler.compGeneratingUnwindEpilog, Is.False);
        });
    }

    [Test]
    public static void ReserveAndEmitFinalizeTheRootUnwindFragmentWithoutAnEe()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = new UnwindInfo() }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.info.compMatchedVM = false;
            compiler.info.compTotalHotCodeSize = 32;
            compiler.info.compNativeCodeSize = 32;
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();

            compiler.unwindBegProlog();
            compiler.unwindEndProlog();
            codeGen.Emitter.emitEndProlog();
            codeGen.Emitter.emitCurIG = CreateGroup();
            compiler.unwindReserve();
            compiler.unwindEmit(null, null);

            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            var unwindInfoType = typeof(UnwindInfo);
            var fragmentType = unwindInfoType.GetNestedType("UnwindFragmentInfo", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("UnwindFragmentInfo type was not found.");
            var fragment = GetPrivateField<object>(unwindInfoType, unwindInfo, "uwiFragmentFirst");
            Assert.That(GetPrivateField<uint>(fragmentType, fragment, "ufiSize"), Is.GreaterThan(0));
            Assert.That(compiler.info.compMatchedVM, Is.False);
        });
    }

#if DEBUG
    [Test]
    [NonParallelizable]
    public static void DebugDumpShowsLoongArchBufferAndEpilogState()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var unwindInfo = new UnwindInfo();
        unwindInfo.InitUnwindInfo(compiler, null, null);
        var unwindInfoType = typeof(UnwindInfo);
        var fragmentType = unwindInfoType.GetNestedType("UnwindFragmentInfo", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindFragmentInfo type was not found.");
        var fragment = GetPrivateField<object>(unwindInfoType, unwindInfo, "uwiFragmentFirst");
        var epilogType = typeof(UnwindInfo).GetNestedType("UnwindEpilogInfo", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindEpilogInfo type was not found.");
        var epilog = Activator.CreateInstance(
            epilogType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [compiler], null)
            ?? throw new InvalidOperationException("UnwindEpilogInfo construction failed.");
        SetPrivateField(fragmentType, fragment, "ufiEpilogList", epilog);
        SetPrivateField(fragmentType, fragment, "ufiEpilogLast", epilog);

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previousWriter = Globals.s_jitstdout;
        try
        {
            Globals.s_jitstdout = writer;
            unwindInfo.Dump(true);
            writer.Flush();
        }
        finally
        {
            Globals.s_jitstdout = previousWriter;
        }

        var output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.That(output, Does.Contain("UnwindInfo"));
        Assert.That(output, Does.Contain("UnwindPrologCodes"));
        Assert.That(output, Does.Contain("upcCodeSlot: 20"));
        Assert.That(output, Does.Contain("uwiInitialized: 0x0facade0"));
        Assert.That(output, Does.Contain("ufiInitialized: 0x0facade0"));
        Assert.That(output, Does.Contain("UnwindEpilogInfo"));
        Assert.That(output, Does.Contain("uecFinalized: false"));
    }
#endif

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    private static insGroup CreateGroup(
        uint offset = 0,
        ushort size = 0,
        InsGroupFlags flags = InsGroupFlags.None)
    {
        var group = new insGroup
        {
            igOffs = offset,
            igSize = size,
            igFlags = flags,
        };
#if DEBUG
        group.igSelf = group;
#endif
        return group;
    }

    private static T GetPrivateField<T>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
            DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        object instance,
        string name)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was not found.");
        return (T)(field.GetValue(instance)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was null."));
    }

    private static object? GetPrivateNullableField(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
            DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        object instance,
        string name)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was not found.");
        return field.GetValue(instance);
    }

    private static void SetPrivateField(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
            DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        object instance,
        string name,
        object value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was not found.");
        field.SetValue(instance, value);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);
}

#if DEBUG
internal static unsafe class LoongArchUnwindDiagnosticsTests
{
    [TestCase(0xFFFFFFFFu, 27u, 5u, 31u)]
    [TestCase(0xFFFFFFFFu, 22u, 5u, 31u)]
    [TestCase(0xFFFFFFFFu, 0u, 18u, 0x3FFFFu)]
    [TestCase(0x80000000u, 31u, 1u, 1u)]
    [TestCase(0x12345678u, 16u, 8u, 0x34u)]
    public static void HeaderBitExtractionPreservesUnsignedFields(uint word, uint start, uint length, uint expected)
    {
        Assert.That(Globals.ExtractBits(word, start, length), Is.EqualTo(expected));
    }

    [TestCase(new byte[] { 0x01 }, "alloc_s #1 (0x01); addi.d sp, sp, -16 (0x010)")]
    [TestCase(new byte[] { 0xC0, 0x20 }, "alloc_m #32 (0x020); addi.d sp, sp, -512 (0x0200)")]
    [TestCase(new byte[] { 0xD0, 0x01, 0x02 }, "save_reg X#1 Z#2 (0x02);")]
    [TestCase(new byte[] { 0xDC, 0x10, 0x02 }, "save_freg X#1 Z#2 (0x02);")]
    [TestCase(new byte[] { 0xE0, 0x00, 0x01, 0x02 }, "alloc_l 258 (0x000102);")]
    [TestCase(new byte[] { 0xE1 }, "set_fp; move ")]
    [TestCase(new byte[] { 0xE2, 0x01, 0x02 }, "add_fp 258 (0x102);")]
    [TestCase(new byte[] { 0xE3 }, "nop")]
    [TestCase(new byte[] { 0xE4 }, "end")]
    [TestCase(new byte[] { 0xE5 }, "end_c")]
    [TestCase(new byte[] { 0xE6 }, "save_next")]
    public static void DumpDecodesLoongArchOpcodesFromAnUnalignedHeader(byte[] code, string expected)
    {
        var blob = new byte[9];
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(1), (1u << 27) | 1u);
        blob.AsSpan(5).Fill(0xE4);
        code.CopyTo(blob, 5);
        var text = Dump(blob, 1, 0, 4, 8);

        Assert.That(text, Does.Contain(expected));
        Assert.That(text, Does.Contain("  No epilogs"));
        Assert.That(text, Does.Contain("  Function Length   : 1 (0x00001) Actual length = 4 (0x000004)"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DumpPreservesBothEpilogScopeFormsAndFuncletOffsets(bool singleEpilog)
    {
        var blob = new byte[singleEpilog ? 8 : 12];
        var header = (1u << 27) | (singleEpilog ? 1u << 21 : 1u << 22) | 2u;
        BinaryPrimitives.WriteUInt32LittleEndian(blob, header);
        if (!singleEpilog)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(4), (2u << 22) | 1u);
        }

        blob.AsSpan(blob.Length - 4).Fill(0xE4);
        var text = Dump(blob, 0, 16, 24, (uint)blob.Length);

        Assert.That(text, Does.Contain(singleEpilog
            ? "  --- One epilog, unwind codes at 0"
            : "Offset from main function begin = 20 (0x000014)"));
        Assert.That(text, Does.Contain($"    ---- Epilog start at index {(singleEpilog ? 0 : 2)} ----"));
    }

    [Test]
    public static void DumpPreservesExtendedHeaderCounts()
    {
        var blob = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(blob, 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(4), 1u << 16);
        blob.AsSpan(8).Fill(0xE4);
        var text = Dump(blob, 0, 0, 4, 12);

        Assert.That(text, Does.Contain("  ---- Extension word ----"));
        Assert.That(text, Does.Contain("  Extended Code Words        : 1"));
        Assert.That(text, Does.Contain("  Extended Epilog Count      : 0"));
    }

    private static string Dump(byte[] blob, int headerOffset, uint start, uint end, uint size)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = Globals.s_jitstdout;
        try
        {
            Globals.s_jitstdout = writer;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            fixed (byte* pointer = blob)
            {
                Globals.DumpUnwindInfo(compiler, true, start, end, pointer + headerOffset, size);
            }

            writer.Flush();
        }
        finally
        {
            Globals.s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
#endif
#endif
