// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class GenTreeVarRefKindsTests
{
    [Test]
    public static void ValuesMatchNativeFlags()
    {
        Assert.That((int)varRefKinds.VR_INVARIANT, Is.Zero);
        Assert.That((int)varRefKinds.VR_NONE, Is.EqualTo((int)varRefKinds.VR_INVARIANT));
        Assert.That((int)varRefKinds.VR_IND_REF, Is.EqualTo(0x01));
        Assert.That((int)varRefKinds.VR_IND_SCL, Is.EqualTo(0x02));
        Assert.That((int)varRefKinds.VR_GLB_VAR, Is.EqualTo(0x04));
        Assert.That(varRefKinds.VR_IND_REF | varRefKinds.VR_GLB_VAR, Is.EqualTo((varRefKinds)0x05));
    }
}
