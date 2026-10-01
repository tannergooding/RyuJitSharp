// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
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
internal static unsafe class HardwareIntrinsicPropertyCompletionTests
{
    [Test]
    public static void TernaryRmwMatchesAllPinnedTruthTableUseFlags()
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            for (var control = 0; control <= byte.MaxValue; control++)
            {
                var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_TernaryLogic, TYP_INT, 16,
                    value, value, value, compiler.gtNewIconNode(TYP_INT, control));
                var expected = TernaryLogicInfo.Lookup((byte)control).GetAllUseFlags() == TernaryLogicUseFlags.ABC;
                Assert.That(node.IsRmwHWIntrinsic(compiler), Is.EqualTo(expected), $"Control {control:X2}");
            }
        });
    }

    [TestCase(TYP_FLOAT, false, 3, true)]
    [TestCase(TYP_DOUBLE, false, 3, false)]
    [TestCase(TYP_DOUBLE, false, 2, true)]
    [TestCase(TYP_FLOAT, true, 1, false)]
    [TestCase(TYP_DOUBLE, true, 2, false)]
    [TestCase(TYP_DOUBLE, true, 0, true)]
    public static void FixupRmwReadsOnlyTheNativeTableWords(
        var_types baseType, bool scalar, int zeroWord, bool expected)
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            var table = compiler.gtNewVconNode(TYP_SIMD16);
            for (var index = 0; index < 4; index++)
            {
                table.SimdVal.u32[index] = 0x1111_1111;
            }
            table.SimdVal.u32[zeroWord] = 0x1111_1110;
            var intrinsic = scalar ? NI_AVX512_FixupScalar : NI_AVX512_Fixup;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, intrinsic, baseType, 16,
                value, value, table, compiler.gtNewIconNode(TYP_INT, 0));

            Assert.That(node.IsRmwHWIntrinsic(compiler), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonconstantFixupOperandsRemainRmw(bool dynamicControl)
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            var table = compiler.gtNewVconNode(TYP_SIMD16);
            table.SimdVal.AsSpan<uint>().Fill(0x1111_1111);
            GenTree tableOperand = dynamicControl ? table : new GenTreeLclVar(TYP_SIMD16, 0);
            GenTree control = dynamicControl ? new GenTreeLclVar(TYP_INT, 1)
                : compiler.gtNewIconNode(TYP_INT, 0);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_Fixup, TYP_FLOAT, 16,
                value, value, tableOperand, control);

            Assert.That(node.IsRmwHWIntrinsic(compiler), Is.True);
        });
    }

    [Test]
    public static void MergeDestinationsAreRmwUnlessContainedZero(
        [Values(NI_AVX512_CompressMask, NI_AVX512_ExpandMask, NI_AVX512_BlendVariableMask)] NamedIntrinsic intrinsic,
        [Values(false, true)] bool contained)
    {
        WithCompiler(compiler =>
        {
            var destination = compiler.gtNewVconNode(TYP_SIMD16);
            destination.IsContained = contained;
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            var operation = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_Add, TYP_INT, 16, value, value);
            operation.Flags |= GTF_HW_EM_OP;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, intrinsic, TYP_INT, 16,
                destination, operation, new GenTreeLclVar(TYP_MASK, 1));

            Assert.That(node.IsRmwHWIntrinsic(compiler), Is.EqualTo(!contained));
        });
    }

    [Test]
    public static void BlendWithoutAnEmbeddedOperationIsNotRmw()
    {
        WithCompiler(compiler =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_BlendVariableMask, TYP_INT, 16,
                compiler.gtNewVconNode(TYP_SIMD16), new GenTreeLclVar(TYP_SIMD16, 0),
                new GenTreeLclVar(TYP_MASK, 1));

            Assert.That(node.IsRmwHWIntrinsic(compiler), Is.False);
        });
    }

    [TestCase((byte)16, TYP_INT, TYP_LONG, 2)]
    [TestCase((byte)16, TYP_UINT, TYP_ULONG, 2)]
    [TestCase((byte)32, TYP_FLOAT, TYP_DOUBLE, 4)]
    [TestCase((byte)64, TYP_FLOAT, TYP_DOUBLE, 8)]
    public static void BitwiseMaskReinterpretationPreservesLaneScalingAndWrapperAgreement(
        byte size, var_types baseType, var_types expectedType, int maskSize)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var value = compiler.gtNewVconNode(type);
            var node = new GenTreeHWIntrinsic(type, NI_AVX512_And, baseType, size, value, value);
            var targetType = TYP_UNKNOWN;
            var wrapperType = TYP_UNKNOWN;

            Assert.That(node.IsEmbeddedMaskingCompatible(compiler, maskSize, ref targetType,
                out var broadcastOperandIndex), Is.True);
            Assert.That(targetType, Is.EqualTo(expectedType));
            Assert.That(broadcastOperandIndex, Is.Zero);
            Assert.That(node.IsEmbeddedMaskingCompatible(compiler, maskSize, ref wrapperType), Is.True);
            Assert.That(wrapperType, Is.EqualTo(targetType));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MaskingConfigurationGatePreservesEarlyReturnOutputContracts(bool enabled)
    {
        WithCompiler(compiler =>
        {
            EnableEmbeddedMasking(ref JitConfig) = enabled ? 1 : 0;
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_Add, TYP_FLOAT, 16, value, value);
            var targetType = TYP_ULONG;
            var result = node.IsEmbeddedMaskingCompatible(compiler, 4, ref targetType, out var broadcastOperandIndex);

            Assert.That(result, Is.EqualTo(enabled));
            Assert.That(targetType, Is.EqualTo(enabled ? TYP_UNDEF : TYP_ULONG));
            Assert.That(broadcastOperandIndex, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedMemoryCannotHaveItsFaultSuppressedByAMask(bool broadcast)
    {
        WithCompiler(compiler =>
        {
            var address = compiler.gtNewZeroConNode(TYP_BYREF);
            GenTree memory;
            if (broadcast)
            {
                memory = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX_BroadcastScalarToVector128,
                    TYP_FLOAT, 16, address);
            }
            else
            {
                memory = new GenTreeIndir(GT_IND, TYP_SIMD16, address);
            }
            memory.Flags |= GTF_EXCEPT;
            memory.IsContained = true;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_Add, TYP_FLOAT, 16,
                compiler.gtNewVconNode(TYP_SIMD16), memory);
            var targetType = TYP_ULONG;

            Assert.That(node.IsEmbeddedMaskingCompatible(compiler, 4, ref targetType,
                out var broadcastOperandIndex), Is.False);
            Assert.That(targetType, Is.EqualTo(TYP_ULONG));
            Assert.That(broadcastOperandIndex, Is.Zero);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableEmbeddedMasking")]
    private static extern ref int EnableEmbeddedMasking(ref JitConfigValues config);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        EnableEmbeddedMasking(ref JitConfig) = 1;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info = new Compiler.Info();
        foreach (var instructionSet in new[] { InstructionSet_AVX, InstructionSet_AVX512 })
        {
            compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
            compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
        }
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.Emitter.emitBegCG(compiler, null);
        codeGen.Emitter.UseVexEncodings = true;
        codeGen.Emitter.UseEvexEncodings = true;
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
