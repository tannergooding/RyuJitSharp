// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class ClassLayoutRegisterTypeTests
{
    [TestCase(0, TYP_UNDEF)]
    [TestCase(1, TYP_UBYTE)]
    [TestCase(2, TYP_USHORT)]
    [TestCase(4, TYP_INT)]
#if TARGET_64BIT || TARGET_WASM
    [TestCase(8, TYP_LONG)]
#else
    [TestCase(8, TYP_UNDEF)]
#endif
    [TestCase(12, TYP_UNDEF)]
#if FEATURE_SIMD
    [TestCase(16, TYP_SIMD16)]
#else
    [TestCase(16, TYP_UNDEF)]
#endif
    public static void RegisterTypePreservesTargetScalarAndSimdWidths(int size, var_types expected)
    {
        var layout = new ClassLayout(size);

        Assert.That(layout.RegisterType, Is.EqualTo(expected));
    }
}
