// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;
#if DEBUG && TARGET_AMD64
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif

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
        var expected = new regMaskTP(SRBM_ARG_REGS);
#if TARGET_ARM64
        if (!TargetOS.IsWindows || !callConvIsInstanceMethodCallConv(callConv))
        {
            var retBuffer = new regMaskTP(SRBM_ARG_RET_BUFF);
            Assert.That(theFixedRetBuffMask(callConv), Is.EqualTo(retBuffer));
            Assert.That(PopCount(retBuffer), Is.EqualTo(1));
            expected |= retBuffer;
        }
#elif TARGET_AMD64 && SWIFT_SUPPORT
        if (callConv == CorInfoCallConvExtension.Swift)
        {
            var retBuffer = new regMaskTP(SRBM_SWIFT_ARG_RET_BUFF);
            Assert.That(theFixedRetBuffMask(callConv), Is.EqualTo(retBuffer));
            Assert.That(PopCount(retBuffer), Is.EqualTo(1));
            expected |= retBuffer;
        }
#endif

#if SWIFT_SUPPORT
        if (callConv == CorInfoCallConvExtension.Swift)
        {
            expected |= new regMaskTP(SRBM_SWIFT_SELF | SRBM_SWIFT_ERROR);
        }
#endif

        Assert.That(fullIntArgRegMask(callConv), Is.EqualTo(expected));
#if HAS_MORE_THAN_64_REGISTERS
        Assert.That(fullIntArgRegMask(callConv).Upper, Is.EqualTo((regMask)0));
#endif
    }

#if DEBUG && TARGET_AMD64
    private static readonly List<string?> s_assertions = [];

    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.C)]
    [TestCase(CorInfoCallConvExtension.Thiscall)]
    public static void InvalidFixedBufferRequestPreservesNativeAssertionSequence(CorInfoCallConvExtension callConv)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();

        var result = theFixedRetBuffMask(callConv);

        Assert.That(s_assertions, Is.EqualTo<string[]>([
            "hasFixedRetBuffReg(callConv)",
#if SWIFT_SUPPORT
            "callConv == CorInfoCallConvExtension::Swift",
#endif
        ]));
#if SWIFT_SUPPORT
        Assert.That(result, Is.EqualTo(new regMaskTP(SRBM_SWIFT_ARG_RET_BUFF)));
#else
        Assert.That(result, Is.EqualTo(RBM_NONE));
#endif
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }
#endif
}
