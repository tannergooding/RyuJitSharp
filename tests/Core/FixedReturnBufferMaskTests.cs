// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FixedReturnBufferMaskTests
{
    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.C)]
    [TestCase(CorInfoCallConvExtension.Stdcall)]
    [TestCase(CorInfoCallConvExtension.Thiscall)]
    [TestCase(CorInfoCallConvExtension.Fastcall)]
    [TestCase(CorInfoCallConvExtension.CMemberFunction)]
    [TestCase(CorInfoCallConvExtension.StdcallMemberFunction)]
    [TestCase(CorInfoCallConvExtension.FastcallMemberFunction)]
    [TestCase(CorInfoCallConvExtension.Swift)]
    public static void FullArgumentMaskIncludesOnlyNativeConventionRegisters(CorInfoCallConvExtension callConv)
    {
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET);
        var expected = new regMaskTP(SRBM_ARG_REGS);

        if (Configuration.TARGET_ARM64 && (!TargetOS.IsWindows || !callConvIsInstanceMethodCallConv(callConv)))
        {
            var retBuffer = new regMaskTP(SRBM_ARG_RET_BUFF);
            Assert.That(theFixedRetBuffMask(callConv), Is.EqualTo(retBuffer));
            Assert.That(PopCount(retBuffer), Is.EqualTo(1));
            expected |= retBuffer;
        }
        else if (Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT &&
            callConv == CorInfoCallConvExtension.Swift)
        {
            var retBuffer = new regMaskTP(SRBM_SWIFT_ARG_RET_BUFF);
            Assert.That(theFixedRetBuffMask(callConv), Is.EqualTo(retBuffer));
            Assert.That(PopCount(retBuffer), Is.EqualTo(1));
            expected |= retBuffer;
        }

        if (Configuration.SWIFT_SUPPORT && callConv == CorInfoCallConvExtension.Swift)
        {
            expected |= Globals.genRegMask(TargetRuntimeMetadata.SwiftSelfRegister) |
                Globals.genRegMask(TargetRuntimeMetadata.SwiftErrorRegister);
        }

        Assert.That(fullIntArgRegMask(callConv), Is.EqualTo(expected));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.That(fullIntArgRegMask(callConv).Upper, Is.EqualTo((regMask)0));
        }
    }

    private static readonly List<string?> s_assertions = [];

    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.C)]
    [TestCase(CorInfoCallConvExtension.Thiscall)]
    public static void InvalidFixedBufferRequestPreservesNativeAssertionSequence(CorInfoCallConvExtension callConv)
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.TARGET_AMD64);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();

        var result = theFixedRetBuffMask(callConv);

        string[] expectedAssertions = Configuration.SWIFT_SUPPORT
            ? ["hasFixedRetBuffReg(callConv)", "callConv == CorInfoCallConvExtension::Swift"]
            : ["hasFixedRetBuffReg(callConv)"];
        Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
        Assert.That(result, Is.EqualTo(Configuration.SWIFT_SUPPORT
            ? new regMaskTP(SRBM_SWIFT_ARG_RET_BUFF)
            : RBM_NONE));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }
}
