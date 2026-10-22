// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenSharedGenerationTests
{
    [Test]
    public static void ConstructionPreservesCompilerAndGcRegisterOwnerAliases()
    {
        WithGeneration((compiler, codeGen) =>
        {
            Assert.That(codeGen.Compiler, Is.SameAs(compiler));
            Assert.That(codeGen.GCInfo.Compiler, Is.SameAs(compiler));
            Assert.That(codeGen.RegSet.Compiler, Is.SameAs(compiler));
            Assert.That(EmitterOwner(codeGen.Emitter), Is.SameAs(codeGen));
            Assert.That(Unsafe.AreSame(ref codeGen.GCInfo, ref codeGen.Emitter.GCInfo), Is.True);
            Assert.That(Unsafe.AreSame(ref codeGen.GCInfo, ref codeGen.RegSet.GCInfo), Is.True);
            Assert.That(Unsafe.AreSame(ref codeGen.RegSet, ref codeGen.GCInfo.RegSet), Is.True);
        });
    }

    [Test]
    public static void ConstructionRetainsNativeTargetDefaults()
    {
        WithGeneration((compiler, codeGen) =>
        {
            Assert.That(codeGen.Interruptible, Is.False);
            Assert.That(codeGen.CalleeRegArgMaskLiveIn, Is.EqualTo(Globals.RBM_NONE));
            Assert.That(compiler.genCallSite2DebugInfoMap, Is.Null);
            if (!Configuration.TARGET_X86)
            {
                Assert.That(StackArgumentVariable(codeGen), Is.EqualTo(Globals.BAD_VAR_NUM));
            }
            if (Configuration.TARGET_ARM || Configuration.TARGET_ARM64 ||
                Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                Assert.That(codeGen.HasTailCalls, Is.False);
            }
            if (Configuration.UNIX_X86_ABI)
            {
                Assert.That(CurrentNestedAlignment(codeGen), Is.EqualTo(0u));
                Assert.That(MaximumNestedAlignment(codeGen), Is.EqualTo(0u));
            }
            if (Configuration.TARGET_ARM64)
            {
                Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.False);
                Assert.That(codeGen.genForceFuncletFrameType5, Is.False);
                Assert.That(codeGen.genReverseAndPairCalleeSavedRegisters, Is.False);
            }
            if (TestConfiguration.IsDebug)
            {
                Assert.That(Compiler.DebugData.Get(compiler).CodeGenCurrentDisplayOffset, Is.EqualTo(uint.MaxValue));
                if (Configuration.HAS_FIXED_REGISTER_SET)
                {
                    Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(unchecked((int)Globals.INVALID_POINT_CD)));
                }
                if (Configuration.TARGET_XARCH)
                {
                    Assert.That(compiler.compCalleeFPRegsSavedMask, Is.EqualTo(unchecked((regMask)(-1))));
                }
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConstructionCopiesDebugVerbosity(bool verbose)
    {
        WithGeneration(
            (compiler, _) => Assert.That(
                Configuration.DEBUG && Compiler.DebugData.Get(compiler).CodeGenVerbose,
                Is.EqualTo(Configuration.DEBUG && verbose)),
            verbose);
    }

    [Test]
    public static void DebugOnlyPropertiesPreserveReleaseContract()
    {
        WithGeneration((compiler, codeGen) =>
        {
            if (TestConfiguration.IsDebug)
            {
                Assert.That(codeGen.IsGcTypeFixed, Is.False);
                Assert.That(codeGen.Verbose, Is.False);

                codeGen.Verbose = true;

                Assert.That(codeGen.Verbose, Is.True);
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => _ = codeGen.IsGcTypeFixed);
                _ = Assert.Throws<InvalidOperationException>(() => _ = codeGen.Verbose);
                _ = Assert.Throws<InvalidOperationException>(() => codeGen.Verbose = true);
            }
        });
    }

    [Test]
    public static void UnsupportedMachinePhaseStillClearsBorrowedOutputAddresses()
    {
        Assume.That(
            !Configuration.TARGET_AMD64 || !Configuration.WINDOWS_AMD64_ABI,
            "This test covers targets outside the Windows AMD64 ABI.");

        var previousConfig = Globals.JitConfig;
        try
        {
            if (Configuration.TARGET_WASM)
            {
                TestConfiguration.SetJitConfigValue(ref Globals.JitConfig, "_jitWasmFunclets", 1);
            }

            WithGeneration((compiler, codeGen) =>
            {
                var code = (void*)0x1234;
                var size = 77;
                CodePointerAddress(codeGen) = (void**)0x5678;
                NativeSizeAddress(codeGen) = (int*)0x9ABC;

                var error = Assert.Throws<FatalJitException>(() => codeGen.genGenerateCode(out code, out size));

                Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
                Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(Phases.PHASE_GENERATE_CODE));
                Assert.That((nuint)CodePointerAddress(codeGen), Is.EqualTo((nuint)0));
                Assert.That((nuint)NativeSizeAddress(codeGen), Is.EqualTo((nuint)0));
                Assert.That((nuint)code, Is.EqualTo((nuint)0x1234));
                Assert.That(size, Is.EqualTo(77));
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [Test]
    public static void LateDisassemblyRejectsBeforeBorrowingOutputsOrStartingAPhase()
    {
        Assume.That(Configuration.LATE_DISASM);
        WithGeneration((compiler, codeGen) =>
        {
            compiler.opts.doLateDisasm = true;
            compiler.mostRecentlyActivePhase = Phases.PHASE_EMIT_GCEH;
            var code = (void*)0x1234;
            var size = 77;

            var error = Assert.Throws<FatalJitException>(() => codeGen.genGenerateCode(out code, out size));

            Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(error, Has.Message.Contains("native callback bridge"));
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(Phases.PHASE_EMIT_GCEH));
            Assert.That((nuint)CodePointerAddress(codeGen), Is.EqualTo((nuint)0));
            Assert.That((nuint)NativeSizeAddress(codeGen), Is.EqualTo((nuint)0));
            Assert.That((nuint)code, Is.EqualTo((nuint)0x1234));
            Assert.That(size, Is.EqualTo(77));
        });
    }

    [TestCase(0, (ushort)0, false)]
    [TestCase(0, (ushort)1, false)]
    [TestCase(0, (ushort)2, true)]
    [TestCase(0, (ushort)3, true)]
    [TestCase(1, (ushort)2, false)]
    [TestCase(-1, (ushort)2, false)]
    public static void WasmFuncletPredicatePreservesConfigurationAndCount(
        int enabled, ushort count, bool rejected)
    {
        Assume.That(Configuration.TARGET_WASM, "This test requires the Wasm target.");

        var previousConfig = Globals.JitConfig;
        try
        {
            TestConfiguration.SetJitConfigValue(ref Globals.JitConfig, "_jitWasmFunclets", enabled);
            WithGeneration((compiler, codeGen) =>
            {
                compiler.fgFuncletsCreated = true;
                compiler.compFuncInfoCount = count;
                compiler.compFuncInfos = new FuncInfoDsc[count];

                if (rejected)
                {
                    var error = Assert.Throws<FatalJitException>(() => CheckWasmFunclets(codeGen));
                    Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_R2R_UNSUPPORTED));
                }
                else
                {
                    CheckWasmFunclets(codeGen);
                }
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [Test]
    public static void WasmFuncletRejectionPrecedesPhaseAndOutputMutation()
    {
        Assume.That(Configuration.TARGET_WASM, "This test requires the Wasm target.");

        var previousConfig = Globals.JitConfig;
        try
        {
            TestConfiguration.SetJitConfigValue(ref Globals.JitConfig, "_jitWasmFunclets", 0);
            WithGeneration((compiler, codeGen) =>
            {
                compiler.fgFuncletsCreated = true;
                compiler.compFuncInfoCount = 2;
                compiler.compFuncInfos = new FuncInfoDsc[2];
                compiler.mostRecentlyActivePhase = Phases.PHASE_EMIT_GCEH;
                var code = (void*)0x1234;
                var size = 77;

                var error = Assert.Throws<FatalJitException>(() => codeGen.genGenerateCode(out code, out size));

                Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_R2R_UNSUPPORTED));
                Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(Phases.PHASE_EMIT_GCEH));
                Assert.That((nuint)CodePointerAddress(codeGen), Is.EqualTo((nuint)0));
                Assert.That((nuint)NativeSizeAddress(codeGen), Is.EqualTo((nuint)0));
                Assert.That((nuint)code, Is.EqualTo((nuint)0x1234));
                Assert.That(size, Is.EqualTo(77));
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCheckWasmFunclets")]
    private static extern void CheckWasmFunclets(CodeGen codeGen);

    [TestCase("", false)]
    [TestCase("00001233", false)]
    [TestCase("00001234", true)]
    [TestCase("00001230-00001240", true)]
    public static void WasmDebugRangeUsesTheInlineRootAndExcludesEmptyConfiguration(string range, bool rejected)
    {
        Assume.That(Configuration.TARGET_WASM, "This test requires the Wasm target.");
        Assume.That(TestConfiguration.IsDebug);

        var previousConfig = Globals.JitConfig;
        var previousRange = Compiler.DebugData.CodeGenStaticData.JitR2RUnsupportedRange;
        try
        {
            Compiler.DebugData.CodeGenStaticData.JitR2RUnsupportedRange = default;
            var bytes = System.Text.Encoding.ASCII.GetBytes(range + "\0");
            fixed (byte* rangePointer = bytes)
            {
                UnsupportedRangeConfig(ref Globals.JitConfig) = rangePointer;
                WithGeneration((compiler, codeGen) =>
                {
                    var root = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                    root.info.compFullName = "SharedGenerationRange";
                    MethodHash(ref root.info) = 0x1234;
                    compiler.info.compFullName = "InlineeWithDifferentHash";
                    MethodHash(ref compiler.info) = 0x5678;
                    compiler.impInlineInfo = new InlineInfo { InlineRoot = root, InlinerCompiler = root };
                    if (rejected)
                    {
                        var error = Assert.Throws<FatalJitException>(() => CheckWasmRange(codeGen));
                        Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_R2R_UNSUPPORTED));
                    }
                    else
                    {
                        CheckWasmRange(codeGen);
                    }
                    Assert.That(Compiler.DebugData.CodeGenStaticData.JitR2RUnsupportedRange.IsInit, Is.True);
                });
            }
        }
        finally
        {
            Compiler.DebugData.CodeGenStaticData.JitR2RUnsupportedRange = previousRange;
            Globals.JitConfig = previousConfig;
        }
    }

    private static ref byte* UnsupportedRangeConfig(ref JitConfigValues config)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref config)._jitR2RUnsupportedRange;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compMethodHashPrivate")]
    private static extern ref int MethodHash(ref Compiler.Info info);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCheckWasmR2RRange")]
    private static extern void CheckWasmRange(CodeGen codeGen);

    private static void WithGeneration(Action<Compiler, CodeGen> action, bool verbose = false)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        if (verbose)
        {
            TestConfiguration.EnableVerbose(compiler);
        }

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "codeGen")]
    private static extern ref CodeGen EmitterOwner(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_codePtr")]
    private static extern ref void** CodePointerAddress(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nativeSizeOfCode")]
    private static extern ref int* NativeSizeAddress(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stkArgVarNum")]
    private static extern ref int StackArgumentVariable(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_curNestedAlignment")]
    private static extern uint CurrentNestedAlignment(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "maxNestedAlignment")]
    private static extern ref uint MaximumNestedAlignment(CodeGen codeGen);

}
