// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 || TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class StackRegisterOperationsTests
{
    private static regMaskTP NonemptyMask => genRegMask(REG_INT_FIRST) | genRegMask(REG_INT_FIRST + 1);

#if DEBUG
    private static readonly List<string?> s_assertions = [];
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyPushClearsBothOutputsIncludingAliasedStorage(bool aliasOutputs)
    {
        WithCodeGen(codeGen =>
        {
            SeedState(codeGen);
            var byrefRegs = NonemptyMask;
            var noRefRegs = NonemptyMask;
            var pushed = aliasOutputs
                ? codeGen.genPushRegs(RBM_NONE, ref byrefRegs, ref byrefRegs)
                : codeGen.genPushRegs(RBM_NONE, ref byrefRegs, ref noRefRegs);

            Assert.That(pushed, Is.EqualTo(RBM_NONE));
            Assert.That(byrefRegs, Is.EqualTo(RBM_NONE));
            Assert.That(noRefRegs, Is.EqualTo(aliasOutputs ? NonemptyMask : RBM_NONE));
            AssertUnchangedState(codeGen);
            AssertNoDiagnostics();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyPopReturnsBeforeSubsetAndLiveGcAssertions(bool inconsistentMasks)
    {
        WithCodeGen(codeGen =>
        {
            SeedState(codeGen);
            var mask = inconsistentMasks ? NonemptyMask : RBM_NONE;

            codeGen.genPopRegs(RBM_NONE, mask, mask);

            AssertUnchangedState(codeGen);
            AssertNoDiagnostics();
        });
    }

    [TestCase(1u)]
    [TestCase(2u)]
    [TestCase(7u)]
    [TestCase(4096u)]
    public static void SinglePopSubtractsExactlyOneTargetRegisterWord(uint words)
    {
        WithCodeGen(codeGen =>
        {
            SeedState(codeGen);
            StackLevel(codeGen) = words * REGSIZE_BYTES;

            codeGen.genSinglePop();

            Assert.That(codeGen.getCurrentStackLevel(), Is.EqualTo((words - 1) * REGSIZE_BYTES));
            AssertGcAndEmitterUnchanged(codeGen);
            AssertNoDiagnostics();
        });
    }

#if FEATURE_FIXED_OUT_ARGS
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void NonemptyMasksKeepExactNyiAndConfiguredContinuation(bool push, bool continueAfterNyi)
    {
        WithCodeGen(codeGen =>
        {
            SeedState(codeGen);
#if DEBUG
            NyiConfiguration(ref JitConfig) = continueAfterNyi ? 3 : 1;
#else
            NyiConfiguration(ref JitConfig) = continueAfterNyi ? 2 : 0;
#endif
            var byrefRegs = NonemptyMask;
            var noRefRegs = NonemptyMask;
            var pushed = NonemptyMask;

            void Operate()
            {
                if (push)
                {
                    pushed = codeGen.genPushRegs(NonemptyMask, ref byrefRegs, ref noRefRegs);
                }
                else
                {
                    codeGen.genPopRegs(NonemptyMask, byrefRegs, noRefRegs);
                }
            }

            if (continueAfterNyi)
            {
                Operate();
            }
            else
            {
                var failure = Assert.Throws<FatalJitException>(Operate);
                Assert.That(failure, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_SKIPPED));
            }

            Assert.That(byrefRegs, Is.EqualTo(push ? RBM_NONE : NonemptyMask));
            Assert.That(noRefRegs, Is.EqualTo(push ? RBM_NONE : NonemptyMask));
            Assert.That(pushed, Is.EqualTo(push && continueAfterNyi ? RBM_NONE : NonemptyMask));
            AssertUnchangedState(codeGen);
#if DEBUG
            string[] expectedAssertions = [
                push ? "NYI: Don't call genPushRegs with real regs!" : "NYI: Don't call genPopRegs with real regs!",
            ];
            Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
#endif
        });
    }
#endif

    private static void SeedState(CodeGen codeGen)
    {
        StackLevel(codeGen) = 3u * REGSIZE_BYTES;
        codeGen.GCInfo.gcRegGCrefSetCur = genRegMask(REG_INT_FIRST);
        codeGen.GCInfo.gcRegByrefSetCur = genRegMask(REG_INT_FIRST + 1);
    }

    private static void AssertUnchangedState(CodeGen codeGen)
    {
        Assert.That(codeGen.getCurrentStackLevel(), Is.EqualTo(3u * REGSIZE_BYTES));
        AssertGcAndEmitterUnchanged(codeGen);
    }

    private static void AssertGcAndEmitterUnchanged(CodeGen codeGen)
    {
        Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(genRegMask(REG_INT_FIRST)));
        Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(genRegMask(REG_INT_FIRST + 1)));
        Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
    }

    private static void AssertNoDiagnostics()
    {
#if DEBUG
        Assert.That(s_assertions, Is.Empty);
#endif
    }

    private static void WithCodeGen(Action<CodeGen> action)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
#if FUNC_INFO_LOGGING
        var previousFunctionLog = Compiler.compJitFuncInfoFile;
#endif
#if MEASURE_FATAL
        var previousNyiCount = s_fatalNyiCount;
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;

        try
        {
            JitConfig = default;
            JitTls.Compiler = compiler;
#if FUNC_INFO_LOGGING
            Compiler.compJitFuncInfoFile = null;
#endif
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(codeGen);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
#if FUNC_INFO_LOGGING
            Compiler.compJitFuncInfoFile = previousFunctionLog;
#endif
#if MEASURE_FATAL
            s_fatalNyiCount = previousNyiCount;
#endif
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitAssertOnNYI")]
    private static extern ref int NyiConfiguration(ref JitConfigValues config);

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));

        return 0;
    }
#endif
}
#endif
