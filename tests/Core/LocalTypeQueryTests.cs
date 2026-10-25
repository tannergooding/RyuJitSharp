// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LocalTypeQueryTests
{
    [TestCase(TYP_BYTE, TYP_INT)]
    [TestCase(TYP_UBYTE, TYP_INT)]
    [TestCase(TYP_SHORT, TYP_INT)]
    [TestCase(TYP_USHORT, TYP_INT)]
    [TestCase(TYP_INT, TYP_INT)]
    [TestCase(TYP_UINT, TYP_INT)]
    [TestCase(TYP_LONG, TYP_LONG)]
    [TestCase(TYP_ULONG, TYP_LONG)]
    [TestCase(TYP_FLOAT, TYP_FLOAT)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE)]
    [TestCase(TYP_REF, TYP_REF)]
    [TestCase(TYP_BYREF, TYP_BYREF)]
    [TestCase(TYP_STRUCT, TYP_STRUCT)]
    [TestCase(TYP_SIMD16, TYP_SIMD16)]
    public static void ActualTypeNormalizesWithoutChangingDeclaredType(var_types type, var_types expected)
    {
        if (type is TYP_SIMD16)
        {
            Assume.That(Configuration.FEATURE_SIMD);
        }

        var compiler = NewCompiler();
        compiler.lvaTable[0].Type = type;
        compiler.lvaTable[1].Type = type;
        compiler.lvaTable[1].lvIsParam = true;

        Assert.That(compiler.lvaGetActualType(0), Is.EqualTo(expected));
        Assert.That(compiler.lvaGetActualType(1), Is.EqualTo(expected));
        Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(type));
        Assert.That(compiler.lvaTable[1].Type, Is.EqualTo(type));
    }

    private static IEnumerable<TestCaseData> ManglingCases()
    {
        foreach (var type in Enum.GetValues<var_types>())
        {
            if (type >= TYP_SIMD_ARM64)
            {
                continue;
            }

            yield return new TestCaseData(type, false);
            yield return new TestCaseData(type, true);
        }
    }

    [TestCaseSource(nameof(ManglingCases))]
    public static void VarArgsManglingPreservesTargetOsAndUnchangedTypes(var_types type, bool varArgs)
    {
        var compiler = NewCompiler();
        compiler.info.compIsVarArgs = varArgs;
        var expected = type;
        if (Configuration.TARGET_ARMARCH)
        {
            var softFP = compiler.opts.compUseSoftFP;
            if (softFP || (TargetOS.IsWindows && varArgs))
            {
                expected = type switch {
                    TYP_FLOAT => TYP_INT,
                    TYP_DOUBLE => TYP_LONG,
                    _ => type,
                };
                if (Configuration.FEATURE_SIMD &&
                    (type is TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16))
                {
                    expected = TYP_STRUCT;
                }
                if (Configuration.FEATURE_SIMD && Configuration.TARGET_ARM64 &&
                    type is TYP_SIMD_ARM64)
                {
                    expected = TYP_STRUCT;
                }
            }
        }
        Assert.That(compiler.mangleVarArgsType(type), Is.EqualTo(expected));
        Assert.That(compiler.info.compIsVarArgs, Is.EqualTo(varArgs));
    }

    private static Compiler NewCompiler()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.opts.compUseSoftFP = Configuration.ARM_SOFTFP && Configuration.TARGET_ARM;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;

        return compiler;
    }
}
