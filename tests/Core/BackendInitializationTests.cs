// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class BackendInitializationTests
{
    [Test]
    public static void CodeGenerationComponentsShareGcAndRegisterState()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);

        Assert.That(codeGen.GCInfo.Compiler, Is.SameAs(compiler));
        Assert.That(codeGen.RegSet.Compiler, Is.SameAs(compiler));
        Assert.That(Unsafe.AreSame(ref codeGen.GCInfo, ref codeGen.Emitter.GCInfo), Is.True);
        Assert.That(Unsafe.AreSame(ref codeGen.GCInfo, ref codeGen.RegSet.GCInfo), Is.True);
        Assert.That(Unsafe.AreSame(ref codeGen.RegSet, ref codeGen.GCInfo.RegSet), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FramePointerWriteResetPreservesTheCurrentValue(bool required)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler) {
            IsFramePointerRequired = required,
        };

        Assert.That(codeGen.IsFramePointerRequired, Is.EqualTo(required));
        codeGen.ResetWritePhaseForFramePointerRequired();
        Assert.That(codeGen.IsFramePointerRequired, Is.EqualTo(required));
        codeGen.ResetWritePhaseForFramePointerRequired();
        codeGen.IsFramePointerRequired = !required;
        Assert.That(codeGen.IsFramePointerRequired, Is.EqualTo(!required));
    }

    [Test]
    public static void SpillTemporarySizeIsUnknownUntilPreallocation()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);

        Assert.That(codeGen.RegSet.HasComputedTmpSize, Is.False);
        codeGen.RegSet.tmpBeginPreAllocateTemps();
        Assert.That(codeGen.RegSet.HasComputedTmpSize, Is.True);
        Assert.That(codeGen.RegSet.tmpTotalSize, Is.Zero);
        codeGen.RegSet.tmpInit();
        Assert.That(codeGen.RegSet.HasComputedTmpSize, Is.False);
    }
}
