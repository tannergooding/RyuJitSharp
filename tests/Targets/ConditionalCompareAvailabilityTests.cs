// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.insCFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ConditionalCompareAvailabilityTests
{
    [Test]
    public static void CcmpConstructionIsExplicitlyUnsupportedOutsideX64AndArm64()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        try
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
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
