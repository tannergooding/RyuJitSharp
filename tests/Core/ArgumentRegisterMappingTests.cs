// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ArgumentRegisterMappingTests
{
    [SetUp]
    public static void RequireFixedRegisterSet()
    {
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET, "This fixture requires fixed argument registers.");
    }

    private static readonly CorInfoCallConvExtension[] s_callConventions = [
        CorInfoCallConvExtension.Managed,
        CorInfoCallConvExtension.C,
        CorInfoCallConvExtension.Stdcall,
        CorInfoCallConvExtension.Thiscall,
        CorInfoCallConvExtension.Fastcall,
        CorInfoCallConvExtension.CMemberFunction,
        CorInfoCallConvExtension.StdcallMemberFunction,
        CorInfoCallConvExtension.FastcallMemberFunction,
        CorInfoCallConvExtension.Swift,
    ];

    private static IEnumerable<TestCaseData> IntegerArguments()
    {
        (regNumber Reg, regMaskTP Mask)[] expected;
        if (Configuration.TARGET_AMD64)
        {
            expected = Configuration.UNIX_AMD64_ABI
                ? [(REG_EDI, RBM_EDI), (REG_ESI, RBM_ESI), (REG_EDX, RBM_EDX), (REG_ECX, RBM_ECX),
                    (REG_R8, RBM_R8), (REG_R9, RBM_R9)]
                : [(REG_ECX, RBM_ECX), (REG_EDX, RBM_EDX), (REG_R8, RBM_R8), (REG_R9, RBM_R9)];
        }
        else if (Configuration.TARGET_X86)
        {
            expected = [(REG_ECX, RBM_ECX), (REG_EDX, RBM_EDX)];
        }
        else if (Configuration.TARGET_ARM)
        {
            expected = [(REG_R0, RBM_R0), (REG_R1, RBM_R1), (REG_R2, RBM_R2), (REG_R3, RBM_R3)];
        }
        else if (Configuration.TARGET_ARM64)
        {
            expected = [
                (REG_ARM64_R0, RBM_ARM64_R0), (REG_ARM64_R1, RBM_ARM64_R1),
                (REG_ARM64_R2, RBM_ARM64_R2), (REG_ARM64_R3, RBM_ARM64_R3),
                (REG_ARM64_R4, RBM_ARM64_R4), (REG_ARM64_R5, RBM_ARM64_R5),
                (REG_ARM64_R6, RBM_ARM64_R6), (REG_ARM64_R7, RBM_ARM64_R7),
            ];
        }
        else if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            expected = [
                (REG_A0, RBM_A0), (REG_A1, RBM_A1), (REG_A2, RBM_A2), (REG_A3, RBM_A3),
                (REG_A4, RBM_A4), (REG_A5, RBM_A5), (REG_A6, RBM_A6), (REG_A7, RBM_A7),
            ];
        }
        else
        {
            expected = [(REG_NA, RBM_NONE)];
        }

        for (var index = 0; index < expected.Length; index++)
        {
            yield return new TestCaseData((uint)index, expected[index].Reg, expected[index].Mask);
        }
    }

    private static IEnumerable<TestCaseData> FloatingArguments()
    {
        (regNumber Reg, regMaskTP Mask)[] expected;
        if (Configuration.TARGET_AMD64)
        {
            expected = Configuration.UNIX_AMD64_ABI
                ? [
                    (REG_XMM0, RBM_XMM0), (REG_XMM1, RBM_XMM1), (REG_XMM2, RBM_XMM2), (REG_XMM3, RBM_XMM3),
                    (REG_XMM4, RBM_XMM4), (REG_XMM5, RBM_XMM5), (REG_XMM6, RBM_XMM6), (REG_XMM7, RBM_XMM7),
                ]
                : [(REG_XMM0, RBM_XMM0), (REG_XMM1, RBM_XMM1), (REG_XMM2, RBM_XMM2), (REG_XMM3, RBM_XMM3)];
        }
        else if (Configuration.TARGET_ARM64)
        {
            expected = [
                (REG_ARM64_V0, RBM_ARM64_V0), (REG_ARM64_V1, RBM_ARM64_V1),
                (REG_ARM64_V2, RBM_ARM64_V2), (REG_ARM64_V3, RBM_ARM64_V3),
                (REG_ARM64_V4, RBM_ARM64_V4), (REG_ARM64_V5, RBM_ARM64_V5),
                (REG_ARM64_V6, RBM_ARM64_V6), (REG_ARM64_V7, RBM_ARM64_V7),
            ];
        }
        else if (Configuration.TARGET_ARM)
        {
            expected = [
                (REG_F0, RBM_F0), (REG_F1, RBM_F1), (REG_F2, RBM_F2), (REG_F3, RBM_F3),
                (REG_F4, RBM_F4), (REG_F5, RBM_F5), (REG_F6, RBM_F6), (REG_F7, RBM_F7),
                (REG_F8, RBM_F8), (REG_F9, RBM_F9), (REG_F10, RBM_F10), (REG_F11, RBM_F11),
                (REG_F12, RBM_F12), (REG_F13, RBM_F13), (REG_F14, RBM_F14), (REG_F15, RBM_F15),
            ];
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            expected = [
                (REG_F0, RBM_F0), (REG_F1, RBM_F1), (REG_F2, RBM_F2), (REG_F3, RBM_F3),
                (REG_F4, RBM_F4), (REG_F5, RBM_F5), (REG_F6, RBM_F6), (REG_F7, RBM_F7),
            ];
        }
        else if (Configuration.TARGET_RISCV64)
        {
            expected = [
                (REG_FA0, RBM_FA0), (REG_FA1, RBM_FA1), (REG_FA2, RBM_FA2), (REG_FA3, RBM_FA3),
                (REG_FA4, RBM_FA4), (REG_FA5, RBM_FA5), (REG_FA6, RBM_FA6), (REG_FA7, RBM_FA7),
            ];
        }
        else
        {
            expected = [(REG_NA, RBM_NONE)];
        }

        for (var index = 0; index < expected.Length; index++)
        {
            yield return new TestCaseData((uint)index, expected[index].Reg, expected[index].Mask);
        }
    }

    [TestCaseSource(nameof(IntegerArguments))]
    public static void IntegerIndicesPreserveTableOrderMasksAndOrdinaryConventionRoundTrips(
        uint index, regNumber expectedReg, regMaskTP expectedMask)
    {
        WithoutNativeAssertions(() => {
            Assert.That(IntArgRegs[(int)index], Is.EqualTo(expectedReg));
            Assert.That(genMapIntRegArgNumToRegMask(index), Is.EqualTo(expectedMask));
            Assert.That(genMapArgNumToRegMask(index, TYP_INT), Is.EqualTo(expectedMask));
            AssertNormalBank(expectedMask);

            foreach (var callConv in s_callConventions)
            {
                Assert.That(genMapIntRegArgNumToRegNum(index, callConv), Is.EqualTo(expectedReg));
                Assert.That(genMapRegArgNumToRegNum(index, TYP_INT, callConv), Is.EqualTo(expectedReg));
                if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
                {
                    Assert.That(genMapRegNumToRegArgNum(expectedReg, TYP_INT, callConv), Is.EqualTo(index));
                }
            }
        });
    }

    [TestCaseSource(nameof(FloatingArguments))]
    public static void FloatingIndicesUseSingleRegisterMasksAndRoundTripAcrossConventions(
        uint index, regNumber expectedReg, regMaskTP expectedMask)
    {
        Assume.That(!Configuration.TARGET_X86);
        WithoutNativeAssertions(() => {
            Assert.That(FltArgRegs[(int)index], Is.EqualTo(expectedReg));
            Assert.That(genMapFloatRegArgNumToRegNum(index), Is.EqualTo(expectedReg));
            Assert.That(genMapFloatRegArgNumToRegMask(index), Is.EqualTo(expectedMask));
            Assert.That(genMapArgNumToRegMask(index, TYP_FLOAT), Is.EqualTo(expectedMask));
            Assert.That(genMapFloatRegNumToRegArgNum(expectedReg), Is.EqualTo(index));
            AssertNormalBank(expectedMask);

            foreach (var callConv in s_callConventions)
            {
                Assert.That(genMapRegArgNumToRegNum(index, TYP_FLOAT, callConv), Is.EqualTo(expectedReg));
                Assert.That(genMapRegNumToRegArgNum(expectedReg, TYP_FLOAT, callConv), Is.EqualTo(index));
            }
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_STRUCT)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD_ARM64)]
    [TestCase(TYP_MASK)]
    public static void TypeDispatchUsesFloatingRegistersOnlyForNativeArgumentTypes(var_types type)
    {
        Assume.That(type is not (TYP_FLOAT or TYP_DOUBLE) || !Configuration.TARGET_X86);
        Assume.That(type is not (TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 or TYP_SIMD_ARM64)
            || Configuration.FEATURE_SIMD);
        Assume.That(type != TYP_SIMD_ARM64 || Configuration.TARGET_ARM64);
        Assume.That(type != TYP_MASK || Configuration.FEATURE_MASKED_HW_INTRINSICS);

        var usesFloat = type is TYP_FLOAT or TYP_DOUBLE;
        if (Configuration.TARGET_ARM64
            && type is TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 or TYP_SIMD_ARM64)
        {
            usesFloat = true;
        }

        regNumber expectedInt;
        regMaskTP expectedIntMask;
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
            expectedInt = REG_ECX;
            expectedIntMask = RBM_ECX;
            if (Configuration.UNIX_AMD64_ABI)
            {
                expectedInt = REG_EDI;
                expectedIntMask = RBM_EDI;
            }
        }
        else if (Configuration.TARGET_ARM)
        {
            expectedInt = REG_R0;
            expectedIntMask = RBM_R0;
        }
        else if (Configuration.TARGET_ARM64)
        {
            expectedInt = REG_ARM64_R0;
            expectedIntMask = RBM_ARM64_R0;
        }
        else
        {
            expectedInt = REG_A0;
            expectedIntMask = RBM_A0;
        }

        regNumber expectedFloat;
        regMaskTP expectedFloatMask;
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
            expectedFloat = REG_XMM0;
            expectedFloatMask = RBM_XMM0;
        }
        else if (Configuration.TARGET_ARM64)
        {
            expectedFloat = REG_ARM64_V0;
            expectedFloatMask = RBM_ARM64_V0;
        }
        else if (Configuration.TARGET_RISCV64)
        {
            expectedFloat = REG_FA0;
            expectedFloatMask = RBM_FA0;
        }
        else
        {
            expectedFloat = REG_F0;
            expectedFloatMask = RBM_F0;
        }

        var expectedReg = usesFloat ? expectedFloat : expectedInt;
        var expectedMask = usesFloat ? expectedFloatMask : expectedIntMask;
        if (Configuration.TARGET_ARM && type == TYP_DOUBLE)
        {
            expectedMask = RBM_F0 | RBM_F1;
        }

        WithoutNativeAssertions(() => {
            Assert.That(genMapArgNumToRegMask(0, type), Is.EqualTo(expectedMask));
            foreach (var callConv in s_callConventions)
            {
                Assert.That(genMapRegArgNumToRegNum(0, type, callConv), Is.EqualTo(expectedReg));
                if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
                {
                    Assert.That(genMapRegNumToRegArgNum(expectedReg, type, callConv), Is.Zero);
                }
            }
        });
    }

    [TestCase(CorInfoCallConvExtension.Managed, false)]
    [TestCase(CorInfoCallConvExtension.C, false)]
    [TestCase(CorInfoCallConvExtension.Stdcall, false)]
    [TestCase(CorInfoCallConvExtension.Thiscall, true)]
    [TestCase(CorInfoCallConvExtension.Fastcall, false)]
    [TestCase(CorInfoCallConvExtension.CMemberFunction, true)]
    [TestCase(CorInfoCallConvExtension.StdcallMemberFunction, true)]
    [TestCase(CorInfoCallConvExtension.FastcallMemberFunction, true)]
    [TestCase(CorInfoCallConvExtension.Swift, false)]
    public static void FixedBufferSpecialIndexIsUnsignedAndDoesNotReplaceOrdinaryArguments(
        CorInfoCallConvExtension callConv, bool instanceConvention)
    {
        bool expectedFixed;
        uint expectedIndex = 0;
        var expectedReg = REG_NA;
        if (Configuration.TARGET_ARM64)
        {
            expectedFixed = !TargetOS.IsWindows || !instanceConvention;
            expectedIndex = 8;
            expectedReg = REG_ARM64_R8;
        }
        else if (Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT)
        {
            expectedFixed = callConv == CorInfoCallConvExtension.Swift;
            expectedIndex = Configuration.UNIX_AMD64_ABI ? 6u : 4u;
            expectedReg = REG_X64_RAX;
        }
        else
        {
            expectedFixed = false;
        }

        WithoutNativeAssertions(() => {
            Assert.That(hasFixedRetBuffReg(callConv), Is.EqualTo(expectedFixed));
            if ((Configuration.TARGET_ARM64 || (Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT))
                && expectedFixed)
            {
                Assert.That(unchecked((uint)theFixedRetBuffArgNum(callConv)), Is.EqualTo(expectedIndex));
                Assert.That(genMapIntRegArgNumToRegNum(expectedIndex, callConv), Is.EqualTo(expectedReg));
                Assert.That(genMapRegArgNumToRegNum(expectedIndex, TYP_STRUCT, callConv), Is.EqualTo(expectedReg));
                Assert.That(genMapRegNumToRegArgNum(expectedReg, TYP_STRUCT, callConv), Is.EqualTo(expectedIndex));
            }
            Assert.That(genMapIntRegArgNumToRegNum(0, callConv), Is.EqualTo(IntArgRegs[0]));
        });
    }

    private static IEnumerable<TestCaseData> ArmDoubleArgumentCases()
    {
        yield return new TestCaseData(0U, REG_ARM32_F0, REG_ARM32_F1);
        yield return new TestCaseData(2U, REG_ARM32_F2, REG_ARM32_F3);
        yield return new TestCaseData(4U, REG_ARM32_F4, REG_ARM32_F5);
        yield return new TestCaseData(6U, REG_ARM32_F6, REG_ARM32_F7);
        yield return new TestCaseData(8U, REG_ARM32_F8, REG_ARM32_F9);
        yield return new TestCaseData(10U, REG_ARM32_F10, REG_ARM32_F11);
        yield return new TestCaseData(12U, REG_ARM32_F12, REG_ARM32_F13);
        yield return new TestCaseData(14U, REG_ARM32_F14, REG_ARM32_F15);
    }

    [TestCaseSource(nameof(ArmDoubleArgumentCases))]
    public static void ArmDoubleArgumentsExpandOnlyTheEvenLowRegister(uint index, regNumber low, regNumber high)
    {
        Assume.That(Configuration.TARGET_ARM);
        var expected = new regMaskTP(unchecked((regMask)((1UL << (int)low) | (1UL << (int)high))));
        WithoutNativeAssertions(() => Assert.That(genMapArgNumToRegMask(index, TYP_DOUBLE), Is.EqualTo(expected)));
    }

    private static void AssertNormalBank(regMaskTP mask)
    {
        Assert.That(mask.Lower, Is.Not.EqualTo(SRBM_NONE));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.That(mask.Upper, Is.EqualTo(SRBM_NONE));
        }
    }

    private static void WithoutNativeAssertions(Action action)
    {
        if (TestConfiguration.IsDebug)
        {
            WithAssertionRecorder(action);
            Assert.That(s_assertions, Is.Empty);
        }
        else
        {
            action();
        }
    }

    private static readonly List<string?> s_assertions = [];

    [TestCase(0, false, "argNum < ArrLen(intArgRegs)")]
    [TestCase(0, true, "argNum < ArrLen(intArgRegs)")]
    [TestCase(1, false, "argNum < ArrLen(intArgMasks)")]
    [TestCase(1, true, "argNum < ArrLen(intArgMasks)")]
    [TestCase(2, false, "argNum < ArrLen(fltArgRegs)")]
    [TestCase(2, true, "argNum < ArrLen(fltArgRegs)")]
    [TestCase(3, false, "argNum < ArrLen(fltArgMasks)")]
    [TestCase(3, true, "argNum < ArrLen(fltArgMasks)")]
    public static void InvalidTableIndicesReportTheNativeBoundsBeforeManagedIndexing(
        int mapper, bool maximumIndex, string expected)
    {
        Assume.That(TestConfiguration.IsDebug);
        if (mapper >= 2)
        {
            Assume.That(!Configuration.TARGET_X86);
        }

        var length = (uint)IntArgRegs.Length;
        if (mapper >= 2)
        {
            length = (uint)FltArgRegs.Length;
        }
        // The first index beyond the integer table can be a valid fixed buffer on ARM64.
        var index = maximumIndex ? uint.MaxValue : length + (mapper == 0 ? 1U : 0U);
        WithAssertionRecorder(() => Assert.Throws<IndexOutOfRangeException>(() => {
            switch (mapper)
            {
                case 0:
                {
                    _ = genMapIntRegArgNumToRegNum(index, CorInfoCallConvExtension.StdcallMemberFunction);
                    break;
                }

                case 1:
                {
                    _ = genMapIntRegArgNumToRegMask(index);
                    break;
                }

                case 2:
                {
                    _ = genMapFloatRegArgNumToRegNum(index);
                    break;
                }

                case 3:
                {
                    _ = genMapFloatRegArgNumToRegMask(index);
                    break;
                }
            }
        }));
        string[] expectedAssertions = [expected];

        Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
    }

    private static IEnumerable<TestCaseData> FloatingReverseCases()
    {
        yield return new TestCaseData(TargetRuntimeMetadata.IntegerRegisterFirst);
        yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterLast);
    }

    [TestCaseSource(nameof(FloatingReverseCases))]
    public static void FloatingReverseReportsInvalidArgumentMembershipWithoutObservingItsNumericContinuation(
        regNumber reg)
    {
        Assume.That(TestConfiguration.IsDebug);
        WithAssertionRecorder(() => _ = genMapFloatRegNumToRegArgNum(reg));
        List<string> expected = ["genRegMask(regNum) & RBM_FLTARG_REGS"];
        if (Configuration.TARGET_X86)
        {
            expected.Add("!\"flt reg args not allowed\"");
        }
        else if (Configuration.TARGET_AMD64 && !Configuration.UNIX_AMD64_ABI)
        {
            expected.Add("!\"invalid register arg register\"");
        }
        Assert.That(s_assertions, Is.EqualTo(expected));
    }

    [Test]
    public static void GenericIntegerReverseKeepsTheExistingHelpersDiagnostics()
    {
        Assume.That(TestConfiguration.IsDebug);
        Assume.That(Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64);
        WithAssertionRecorder(() => _ = genMapRegNumToRegArgNum(REG_FP_FIRST, TYP_INT, CorInfoCallConvExtension.Managed));
        string[] expected = [
            "(regMaskTP.CreateFromRegNum(regNum, regNum.SingleTypeMask) & fullIntArgRegMask(callConv)) != RBM_NONE",
            "invalid register arg register",
        ];
        Assert.That(s_assertions, Is.EqualTo(expected));
    }

    [TestCase(1U)]
    [TestCase(3U)]
    [TestCase(15U)]
    public static void ArmDoubleArgumentsDiagnoseAnOddLowRegister(uint index)
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.TARGET_ARM);
        WithAssertionRecorder(() => _ = genMapArgNumToRegMask(index, TYP_DOUBLE));
        string[] expected = ["(result & RBM_ALLDOUBLE) != 0"];
        Assert.That(s_assertions, Is.EqualTo(expected));
    }

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
        action();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));

        return 0;
    }

    [TestCase(0U)]
    [TestCase(uint.MaxValue)]
    public static void X86FloatingForwardMappingsKeepNoRegisterAndEmptyMaskContinuations(uint index)
    {
        Assume.That(Configuration.TARGET_X86);

        void AssertMappings()
        {
            Assert.That(genMapFloatRegArgNumToRegNum(index), Is.EqualTo(REG_NA));
            Assert.That(genMapFloatRegArgNumToRegMask(index), Is.EqualTo(RBM_NONE));
            Assert.That(genMapRegArgNumToRegNum(index, TYP_FLOAT, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_NA));
            Assert.That(genMapArgNumToRegMask(index, TYP_FLOAT), Is.EqualTo(RBM_NONE));
        }

        if (TestConfiguration.IsDebug)
        {
            WithAssertionRecorder(AssertMappings);
            string[] expected = [
                "!\"no x86 float arg regs\\n\"", "!\"no x86 float arg regs\\n\"",
                "!\"no x86 float arg regs\\n\"", "!\"no x86 float arg regs\\n\"",
            ];
            Assert.That(s_assertions, Is.EqualTo(expected));
        }
        else
        {
            AssertMappings();
        }
    }
}
