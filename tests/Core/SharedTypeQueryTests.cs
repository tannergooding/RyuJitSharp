// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class SharedTypeQueryTests
{
    private static IEnumerable<var_types> Types()
    {
        foreach (var type in Enum.GetValues<var_types>())
        {
            yield return type;
        }

        yield return (var_types)byte.MaxValue;
    }

    [TestCaseSource(nameof(Types))]
    public static void GcDiagnosticUsesExactNativeTokensAndDefault(var_types type)
    {
        var expected = type == TYP_REF ? "gcr" : type == TYP_BYREF ? "byr" : "non";

        Assert.That(varTypeGCstring(type), Is.EqualTo(expected));
    }
}
