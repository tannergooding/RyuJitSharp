// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class CompilerHelperReturnTypeTests
{
    [Test]
    public static void HelperReturnTypesMatchTargetConfiguration()
    {
        Assert.That(Compiler.HelperInitClassRetType,
            Is.EqualTo(Configuration.TARGET_WASM ? TYP_I_IMPL : var_types.TYP_VOID));
        Assert.That(Compiler.HelperUnboxDiscardedRetType,
            Is.EqualTo(Configuration.TARGET_WASM ? var_types.TYP_BYREF : var_types.TYP_VOID));
    }
}
