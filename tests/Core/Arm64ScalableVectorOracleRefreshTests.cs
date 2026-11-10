// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64ScalableVectorOracleRefreshTests
{
    [Test]
    public static void VectorTResolvesGenericVectorIntrinsics()
    {
        Assume.That(Configuration.TARGET_ARM64 && Configuration.FEATURE_HW_INTRINSICS && Configuration.FEATURE_SIMD,
            "This test requires ARM64 hardware-intrinsic and SIMD support.");

        CORINFO_SIG_INFO sig = default;
        sig.numArgs = 2;

        var intrinsic = HWIntrinsicInfo.LookupId(&sig, InstructionSet_VectorT, "op_Addition"u8);
        Assert.That(intrinsic, Is.EqualTo(NI_Vector_op_Addition));

        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            EnableHardwareIntrinsics(ref JitConfig) = 1;
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_VectorT);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_VectorT);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_VectorT);

            Assert.That(
                ResolveHardwareIntrinsicId(compiler, intrinsic, InstructionSet_VectorT, isXplatIntrinsic: true),
                Is.EqualTo(intrinsic));
        });
    }

    [TestCase(TYP_INT, 1UL)]
    [TestCase(TYP_FLOAT, 0x3F800000UL)]
    [TestCase(TYP_DOUBLE, 0x3FF0000000000000UL)]
    public static void VectorOneImportsAsARepeatedScalableConstant(var_types baseType, ulong value)
    {
        Assume.That(Configuration.TARGET_ARM64 && Configuration.FEATURE_HW_INTRINSICS && Configuration.FEATURE_SIMD,
            "This test requires ARM64 hardware-intrinsic and SIMD support.");

        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            CORINFO_SIG_INFO sig = default;
            var result = ImportXplat(compiler, NI_Vector_get_One, default, default, in sig, default,
                baseType, TYP_SIMD_ARM64, SIZE_UNKNOWN, false);
            var constant = result?.AsVecCon() ?? throw new AssertionException("Expected a scalable Vector.One constant.");

            Assert.That(constant.Type, Is.EqualTo(TYP_SIMD_ARM64));
            Assert.That(constant.SimdScalableVal.BaseType, Is.EqualTo(baseType));
            Assert.That(constant.SimdScalableVal.Kind, Is.EqualTo(SimdScalableRepeated));
            Assert.That(constant.SimdScalableVal.Index.u64[0], Is.EqualTo(value));
            Assert.That(constant.SimdScalableVal.Step.u64[0], Is.Zero);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveHardwareIntrinsicId")]
    private static extern NamedIntrinsic ResolveHardwareIntrinsicId(
        Compiler compiler, NamedIntrinsic intrinsic, ManagedInstructionSet isa, bool isXplatIntrinsic);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impXplatIntrinsic")]
    private static extern GenTree? ImportXplat(
        Compiler compiler,
        NamedIntrinsic intrinsic,
        CORINFO_CLASS_STRUCT_* clsHnd,
        CORINFO_METHOD_STRUCT_* method,
        in CORINFO_SIG_INFO sig,
        in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType,
        var_types retType,
        byte simdSize,
        bool mustExpand);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableHWIntrinsic")]
    private static extern ref int EnableHardwareIntrinsics(ref JitConfigValues jitConfig);
}
