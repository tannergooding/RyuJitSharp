// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.FuncKind;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class FuncletCreationTargetTests
{
    [Test]
    public static void RootDescriptorUsesNativeTargetDefaults()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.compHndBBtabCount = 0;

        Assert.That(compiler.fgCreateFunclets(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        Assert.That(compiler.fgFuncletsCreated, Is.True);
        Assert.That(compiler.compFuncInfoCount, Is.EqualTo(1));
        Assert.That(compiler.compFuncInfos[0].funKind, Is.EqualTo(FUNC_ROOT));
#if !HAS_FIXED_REGISTER_SET
        Assert.That(compiler.compFuncInfos[0].funStackPointerReg, Is.EqualTo(REG_NA));
        Assert.That(compiler.compFuncInfos[0].funFramePointerReg, Is.EqualTo(REG_NA));
#endif
#if TARGET_WASM
        Assert.That(compiler.compFuncInfos[0].funWasmLocalDecls, Is.Null);
        Assert.That(compiler.compFuncInfos[0].funWasmExnRefLocalIndex, Is.EqualTo(uint.MaxValue));
#endif
    }
}
