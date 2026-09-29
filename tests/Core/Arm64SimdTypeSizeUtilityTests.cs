// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_SIMD && (TARGET_ARM64 || TARGET_AMD64)
using System;
using NUnit.Framework;
#if TARGET_AMD64
using static RyuJitSharp.CORINFO_InstructionSet;
#endif
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64SimdTypeSizeUtilityTests
{
    [TestCase(TYP_BYTE, TYP_UBYTE)]
    [TestCase(TYP_UBYTE, TYP_UBYTE)]
    [TestCase(TYP_SHORT, TYP_USHORT)]
    [TestCase(TYP_USHORT, TYP_USHORT)]
    [TestCase(TYP_INT, TYP_UINT)]
    [TestCase(TYP_UINT, TYP_UINT)]
    [TestCase(TYP_FLOAT, TYP_UINT)]
    [TestCase(TYP_LONG, TYP_ULONG)]
    [TestCase(TYP_ULONG, TYP_ULONG)]
    [TestCase(TYP_DOUBLE, TYP_ULONG)]
    public static void SimdElementTypesPreserveNativeUnsignedMapping(var_types input, var_types expected)
    {
        Assert.That(Compiler.getUnsignedSimdBaseType(input), Is.EqualTo(expected));
    }

    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 4)]
    [TestCase(4, 4)]
    [TestCase(5, 8)]
    [TestCase(8, 8)]
    [TestCase(17, 8)]
    [TestCase(int.MinValue, 8)]
    [TestCase(-1, 8)]
    public static void GprSizesRoundToNativeRegisterWidth(int size, int expected)
    {
        Assert.That(Compiler.roundUpGprSize(size), Is.EqualTo(expected));
    }

    [TestCase(1, TYP_UBYTE)]
    [TestCase(2, TYP_USHORT)]
    [TestCase(3, TYP_INT)]
    [TestCase(5, TYP_LONG)]
    [TestCase(-1, TYP_LONG)]
    public static void GprTypesUseNativeZeroExtendableKinds(int size, var_types expected)
    {
        Assert.That(Compiler.roundUpGprType(size), Is.EqualTo(expected));
    }

    [Test]
    public static void ZeroGprSizeDoesNotReturnAnUndefinedType()
    {
        _ = Assert.Throws<FatalJitException>(() => Compiler.roundUpGprType(0));
    }

    [TestCase(1u, TYP_UBYTE)]
    [TestCase(2u, TYP_USHORT)]
    [TestCase(3u, TYP_USHORT)]
    [TestCase(4u, TYP_INT)]
    [TestCase(7u, TYP_INT)]
    [TestCase(8u, TYP_LONG)]
    [TestCase(15u, TYP_LONG)]
    [TestCase(16u, TYP_SIMD16)]
    [TestCase(0x80000000u, TYP_SIMD16)]
    [TestCase(uint.MaxValue, TYP_SIMD16)]
    public static void RoundDownMaxTypePreservesUnsignedSizeAndRegisterFloor(uint size, var_types expected)
    {
        WithFixedVectorWidth(compiler => {
            Assert.That(compiler.roundDownMaxType(size), Is.EqualTo(expected));
            Assert.That(compiler.roundDownMaxType(unchecked((int)size)), Is.EqualTo(expected));
        });
    }

#if TARGET_AMD64
    [TestCase(false, TYP_SIMD16)]
    [TestCase(true, TYP_SIMD32)]
    public static void ConservativeRoundingRequiresAvx2ForSimd32(bool avx2, var_types expected)
    {
        WithFixedVectorWidth(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX2);
            if (avx2)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX2);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX2);
            }

            Assert.That(compiler.roundDownMaxType(32u, false), Is.EqualTo(TYP_SIMD32));
            Assert.That(compiler.roundDownMaxType(32u, true), Is.EqualTo(expected));
            Assert.That(compiler.roundDownMaxType(32, true), Is.EqualTo(expected));
        });
    }
#endif

#if !DEBUG
    [Test]
    public static void ZeroSizeKeepsNativeReleaseLog2Fallback()
    {
        WithFixedVectorWidth(compiler => {
            Assert.That(compiler.roundDownMaxType(0u), Is.EqualTo(TYP_UBYTE));
            Assert.That(compiler.roundDownMaxType(0u, true), Is.EqualTo(TYP_UBYTE));
        });
    }
#endif

    private static void WithFixedVectorWidth(Action<Compiler> action)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
#if TARGET_AMD64
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX);
#endif
            action(compiler);
        });
    }
}
#endif
