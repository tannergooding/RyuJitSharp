// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class ClassLayoutRegisterTypeTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(12)]
    [TestCase(16)]
    public static void RegisterTypePreservesTargetScalarAndSimdWidths(int size)
    {
        var layout = new ClassLayout(size);
        var expectedType = size switch
        {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            4 => TYP_INT,
            8 => Configuration.TARGET_64BIT || Configuration.TARGET_WASM ? TYP_LONG : TYP_UNDEF,
            16 => Configuration.FEATURE_SIMD ? TYP_SIMD16 : TYP_UNDEF,
            _ => TYP_UNDEF,
        };

        Assert.That(layout.RegisterType, Is.EqualTo(expectedType));
    }
}
