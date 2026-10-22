// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "NUnit instantiates test fixtures through reflection.")]
internal sealed unsafe class FloatingRegisterNameTests
{
    [SetUp]
    public void RequireApplicableConfiguration()
    {
        Assume.That(TestConfiguration.IsDebug);
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET);
    }

    [TestCaseSource(nameof(FloatingNameTypeCases))]
    public static void EveryTableEntryUsesTheNativeTypeSelectionAndStableStorage(var_types type)
    {
        var prefix = Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64
            ? ""
            : Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH
                ? type switch
                {
                    TYP_SIMD64 => "z",
                    TYP_SIMD32 => "y",
                    _ => "x",
                }
                : "x";

        for (var number = 0; number < (int)REG_COUNT; number++)
        {
            var reg = (regNumber)number;
            var name = reg.GetFloatName(type);
            Assert.That(name, Is.EqualTo(prefix + reg.Name));
            Assert.That(reg.GetFloatName(type), Is.SameAs(name));
        }
    }

    [NonParallelizable]
    internal static class FloatingRegisterNameReleaseTests
    {
        [Test]
        public static void FloatingRegisterNameAccessIsRejectedOutsideDebug()
        {
            Assume.That(!TestConfiguration.IsDebug);

            Assert.That(
                () => ((regNumber)0).GetFloatName(TYP_FLOAT),
                Throws.TypeOf<System.InvalidOperationException>());
        }
    }

    private static IEnumerable<TestCaseData> FloatingNameTypeCases()
    {
        if (Configuration.TARGET_ARM)
        {
            yield break;
        }

        yield return new TestCaseData(TYP_FLOAT);
        yield return new TestCaseData(TYP_DOUBLE);
        yield return new TestCaseData(TYP_INT);
        yield return new TestCaseData(TYP_STRUCT);
        yield return new TestCaseData(TYP_VOID);

        if (Configuration.FEATURE_SIMD)
        {
            yield return new TestCaseData(TYP_SIMD8);
            yield return new TestCaseData(TYP_SIMD12);
            yield return new TestCaseData(TYP_SIMD16);

            if (Configuration.TARGET_XARCH)
            {
                yield return new TestCaseData(TYP_SIMD32);
                yield return new TestCaseData(TYP_SIMD64);
            }
            else if (Configuration.TARGET_ARM64)
            {
                yield return new TestCaseData(TYP_SIMD_ARM64);
            }
        }

        if (Configuration.FEATURE_SIMD && Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            yield return new TestCaseData(TYP_MASK);
        }
    }

    [TestCaseSource(nameof(RepresentativeNameCases))]
    public static void RepresentativeNamesMatchIndependentNativeLiterals(
        regNumber reg, var_types type, string expected)
    {
        Assert.That(reg.GetFloatName(type), Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> RepresentativeNameCases()
    {
        if (Configuration.TARGET_AMD64)
        {
            yield return new TestCaseData(REG_X64_RAX, TYP_FLOAT, "xrax");
            yield return new TestCaseData(REG_X64_R31, TYP_DOUBLE, "xr31");
            yield return new TestCaseData(REG_X64_XMM0, TYP_FLOAT, "xmm0");
            yield return new TestCaseData(REG_X64_XMM31, TYP_DOUBLE, "xmm31");
            yield return new TestCaseData(REG_X64_K0, TYP_FLOAT, "xk0");
            yield return new TestCaseData(REG_X64_K7, TYP_DOUBLE, "xk7");
            yield return new TestCaseData(REG_X64_STK, TYP_FLOAT, "xSTK");

            if (Configuration.FEATURE_SIMD)
            {
                yield return new TestCaseData(REG_X64_RAX, TYP_SIMD32, "yrax");
                yield return new TestCaseData(REG_X64_RAX, TYP_SIMD64, "zrax");
                yield return new TestCaseData(REG_X64_XMM0, TYP_SIMD32, "ymm0");
                yield return new TestCaseData(REG_X64_XMM31, TYP_SIMD64, "zmm31");
                yield return new TestCaseData(REG_X64_K0, TYP_SIMD32, "yk0");
                yield return new TestCaseData(REG_X64_K7, TYP_SIMD64, "zk7");
                yield return new TestCaseData(REG_X64_STK, TYP_SIMD32, "ySTK");
                yield return new TestCaseData(REG_X64_STK, TYP_SIMD64, "zSTK");
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            yield return new TestCaseData(REG_ARM64_R0, TYP_FLOAT, "x0");
            yield return new TestCaseData(REG_ARM64_IP0, TYP_DOUBLE, "xip0");
            yield return new TestCaseData(REG_ARM64_ZR, TYP_FLOAT, "xzr");
            yield return new TestCaseData(REG_ARM64_V0, TYP_FLOAT, "d0");
            yield return new TestCaseData(REG_ARM64_V31, TYP_DOUBLE, "d31");
            yield return new TestCaseData(REG_ARM64_P0, TYP_FLOAT, "p0");
            yield return new TestCaseData(REG_ARM64_P15, TYP_DOUBLE, "p15");
            yield return new TestCaseData(REG_ARM64_SP, TYP_FLOAT, "sp");
            yield return new TestCaseData(REG_ARM64_FFR, TYP_DOUBLE, "ffr");
            yield return new TestCaseData(REG_ARM64_STK, TYP_FLOAT, "STK");

            if (Configuration.FEATURE_SIMD)
            {
                yield return new TestCaseData(REG_ARM64_V0, TYP_SIMD16, "d0");
                yield return new TestCaseData(REG_ARM64_V31, TYP_SIMD_ARM64, "d31");
                yield return new TestCaseData(REG_ARM64_STK, TYP_SIMD_ARM64, "STK");
            }
        }
        else
        {
            yield break;
        }
    }

    [Test]
    public static void ArmSinglePrecisionUsesEveryFloatBaseName()
    {
        Assume.That(Configuration.TARGET_ARM, "This test requires the ARM target.");

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
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

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
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

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
}
