// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class RiscVInstructionEncodingTests
{
    [Test]
    public static void EncodesRiscVInstructionFormats()
    {
        Assert.That(Emitter.insEncodeRTypeInstr(0x33, 0x1b, 6, 0x13, 0x16, 0x55), Is.EqualTo(0xAB69EDB3u));
        Assert.That(Emitter.insEncodeITypeInstr(0x13, 5, 7, 27, 0xA95), Is.EqualTo(0xA95DF293u));
        Assert.That(Emitter.insEncodeSTypeInstr(0x23, 3, 9, 26, 0xA6D), Is.EqualTo(0xA7A4B6A3u));
        Assert.That(Emitter.insEncodeUTypeInstr(0x37, 17, 0xABCDE), Is.EqualTo(0xABCDE8B7u));
        Assert.That(Emitter.insEncodeBTypeInstr(0x63, 5, 21, 9, 0x157A), Is.EqualTo(0xD69ADD63u));
        Assert.That(Emitter.insEncodeJTypeInstr(0x6F, 13, 0x1ABCD6), Is.EqualTo(0xCD7AB6EFu));
    }

    [Test]
    public static void MapsCompressedRegistersAndArithmeticInstructions()
    {
        var compressedRegisters = new (regNumber Register, uint Encoded)[]
        {
            (REG_FP, 0),
            (REG_S1, 1),
            (REG_A0, 2),
            (REG_A1, 3),
            (REG_A2, 4),
            (REG_A3, 5),
            (REG_A4, 6),
            (REG_A5, 7),
        };

        foreach (var (register, encoded) in compressedRegisters)
        {
            Assert.That(Emitter.tryGetRvcRegisterNumber(register), Is.EqualTo(encoded));
            Assert.That(Emitter.getRegNumberFromRvcReg(encoded), Is.EqualTo(register));
        }

        Assert.That(Emitter.tryGetRvcRegisterNumber(REG_R0), Is.EqualTo(uint.MaxValue));
        Assert.That(Emitter.getCompressedArithmeticIns(INS_and), Is.EqualTo(INS_c_and));
        Assert.That(Emitter.getCompressedArithmeticIns(INS_or), Is.EqualTo(INS_c_or));
        Assert.That(Emitter.getCompressedArithmeticIns(INS_xor), Is.EqualTo(INS_c_xor));
        Assert.That(Emitter.getCompressedArithmeticIns(INS_sub), Is.EqualTo(INS_c_sub));
        Assert.That(Emitter.getCompressedArithmeticIns(INS_addw), Is.EqualTo(INS_c_addw));
        Assert.That(Emitter.getCompressedArithmeticIns(INS_subw), Is.EqualTo(INS_c_subw));
    }

    [Test]
    public static void SelectsCompressedRegisterArithmeticForms()
    {
        Assert.That(
            Emitter.tryGetCompressedIns_R_R_R(INS_add, default, REG_A0, REG_R0, REG_A1, default),
            Is.EqualTo(INS_c_mv));
        Assert.That(
            Emitter.tryGetCompressedIns_R_R_R(INS_add, default, REG_A0, REG_A0, REG_A1, default),
            Is.EqualTo(INS_c_add));
        Assert.That(
            Emitter.tryGetCompressedIns_R_R_R(INS_subw, default, REG_S1, REG_S1, REG_A0, default),
            Is.EqualTo(INS_c_subw));
        Assert.That(
            Emitter.tryGetCompressedIns_R_R_R(INS_add, default, REG_A0, REG_A1, REG_A2, default),
            Is.EqualTo(INS_none));
    }

    [Test]
    public static void PreservesImmediateMasksAndSignedTrimming()
    {
        Assert.That(Emitter.WordMask(0), Is.Zero);
        Assert.That(Emitter.WordMask(32), Is.EqualTo(uint.MaxValue));
        Assert.That(Emitter.BitMask64(0), Is.Zero);
        Assert.That(Emitter.BitMask64(64), Is.EqualTo(ulong.MaxValue));
        Assert.That(Emitter.LowerNBitsOfWord(-1, 12), Is.EqualTo(0xFFFu));
        Assert.That(Emitter.UpperNBitsOfWord(-1, 20), Is.EqualTo(0xFFFFFu));
        Assert.That(Emitter.UpperNBitsOfWordSignExtend(0x7FFFFFFF, 20), Is.EqualTo(0x80000u));

        Assert.That(Emitter.TrimSignedToImm12(-2048), Is.EqualTo(0x800u));
        Assert.That(Emitter.TrimSignedToImm12(2047), Is.EqualTo(0x7FFu));
        Assert.That(Emitter.TrimSignedToImm13(-4096), Is.EqualTo(0x1000u));
        Assert.That(Emitter.TrimSignedToImm13(4095), Is.EqualTo(0xFFFu));
        Assert.That(Emitter.TrimSignedToImm20(-524288), Is.EqualTo(0x80000u));
        Assert.That(Emitter.TrimSignedToImm20(524287), Is.EqualTo(0x7FFFFu));
        Assert.That(Emitter.TrimSignedToImm21(-1048576), Is.EqualTo(0x100000u));
        Assert.That(Emitter.TrimSignedToImm21(1048575), Is.EqualTo(0xFFFFFu));
    }

    [Test]
    public static void ClassifiesLoadsStoresAndLocalStackWrites()
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));

        Assert.That(emitter.emitInsIsLoad(INS_lw), Is.True);
        Assert.That(emitter.emitInsIsStore(INS_lw), Is.False);
        Assert.That(emitter.emitInsIsLoadOrStore(INS_lw), Is.True);
        Assert.That(emitter.emitInsIsLoad(INS_sw), Is.False);
        Assert.That(emitter.emitInsIsStore(INS_sw), Is.True);
        Assert.That(emitter.emitInsIsLoadOrStore(INS_sw), Is.True);
        Assert.That(emitter.emitInsIsLoadOrStore(INS_addi), Is.False);

        foreach (var ins in new[] { INS_sd, INS_sw, INS_sb, INS_sh })
        {
            var descriptor = NewDescriptor(ins, isLocal: true);
            Assert.That(emitter.emitInsWritesToLclVarStackLoc(descriptor), Is.True);
        }

        Assert.That(emitter.emitInsWritesToLclVarStackLoc(NewDescriptor(INS_sd, isLocal: false)), Is.False);
        Assert.That(emitter.emitInsWritesToLclVarStackLoc(NewDescriptor(INS_lw, isLocal: true)), Is.False);
    }

    private static Emitter.instrDesc NewDescriptor(instruction ins, bool isLocal)
    {
        var descriptorType = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic);
        if (descriptorType is null)
        {
            throw new InvalidOperationException("The basic RISC-V instruction descriptor type is unavailable.");
        }

        var descriptor = (Emitter.instrDesc)RuntimeHelpers.GetUninitializedObject(descriptorType);
        descriptor.idIns(ins);
        if (isLocal)
        {
            descriptor.idSetIsLclVar();
        }

        return descriptor;
    }
}
#endif
