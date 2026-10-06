// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class Arm64BlockMemoryUnrollTests
{
    [TestCase(1, new[] { INS_strb })]
    [TestCase(2, new[] { INS_strh })]
    [TestCase(3, new[] { INS_strh, INS_strb })]
    [TestCase(7, new[] { INS_str, INS_str })]
    [TestCase(8, new[] { INS_str })]
    [TestCase(9, new[] { INS_str, INS_strb })]
    [TestCase(16, new[] { INS_stp })]
    [TestCase(17, new[] { INS_stp, INS_strb })]
    [TestCase(32, new[] { INS_stp, INS_stp })]
    [TestCase(64, new[] { INS_movi, INS_stp, INS_stp })]
    public static void InitBlockUnrollChoosesExactStoreSequence(int size, instruction[] expectedInstructions)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = CreateBlock(size, Physical(REG_R1, TYP_BYREF));
            codeGen.InternalRegisters.Add(block, Mask(REG_R16) | Mask(REG_V16));
            codeGen.genCodeForInitBlkUnroll(block);

            var emitted = Descriptors(codeGen);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo(expectedInstructions));

            if (size is 64)
            {
                Assert.That(emitted[0].idInsOpt(), Is.EqualTo(INS_OPTS_16B));
                Assert.That(emitted[1].idOpSize(), Is.EqualTo(EA_16BYTE));
                Assert.That(emitted[2].idOpSize(), Is.EqualTo(EA_16BYTE));
            }
        });
    }

    [Test]
    public static void InitBlockUnrollMaterializesUnencodableDestinationOffset()
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var address = new GenTreeAddrMode(TYP_BYREF, Physical(REG_R1, TYP_BYREF), null, 0, 260)
            {
                IsContained = true,
            };
            var block = CreateBlock(8, address);
            codeGen.InternalRegisters.Add(block, Mask(REG_R16));
            codeGen.genCodeForInitBlkUnroll(block);

            var emitted = Descriptors(codeGen);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo([INS_add, INS_str]));
            Assert.That(Emitter.emitGetInsSC(emitted[0]), Is.EqualTo((nint)260));
            Assert.That(emitted[1].idReg2(), Is.EqualTo(emitted[0].idReg1()));
        });
    }

    [TestCase(3, new[] { INS_ldrh, INS_strh, INS_ldrb, INS_strb }, new[] { 2, 2, 1, 1 }, new[] { 0, 0, 2, 2 })]
    [TestCase(7, new[] { INS_ldr, INS_str, INS_ldr, INS_str }, new[] { 4, 4, 4, 4 }, new[] { 0, 0, 3, 3 })]
    [TestCase(9, new[] { INS_ldr, INS_str, INS_ldrb, INS_strb }, new[] { 8, 8, 1, 1 }, new[] { 0, 0, 8, 8 })]
    [TestCase(16, new[] { INS_ldp, INS_stp }, new[] { 8, 8 }, new[] { 0, 0 })]
    [TestCase(17, new[] { INS_ldp, INS_stp, INS_ldrb, INS_strb }, new[] { 8, 8, 1, 1 }, new[] { 0, 0, 16, 16 })]
    [TestCase(32, new[] { INS_ldp, INS_stp }, new[] { 16, 16 }, new[] { 0, 0 })]
    [TestCase(40, new[] { INS_ldp, INS_stp, INS_ldr, INS_str }, new[] { 16, 16, 8, 8 }, new[] { 0, 0, 32, 32 })]
    public static void CopyBlockUnrollPreservesStoreWidthsAndOffsets(
        int size,
        instruction[] expectedInstructions,
        int[] expectedWidths,
        int[] expectedOffsets)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = CreateCopyBlock(size);
            codeGen.InternalRegisters.Add(
                block,
                Mask(REG_R16) | Mask(REG_R17) | Mask(REG_V16) | Mask(REG_V17));
            codeGen.genCodeForStoreBlk(block);

            var emitted = Descriptors(codeGen);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo(expectedInstructions));
            Assert.That(emitted.ConvertAll(static descriptor => (int)descriptor.idOpSize()), Is.EqualTo(expectedWidths));
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idAddr().iiaAddrMode.amDisp),
                Is.EqualTo(expectedOffsets));

            for (var index = 0; index < emitted.Count; index += 2)
            {
                Assert.That(emitted[index].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R2));
                Assert.That(emitted[index + 1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R1));
            }
        });
    }

    [Test]
    public static void CopyBlockUnrollMaterializesAnUnencodableSourceOffset()
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = CreateCopyBlock(8, sourceOffset: 32768);
            codeGen.InternalRegisters.Add(block, Mask(REG_R16));
            codeGen.genCodeForStoreBlk(block);

            var emitted = Descriptors(codeGen);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo([INS_add, INS_ldr, INS_str]));
            Assert.That(Emitter.emitGetInsSC(emitted[0]), Is.EqualTo((nint)32768));
            Assert.That(emitted[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(emitted[0].idReg1()));
            Assert.That(emitted[1].idAddr().iiaAddrMode.amDisp, Is.Zero);
            Assert.That(emitted[2].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R1));
            Assert.That(emitted[2].idAddr().iiaAddrMode.amDisp, Is.Zero);
        });
    }

    [Test]
    public static void CopyBlockUnrollMaterializesAnUnencodableDestinationOffset()
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = CreateCopyBlock(8, destinationOffset: 32768);
            codeGen.InternalRegisters.Add(block, Mask(REG_R16));
            codeGen.genCodeForStoreBlk(block);

            var emitted = Descriptors(codeGen);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo([INS_add, INS_ldr, INS_str]));
            Assert.That(Emitter.emitGetInsSC(emitted[0]), Is.EqualTo((nint)32768));
            Assert.That(emitted[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R2));
            Assert.That(emitted[1].idAddr().iiaAddrMode.amDisp, Is.Zero);
            Assert.That(emitted[2].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(emitted[0].idReg1()));
            Assert.That(emitted[2].idAddr().iiaAddrMode.amDisp, Is.Zero);
        });
    }

    private static GenTreeBlk CreateCopyBlock(int size, int sourceOffset = 0, int destinationOffset = 0)
    {
        var sourceAddress = new GenTreeAddrMode(
            TYP_BYREF,
            Physical(REG_R2, TYP_BYREF),
            null,
            0,
            sourceOffset)
        {
            IsContained = true,
        };
        var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress)
        {
            IsContained = true,
        };
        var destination = new GenTreeAddrMode(
            TYP_BYREF,
            Physical(REG_R1, TYP_BYREF),
            null,
            0,
            destinationOffset)
        {
            IsContained = true,
        };

        return new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout((uint)size))
        {
            _kind = BlkOpKindUnroll,
        };
    }

    private static GenTreeBlk CreateBlock(int size, GenTree address)
    {
        var data = new GenTreeIntCon(TYP_INT, 0)
        {
            IsContained = true,
        };

        return new GenTreeBlk(TYP_STRUCT, address, data, new ClassLayout((uint)size))
        {
            _kind = BlkOpKindUnroll,
        };
    }

    private static GenTreePhysReg Physical(regNumber reg, var_types type)
    {
        return new GenTreePhysReg(reg, type) { RegNum = reg };
    }

    private static regMaskTP Mask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
