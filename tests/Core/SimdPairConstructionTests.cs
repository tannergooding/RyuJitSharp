// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdPairConstructionTests
{
#if TARGET_XARCH
    [TestCase(TYP_BYTE, (byte)16, 0x00FF00FF00FF00FFUL)]
    [TestCase(TYP_UBYTE, (byte)16, 0x00FF00FF00FF00FFUL)]
    [TestCase(TYP_SHORT, (byte)16, 0x0000FFFF0000FFFFUL)]
    [TestCase(TYP_USHORT, (byte)16, 0x0000FFFF0000FFFFUL)]
    [TestCase(TYP_BYTE, (byte)32, 0x00FF00FF00FF00FFUL)]
    [TestCase(TYP_UBYTE, (byte)32, 0x00FF00FF00FF00FFUL)]
    [TestCase(TYP_SHORT, (byte)32, 0x0000FFFF0000FFFFUL)]
    [TestCase(TYP_USHORT, (byte)32, 0x0000FFFF0000FFFFUL)]
    public static void PackedNarrowTruncatesBeforeUnsignedSaturationAndOwnsDistinctMasks(
        var_types baseType, byte size, ulong mask)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var result = compiler.gtNewSimdNarrowNode(type, left, right, baseType, size).AsHWIntrinsic();
            var packed = size == 32 ? result.GetOp(1).AsHWIntrinsic() : result;
            Assert.That(packed.HWIntrinsicId,
                Is.EqualTo(size == 32 ? NI_AVX2_PackUnsignedSaturate : NI_X86Base_PackUnsignedSaturate));
            Assert.That(packed.SimdBaseType, Is.EqualTo(baseType.Size == 1 ? TYP_UBYTE : TYP_USHORT));
            var lower = packed.GetOp(1).AsHWIntrinsic();
            var upper = packed.GetOp(2).AsHWIntrinsic();
            Assert.That(lower.GetOp(1), Is.SameAs(left));
            Assert.That(upper.GetOp(1), Is.SameAs(right));
            var lowerMask = lower.GetOp(2).AsVecCon();
            var upperMask = upper.GetOp(2).AsVecCon();
            Assert.That(lowerMask, Is.Not.SameAs(upperMask));
            for (var index = 0; index < size / 8; index++)
            {
                Assert.That(lowerMask.SimdVal.u64[index], Is.EqualTo(mask));
                Assert.That(upperMask.SimdVal.u64[index], Is.EqualTo(mask));
            }
            if (size == 32)
            {
                Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AVX2_Permute4x64));
                Assert.That(result.GetOp(2).AsIntCon().IconValue, Is.EqualTo((nint)SHUFFLE_WYZX));
            }
        });
    }

    [TestCase(TYP_BYTE, TYP_SHORT, NI_AVX512_ConvertToVector128SByte)]
    [TestCase(TYP_UBYTE, TYP_USHORT, NI_AVX512_ConvertToVector128Byte)]
    [TestCase(TYP_SHORT, TYP_INT, NI_AVX512_ConvertToVector128Int16)]
    [TestCase(TYP_USHORT, TYP_UINT, NI_AVX512_ConvertToVector128UInt16)]
    [TestCase(TYP_INT, TYP_LONG, NI_AVX512_ConvertToVector128Int32)]
    [TestCase(TYP_UINT, TYP_ULONG, NI_AVX512_ConvertToVector128UInt32)]
    public static void Avx512NarrowConvertsBothSourcesBeforeJoiningTheirLowHalves(
        var_types destination, var_types source, NamedIntrinsic conversion)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX512);
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdNarrowNode(TYP_SIMD16, left, right, destination, 16).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_X86Base_MoveLowToHigh));
            for (var index = 1; index <= 2; index++)
            {
                var narrowed = result.GetOp(index).AsHWIntrinsic();
                Assert.That(narrowed.HWIntrinsicId, Is.EqualTo(conversion));
                Assert.That(narrowed.SimdBaseType, Is.EqualTo(source));
                Assert.That(narrowed.GetOp(1), Is.SameAs(index == 1 ? left : right));
            }
        });
    }

    [TestCase((byte)16, false, NI_X86Base_ConvertToVector128Single)]
    [TestCase((byte)16, true, NI_X86Base_ConvertToVector128Single)]
    [TestCase((byte)32, false, NI_AVX_ConvertToVector128Single)]
    [TestCase((byte)32, true, NI_AVX_ConvertToVector128Single)]
    [TestCase((byte)64, true, NI_AVX512_ConvertToVector256Single)]
    public static void FloatingNarrowRetainsDoubleConversionAndSourceIdentity(
        byte size, bool avx512, NamedIntrinsic conversion)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX);
            if (avx512)
            {
                Enable(compiler, InstructionSet_AVX512);
            }
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var result = compiler.gtNewSimdNarrowNode(type, left, right, TYP_FLOAT, size).AsHWIntrinsic();
            var lower = result.GetOp(1).AsHWIntrinsic();
            if (size != 16)
            {
                Assert.That(lower.HWIntrinsicId,
                    Is.EqualTo(size == 32 ? NI_Vector_ToVector256Unsafe : NI_Vector_ToVector512Unsafe));
                lower = lower.GetOp(1).AsHWIntrinsic();
            }
            var upper = result.GetOp(2).AsHWIntrinsic();
            Assert.That(lower.HWIntrinsicId, Is.EqualTo(conversion));
            Assert.That(upper.HWIntrinsicId, Is.EqualTo(conversion));
            Assert.That(lower.SimdBaseType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(upper.SimdBaseType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(lower.GetOp(1), Is.SameAs(left));
            Assert.That(upper.GetOp(1), Is.SameAs(right));
        });
    }

    [TestCase((byte)32, TYP_BYTE, false, NI_AVX512v2_PermuteVar32x8x2)]
    [TestCase((byte)32, TYP_USHORT, true, NI_AVX512_PermuteVar16x16x2)]
    [TestCase((byte)32, TYP_FLOAT, false, NI_AVX512_PermuteVar8x32x2)]
    [TestCase((byte)32, TYP_LONG, true, NI_AVX512_PermuteVar4x64x2)]
    [TestCase((byte)64, TYP_UBYTE, true, NI_AVX512v2_PermuteVar64x8x2)]
    [TestCase((byte)64, TYP_SHORT, false, NI_AVX512_PermuteVar32x16x2)]
    [TestCase((byte)64, TYP_UINT, true, NI_AVX512_PermuteVar16x32x2)]
    [TestCase((byte)64, TYP_DOUBLE, false, NI_AVX512_PermuteVar8x64x2)]
    public static void WideZipSelectsEveryLaneFromTheRequestedSourceHalves(
        byte size, var_types baseType, bool upper, NamedIntrinsic intrinsic)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX512);
            Enable(compiler, InstructionSet_AVX512v2);
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var result = compiler.gtNewSimdZipNode(type, left, right, baseType, size, upper).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(result.GetOp(1), Is.SameAs(left));
            Assert.That(result.GetOp(3), Is.SameAs(right));
            var indices = result.GetOp(2).AsVecCon();
            var count = size / baseType.Size;
            var indexType = baseType.Size switch
            {
                1 => TYP_UBYTE,
                2 => TYP_USHORT,
                4 => TYP_UINT,
                _ => TYP_ULONG,
            };
            for (var index = 0; index < count; index++)
            {
                var expected = (upper ? count / 2 : 0) + (index / 2) + ((index & 1) == 0 ? 0 : count);
                Assert.That(indices.GetElementIntegral(indexType, index), Is.EqualTo((long)expected));
            }
        });
    }

    [TestCase((byte)32, false)]
    [TestCase((byte)32, true)]
    public static void RecursiveZipSharesTheSelectedHalvesWithoutReevaluatingSources(byte size, bool upper)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            var left = Local(compiler, TYP_SIMD32);
            var right = Local(compiler, TYP_SIMD32);
            var result = compiler.gtNewSimdZipNode(TYP_SIMD32, left, right, TYP_INT, size, upper).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_WithUpper));
            var lower = result.GetOp(1).AsHWIntrinsic().GetOp(1).AsHWIntrinsic();
            var higher = result.GetOp(2).AsHWIntrinsic();
            Assert.That(lower.HWIntrinsicId, Is.EqualTo(NI_X86Base_UnpackLow));
            Assert.That(higher.HWIntrinsicId, Is.EqualTo(NI_X86Base_UnpackHigh));
            Assert.That(lower.GetOp(1).Oper, Is.EqualTo(GT_COMMA));
            Assert.That(lower.GetOp(2).Oper, Is.EqualTo(GT_COMMA));
            Assert.That(higher.GetOp(1).AsLclVar().LclNum,
                Is.EqualTo(lower.GetOp(1).AsOp().Op2.AsLclVar().LclNum));
            Assert.That(higher.GetOp(2).AsLclVar().LclNum,
                Is.EqualTo(lower.GetOp(2).AsOp().Op2.AsLclVar().LclNum));
            Assert.That(lower.GetOp(1).AsOp().Op1.AsLclVar().Data.AsHWIntrinsic().HWIntrinsicId,
                Is.EqualTo(upper ? NI_Vector_GetUpper : NI_Vector_GetLower));
            Assert.That(lower.GetOp(2).AsOp().Op1.AsLclVar().Data.AsHWIntrinsic().GetOp(1), Is.SameAs(right));
        });
    }

    [TestCase((byte)16)]
    [TestCase((byte)32)]
    public static void ConcatUpperUpperCapturesLeftBeforeTheRightOperandChangesItsLocal(byte size)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var write = compiler.gtNewStoreLclVarNode(left.LclNum, compiler.gtNewVconNode(type));
            var right = compiler.gtNewBinaryNode(GT_COMMA, type, write, Local(compiler, type));
            var result = compiler.gtNewSimdConcatNode(type, left, right, TYP_INT, size, true, true).AsHWIntrinsic();
            var sequence = result.GetOp(1).AsOp();
            Assert.That(sequence.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(sequence.Op1.AsLclVar().Data, Is.SameAs(left));
            Assert.That(sequence.Op2, Is.SameAs(right));
            var retainedLeft = size == 16 ? result.GetOp(2) : result.GetOp(2).AsHWIntrinsic().GetOp(1);
            Assert.That(retainedLeft.AsLclVar().LclNum, Is.EqualTo(sequence.Op1.AsLclVar().LclNum));
            Assert.That((result.Flags & GTF_REVERSE_OPS) != 0, Is.False);
        });
    }
#endif

#if TARGET_XARCH || TARGET_ARM64
#if TARGET_XARCH
    [TestCase((byte)32, false, false)]
    [TestCase((byte)32, false, true)]
    [TestCase((byte)32, true, false)]
    [TestCase((byte)32, true, true)]
    [TestCase((byte)64, false, false)]
    [TestCase((byte)64, false, true)]
    [TestCase((byte)64, true, false)]
    [TestCase((byte)64, true, true)]
#else
    [TestCase((byte)16, false, false)]
    [TestCase((byte)16, false, true)]
    [TestCase((byte)16, true, false)]
    [TestCase((byte)16, true, true)]
#endif
    public static void WideConcatKeepsExactHalfExtractionAndInsertion(byte size, bool leftUpper, bool rightUpper)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var result = compiler.gtNewSimdConcatNode(type, left, right, TYP_LONG, size,
                leftUpper, rightUpper).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(leftUpper && rightUpper
                ? NI_Vector_WithLower : NI_Vector_WithUpper));
            if (!leftUpper)
            {
                Assert.That(result.GetOp(1), Is.SameAs(left));
                var extracted = result.GetOp(2).AsHWIntrinsic();
                Assert.That(extracted.HWIntrinsicId, Is.EqualTo(rightUpper ? NI_Vector_GetUpper : NI_Vector_GetLower));
                Assert.That(extracted.GetOp(1), Is.SameAs(right));
            }
            else if (rightUpper)
            {
                Assert.That(result.GetOp(1), Is.SameAs(right));
                Assert.That(result.GetOp(2).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Vector_GetUpper));
                Assert.That(result.GetOp(2).AsHWIntrinsic().GetOp(1), Is.SameAs(left));
            }
            else
            {
                var widened = result.GetOp(1).AsHWIntrinsic();
#if TARGET_XARCH
                Assert.That(widened.HWIntrinsicId, Is.EqualTo(size == 64
                    ? NI_Vector_ToVector512Unsafe : NI_Vector_ToVector256Unsafe));
#else
                Assert.That(widened.HWIntrinsicId, Is.EqualTo(NI_Vector_ToVector128Unsafe));
#endif
                Assert.That(widened.SimdSize, Is.EqualTo(size / 2));
                Assert.That(widened.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Vector_GetUpper));
                Assert.That(widened.GetOp(1).AsHWIntrinsic().GetOp(1), Is.SameAs(left));
                Assert.That(result.GetOp(2).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Vector_GetLower));
                Assert.That(result.GetOp(2).AsHWIntrinsic().GetOp(1), Is.SameAs(right));
            }
        });
    }

    [Test]
    public static void SingleLaneZipDropsOnlyAnUnobservableRightOperand()
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD8);
            var right = Local(compiler, TYP_SIMD8);
            Assert.That(compiler.gtNewSimdZipNode(TYP_SIMD8, left, right, TYP_LONG, 8, upper: false),
                Is.SameAs(left));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SingleLaneZipPreservesObservableRightOperandAndCapturesLeftFirst(bool invariant)
    {
        WithCompiler(compiler =>
        {
            var left = invariant ? (GenTree)compiler.gtNewVconNode(TYP_SIMD8) : Local(compiler, TYP_SIMD8);
            var right = compiler.gtNewStoreLclVarNode(Local(compiler, TYP_SIMD8).LclNum,
                compiler.gtNewVconNode(TYP_SIMD8));
            var result = compiler.gtNewSimdZipNode(TYP_SIMD8, left, right, TYP_LONG, 8, upper: true);
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            if (!invariant)
            {
                var capture = result.AsOp().Op1.AsOp();
                Assert.That(capture.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(capture.Op1.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(capture.Op1.AsLclVar().Data, Is.SameAs(left));
                Assert.That(capture.Op2.AsLclVar().LclNum, Is.EqualTo(capture.Op1.AsLclVar().LclNum));
                var effects = result.AsOp().Op2.AsOp();
                Assert.That(effects.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(effects.Op1, Is.SameAs(right));
                Assert.That(effects.Op2.AsLclVar().LclNum, Is.EqualTo(capture.Op1.AsLclVar().LclNum));
            }
            else
            {
                Assert.That(result.AsOp().Op1, Is.SameAs(right));
                Assert.That(result.AsOp().Op2, Is.SameAs(left));
            }
            Assert.That(Contains(result, right), Is.True);
            Assert.That(CountReferences(result, right), Is.EqualTo(1));
        });
    }
#endif

#if TARGET_ARM64
    [TestCase((byte)8, TYP_FLOAT, NI_AdvSimd_Arm64_ConvertToSingleLower)]
    [TestCase((byte)16, TYP_FLOAT, NI_AdvSimd_Arm64_ConvertToSingleUpper)]
    [TestCase((byte)8, TYP_UBYTE, NI_AdvSimd_ExtractNarrowingLower)]
    [TestCase((byte)16, TYP_USHORT, NI_AdvSimd_ExtractNarrowingUpper)]
    public static void Arm64NarrowKeepsNativeLowerUpperConstruction(
        byte size, var_types baseType, NamedIntrinsic intrinsic)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var result = compiler.gtNewSimdNarrowNode(type, left, right, baseType, size).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
            if (size == 16)
            {
                Assert.That(result.GetOp(1).AsHWIntrinsic().Type, Is.EqualTo(TYP_SIMD8));
                Assert.That(result.GetOp(1).AsHWIntrinsic().GetOp(1), Is.SameAs(left));
                Assert.That(result.GetOp(2), Is.SameAs(right));
            }
            else
            {
                var joined = result.GetOp(1).AsHWIntrinsic();
                Assert.That(joined.HWIntrinsicId, Is.EqualTo(NI_Vector_WithUpper));
                Assert.That(joined.SimdBaseType, Is.EqualTo(baseType == TYP_FLOAT ? TYP_DOUBLE : baseType));
                Assert.That(joined.GetOp(1).AsHWIntrinsic().GetOp(1), Is.SameAs(left));
                Assert.That(joined.GetOp(2), Is.SameAs(right));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void Arm64EightByteConcatUsesUInt32LaneInsertion(bool leftUpper, bool rightUpper)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD8);
            var right = Local(compiler, TYP_SIMD8);
            var result = compiler.gtNewSimdConcatNode(TYP_SIMD8, left, right, TYP_BYTE, 8,
                leftUpper, rightUpper).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Arm64_InsertSelectedScalar));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_UINT));
            Assert.That(result.GetOp(2).AsIntCon().IconValue, Is.EqualTo((nint)1));
            Assert.That(result.GetOp(3), Is.SameAs(right));
            Assert.That(result.GetOp(4).AsIntCon().IconValue, Is.EqualTo((nint)(rightUpper ? 1 : 0)));
            if (leftUpper)
            {
                var lower = result.GetOp(1).AsHWIntrinsic();
                Assert.That(lower.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Arm64_InsertSelectedScalar));
                Assert.That(lower.GetOp(2).AsIntCon().IconValue, Is.EqualTo((nint)0));
                Assert.That(lower.GetOp(4).AsIntCon().IconValue, Is.EqualTo((nint)1));
            }
            else
            {
                Assert.That(result.GetOp(1), Is.SameAs(left));
            }
        });
    }

    [TestCase(false, NI_AdvSimd_Arm64_ZipLow)]
    [TestCase(true, NI_AdvSimd_Arm64_ZipHigh)]
    public static void Arm64ZipUsesTheNativeHalfIntrinsic(bool upper, NamedIntrinsic intrinsic)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdZipNode(TYP_SIMD16, left, right, TYP_INT, 16, upper).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(result.GetOp(1), Is.SameAs(left));
            Assert.That(result.GetOp(2), Is.SameAs(right));
        });
    }
#endif

#if TARGET_WASM
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void WasmCallersReachTheTrackedTwoSourceShuffleDependency(int operation)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                _ = operation switch
                {
                    0 => compiler.gtNewSimdNarrowNode(TYP_SIMD16, left, right, TYP_UBYTE, 16),
                    1 => compiler.gtNewSimdConcatNode(TYP_SIMD16, left, right, TYP_INT, 16, true, false),
                    _ => compiler.gtNewSimdZipNode(TYP_SIMD16, left, right, TYP_SHORT, 16, true),
                };
            });
            Assert.That(exception!.Message, Does.Contain("Wasm two-source SIMD shuffle construction"));
        });
    }
#endif

    private static bool Contains(GenTree tree, GenTree value) => CountReferences(tree, value) != 0;

    private static int CountReferences(GenTree tree, GenTree value)
    {
        var count = ReferenceEquals(tree, value) ? 1 : 0;
        foreach (var operand in tree.Operands)
        {
            count += CountReferences(operand, value);
        }

        return count;
    }

    private static GenTreeLclVar Local(Compiler compiler, var_types type)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = type };

        return compiler.gtNewLclvNode(type, index);
    }

    private static void Enable(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[128];
        compiler.lvaCount = 2;
        compiler.lvaTable[0] = new LclVarDsc { Type = TYP_SIMD16 };
        compiler.lvaTable[1] = new LclVarDsc { Type = TYP_INT };
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
#endif
