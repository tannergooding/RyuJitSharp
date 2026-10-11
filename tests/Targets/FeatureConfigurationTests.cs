// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

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
    }

    [Test]
    public static void MaskConstantConstructionRequiresMaskedHardwareIntrinsics()
    {
        WithCompiler(() =>
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

    private static unsafe void WithCompiler(Action action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        try
        {
            action();
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
