// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32IndirectLoadStoreTests
{
    [Test]
    public static void ContainedIndexedLoadUsesScaledBaseAndIndex()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var address = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 0) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);

            RecordArm32Instructions(() =>
                EmitLoadStoreOp(codeGen.Emitter, INS_ldr, EA_4BYTE, REG_R0, indir));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)2));
        });
    }

    [Test]
    public static void ContainedBaseOffsetStoreUsesLoadStoreImmediate()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            baseAddress.RegNum = REG_R1;
            var address = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 8) { IsContained = true };
            var data = compiler.gtNewLclvNode(TYP_INT, 0);
            data.RegNum = REG_R3;
            var store = new GenTreeStoreInd(TYP_INT, address, data);

            RecordArm32Instructions(() =>
                EmitLoadStoreOp(codeGen.Emitter, INS_str, EA_4BYTE, REG_R3, store));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)8));
        });
    }

    [Test]
    public static void ContainedIndexedLoadWithOffsetFormsPartialAddressInTemporary()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var address = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 8) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);
            codeGen.InternalRegisters.Add(indir, new regMaskTP(SRBM_R3));

            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;
            RecordArm32Instructions(() =>
                EmitLoadStoreOp(codeGen.Emitter, INS_ldr, EA_4BYTE, REG_R0, indir));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emittedInstructions = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
#if DEBUG
            Assert.That(emittedInstructions, Has.Count.EqualTo(1));
            Assert.That(emittedInstructions[0].idIns(), Is.EqualTo(INS_add));
            Assert.That(emittedInstructions[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(emittedInstructions[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emittedInstructions[0].idReg3(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.Emitter.emitGetInsSC(emittedInstructions[0]), Is.EqualTo((nint)2));
#else
            Assert.That(emittedInstructions, Has.Count.EqualTo(2));
            Assert.That(emittedInstructions[0].idIns(), Is.EqualTo(INS_add));
            Assert.That(emittedInstructions[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(emittedInstructions[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emittedInstructions[0].idReg3(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.Emitter.emitGetInsSC(emittedInstructions[0]), Is.EqualTo((nint)2));
            Assert.That(emittedInstructions[1].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(emittedInstructions[1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emittedInstructions[1].idReg2(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.Emitter.emitGetInsSC(emittedInstructions[1]), Is.EqualTo((nint)8));
#endif
        });
    }

    [TestCase(TYP_FLOAT, false)]
    [TestCase(TYP_DOUBLE, false)]
    [TestCase(TYP_FLOAT, true)]
    [TestCase(TYP_DOUBLE, true)]
    public static void UnalignedFloatingAccessUsesIntegerWordTransfers(var_types type, bool store)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            address.RegNum = REG_R1;
            var dataReg = REG_F0;
            GenTreeIndir indir;

            if (store)
            {
                var data = compiler.gtNewLclvNode(type, 0);
                data.RegNum = dataReg;
                indir = new GenTreeStoreInd(type, address, data) { Flags = GTF_IND_UNALIGNED };
            }
            else
            {
                indir = new GenTreeIndir(GT_IND, type, address)
                {
                    Flags = GTF_IND_UNALIGNED,
                    RegNum = dataReg,
                };
            }

            var tempRegisters = type is TYP_FLOAT
                ? SRBM_R2
                : SRBM_R2 | SRBM_R3;
            codeGen.InternalRegisters.Add(indir, new regMaskTP(tempRegisters));

            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;
            var ins = store ? INS_vstr : INS_vldr;
            var attr = type is TYP_FLOAT ? EA_4BYTE : EA_8BYTE;
            RecordArm32Instructions(() => EmitLoadStoreOp(codeGen.Emitter, ins, attr, dataReg, indir));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emittedInstructions = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            instruction[] expected = type is TYP_FLOAT
                ? store ? [INS_vmov_f2i, INS_str] : [INS_ldr, INS_vmov_i2f]
                : store
                    ? [INS_vmov_d2i, INS_str, INS_str]
                    : [INS_ldr, INS_ldr, INS_vmov_i2d];

#if DEBUG
            Assert.That(emittedInstructions, Has.Count.EqualTo(1));
            Assert.That(emittedInstructions[0].idIns(), Is.EqualTo(expected[0]));
#else
            Assert.That(emittedInstructions.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo(expected));
#endif
        });
    }

    private static void RecordArm32Instructions(TestDelegate action)
    {
#if DEBUG
        var failure = Assert.Throws<FatalJitException>(action)
            ?? throw new AssertionException("Missing expected ARM target sanity-check skip.");
        Assert.That(failure.Message, Is.EqualTo("Instruction sanity checking outside AMD64 is not ported."));
#else
        action();
#endif
    }

    private static Emitter.instrDesc LastInstruction(Emitter emitter)
    {
        var descriptors = CurrentDescriptors(emitter)
            ?? throw new AssertionException("Missing descriptor buffer.");
        Assert.That(descriptors, Is.Not.Empty);
        return descriptors[^1];
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsLoadStoreOp")]
    private static extern void EmitLoadStoreOp(
        Emitter emitter, instruction ins, emitAttr attr, regNumber dataReg, GenTreeIndir indir);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
