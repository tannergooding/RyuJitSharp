// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class X86EmitterAddressStackOutputTests
{
    [Test]
    public static void ZeroOffsetEpilogStackAddressRetainsLea()
    {
        Assume.That(Configuration.TARGET_X86, "requires the x86 target");

        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_ARX(INS_lea, EA_4BYTE, REG_X86_ESP, REG_X86_EBP, REG_X86_NA, 1, 0);

            var descriptor = LastInstruction(emitter)
                ?? throw new AssertionException("No epilog stack-address instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_lea));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_X86_ESP));
            Assert.That(descriptor.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_X86_EBP));
            Assert.That(descriptor.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_X86_NA));
        });
    }

    [TestCase(REG_X86_NA, REG_X86_NA, 0u, 0x12345678, "A178563412")]
    [TestCase(REG_X86_EBX, REG_X86_ECX, 1u, 127, "8B444B7F")]
    public static void AddressLoadsRetainAccumulatorMoffsetAndScaledSib(regNumber baseReg, regNumber index,
        uint scale, int displacement, string hex)
    {
        Assume.That(Configuration.TARGET_X86, "requires the x86 target");

        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            var id = NewAddress(emitter, EA_4BYTE, displacement);
            id.idIns(INS_mov);
            id.idInsFmt(IF_XARCH_RWR_ARD);
            id.idReg1(REG_X86_EAX);
            id.idAddr().iiaAddrMode.amBaseReg = baseReg;
            id.idAddr().iiaAddrMode.amIndxReg = index;
            id.idAddr().iiaAddrMode.amScale = scale;
            var buffer = stackalloc byte[32];
            X86EmitterStaticOutputTests.SetEmitIssuing(emitter);
            var end = emitter.emitOutputAM(buffer, id, (ulong)insCodeRM(INS_mov), null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr attr, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);
}
