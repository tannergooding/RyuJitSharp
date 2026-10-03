// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.insCond;

namespace RyuJitSharp.UnitTests;

internal static class Arm64ConditionMappingTests
{
    [TestCase(EJ_eq, INS_COND_EQ)]
    [TestCase(EJ_ne, INS_COND_NE)]
    [TestCase(EJ_hs, INS_COND_HS)]
    [TestCase(EJ_lo, INS_COND_LO)]
    [TestCase(EJ_mi, INS_COND_MI)]
    [TestCase(EJ_pl, INS_COND_PL)]
    [TestCase(EJ_vs, INS_COND_VS)]
    [TestCase(EJ_vc, INS_COND_VC)]
    [TestCase(EJ_hi, INS_COND_HI)]
    [TestCase(EJ_ls, INS_COND_LS)]
    [TestCase(EJ_ge, INS_COND_GE)]
    [TestCase(EJ_lt, INS_COND_LT)]
    [TestCase(EJ_gt, INS_COND_GT)]
    [TestCase(EJ_le, INS_COND_LE)]
    public static void JumpKindsMapToTheirArm64ConditionCodes(emitJumpKind condition, insCond expected)
    {
        Assert.That(CodeGen.JumpKindToInsCond(condition), Is.EqualTo(expected));
    }
}
#endif
