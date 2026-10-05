#if TARGET_LOONGARCH64
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

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
                    groups[index] = new insGroup
                    {
                        igOffs = offset,
                        igSize = fullGroupSize,
                    };
                    offset = unchecked(offset + fullGroupSize);
                    if (index > 0)
                    {
                        groups[index - 1].igNext = groups[index];
                    }
                }

                groups[^1] = new insGroup { igOffs = offset, igSize = 24 };
                groups[^2].igNext = groups[^1];
                FirstGroup(codeGen.Emitter) = groups[0];
                codeGen.Emitter.emitCurIG = groups[^1];
                compiler.info.compTotalHotCodeSize = unchecked(offset + 24);

                var unwindInfo = new UnwindInfo();
                unwindInfo.InitUnwindInfo(compiler, null, null);
                unwindInfo.Split();

                var firstFragment = GetPrivateField<object>(unwindInfo, "uwiFragmentFirst");
                var secondFragment = GetPrivateNullableField(firstFragment, "ufiNext")
                    ?? throw new AssertionException("The split did not create a second fragment.");
                var secondLocation = GetPrivateField<emitLocation?>(secondFragment, "ufiEmitLoc")
                    ?? throw new AssertionException("The second fragment has no emitter location.");
                var splitOffset = secondLocation.Value.CodeOffset(codeGen.Emitter);

                Assert.That(compiler.info.compTotalHotCodeSize, Is.GreaterThan(fragmentSize));
                Assert.That(splitOffset, Is.GreaterThan(0));
                Assert.That(splitOffset, Is.LessThanOrEqualTo(fragmentSize));
                Assert.That(GetPrivateNullableField(secondFragment, "ufiNext"), Is.Null);
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [Test]
    public static void PrologLifecycleCapturesStateAndRecordsPaddingNop()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();

            compiler.unwindBegProlog();
            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            Assert.That(unwindInfo.GetCurrentEmitterLocation().HasValue, Is.True);

            AllocateNop(codeGen.Emitter);
            compiler.unwindPadding();

            var fragment = GetPrivateField<object>(unwindInfo, "uwiFragmentFirst");
            var prologCodes = GetPrivateField<object>(fragment, "ufiPrologCodes");
            var codeSlot = GetPrivateField<int>(prologCodes, "upcCodeSlot");
            var memory = GetPrivateField<byte[]>(prologCodes, "upcMem");
            Assert.That(memory[codeSlot], Is.EqualTo(0xE3));

            compiler.unwindEndProlog();
            Assert.That(compiler.compGeneratingUnwindProlog, Is.False);

            codeGen.Emitter.emitCurIG = new insGroup { igFlags = InsGroupFlags.Epilog };
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
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.info.compMatchedVM = false;
            compiler.info.compTotalHotCodeSize = 32;
            compiler.info.compNativeCodeSize = 32;
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();

            compiler.unwindBegProlog();
            compiler.unwindEndProlog();
            codeGen.Emitter.emitEndProlog();
            codeGen.Emitter.emitCurIG = new insGroup();
            compiler.unwindReserve();
            compiler.unwindEmit(null, null);

            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            var fragment = GetPrivateField<object>(unwindInfo, "uwiFragmentFirst");
            Assert.That(GetPrivateField<uint>(fragment, "ufiSize"), Is.GreaterThan(0));
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
        var fragment = GetPrivateField<object>(unwindInfo, "uwiFragmentFirst");
        var epilogType = typeof(UnwindInfo).GetNestedType("UnwindEpilogInfo", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindEpilogInfo type was not found.");
        var epilog = Activator.CreateInstance(
            epilogType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [compiler], null)
            ?? throw new InvalidOperationException("UnwindEpilogInfo construction failed.");
        SetPrivateField(fragment, "ufiEpilogList", epilog);
        SetPrivateField(fragment, "ufiEpilogLast", epilog);

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

    private static Emitter.instrDescBasic AllocateNop(Emitter emitter)
    {
        var descriptor = AllocateSmall(emitter, emitAttr.EA_4BYTE);
        descriptor.idIns(instruction.INS_nop);
        descriptor.idInsFmt(Emitter.instrFormat.IF_SN_0A);
        CurrentGroupSize(emitter) += 4;
        return descriptor;
    }

    private static T GetPrivateField<T>(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was not found.");
        return (T)(field.GetValue(instance)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was null."));
    }

    private static object? GetPrivateNullableField(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was not found.");
        return field.GetValue(instance);
    }

    private static void SetPrivateField(object instance, string name, object value)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} was not found.");
        field.SetValue(instance, value);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrSmall")]
    private static extern Emitter.instrDescBasic AllocateSmall(Emitter emitter, emitAttr attr);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentGroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);
}
#endif
