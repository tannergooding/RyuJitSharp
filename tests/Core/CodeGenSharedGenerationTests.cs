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
#if !TARGET_X86
            Assert.That(StackArgumentVariable(codeGen), Is.EqualTo(Globals.BAD_VAR_NUM));
#endif
#if TARGET_ARM || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            Assert.That(codeGen.HasTailCalls, Is.False);
#endif
#if UNIX_X86_ABI
            Assert.That(CurrentNestedAlignment(codeGen), Is.EqualTo(0u));
            Assert.That(MaximumNestedAlignment(codeGen), Is.EqualTo(0u));
#endif
#if TARGET_ARM64
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.False);
            Assert.That(codeGen.genForceFuncletFrameType5, Is.False);
            Assert.That(codeGen.genReverseAndPairCalleeSavedRegisters, Is.False);
#endif
#if DEBUG
            Assert.That(CurrentDisplayOffset(codeGen), Is.EqualTo(uint.MaxValue));
#if HAS_FIXED_REGISTER_SET
            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(unchecked((int)Globals.INVALID_POINT_CD)));
#endif
#if TARGET_XARCH
            Assert.That(compiler.compCalleeFPRegsSavedMask, Is.EqualTo(unchecked((regMask)(-1))));
#endif
#endif
        });
    }

#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
    [Test]
    public static void UnsupportedMachinePhaseStillClearsBorrowedOutputAddresses()
    {
        var previousConfig = Globals.JitConfig;
        try
        {
#if TARGET_WASM
            WasmFunclets(ref Globals.JitConfig) = 1;
#endif
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
#endif

#if LATE_DISASM
    [Test]
    public static void LateDisassemblyRejectsBeforeBorrowingOutputsOrStartingAPhase()
    {
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
#endif

#if TARGET_WASM
    [TestCase(0, 0, false)]
    [TestCase(0, 1, false)]
    [TestCase(0, 2, true)]
    [TestCase(0, 3, true)]
    [TestCase(1, 2, false)]
    [TestCase(-1, 2, false)]
    public static void WasmFuncletPredicatePreservesConfigurationAndCount(
        int enabled, ushort count, bool rejected)
    {
        var previousConfig = Globals.JitConfig;
        try
        {
            WasmFunclets(ref Globals.JitConfig) = enabled;
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
        var previousConfig = Globals.JitConfig;
        try
        {
            WasmFunclets(ref Globals.JitConfig) = 0;
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitWasmFunclets")]
    private static extern ref int WasmFunclets(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCheckWasmFunclets")]
    private static extern void CheckWasmFunclets(CodeGen codeGen);

#if DEBUG
    [TestCase("", false)]
    [TestCase("00001233", false)]
    [TestCase("00001234", true)]
    [TestCase("00001230-00001240", true)]
    public static void WasmDebugRangeUsesTheInlineRootAndExcludesEmptyConfiguration(string range, bool rejected)
    {
        var previousConfig = Globals.JitConfig;
        var previousRange = UnsupportedRange(null);
        try
        {
            UnsupportedRange(null) = default;
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
                    Assert.That(UnsupportedRange(null).IsInit, Is.True);
                });
            }
        }
        finally
        {
            UnsupportedRange(null) = previousRange;
            Globals.JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_jitR2RUnsupportedRange")]
    private static extern ref ConfigMethodRange UnsupportedRange(CodeGen? codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitR2RUnsupportedRange")]
    private static extern ref byte* UnsupportedRangeConfig(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compMethodHashPrivate")]
    private static extern ref int MethodHash(ref Compiler.Info info);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCheckWasmR2RRange")]
    private static extern void CheckWasmRange(CodeGen codeGen);
#endif
#endif

    private static void WithGeneration(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
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

#if !TARGET_X86
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stkArgVarNum")]
    private static extern ref int StackArgumentVariable(CodeGen codeGen);
#endif

#if UNIX_X86_ABI
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_curNestedAlignment")]
    private static extern uint CurrentNestedAlignment(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "maxNestedAlignment")]
    private static extern ref uint MaximumNestedAlignment(CodeGen codeGen);
#endif

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_genCurDispOffset")]
    private static extern ref uint CurrentDisplayOffset(CodeGen codeGen);
#endif
}
