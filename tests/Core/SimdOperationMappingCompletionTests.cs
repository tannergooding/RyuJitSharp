// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
#if TARGET_WASM
using System.Collections.Generic;
#endif
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
#if TARGET_XARCH
using static RyuJitSharp.CORINFO_InstructionSet;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdOperationMappingCompletionTests
{
#if TARGET_WASM
    [TestCase(GT_ADD, TYP_INT, NI_PackedSimd_Add)]
    [TestCase(GT_ADD, TYP_FLOAT, NI_PackedSimd_Add)]
    [TestCase(GT_AND, TYP_UINT, NI_PackedSimd_And)]
    [TestCase(GT_DIV, TYP_DOUBLE, NI_PackedSimd_Divide)]
    [TestCase(GT_LSH, TYP_UBYTE, NI_PackedSimd_ShiftLeft)]
    [TestCase(GT_MUL, TYP_LONG, NI_PackedSimd_Multiply)]
    [TestCase(GT_MUL, TYP_BYTE, NI_Illegal)]
    [TestCase(GT_MUL, TYP_UBYTE, NI_Illegal)]
    [TestCase(GT_OR, TYP_ULONG, NI_PackedSimd_Or)]
    [TestCase(GT_RSH, TYP_LONG, NI_PackedSimd_ShiftRightArithmetic)]
    [TestCase(GT_RSZ, TYP_ULONG, NI_PackedSimd_ShiftRightLogical)]
    [TestCase(GT_SUB, TYP_SHORT, NI_PackedSimd_Subtract)]
    [TestCase(GT_XOR, TYP_FLOAT, NI_PackedSimd_Xor)]
    [TestCase(GT_ROL, TYP_INT, NI_Illegal)]
    [TestCase(GT_ROR, TYP_LONG, NI_Illegal)]
    public static void WasmBinaryMappingRetainsSupportedIdsAndNativeUnsupportedCases(
        genTreeOps operation, var_types baseType, NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            GenTree right = operation is GT_LSH or GT_RSH or GT_RSZ or GT_ROL or GT_ROR
                ? compiler.gtNewIconNode(TYP_INT, 3) : Local(compiler, TYP_SIMD16);
            Assert.That(compiler.GetHWIntrinsicIdForBinOp(operation, left, right, baseType, 16, false),
                Is.EqualTo(expected));
        });
    }

    [TestCase(GT_LSH, NI_PackedSimd_ShiftLeft)]
    [TestCase(GT_RSH, NI_PackedSimd_ShiftRightArithmetic)]
    [TestCase(GT_RSZ, NI_PackedSimd_ShiftRightLogical)]
    public static void WasmShiftMappingRetainsThePinnedIdForVectorShiftOperands(
        genTreeOps operation, NamedIntrinsic expected)
    {
        WithCompiler(compiler => Assert.That(compiler.GetHWIntrinsicIdForBinOp(operation, Local(compiler, TYP_SIMD16),
            Local(compiler, TYP_SIMD16), TYP_INT, 16, false), Is.EqualTo(expected)));
    }

    [TestCase(GT_ADD, NI_PackedSimd_Add)]
    [TestCase(GT_DIV, NI_PackedSimd_Divide)]
    [TestCase(GT_MUL, NI_PackedSimd_Multiply)]
    [TestCase(GT_SUB, NI_PackedSimd_Subtract)]
    public static void WasmFloatingScalarMappingUsesThePinnedPackedId(genTreeOps operation, NamedIntrinsic expected)
    {
        WithCompiler(compiler => Assert.That(compiler.GetHWIntrinsicIdForBinOp(operation, Local(compiler, TYP_SIMD16),
            Local(compiler, TYP_SIMD16), TYP_FLOAT, 16, true), Is.EqualTo(expected)));
    }

    [TestCase(false, NI_Illegal)]
    [TestCase(true, NI_PackedSimd_AndNot)]
    public static void WasmAndNotRemainsRestrictedToLir(bool lir, NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            compiler.fgNodeThreading = lir ? NodeThreading.LIR : NodeThreading.None;
            Assert.That(compiler.GetHWIntrinsicIdForBinOp(GT_AND_NOT, Local(compiler, TYP_SIMD16),
                Local(compiler, TYP_SIMD16), TYP_INT, 16, false), Is.EqualTo(expected));
        });
    }

    private static IEnumerable<TestCaseData> WasmComparisonCases()
    {
        foreach (var baseType in new[] { TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT,
            TYP_UINT, TYP_LONG, TYP_ULONG, TYP_FLOAT, TYP_DOUBLE })
        {
            foreach (var operation in new[] { GT_EQ, GT_NE, GT_LT, GT_LE, GT_GT, GT_GE })
            {
                foreach (var reverse in new[] { false, true })
                {
                    var mappedOperation = reverse ? operation.ReverseRelop : operation;
                    var expected = mappedOperation switch
                    {
                        GT_EQ => NI_PackedSimd_CompareEqual,
                        GT_NE => NI_PackedSimd_CompareNotEqual,
                        _ when baseType == TYP_ULONG => NI_Illegal,
                        GT_LT => NI_PackedSimd_CompareLessThan,
                        GT_LE => NI_PackedSimd_CompareLessThanOrEqual,
                        GT_GT => NI_PackedSimd_CompareGreaterThan,
                        GT_GE => NI_PackedSimd_CompareGreaterThanOrEqual,
                        _ => throw new InvalidOperationException(),
                    };
                    yield return new TestCaseData(operation, baseType, reverse, expected);
                }
            }
        }
    }

    [TestCaseSource(nameof(WasmComparisonCases))]
    public static void WasmComparisonMappingPreservesSignednessAndReverseConditionDispatch(
        genTreeOps operation, var_types baseType, bool reverse, NamedIntrinsic expected)
    {
        WithCompiler(compiler => Assert.That(compiler.GetHWIntrinsicIdForCmpOp(operation, TYP_SIMD16,
            Local(compiler, TYP_SIMD16), Local(compiler, TYP_SIMD16), baseType, 16, false, reverse),
            Is.EqualTo(expected)));
    }

    [TestCase(TYP_USHORT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_FLOAT)]
    public static void WasmSafeVariableShuffleConstructsExpansionAndFullWidthRangeMask(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var indices = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, baseType, 16, false)
                .AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_And));
            var swizzle = result.GetOp(1).AsHWIntrinsic();
            Assert.That(swizzle.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            Assert.That(swizzle.GetOp(1), Is.SameAs(source));
            var expansion = swizzle.GetOp(2).AsHWIntrinsic();
            Assert.That(expansion.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Or));
            var lowBytes = expansion.GetOp(1).AsHWIntrinsic();
            Assert.That(lowBytes.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            var shift = lowBytes.GetOp(1).AsHWIntrinsic();
            Assert.That(shift.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_ShiftLeft));
            Assert.That(shift.GetOp(1), Is.SameAs(indices));
            var comparison = result.GetOp(2).AsHWIntrinsic();
            Assert.That(comparison.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_CompareLessThan));
            var indexType = baseType.Size == 2 ? TYP_USHORT : TYP_UINT;
            Assert.That(comparison.SimdBaseType, Is.EqualTo(indexType));
            Assert.That(comparison.GetOp(1).AsLclVarCommon().LclNum, Is.EqualTo(indices.LclNum));
            Assert.That(comparison.GetOp(1), Is.Not.SameAs(indices));
            Assert.That(comparison.GetOp(2).GetIntegralVectorConstElement(0, indexType),
                Is.EqualTo(unchecked((ulong)(16 / baseType.Size))));
        });
    }

    [TestCase(TYP_LONG)]
    [TestCase(TYP_DOUBLE)]
    public static void WasmNativeLongElementShuffleConstructsOrWithoutAnUnsignedRangeMask(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var indices = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, baseType, 16, true)
                .AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            Assert.That(result.GetOp(1), Is.SameAs(source));
            Assert.That(result.GetOp(2).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Or));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WasmUnzipConstructsBothScatterMasksAndCombinesThemInSourceOrder(bool odd)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdUnzipNode(TYP_SIMD16, left, right, TYP_SHORT, 16, odd).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Or));
            for (var source = 0; source < 2; source++)
            {
                var scatter = result.GetOp(source + 1).AsHWIntrinsic();
                Assert.That(scatter.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
                Assert.That(scatter.GetOp(1), Is.SameAs(source == 0 ? left : right));
                for (var lane = 0; lane < 8; lane++)
                {
                    for (var offset = 0; offset < 2; offset++)
                    {
                        var suppliesLane = (lane / 4) == source;
                        var laneIndex = ((lane % 4) * 2) + (odd ? 1 : 0);
                        var expected = suppliesLane ? (laneIndex * 2) + offset : 0xFF;
                        Assert.That(scatter.GetOp(2).AsVecCon().SimdVal.u8[(lane * 2) + offset],
                            Is.EqualTo(unchecked((byte)expected)));
                    }
                }
            }
        });
    }
#endif

#if TARGET_XARCH
    [TestCase(GT_ADD, (byte)16, TYP_INT, false, NI_X86Base_Add)]
    [TestCase(GT_ADD, (byte)32, TYP_UINT, false, NI_AVX2_Add)]
    [TestCase(GT_DIV, (byte)64, TYP_DOUBLE, false, NI_AVX512_Divide)]
    [TestCase(GT_DIV, (byte)16, TYP_FLOAT, true, NI_X86Base_DivideScalar)]
    [TestCase(GT_MUL, (byte)16, TYP_BYTE, false, NI_Illegal)]
    [TestCase(GT_MUL, (byte)16, TYP_DOUBLE, true, NI_X86Base_MultiplyScalar)]
    [TestCase(GT_OR, (byte)32, TYP_INT, false, NI_AVX2_Or)]
    [TestCase(GT_XOR, (byte)32, TYP_FLOAT, false, NI_AVX_Xor)]
    public static void XarchBinaryWidthAndScalarControlsRemainUnchanged(
        genTreeOps operation, byte size, var_types baseType, bool scalar, NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            var type = Compiler.GetSimdTypeForSize(size);
            Assert.That(compiler.GetHWIntrinsicIdForBinOp(operation, Local(compiler, type),
                Local(compiler, type), baseType, size, scalar), Is.EqualTo(expected));
        });
    }

    [TestCase(GT_LT, TYP_FLOAT, false, NI_X86Base_CompareLessThan)]
    [TestCase(GT_LT, TYP_FLOAT, true, NI_X86Base_CompareNotLessThan)]
    [TestCase(GT_NE, TYP_DOUBLE, true, NI_X86Base_CompareEqual)]
    [TestCase(GT_EQ, TYP_UINT, false, NI_X86Base_CompareEqual)]
    [TestCase(GT_NE, TYP_INT, false, NI_Illegal)]
    public static void XarchComparisonReverseAndUnsupportedControlsRemainUnchanged(
        genTreeOps operation, var_types baseType, bool reverse, NamedIntrinsic expected)
    {
        WithCompiler(compiler => Assert.That(compiler.GetHWIntrinsicIdForCmpOp(operation, TYP_SIMD16,
            Local(compiler, TYP_SIMD16), Local(compiler, TYP_SIMD16), baseType, 16, false, reverse),
            Is.EqualTo(expected)));
    }

    private static void Enable(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
    }
#endif

#if TARGET_ARM64
    [TestCase(GT_ADD, (byte)8, TYP_LONG, false, NI_AdvSimd_AddScalar)]
    [TestCase(GT_DIV, (byte)8, TYP_DOUBLE, true, NI_AdvSimd_DivideScalar)]
    [TestCase(GT_MUL, (byte)16, TYP_LONG, false, NI_Illegal)]
    [TestCase(GT_MUL, (byte)16, TYP_DOUBLE, false, NI_AdvSimd_Arm64_Multiply)]
    public static void Arm64BinaryScalarAndUnsupportedControlsRemainUnchanged(
        genTreeOps operation, byte size, var_types baseType, bool scalar, NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            Assert.That(compiler.GetHWIntrinsicIdForBinOp(operation, Local(compiler, type),
                Local(compiler, type), baseType, size, scalar), Is.EqualTo(expected));
        });
    }

    [TestCase(GT_EQ, (byte)8, TYP_DOUBLE, false, NI_AdvSimd_Arm64_CompareEqualScalar)]
    [TestCase(GT_LT, (byte)16, TYP_UINT, false, NI_AdvSimd_CompareLessThan)]
    [TestCase(GT_NE, (byte)16, TYP_INT, false, NI_Illegal)]
    [TestCase(GT_NE, (byte)16, TYP_FLOAT, true, NI_AdvSimd_CompareEqual)]
    [TestCase(GT_EQ, (byte)16, TYP_FLOAT, true, NI_Illegal)]
    [TestCase(GT_GT, (byte)16, TYP_DOUBLE, true, NI_Illegal)]
    public static void Arm64ComparisonWidthAndFloatingReverseRestrictionsRemainUnchanged(
        genTreeOps operation, byte size, var_types baseType, bool reverse, NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            Assert.That(compiler.GetHWIntrinsicIdForCmpOp(operation, type, Local(compiler, type),
                Local(compiler, type), baseType, size, false, reverse), Is.EqualTo(expected));
        });
    }
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void MappingDoesNotRewriteNaNPayloadOrSignedZeroOperands(bool reverse)
    {
        WithCompiler(compiler =>
        {
            var left = compiler.gtNewVconNode(TYP_SIMD16);
            var right = compiler.gtNewVconNode(TYP_SIMD16);
            const ulong nanBits = 0x7FF8000000001234;
            const ulong negativeZeroBits = 0x8000000000000000;
            left.SimdVal.u64[0] = nanBits;
            right.SimdVal.u64[0] = negativeZeroBits;
            var count = compiler.lvaCount;
            _ = compiler.GetHWIntrinsicIdForCmpOp(GT_GT, TYP_SIMD16, left, right, TYP_DOUBLE, 16, false, reverse);
            _ = compiler.GetHWIntrinsicIdForBinOp(GT_ADD, left, right, TYP_DOUBLE, 16, false);
            Assert.That(left.SimdVal.u64[0], Is.EqualTo(nanBits));
            Assert.That(right.SimdVal.u64[0], Is.EqualTo(negativeZeroBits));
            Assert.That(compiler.lvaCount, Is.EqualTo(count));
        });
    }

    private static GenTreeLclVar Local(Compiler compiler, var_types type)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = type };

        return compiler.gtNewLclvNode(type, index);
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
