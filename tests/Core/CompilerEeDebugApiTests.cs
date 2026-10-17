// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerEeDebugApiTests
{
    [Test]
    public static void EeTryGetClassSizeRejectsReleaseAccess()
    {
        if (!TestConfiguration.IsDebug)
        {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            _ = Assert.Throws<InvalidOperationException>(() => compiler.eeTryGetClassSize(default));
        }
    }
}
