// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64CodeGenHelperCallTests
{
    [Test]
    public static void DirectHelperLookupReachesTheExplicitUnportedCallRecorder()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;

            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genEmitHelperCall(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN)) ??
                throw new AssertionException("The unported ARM64 call recorder did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Call instruction recording requires xarch."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }
}
#endif
