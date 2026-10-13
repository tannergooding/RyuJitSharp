// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.CorInfoType;

namespace RyuJitSharp.UnitTests;

internal static class CorInfoTypeMappingTests
{
    [Test]
    public static void NativeIntegerTypesUseConfiguredTargetWidth()
    {
        Assert.That(CORINFO_TYPE_NATIVEINT.VarType, Is.EqualTo(Globals.TYP_I_IMPL));
        Assert.That(CORINFO_TYPE_NATIVEUINT.VarType, Is.EqualTo(Globals.TYP_I_IMPL));
        Assert.That(CORINFO_TYPE_PTR.VarType, Is.EqualTo(Globals.TYP_I_IMPL));

        Assert.That(CORINFO_TYPE_NATIVEINT.PreciseVarType, Is.EqualTo(Globals.TYP_I_IMPL));
        Assert.That(CORINFO_TYPE_NATIVEUINT.PreciseVarType, Is.EqualTo(Globals.TYP_U_IMPL));
        Assert.That(CORINFO_TYPE_PTR.PreciseVarType, Is.EqualTo(Globals.TYP_U_IMPL));
    }
}
