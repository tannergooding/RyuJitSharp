// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class RiscVEmitterFlagsOverloadTests
{
    [Test]
    public static void RegisterTransferFlagsOverloadTerminatesAtNyiBoundary()
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));

        var failure = Assert.Throws<FatalJitException>(() =>
            emitter.emitIns_R_R(INS_add, EA_8BYTE, REG_A0, REG_A1, default(insFlags)));

        Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(failure?.Message, Does.Contain("RISCV64: NYI"));
    }
}
#endif
