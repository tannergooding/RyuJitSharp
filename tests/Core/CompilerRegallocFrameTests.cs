// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerRegallocFrameTests
{
    [Test]
    public static void NativeCallsRequireFramePointerFrames()
    {
        WithCompiler((compiler, _) => {
            NativeCallCount(compiler) = 1;

            var requiresFrame = compiler.rpMustCreateEBPFrame(out var reason);

            Assert.That(requiresFrame, Is.True);
#if DEBUG
            Assert.That(reason, Is.EqualTo("Uses PInvoke"));
#else
            Assert.That(reason, Is.Null);
#endif
        });
    }

    [Test]
    public static void MinoptsFrameRequirementMatchesNativePlatformPolicy()
    {
        WithCompiler((compiler, _) => {
            compiler.opts.SetMinOpts(true);

            var requiresFrame = compiler.rpMustCreateEBPFrame(out var reason);

#if ETW_EBP_FRAMED
            Assert.That(requiresFrame, Is.True);
#if DEBUG
            Assert.That(reason, Is.EqualTo("Debug Code"));
#endif
#else
            Assert.That(requiresFrame, Is.False);
            Assert.That(reason, Is.Null);
#endif
        });
    }

    [Test]
    public static void FrameTypeSelectionHonorsRequiredPointerAndReservesItsRegister()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = new LinearScan(compiler);
            codeGen.IsFramePointerRequired = true;

            SetFrameType(allocator);

            Assert.That(RpFrameType(compiler), Is.EqualTo(FrameType.FT_EBP_FRAME));
            Assert.That(codeGen.IsFramePointerUsed, Is.True);
            Assert.That(AvailableIntRegs(allocator) & SRBM_FPBASE, Is.EqualTo(SRBM_NONE));
            Assert.That(FrameCheckWasCalled(compiler), Is.False);
        });
    }

    [Test]
    public static void FrameTypeSelectionHonorsExistingFrameRequirement()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = new LinearScan(compiler);
            codeGen.IsFrameRequired = true;

            SetFrameType(allocator);

            Assert.That(RpFrameType(compiler), Is.EqualTo(FrameType.FT_EBP_FRAME));
            Assert.That(codeGen.IsFrameRequired, Is.True);
            Assert.That(codeGen.IsFramePointerUsed, Is.True);
            Assert.That(AvailableIntRegs(allocator) & SRBM_FPBASE, Is.EqualTo(SRBM_NONE));
            Assert.That(FrameCheckWasCalled(compiler), Is.True);
        });
    }

    [Test]
    public static void FrameTypeSelectionCreatesFrameForNativeCalls()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = new LinearScan(compiler);
            NativeCallCount(compiler) = 1;

            SetFrameType(allocator);

            Assert.That(RpFrameType(compiler), Is.EqualTo(FrameType.FT_EBP_FRAME));
            Assert.That(codeGen.IsFrameRequired, Is.True);
            Assert.That(codeGen.IsFramePointerUsed, Is.True);
            Assert.That(AvailableIntRegs(allocator) & SRBM_FPBASE, Is.EqualTo(SRBM_NONE));
        });
    }

    [Test]
    public static void MinoptsWithoutFrameRequirementsUsesStackPointerFrame()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = new LinearScan(compiler);
            compiler.opts.SetMinOpts(true);

            SetFrameType(allocator);

#if ETW_EBP_FRAMED
            Assert.That(RpFrameType(compiler), Is.EqualTo(FrameType.FT_EBP_FRAME));
            Assert.That(codeGen.IsFramePointerUsed, Is.True);
#else
            Assert.That(RpFrameType(compiler), Is.EqualTo(FrameType.FT_ESP_FRAME));
            Assert.That(codeGen.IsFramePointerUsed, Is.False);
            Assert.That(AvailableIntRegs(allocator) & SRBM_FPBASE, Is.Not.EqualTo(SRBM_NONE));
#endif
            Assert.That(FrameCheckWasCalled(compiler), Is.True);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optNativeCallCount")]
    private static extern ref int NativeCallCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "rpFrameType")]
    private static extern ref FrameType RpFrameType(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "rpMustCreateEBPCalled")]
    private static extern ref bool FrameCheckWasCalled(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setFrameType")]
    private static extern void SetFrameType(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        try
        {
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
