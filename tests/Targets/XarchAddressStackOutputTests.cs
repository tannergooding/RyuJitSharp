// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class XarchAddressStackOutputTests
{
    [TestCase(true, -16, "488B45F0")]
    [TestCase(false, 0, "488B0424")]
    public static void StackLoadsRetainFrameAndStackPointerDisplacements(bool framePointerBased, int frameOffset,
        string hex)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, emitter) =>
        {
            compiler.lvaTable[0].Type = TYP_LONG;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
            compiler.lvaTable[0].lvOnFrame = true;
            compiler.lvaTable[0].lvFramePointerBased = framePointerBased;
            var codeGen = compiler.codeGen ?? throw new AssertionException("Missing active code generator.");
            codeGen.IsFramePointerUsed = framePointerBased;
            compiler.lvaTable[0].StackOffset = frameOffset;
            emitter.emitIns_R_S(INS_mov, EA_8BYTE, REG_RAX, 0, 0);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No stack load was recorded.");
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputSV(buffer, id, (ulong)insCodeRM(INS_mov), null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
        });
    }

    [Test]
    public static void AddressLoadRetainsFullWidthDisplacement()
    {
        SysVX64EmitterCallTests.WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_AR(INS_mov, EA_8BYTE, REG_RAX, REG_RCX, 128);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No address load was recorded.");
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputAM(buffer, id, (ulong)insCodeRM(INS_mov), null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString("488B8180000000")));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);
}
#endif
