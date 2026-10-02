// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LocalTypeQueryTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ParameterFlagsReadOnlyTheSelectedDescriptor(bool parameter, bool registerArgument)
    {
        var compiler = NewCompiler();
        compiler.lvaTable[0].lvIsParam = parameter;
        compiler.lvaTable[0].lvIsRegArg = registerArgument;
        compiler.lvaTable[1].lvIsParam = !parameter;
        compiler.lvaTable[1].lvIsRegArg = !registerArgument;

        Assert.That(compiler.lvaIsParameter(0), Is.EqualTo(parameter));
        Assert.That(compiler.lvaIsRegArgument(0), Is.EqualTo(registerArgument));
        Assert.That(compiler.lvaIsParameter(1), Is.EqualTo(!parameter));
        Assert.That(compiler.lvaIsRegArgument(1), Is.EqualTo(!registerArgument));
        Assert.That(compiler.lvaTable[0].lvIsParam, Is.EqualTo(parameter));
        Assert.That(compiler.lvaTable[0].lvIsRegArg, Is.EqualTo(registerArgument));
    }

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
#if FEATURE_SIMD
    [TestCase(TYP_SIMD16, TYP_SIMD16)]
#endif
    public static void ActualTypeNormalizesWithoutChangingDeclaredType(var_types type, var_types expected)
    {
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
            if (type >= TYP_COUNT)
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
#if TARGET_ARMARCH
#if CONFIGURABLE_ARM_ABI
        var softFP = compiler.opts.compUseSoftFP;
#else
        var softFP = Compiler.Options.compUseSoftFP;
#endif
        if (softFP || (TargetOS.IsWindows && varArgs))
        {
            expected = type switch {
                TYP_FLOAT => TYP_INT,
                TYP_DOUBLE => TYP_LONG,
#if FEATURE_SIMD
                TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 => TYP_STRUCT,
#if TARGET_ARM64
                TYP_SIMD => TYP_STRUCT,
#endif
#endif
                _ => type,
            };
        }
#endif
        Assert.That(compiler.mangleVarArgsType(type), Is.EqualTo(expected));
        Assert.That(compiler.info.compIsVarArgs, Is.EqualTo(varArgs));
    }

    private static Compiler NewCompiler()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;

        return compiler;
    }
}
