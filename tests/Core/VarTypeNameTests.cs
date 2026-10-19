// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class VarTypeNameTests
{
    [TestCase(TYP_UNDEF, "<UNDEF>")]
    [TestCase(TYP_INT, "int")]
    [TestCase(TYP_SIMD8, "simd8")]
    [TestCase(TYP_UNKNOWN, "unknown")]
    public static void NamePreservesDebugNamesAndReleaseEnumNames(var_types type, string debugName)
    {
        var expected = TestConfiguration.IsDebug ? debugName : type.ToString();
        Assert.That(type.Name, Is.EqualTo(expected));
    }
}
