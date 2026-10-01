// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
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
internal static unsafe class SimdShuffleTargetCompletionTests
{
    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_DOUBLE)]
    public static void IdentityShuffleReturnsTheOriginalEffectfulSource(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var effect = compiler.gtNewStoreLclVarNode(Local(compiler, TYP_SIMD16).LclNum,
                compiler.gtNewVconNode(TYP_SIMD16));
            var source = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD16, effect, Local(compiler, TYP_SIMD16));
            var indices = compiler.gtNewVconNode(TYP_SIMD16);
            for (var index = 0; index < (16 / baseType.Size); index++)
            {
                indices.SetElementIntegral(IndexType(baseType), index, index);
            }
            Assert.That(compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, baseType, 16, false),
                Is.SameAs(source));
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_FLOAT)]
    public static void AllOutOfRangeShufflePreservesEffectsThenZeroFills(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var effect = compiler.gtNewStoreLclVarNode(Local(compiler, TYP_SIMD16).LclNum,
                compiler.gtNewVconNode(TYP_SIMD16));
            var source = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD16, effect, Local(compiler, TYP_SIMD16));
            var indices = compiler.gtNewVconNode(TYP_SIMD16);
            var count = 16 / baseType.Size;
            for (var index = 0; index < count; index++)
            {
                indices.SetElementIntegral(IndexType(baseType), index, index == 0 ? -1 : count);
            }
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, baseType, 16, false).AsOp();
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(result.Op1, Is.SameAs(effect));
            Assert.That(result.Op2.IsVectorZero, Is.True);
            Assert.That(CountReferences(result, effect), Is.EqualTo(1));
        });
    }

    [Test]
    public static void NativeOutOfRangeByteShuffleUsesTheVariableHardwarePathRatherThanFoldingToZero()
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var indices = compiler.gtNewVconNode(TYP_SIMD16);
            for (var index = 0; index < 16; index++)
            {
                indices.SetElementIntegral(TYP_UBYTE, index, -1);
            }
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, TYP_BYTE, 16, true)
                .AsHWIntrinsic();
#if TARGET_XARCH
            var intrinsic = NI_X86Base_Shuffle;
#elif TARGET_ARM64
            var intrinsic = NI_AdvSimd_Arm64_VectorTableLookup;
#elif TARGET_WASM
            var intrinsic = NI_PackedSimd_Swizzle;
#else
#error Unsupported SIMD shuffle test target
#endif
            Assert.That(result.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(result.GetOp(1), Is.SameAs(source));
            Assert.That(result.GetOp(2), Is.SameAs(indices));
        });
    }

#if TARGET_ARM64 || TARGET_WASM
    [TestCase(false)]
    [TestCase(true)]
    public static void ByteVariableShuffleUsesTheLookupZeroFillWithoutAnAdditionalMask(bool native)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var indices = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, TYP_UBYTE, 16, native)
                .AsHWIntrinsic();
#if TARGET_ARM64
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Arm64_VectorTableLookup));
#else
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
#endif
            Assert.That(result.GetOp(1), Is.SameAs(source));
            Assert.That(result.GetOp(2), Is.SameAs(indices));
        });
    }

    [TestCase(TYP_SHORT)]
    [TestCase(TYP_USHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_ULONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void ConstantShuffleExpandsEveryElementByteAndZeroFillsInvalidSelectors(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var indices = compiler.gtNewVconNode(TYP_SIMD16);
            var count = 16 / baseType.Size;
            for (var index = 0; index < count; index++)
            {
                indices.SetElementIntegral(IndexType(baseType), index,
                    index == 0 ? count - 1 : index == 1 ? -1 : index - 2);
            }
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, baseType, 16, false)
                .AsHWIntrinsic();
#if TARGET_ARM64
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Arm64_VectorTableLookup));
#else
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
#endif
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType is TYP_USHORT or TYP_UINT or TYP_ULONG
                ? TYP_UBYTE : TYP_BYTE));
            Assert.That(result.GetOp(1), Is.SameAs(source));
            Assert.That(result.GetOp(2), Is.Not.SameAs(indices));
            for (var index = 0; index < count; index++)
            {
                var selector = index == 0 ? count - 1 : index - 2;
                for (var offset = 0; offset < baseType.Size; offset++)
                {
                    var expected = index == 1 ? 0xFF : (selector * baseType.Size) + offset;
                    Assert.That(result.GetOp(2).AsVecCon().SimdVal.u8[(index * baseType.Size) + offset],
                        Is.EqualTo(unchecked((byte)expected)));
                }
            }
        });
    }
#endif

#if TARGET_ARM64
    [Test]
    public static void Arm64ConstantHalfSwapCapturesEffectfulSourceOnlyOnce()
    {
        WithCompiler(compiler =>
        {
            var effect = compiler.gtNewStoreLclVarNode(Local(compiler, TYP_SIMD16).LclNum,
                compiler.gtNewVconNode(TYP_SIMD16));
            var value = Local(compiler, TYP_SIMD16);
            var source = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD16, effect, value);
            var indices = compiler.gtNewVconNode(TYP_SIMD16);
            indices.SetElementIntegral(TYP_UINT, 0, 2);
            indices.SetElementIntegral(TYP_UINT, 1, 3);
            indices.SetElementIntegral(TYP_UINT, 2, 0);
            indices.SetElementIntegral(TYP_UINT, 3, 1);
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, TYP_INT, 16, false)
                .AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_ExtractVector128));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_ULONG));
            Assert.That(result.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)1));
            var capture = result.GetOp(1).AsOp();
            Assert.That(capture.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(capture.Op1, Is.SameAs(source));
            var hoisted = capture.Op1.AsOp();
            Assert.That(hoisted.Op1, Is.SameAs(effect));
            Assert.That(hoisted.Op2.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            var store = hoisted.Op2.AsLclVarCommon();
            Assert.That(store.Data, Is.SameAs(value));
            Assert.That(capture.Op2.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(result.GetOp(2).Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(capture.Op2.AsLclVarCommon().LclNum, Is.EqualTo(store.LclNum));
            Assert.That(result.GetOp(2).AsLclVarCommon().LclNum, Is.EqualTo(store.LclNum));
            Assert.That(result.GetOp(2), Is.Not.SameAs(capture.Op2));
            Assert.That(CountReferences(result, effect), Is.EqualTo(1));
        });
    }

    [TestCase((byte)8, TYP_LONG, NI_AdvSimd_ShiftLeftLogicalScalar, TYP_LONG)]
    [TestCase((byte)8, TYP_FLOAT, NI_AdvSimd_ShiftLeftLogical, TYP_INT)]
    [TestCase((byte)16, TYP_INT, NI_AdvSimd_ShiftLeftLogical, TYP_UINT)]
    [TestCase((byte)16, TYP_DOUBLE, NI_AdvSimd_ShiftLeftLogical, TYP_LONG)]
    public static void Arm64VariableShuffleUsesNativeIndexScalingAndUnsignedMask(
        byte size, var_types baseType, NamedIntrinsic shiftIntrinsic, var_types shiftBaseType)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var source = Local(compiler, type);
            var indices = Local(compiler, type);
            var result = compiler.gtNewSimdShuffleNode(type, source, indices, baseType, size, false).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_And));
            var lookup = result.GetOp(1).AsHWIntrinsic();
            Assert.That(lookup.GetOp(1), Is.SameAs(source));
            var expanded = lookup.GetOp(2).AsHWIntrinsic();
            Assert.That(expanded.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Or));
            var broadcast = expanded.GetOp(1).AsHWIntrinsic();
            var shift = broadcast.GetOp(1).AsHWIntrinsic();
            Assert.That(shift.HWIntrinsicId, Is.EqualTo(shiftIntrinsic));
            Assert.That(shift.SimdBaseType, Is.EqualTo(shiftBaseType));
            Assert.That(shift.GetOp(1), Is.SameAs(indices));
            Assert.That(shift.GetOp(2).AsIntCon().IconValue,
                Is.EqualTo((nint)int.TrailingZeroCount(baseType.Size)));
            for (var index = 0; index < size; index++)
            {
                Assert.That(broadcast.GetOp(2).AsVecCon().SimdVal.u8[index],
                    Is.EqualTo(unchecked((byte)((index / baseType.Size) * baseType.Size))));
                Assert.That(expanded.GetOp(2).AsVecCon().SimdVal.u8[index],
                    Is.EqualTo(unchecked((byte)(index & (baseType.Size - 1)))));
            }
            var mask = result.GetOp(2).AsHWIntrinsic();
            Assert.That(mask.SimdBaseType, Is.EqualTo(IndexType(baseType)));
            Assert.That(mask.GetOp(1).AsLclVarCommon().LclNum, Is.EqualTo(indices.LclNum));
            Assert.That(mask.GetOp(1), Is.Not.SameAs(indices));
            Assert.That(mask.GetOp(2).GetIntegralVectorConstElement(0, IndexType(baseType)),
                Is.EqualTo(unchecked((ulong)(size / baseType.Size))));
        });
    }

    [TestCase((byte)8, TYP_LONG)]
    [TestCase((byte)16, TYP_UINT)]
    [TestCase((byte)16, TYP_FLOAT)]
    [TestCase((byte)16, TYP_DOUBLE)]
    public static void Arm64NativeWideElementShuffleDoesNotAddTheSafeRangeMask(byte size, var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var source = Local(compiler, type);
            var indices = Local(compiler, type);
            var result = compiler.gtNewSimdShuffleNode(type, source, indices, baseType, size, true).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(size == 16
                ? NI_AdvSimd_Arm64_VectorTableLookup : NI_AdvSimd_VectorTableLookup));
            Assert.That(result.GetOp(1), Is.SameAs(source));
            var expanded = result.GetOp(2).AsHWIntrinsic();
            Assert.That(expanded.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Or));
            var shift = expanded.GetOp(1).AsHWIntrinsic().GetOp(1).AsHWIntrinsic();
            Assert.That(shift.GetOp(1), Is.SameAs(indices));
        });
    }
#endif

#if TARGET_XARCH
    [Test]
    public static void ReorderedNativeShuffleCapturesSourceBeforeIndicesOverwriteIt()
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            var source = Local(compiler, TYP_SIMD32);
            var effect = compiler.gtNewStoreLclVarNode(source.LclNum, compiler.gtNewVconNode(TYP_SIMD32));
            var indicesValue = Local(compiler, TYP_SIMD32);
            var indices = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD32, effect, indicesValue);
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD32, source, indices, TYP_INT, 32, true)
                .AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AVX2_PermuteVar8x32));
            var reorderedIndices = result.GetOp(1).AsOp();
            Assert.That(reorderedIndices.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(reorderedIndices.Op1.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            var store = reorderedIndices.Op1.AsLclVarCommon();
            Assert.That(store.Data, Is.SameAs(source));
            Assert.That(reorderedIndices.Op2, Is.SameAs(indices));
            Assert.That(indices.AsOp().Op1, Is.SameAs(effect));
            Assert.That(indices.AsOp().Op2, Is.SameAs(indicesValue));
            Assert.That(result.GetOp(2).Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(result.GetOp(2).AsLclVarCommon().LclNum, Is.EqualTo(store.LclNum));
            Assert.That(CountReferences(result, effect), Is.EqualTo(1));
            Assert.That(CountReferences(result, source), Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void XarchVariableByteShuffleMasksOnlyTheSafeOperation(bool native)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var indices = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD16, source, indices, TYP_BYTE, 16, native)
                .AsHWIntrinsic();
            if (native)
            {
                Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_X86Base_Shuffle));
                Assert.That(result.GetOp(2), Is.SameAs(indices));
            }
            else
            {
                Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_X86Base_And));
                Assert.That(result.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_X86Base_Shuffle));
                Assert.That(result.GetOp(2).AsHWIntrinsic().SimdBaseType, Is.EqualTo(TYP_BYTE));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ShuffleValidityPreservesIsaGatingAndCanBecomeValid(bool enabled)
    {
        WithCompiler(compiler =>
        {
            if (enabled)
            {
                Enable(compiler, InstructionSet_AVX2);
            }
            var indices = Local(compiler, TYP_SIMD32);
            var result = compiler.IsValidForShuffle(indices, 32, TYP_INT, out var canBecomeValid, false);
            Assert.That(result, Is.EqualTo(enabled));
            Assert.That(canBecomeValid, Is.EqualTo(enabled));
        });
    }

    [TestCase(TYP_BYTE, false)]
    [TestCase(TYP_BYTE, true)]
    [TestCase(TYP_INT, false)]
    public static void ShuffleValidityPreservesWideByteIsaRestriction(var_types baseType, bool enabled)
    {
        WithCompiler(compiler =>
        {
            if (enabled)
            {
                Enable(compiler, InstructionSet_AVX512v2);
            }
            var indices = Local(compiler, TYP_SIMD64);
            var expected = enabled || (baseType == TYP_INT);
            Assert.That(compiler.IsValidForShuffle(indices, 64, baseType, out var canBecomeValid, false),
                Is.EqualTo(expected));
            Assert.That(canBecomeValid, Is.EqualTo(expected));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void NativeConstantEligibilityScanDoesNotChangeTheEligibilityResult(bool native, bool invalid)
    {
        WithCompiler(compiler =>
        {
            var indices = compiler.gtNewVconNode(TYP_SIMD16);
            for (var index = 0; index < 4; index++)
            {
                indices.SetElementIntegral(TYP_UINT, index, (invalid && (index == 1)) ? -1 : index);
            }
            Assert.That(compiler.IsValidForShuffle(indices, 16, TYP_INT, out var canBecomeValid, native), Is.True);
            Assert.That(canBecomeValid, Is.True);
        });
    }

    private static void Enable(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
    }
#endif

    private static var_types IndexType(var_types type)
    {
        return type.Size switch
        {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            4 => TYP_UINT,
            _ => TYP_ULONG,
        };
    }

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
