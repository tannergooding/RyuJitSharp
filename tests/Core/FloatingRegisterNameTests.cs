// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG && HAS_FIXED_REGISTER_SET
#if TARGET_ARM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FloatingRegisterNameTests
{
#if !TARGET_ARM
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_STRUCT)]
    [TestCase(TYP_VOID)]
#if FEATURE_SIMD
    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
#if TARGET_XARCH
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
#elif TARGET_ARM64
    [TestCase(TYP_SIMD_ARM64)]
#endif
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_MASK)]
#endif
#endif
    public static void EveryTableEntryUsesTheNativeTypeSelectionAndStableStorage(var_types type)
    {
#if TARGET_ARM64 || TARGET_LOONGARCH64
        const string prefix = "";
#elif FEATURE_SIMD && TARGET_XARCH
        var prefix = type switch {
            TYP_SIMD64 => "z",
            TYP_SIMD32 => "y",
            _ => "x",
        };
#else
        const string prefix = "x";
#endif
        for (var number = 0; number < (int)REG_COUNT; number++)
        {
            var reg = (regNumber)number;
            var name = reg.GetFloatName(type);
            Assert.That(name, Is.EqualTo(prefix + reg.Name));
            Assert.That(reg.GetFloatName(type), Is.SameAs(name));
        }
    }

#if TARGET_AMD64
    [TestCase(REG_X64_RAX, TYP_FLOAT, "xrax")]
    [TestCase(REG_X64_R31, TYP_DOUBLE, "xr31")]
    [TestCase(REG_X64_XMM0, TYP_FLOAT, "xmm0")]
    [TestCase(REG_X64_XMM31, TYP_DOUBLE, "xmm31")]
    [TestCase(REG_X64_K0, TYP_FLOAT, "xk0")]
    [TestCase(REG_X64_K7, TYP_DOUBLE, "xk7")]
    [TestCase(REG_X64_STK, TYP_FLOAT, "xSTK")]
#if FEATURE_SIMD
    [TestCase(REG_X64_RAX, TYP_SIMD32, "yrax")]
    [TestCase(REG_X64_RAX, TYP_SIMD64, "zrax")]
    [TestCase(REG_X64_XMM0, TYP_SIMD32, "ymm0")]
    [TestCase(REG_X64_XMM31, TYP_SIMD64, "zmm31")]
    [TestCase(REG_X64_K0, TYP_SIMD32, "yk0")]
    [TestCase(REG_X64_K7, TYP_SIMD64, "zk7")]
    [TestCase(REG_X64_STK, TYP_SIMD32, "ySTK")]
    [TestCase(REG_X64_STK, TYP_SIMD64, "zSTK")]
#endif
#elif TARGET_ARM64
    [TestCase(REG_ARM64_R0, TYP_FLOAT, "x0")]
    [TestCase(REG_ARM64_IP0, TYP_DOUBLE, "xip0")]
    [TestCase(REG_ARM64_ZR, TYP_FLOAT, "xzr")]
    [TestCase(REG_ARM64_V0, TYP_FLOAT, "d0")]
    [TestCase(REG_ARM64_V31, TYP_DOUBLE, "d31")]
    [TestCase(REG_ARM64_P0, TYP_FLOAT, "p0")]
    [TestCase(REG_ARM64_P15, TYP_DOUBLE, "p15")]
    [TestCase(REG_ARM64_SP, TYP_FLOAT, "sp")]
    [TestCase(REG_ARM64_FFR, TYP_DOUBLE, "ffr")]
    [TestCase(REG_ARM64_STK, TYP_FLOAT, "STK")]
#if FEATURE_SIMD
    [TestCase(REG_ARM64_V0, TYP_SIMD16, "d0")]
    [TestCase(REG_ARM64_V31, TYP_SIMD_ARM64, "d31")]
    [TestCase(REG_ARM64_STK, TYP_SIMD_ARM64, "STK")]
#endif
#endif
#if TARGET_AMD64 || TARGET_ARM64
    public static void RepresentativeNamesMatchIndependentNativeLiterals(regNumber reg, var_types type, string expected)
    {
        Assert.That(reg.GetFloatName(type), Is.EqualTo(expected));
    }
#endif
#else
    [Test]
    public static void ArmSinglePrecisionUsesEveryFloatBaseName()
    {
        for (var number = (int)REG_FP_FIRST; number <= (int)REG_FP_LAST; number++)
        {
            var reg = (regNumber)number;
            var name = reg.GetFloatName(TYP_FLOAT);
            Assert.That(name, Is.EqualTo(reg.Name));
            Assert.That(reg.GetFloatName(TYP_FLOAT), Is.SameAs(name));
        }

        Assert.That(REG_FP_FIRST.GetFloatName(TYP_FLOAT), Is.EqualTo("f0"));
        Assert.That(REG_FP_LAST.GetFloatName(TYP_FLOAT), Is.EqualTo("f31"));
    }

    private static IEnumerable<TestCaseData> ArmEvenDoubleRegisterNameCases()
    {
        yield return new TestCaseData(REG_F0, "d0");
        yield return new TestCaseData(REG_F2, "d2");
        yield return new TestCaseData(REG_F4, "d4");
        yield return new TestCaseData(REG_F6, "d6");
        yield return new TestCaseData(REG_F8, "d8");
        yield return new TestCaseData(REG_F10, "d10");
        yield return new TestCaseData(REG_F12, "d12");
        yield return new TestCaseData(REG_F14, "d14");
        yield return new TestCaseData(REG_F16, "d16");
        yield return new TestCaseData(REG_F18, "d18");
        yield return new TestCaseData(REG_F20, "d20");
        yield return new TestCaseData(REG_F22, "d22");
        yield return new TestCaseData(REG_F24, "d24");
        yield return new TestCaseData(REG_F26, "d26");
        yield return new TestCaseData(REG_F28, "d28");
        yield return new TestCaseData(REG_F30, "d30");
    }

    [TestCaseSource(nameof(ArmEvenDoubleRegisterNameCases))]
    public static void ArmEvenRegistersUseTheExactNativeDoubleLiterals(regNumber reg, string expected)
    {
        Assert.That(reg.GetFloatName(TYP_DOUBLE), Is.EqualTo(expected));
        Assert.That(reg.GetFloatName(TYP_STRUCT), Is.EqualTo(expected));
        Assert.That(reg.GetFloatName(TYP_INT), Is.EqualTo(expected));
    }

    private static readonly List<string?> s_assertions = [];

    private static IEnumerable<TestCaseData> ArmInvalidStorageRegisterCases()
    {
        yield return new TestCaseData(REG_F1, TYP_DOUBLE, "d??", "!\"Bad double register\"", null);
        yield return new TestCaseData(REG_FP_LAST, TYP_STRUCT, "d??", "!\"Bad double register\"", null);
        yield return new TestCaseData(REG_INT_FIRST, TYP_DOUBLE, "d??", "genIsValidFloatReg(reg)", "!\"Bad double register\"");
        yield return new TestCaseData(REG_INT_FIRST, TYP_FLOAT, "r0", "genIsValidFloatReg(reg)", null);
    }

    [TestCaseSource(nameof(ArmInvalidStorageRegisterCases))]
    public static void ArmInStorageInvalidRegistersRetainAssertionOrderAndContinuation(
        regNumber reg, var_types type, string expectedName, string firstAssertion, string? secondAssertion)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = new() { doAssert = &RecordAssertion };
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();

        Assert.That(reg.GetFloatName(type), Is.EqualTo(expectedName));
        Assert.That(s_assertions.Count, Is.EqualTo(secondAssertion is null ? 1 : 2));
        Assert.That(s_assertions[0], Is.EqualTo(firstAssertion));
        if (secondAssertion is not null)
        {
            Assert.That(s_assertions[1], Is.EqualTo(secondAssertion));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }
#endif
}
#endif
