// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

internal static class GenTreeOpsIdentityTests
{
    [Test]
    public static void TreeOperationAndValueNumberMetadataUseTheUniversalIdentity()
    {
        var operations = Enum.GetValues<genTreeOps>();

        Assert.That(operations[^1], Is.EqualTo(GT_COUNT));
        Assert.That((int)VNF_Boundary, Is.EqualTo((int)GT_COUNT));

        foreach (var oper in operations)
        {
            if (oper is GT_COUNT)
            {
                continue;
            }

            var name = oper.ToString();
            var useShortName = TestConfiguration.IsDebug || Globals.JitConfig.DisplayMemStats != 0;
            Assert.That(oper.Name, Is.EqualTo(useShortName ? name[3..] : name), name);
            _ = oper.Kind;
            Assert.That(Enum.TryParse<VNFunc>($"VNF_{name[3..]}", out var func), Is.True, name);
            Assert.That((int)func, Is.EqualTo((int)oper), name);

            var arity = oper.IsUnary ? 1 : oper.IsBinary ? 2 : oper is GT_SELECT ? 3 : 0;
            Assert.That(ValueNumStore.VNFuncArity(func), Is.EqualTo(arity), name);
            Assert.That(ValueNumStore.VNFuncIsComparison(func), Is.EqualTo(oper.IsCompare), name);

            if (oper.IsCompare)
            {
                Assert.That(oper.ReverseRelop.ReverseRelop, Is.EqualTo(oper), name);
                Assert.That(oper.SwapRelop.SwapRelop, Is.EqualTo(oper), name);
            }

            if (TestConfiguration.IsDebug)
            {
                _ = oper.DebugKind;
                Assert.That(oper.StructType, Is.Not.Null, name);
            }
        }

        if (!TestConfiguration.IsDebug)
        {
            Assert.Throws<InvalidOperationException>(() => _ = GT_NONE.DebugKind);
        }

        Assert.That(GT_BITTEST_EQ.IsCompare, Is.EqualTo(Configuration.TARGET_XARCH));
        Assert.That(GT_CNS_VEC.IsCnsVec, Is.EqualTo(Configuration.FEATURE_SIMD));
        Assert.That(GT_CNS_VEC.IsConst, Is.EqualTo(Configuration.FEATURE_SIMD));
        Assert.That(GT_CNS_MSK.IsConst,
            Is.EqualTo(Configuration.FEATURE_SIMD && Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That(GT_HWINTRINSIC.IsHWIntrinsic, Is.EqualTo(Configuration.FEATURE_HW_INTRINSICS));
        Assert.That(GT_HWINTRINSIC.IsMultiOp, Is.EqualTo(Configuration.FEATURE_HW_INTRINSICS));
        Assert.That(GT_MUL_LONG.IsMul, Is.EqualTo(Configuration.TARGET_32BIT || Configuration.TARGET_ARM64));
        Assert.That(GT_MUL_LONG.IsMultiRegOp, Is.EqualTo(Configuration.TARGET_32BIT));
        Assert.That(GT_CNS_INT.IsCnsNativeLong, Is.EqualTo(Configuration.TARGET_64BIT));
        Assert.That(GT_CNS_LNG.IsCnsNativeLong, Is.EqualTo(!Configuration.TARGET_64BIT));
        Assert.That(GT_CNS_LNG.IsLong, Is.EqualTo(Configuration.TARGET_32BIT));

#if DEBUG
        var mulLongType = Configuration.TARGET_ARM64 ? typeof(GenTreeOp) : typeof(GenTreeMultiRegOp);
        Assert.That(GT_MUL_LONG.StructType, Is.EqualTo(mulLongType));
#endif
    }
}
