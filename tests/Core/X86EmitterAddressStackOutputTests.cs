// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class X86EmitterAddressStackOutputTests
{
    [TestCase(REG_NA, REG_NA, 0u, 0x12345678, "A178563412")]
    [TestCase(REG_EBX, REG_ECX, 1u, 127, "8B444B7F")]
    public static void AddressLoadsRetainAccumulatorMoffsetAndScaledSib(regNumber baseReg, regNumber index,
        uint scale, int displacement, string hex)
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            var id = NewAddress(emitter, EA_4BYTE, displacement);
            id.idIns(INS_mov);
            id.idInsFmt(IF_RWR_ARD);
            id.idReg1(REG_EAX);
            id.idAddr().iiaAddrMode.amBaseReg = baseReg;
            id.idAddr().iiaAddrMode.amIndxReg = index;
            id.idAddr().iiaAddrMode.amScale = scale;
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputAM(buffer, id, (ulong)insCodeRM(INS_mov), null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr attr, nint displacement);
}
#endif
