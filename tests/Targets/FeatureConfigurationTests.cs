// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FeatureConfigurationTests
{
    [Test]
    public static void MaskedHardwareIntrinsicsMatchTheTargetArchitecture()
    {
        Assert.That(Configuration.FEATURE_MASKED_HW_INTRINSICS,
            Is.EqualTo(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64));
    }

    [Test]
    public static void MaskTypeAndOperatorMetadataAreAvailableOnEveryTarget()
    {
        var maskOper = genTreeOps.GT_CNS_MSK;
        Assert.That(Globals.varTypeIsMask(var_types.TYP_MASK), Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That(maskOper.IsCnsMsk, Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That(maskOper.IsConst, Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That((int)VNFunc.VNF_CNS_MSK, Is.EqualTo((int)genTreeOps.GT_CNS_MSK));
    }

    [Test]
    public static void MaskConstantConstructionRequiresMaskedHardwareIntrinsics()
    {
        WithCompiler(compiler =>
        {
            if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                _ = new GenTreeMskCon(default);
            }
            else
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => new GenTreeMskCon(default));
            }
        });
    }

    [Test]
    public static void MaskValueNumberingMatchesTheConfiguredFeature()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNZeroForType(var_types.TYP_MASK));
                return;
            }

            var value = simdmask_t.AllBitsSet(3);
            var vn = store.VNForSimdMaskCon(value);

            Assert.That(store.TypeOfVN(vn), Is.EqualTo(var_types.TYP_MASK));
            Assert.That(store.GetConstantSimdMask(vn), Is.EqualTo(value));
            Assert.That(store.VNForSimdMaskCon(value), Is.EqualTo(vn));
            Assert.That(store.GetConstantSimdMaskValue(vn).IsScalable, Is.False);
        });
    }

    private static unsafe void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compCodeOpt = Compiler.BLENDED_CODE;
#if DEBUG
        compiler.info.compFullName = nameof(FeatureConfigurationTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }
}
