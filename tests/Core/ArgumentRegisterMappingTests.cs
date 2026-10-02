// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if HAS_FIXED_REGISTER_SET
using System;
using System.Collections.Generic;
#if DEBUG
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ArgumentRegisterMappingTests
{
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
        (regNumber Reg, regMaskTP Mask)[] expected = [
#if TARGET_AMD64
#if UNIX_AMD64_ABI
            (REG_EDI, RBM_EDI), (REG_ESI, RBM_ESI), (REG_EDX, RBM_EDX), (REG_ECX, RBM_ECX),
#else
            (REG_ECX, RBM_ECX), (REG_EDX, RBM_EDX),
#endif
            (REG_R8, RBM_R8), (REG_R9, RBM_R9),
#elif TARGET_X86
            (REG_ECX, RBM_ECX), (REG_EDX, RBM_EDX),
#elif TARGET_ARM || TARGET_ARM64
            (REG_R0, RBM_R0), (REG_R1, RBM_R1), (REG_R2, RBM_R2), (REG_R3, RBM_R3),
#if TARGET_ARM64
            (REG_R4, RBM_R4), (REG_R5, RBM_R5), (REG_R6, RBM_R6), (REG_R7, RBM_R7),
#endif
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
            (REG_A0, RBM_A0), (REG_A1, RBM_A1), (REG_A2, RBM_A2), (REG_A3, RBM_A3),
            (REG_A4, RBM_A4), (REG_A5, RBM_A5), (REG_A6, RBM_A6), (REG_A7, RBM_A7),
#endif
        ];

        for (var index = 0; index < expected.Length; index++)
        {
            yield return new TestCaseData((uint)index, expected[index].Reg, expected[index].Mask);
        }
    }

#if !TARGET_X86
    private static IEnumerable<TestCaseData> FloatingArguments()
    {
        (regNumber Reg, regMaskTP Mask)[] expected = [
#if TARGET_AMD64
            (REG_XMM0, RBM_XMM0), (REG_XMM1, RBM_XMM1), (REG_XMM2, RBM_XMM2), (REG_XMM3, RBM_XMM3),
#if UNIX_AMD64_ABI
            (REG_XMM4, RBM_XMM4), (REG_XMM5, RBM_XMM5), (REG_XMM6, RBM_XMM6), (REG_XMM7, RBM_XMM7),
#endif
#elif TARGET_ARM64
            (REG_V0, RBM_V0), (REG_V1, RBM_V1), (REG_V2, RBM_V2), (REG_V3, RBM_V3),
            (REG_V4, RBM_V4), (REG_V5, RBM_V5), (REG_V6, RBM_V6), (REG_V7, RBM_V7),
#elif TARGET_ARM || TARGET_LOONGARCH64
            (REG_F0, RBM_F0), (REG_F1, RBM_F1), (REG_F2, RBM_F2), (REG_F3, RBM_F3),
            (REG_F4, RBM_F4), (REG_F5, RBM_F5), (REG_F6, RBM_F6), (REG_F7, RBM_F7),
#if TARGET_ARM
            (REG_F8, RBM_F8), (REG_F9, RBM_F9), (REG_F10, RBM_F10), (REG_F11, RBM_F11),
            (REG_F12, RBM_F12), (REG_F13, RBM_F13), (REG_F14, RBM_F14), (REG_F15, RBM_F15),
#endif
#elif TARGET_RISCV64
            (REG_FA0, RBM_FA0), (REG_FA1, RBM_FA1), (REG_FA2, RBM_FA2), (REG_FA3, RBM_FA3),
            (REG_FA4, RBM_FA4), (REG_FA5, RBM_FA5), (REG_FA6, RBM_FA6), (REG_FA7, RBM_FA7),
#endif
        ];

        for (var index = 0; index < expected.Length; index++)
        {
            yield return new TestCaseData((uint)index, expected[index].Reg, expected[index].Mask);
        }
    }
#endif

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
#if TARGET_AMD64 || TARGET_ARM64
                Assert.That(genMapRegNumToRegArgNum(expectedReg, TYP_INT, callConv), Is.EqualTo(index));
#endif
            }
        });
    }

#if !TARGET_X86
    [TestCaseSource(nameof(FloatingArguments))]
    public static void FloatingIndicesUseSingleRegisterMasksAndRoundTripAcrossConventions(
        uint index, regNumber expectedReg, regMaskTP expectedMask)
    {
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
#endif

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_STRUCT)]
#if !TARGET_X86
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
#endif
#if FEATURE_SIMD
    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
#if TARGET_ARM64
    [TestCase(TYP_SIMD)]
#endif
#endif
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_MASK)]
#endif
    public static void TypeDispatchUsesFloatingRegistersOnlyForNativeArgumentTypes(var_types type)
    {
#if TARGET_ARM64
        var usesFloat = type is TYP_FLOAT or TYP_DOUBLE;
#if FEATURE_SIMD
        usesFloat |= type is TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 or TYP_SIMD;
#endif
#else
        var usesFloat = type is TYP_FLOAT or TYP_DOUBLE;
#endif
#if TARGET_AMD64 || TARGET_X86
        var expectedInt = REG_ECX;
        var expectedIntMask = RBM_ECX;
#if UNIX_AMD64_ABI
        expectedInt = REG_EDI;
        expectedIntMask = RBM_EDI;
#endif
#elif TARGET_ARM || TARGET_ARM64
        var expectedInt = REG_R0;
        var expectedIntMask = RBM_R0;
#else
        var expectedInt = REG_A0;
        var expectedIntMask = RBM_A0;
#endif
#if TARGET_AMD64 || TARGET_X86
        var expectedFloat = REG_XMM0;
        var expectedFloatMask = RBM_XMM0;
#elif TARGET_ARM64
        var expectedFloat = REG_V0;
        var expectedFloatMask = RBM_V0;
#elif TARGET_RISCV64
        var expectedFloat = REG_FA0;
        var expectedFloatMask = RBM_FA0;
#else
        var expectedFloat = REG_F0;
        var expectedFloatMask = RBM_F0;
#endif
        var expectedReg = usesFloat ? expectedFloat : expectedInt;
        var expectedMask = usesFloat ? expectedFloatMask : expectedIntMask;
#if TARGET_ARM
        if (type == TYP_DOUBLE)
        {
            expectedMask = RBM_F0 | RBM_F1;
        }
#endif

        WithoutNativeAssertions(() => {
            Assert.That(genMapArgNumToRegMask(0, type), Is.EqualTo(expectedMask));
            foreach (var callConv in s_callConventions)
            {
                Assert.That(genMapRegArgNumToRegNum(0, type, callConv), Is.EqualTo(expectedReg));
#if TARGET_AMD64 || TARGET_ARM64
                Assert.That(genMapRegNumToRegArgNum(expectedReg, type, callConv), Is.Zero);
#endif
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
#if TARGET_ARM64
        var expectedFixed = !TargetOS.IsWindows || !instanceConvention;
        const uint expectedIndex = 8;
        const regNumber expectedReg = REG_R8;
#elif TARGET_AMD64 && SWIFT_SUPPORT
        var expectedFixed = callConv == CorInfoCallConvExtension.Swift;
#if UNIX_AMD64_ABI
        const uint expectedIndex = 6;
#else
        const uint expectedIndex = 4;
#endif
        const regNumber expectedReg = REG_RAX;
#else
        const bool expectedFixed = false;
#endif
        WithoutNativeAssertions(() => {
            Assert.That(hasFixedRetBuffReg(callConv), Is.EqualTo(expectedFixed));
#if TARGET_ARM64 || (TARGET_AMD64 && SWIFT_SUPPORT)
            if (expectedFixed)
            {
                Assert.That(unchecked((uint)theFixedRetBuffArgNum(callConv)), Is.EqualTo(expectedIndex));
                Assert.That(genMapIntRegArgNumToRegNum(expectedIndex, callConv), Is.EqualTo(expectedReg));
                Assert.That(genMapRegArgNumToRegNum(expectedIndex, TYP_STRUCT, callConv), Is.EqualTo(expectedReg));
                Assert.That(genMapRegNumToRegArgNum(expectedReg, TYP_STRUCT, callConv), Is.EqualTo(expectedIndex));
            }
#endif
            Assert.That(genMapIntRegArgNumToRegNum(0, callConv), Is.EqualTo(IntArgRegs[0]));
        });
    }

#if TARGET_ARM
    [TestCase(0U, REG_F0, REG_F1)]
    [TestCase(2U, REG_F2, REG_F3)]
    [TestCase(4U, REG_F4, REG_F5)]
    [TestCase(6U, REG_F6, REG_F7)]
    [TestCase(8U, REG_F8, REG_F9)]
    [TestCase(10U, REG_F10, REG_F11)]
    [TestCase(12U, REG_F12, REG_F13)]
    [TestCase(14U, REG_F14, REG_F15)]
    public static void ArmDoubleArgumentsExpandOnlyTheEvenLowRegister(uint index, regNumber low, regNumber high)
    {
        var expected = new regMaskTP(unchecked((regMask)((1UL << (int)low) | (1UL << (int)high))));
        WithoutNativeAssertions(() => Assert.That(genMapArgNumToRegMask(index, TYP_DOUBLE), Is.EqualTo(expected)));
    }
#endif

    private static void AssertNormalBank(regMaskTP mask)
    {
        Assert.That(mask.Lower, Is.Not.EqualTo(SRBM_NONE));
#if HAS_MORE_THAN_64_REGISTERS
        Assert.That(mask.Upper, Is.EqualTo(SRBM_NONE));
#endif
    }

    private static void WithoutNativeAssertions(Action action)
    {
#if DEBUG
        WithAssertionRecorder(action);
        Assert.That(s_assertions, Is.Empty);
#else
        action();
#endif
    }

#if DEBUG
    private static readonly List<string?> s_assertions = [];

    [TestCase(0, false, "argNum < ArrLen(intArgRegs)")]
    [TestCase(0, true, "argNum < ArrLen(intArgRegs)")]
    [TestCase(1, false, "argNum < ArrLen(intArgMasks)")]
    [TestCase(1, true, "argNum < ArrLen(intArgMasks)")]
#if !TARGET_X86
    [TestCase(2, false, "argNum < ArrLen(fltArgRegs)")]
    [TestCase(2, true, "argNum < ArrLen(fltArgRegs)")]
    [TestCase(3, false, "argNum < ArrLen(fltArgMasks)")]
    [TestCase(3, true, "argNum < ArrLen(fltArgMasks)")]
#endif
    public static void InvalidTableIndicesReportTheNativeBoundsBeforeManagedIndexing(
        int mapper, bool maximumIndex, string expected)
    {
        var length = (uint)IntArgRegs.Length;
#if !TARGET_X86
        if (mapper >= 2)
        {
            length = (uint)FltArgRegs.Length;
        }
#endif
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

#if !TARGET_X86
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
#endif
            }
        }));
        string[] expectedAssertions = [expected];

        Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
    }

    [TestCase(REG_INT_FIRST)]
    [TestCase(REG_FP_LAST)]
    public static void FloatingReverseReportsInvalidArgumentMembershipWithoutObservingItsNumericContinuation(
        regNumber reg)
    {
        WithAssertionRecorder(() => _ = genMapFloatRegNumToRegArgNum(reg));
        List<string> expected = ["genRegMask(regNum) & RBM_FLTARG_REGS"];
#if TARGET_X86
        expected.Add("!\"flt reg args not allowed\"");
#elif TARGET_AMD64 && !UNIX_AMD64_ABI
        expected.Add("!\"invalid register arg register\"");
#endif
        Assert.That(s_assertions, Is.EqualTo(expected));
    }

#if TARGET_AMD64 || TARGET_ARM64
    [Test]
    public static void GenericIntegerReverseKeepsTheExistingHelpersDiagnostics()
    {
        WithAssertionRecorder(() => _ = genMapRegNumToRegArgNum(REG_FP_FIRST, TYP_INT, CorInfoCallConvExtension.Managed));
        string[] expected = [
            "(regMaskTP.CreateFromRegNum(regNum, regNum.SingleTypeMask) & fullIntArgRegMask(callConv)) != RBM_NONE",
            "invalid register arg register",
        ];
        Assert.That(s_assertions, Is.EqualTo(expected));
    }
#endif

#if TARGET_ARM
    [TestCase(1U)]
    [TestCase(3U)]
    [TestCase(15U)]
    public static void ArmDoubleArgumentsDiagnoseAnOddLowRegister(uint index)
    {
        WithAssertionRecorder(() => _ = genMapArgNumToRegMask(index, TYP_DOUBLE));
        string[] expected = ["(result & RBM_ALLDOUBLE) != 0"];
        Assert.That(s_assertions, Is.EqualTo(expected));
    }
#endif

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
#endif

#if TARGET_X86
    [TestCase(0U)]
    [TestCase(uint.MaxValue)]
    public static void X86FloatingForwardMappingsKeepNoRegisterAndEmptyMaskContinuations(uint index)
    {
#if DEBUG
        WithAssertionRecorder(() => {
#endif
            Assert.That(genMapFloatRegArgNumToRegNum(index), Is.EqualTo(REG_NA));
            Assert.That(genMapFloatRegArgNumToRegMask(index), Is.EqualTo(RBM_NONE));
            Assert.That(genMapRegArgNumToRegNum(index, TYP_FLOAT, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_NA));
            Assert.That(genMapArgNumToRegMask(index, TYP_FLOAT), Is.EqualTo(RBM_NONE));
#if DEBUG
        });
        string[] expected = [
            "!\"no x86 float arg regs\\n\"", "!\"no x86 float arg regs\\n\"",
            "!\"no x86 float arg regs\\n\"", "!\"no x86 float arg regs\\n\"",
        ];
        Assert.That(s_assertions, Is.EqualTo(expected));
#endif
    }
#endif
}
#endif
