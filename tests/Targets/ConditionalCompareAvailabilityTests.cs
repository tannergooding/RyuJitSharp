// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insCFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ConditionalCompareAvailabilityTests
{
    [Test]
    public static void CcmpConstructionIsExplicitlyUnsupportedOutsideX64AndArm64()
    {
        WithCompiler(() =>
        {
            var op1 = new GenTreeIntCon(TYP_INT, 1);
            var op2 = new GenTreeIntCon(TYP_INT, 2);
            if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
            {
                _ = new GenTreeCCMP(TYP_VOID, new GenCondition(GenCondition.EQ), op1, op2, INS_FLAGS_NONE);
                return;
            }

            _ = Assert.Throws<FatalJitException>(() =>
                new GenTreeCCMP(TYP_VOID, new GenCondition(GenCondition.EQ), op1, op2, INS_FLAGS_NONE));
        });
    }

    [TestCase(GT_SELECT_INC)]
    [TestCase(GT_SELECT_INCCC)]
    [TestCase(GT_SELECT_INV)]
    [TestCase(GT_SELECT_INVCC)]
    [TestCase(GT_SELECT_NEG)]
    [TestCase(GT_SELECT_NEGCC)]
    public static void Arm64ConditionalSelectConstructionIsUnsupportedOutsideArm64(genTreeOps oper)
    {
        WithCompiler(() =>
        {
            var condition = new GenTreeIntCon(TYP_INT, 0);
            var op1 = new GenTreeIntCon(TYP_INT, 1);
            var op2 = new GenTreeIntCon(TYP_INT, 2);
            if (oper is GT_SELECT_INC or GT_SELECT_INV or GT_SELECT_NEG)
            {
                if (Configuration.TARGET_ARM64)
                {
                    _ = new GenTreeConditional(oper, TYP_INT, condition, op1, op2);
                    return;
                }

                _ = Assert.Throws<FatalJitException>(() =>
                    new GenTreeConditional(oper, TYP_INT, condition, op1, op2));
                return;
            }

            if (Configuration.TARGET_ARM64)
            {
                _ = new GenTreeOpCC(oper, TYP_INT, new GenCondition(GenCondition.EQ), op1, op2);
                return;
            }

            _ = Assert.Throws<FatalJitException>(() =>
                new GenTreeOpCC(oper, TYP_INT, new GenCondition(GenCondition.EQ), op1, op2));
        });
    }

    [TestCase(GT_BFIZ)]
    [TestCase(GT_BFX)]
    public static void Arm64BitfieldConstructionIsUnsupportedOutsideArm64(genTreeOps oper)
    {
        WithCompiler(() =>
        {
            var op1 = new GenTreeIntCon(TYP_INT, 1);
            var op2 = new GenTreeIntCon(TYP_INT, 2);
            if (Configuration.TARGET_ARM64)
            {
                if (oper is GT_BFIZ)
                {
                    _ = new GenTreeOp(oper, TYP_INT, op1, op2);
                }
                else
                {
                    _ = new GenTreeBfm(oper, TYP_INT, op1, null, 0, 8);
                }

                return;
            }

            if (oper is GT_BFIZ)
            {
                _ = Assert.Throws<FatalJitException>(() => new GenTreeOp(oper, TYP_INT, op1, op2));
            }
            else
            {
                _ = Assert.Throws<FatalJitException>(() => new GenTreeBfm(oper, TYP_INT, op1, null, 0, 8));
            }
        });
    }

    private static void WithCompiler(Action action)
    {
        using var tls = TestConfiguration.CreateJitTls();
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
