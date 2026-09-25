// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class EmitterAddressInstructionTests
{
    [TestCase(INS_mov, EA_4BYTE, REG_RAX, 0L, 7u, false)]
    [TestCase(INS_lea, EA_8BYTE, REG_RAX, 0x12345678L, 8u, true)]
    [TestCase(INS_mov, EA_8BYTE | EA_DSP_RELOC_FLG, REG_RAX, 0L, 7u, false)]
    [TestCase(INS_lea, EA_8BYTE | EA_DSP_RELOC_FLG, REG_R8, -8192L, 7u, true)]
    [TestCase(INS_mov, EA_2BYTE, REG_RAX, 8191L, 8u, false)]
    [TestCase(INS_mov, EA_1BYTE, REG_RSI, 8192L, 8u, true)]
    [TestCase(INS_lea, EA_8BYTE, REG_RAX, -8191L, 8u, false)]
    [TestCase(INS_mov, EA_GCREF, REG_RAX, long.MaxValue, 8u, true)]
    public static void AbsoluteAddressesRetainDisplacementWidthAndRelocationSizing(
        instruction ins, emitAttr attr, regNumber reg, long displacement, uint size, bool large)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_AI(ins, attr, reg, (nint)displacement);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_ARD));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)displacement));
            Assert.That(id.idIsLargeDsp(), Is.EqualTo(large));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(large ? 24 : 16));
            Assert.That(id.idIsDspReloc(), Is.EqualTo((attr & EA_DSP_RELOC_FLG) != 0));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
            Assert.That(id.idGCref(), Is.EqualTo((attr & EA_GCREF_FLG) != 0
                ? GCInfo.GCtype.GCT_GCREF : GCInfo.GCtype.GCT_NONE));
        });
    }

    [Test]
    public static void Data16RecordsASeparateOneBytePrefixAndTlsRetainsItsDescriptorBit()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_Data16();
            var prefix = Last(emitter);
            Assert.That(prefix.idIns(), Is.EqualTo(INS_data16));
            Assert.That(prefix.idInsFmt(), Is.EqualTo(IF_NONE));
            Assert.That(prefix.idOpSize(), Is.EqualTo(EA_1BYTE));
            Assert.That(prefix.idCodeSize(), Is.EqualTo(1u));
            Assert.That(prefix.NativeLogicalSize, Is.EqualTo(8));

            emitter.emitIns_R_AI(INS_lea, EA_8BYTE | EA_DSP_RELOC_FLG | EA_CNS_TLSGD_RELOC, REG_RDI, 0);
            Assert.That(Last(emitter).idIsTlsGD(), Is.True);
            Assert.That(Last(emitter).idIsNoGC(), Is.False);
            Assert.That(CurrentCount(emitter), Is.EqualTo(2));
            Assert.That(CurrentSize(emitter), Is.EqualTo(8));
        });
    }

    [TestCase(REG_RAX, 0L, 2u)]
    [TestCase(REG_RSP, 0L, 3u)]
    [TestCase(REG_RBP, 0L, 3u)]
    [TestCase(REG_R12, 0L, 4u)]
    [TestCase(REG_R13, 0L, 4u)]
    [TestCase(REG_R20, 0L, 5u)]
    [TestCase(REG_R21, 0L, 5u)]
    [TestCase(REG_R28, 0L, 5u)]
    [TestCase(REG_R29, 0L, 5u)]
    [TestCase(REG_R31, 0L, 4u)]
    [TestCase(REG_RAX, -128L, 3u)]
    [TestCase(REG_RAX, 127L, 3u)]
    [TestCase(REG_RAX, -129L, 6u)]
    [TestCase(REG_RAX, 128L, 6u)]
    [TestCase(REG_NA, 0L, 7u)]
    public static void BaseRegistersSelectSibDisplacementAndExtendedPrefixes(regNumber reg, long displacement, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseRex2Encodings = true;
            emitter.UsePromotedEvexEncodings = true;
            var id = Address(emitter, INS_mov, EA_4BYTE, reg, REG_NA, 0, (nint)displacement);

            Assert.That(emitter.emitInsSizeAM(id, insCodeRM(INS_mov)), Is.EqualTo(size));
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)displacement));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(reg));
        });
    }

    [TestCase(REG_RAX, REG_RCX, 0u, 0L, 3u, REG_RAX, REG_RCX)]
    [TestCase(REG_RBP, REG_RCX, 0u, 0L, 3u, REG_RCX, REG_RBP)]
    [TestCase(REG_R13, REG_RCX, 0u, 0L, 4u, REG_RCX, REG_R13)]
    [TestCase(REG_R21, REG_RCX, 0u, 0L, 5u, REG_RCX, REG_R21)]
    [TestCase(REG_RBP, REG_R13, 0u, 0L, 5u, REG_RBP, REG_R13)]
    [TestCase(REG_RBP, REG_RCX, 1u, 0L, 4u, REG_RBP, REG_RCX)]
    [TestCase(REG_RBP, REG_RCX, 0u, 1L, 4u, REG_RBP, REG_RCX)]
    [TestCase(REG_RAX, REG_RCX, 2u, 128L, 7u, REG_RAX, REG_RCX)]
    [TestCase(REG_NA, REG_RCX, 1u, 0L, 7u, REG_NA, REG_RCX)]
    [TestCase(REG_NA, REG_RCX, 3u, 1L, 7u, REG_NA, REG_RCX)]
    public static void IndexedAddressesPreserveScaleAndOnlySwapEligibleUnscaledBases(
        regNumber reg, regNumber index, uint scale, long displacement, uint size,
        regNumber expectedBase, regNumber expectedIndex)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseRex2Encodings = true;
            emitter.UsePromotedEvexEncodings = true;
            var id = Address(emitter, INS_mov, EA_4BYTE, reg, index, scale, (nint)displacement);

            Assert.That(emitter.emitInsSizeAM(id, insCodeRM(INS_mov)), Is.EqualTo(size));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(expectedBase));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(expectedIndex));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(scale));
        });
    }

    [Test]
    public static void VectorIndicesCannotBeSwappedIntoTheBaseSlot()
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_vgatherdps, EA_16BYTE, REG_RBP, REG_XMM1, 0, 0, REG_XMM0);

            Assert.That(emitter.emitInsSizeAM(id, insCodeRM(INS_vgatherdps)), Is.EqualTo(7u));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_XMM1));
        });
    }

    [TestCase(REG_NA, REG_NA, 6u)]
    [TestCase(REG_RAX, REG_NA, 6u)]
    [TestCase(REG_RBP, REG_RCX, 7u)]
    public static void DisplacementRelocationsKeepDisp32AndPreventBaseSwapping(
        regNumber reg, regNumber index, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_mov, EA_4BYTE | EA_DSP_RELOC_FLG, reg, index, 0, 0);

            Assert.That(emitter.emitInsSizeAM(id, insCodeRM(INS_mov)), Is.EqualTo(size));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(reg));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(index));
        });
    }

    [TestCase(EA_16BYTE, 1L, false, 5u, 0u)]
    [TestCase(EA_16BYTE, 512L, false, 7u, 2u)]
    [TestCase(EA_64BYTE, 8128L, false, 7u, 2u)]
    [TestCase(EA_64BYTE, -8192L, false, 7u, 2u)]
    [TestCase(EA_64BYTE, 8192L, false, 10u, 0u)]
    [TestCase(EA_64BYTE, 508L, true, 7u, 3u)]
    [TestCase(EA_64BYTE, 512L, true, 10u, 1u)]
    public static void EvexCompressionRetainsOriginalDisplacementsAndBroadcastScaling(
        emitAttr attr, long displacement, bool broadcast, uint size, uint context)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_addps, attr, REG_RAX, REG_NA, 0, (nint)displacement, REG_XMM0);
            if (broadcast)
            {
                id.idSetEvexBroadcastBit();
            }

            Assert.That(emitter.emitInsSizeAM(id, insCodeRM(INS_addps)), Is.EqualTo(size));
            Assert.That(id.idGetEvexbContext(), Is.EqualTo(context));
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)displacement));
        });
    }

    [Test]
    public static void EvexRelocationsAndApxNddKeepTheirDifferentDisplacementContracts()
    {
        WithEmitter((_, emitter) =>
        {
            var vector = Address(emitter, INS_addps, EA_64BYTE | EA_DSP_RELOC_FLG,
                REG_RAX, REG_NA, 0, 64, REG_XMM0);
            Assert.That(emitter.emitInsSizeAM(vector, insCodeRM(INS_addps)), Is.EqualTo(10u));
            Assert.That(vector.idGetEvexbContext(), Is.Zero);

            emitter.UsePromotedEvexEncodings = true;
            var scalar = Address(emitter, INS_add, EA_8BYTE, REG_RAX, REG_NA, 0, 128);
            scalar.idSetEvexNdContext();
            Assert.That(emitter.emitInsSizeAM(scalar, insCodeRM(INS_add)), Is.EqualTo(10u));
            Assert.That(scalar.idIsEvexNdContextSet(), Is.True);
            Assert.That(scalar.idIsEvexNfContextSet(), Is.False);
        });
    }

    [TestCase(INS_movzx, EA_1BYTE, 3u)]
    [TestCase(INS_movsx, EA_1BYTE, 4u)]
    [TestCase(INS_cmove, EA_2BYTE, 4u)]
    [TestCase(INS_cmpxchg, EA_2BYTE, 5u)]
    [TestCase(INS_prefetcht0, EA_1BYTE, 3u)]
    public static void MultiByteOpcodesRetainNativeOperandPrefixSpecialCases(instruction ins, emitAttr attr, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, ins, attr, REG_RAX, REG_NA, 0, 0);
            var code = ins is INS_cmpxchg or INS_prefetcht0 ? insCodeMR(ins) : insCodeRM(ins);

            Assert.That(emitter.emitInsSizeAM(id, code), Is.EqualTo(size));
        });
    }

    [Test]
    public static void ImplicitExtendedImulDestinationCountsItsOpcodeRexPrefix()
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_imul_08, EA_4BYTE, REG_RAX, REG_NA, 0, 0);

            Assert.That(emitter.emitInsSizeAM(id, insCodeMI(INS_imul_08)), Is.EqualTo(3u));
        });
    }

    [TestCase(INS_add, EA_8BYTE, -128, false, 4u)]
    [TestCase(INS_add, EA_8BYTE, 127, false, 4u)]
    [TestCase(INS_add, EA_8BYTE, 128, false, 7u)]
    [TestCase(INS_mov, EA_8BYTE, 1, false, 7u)]
    [TestCase(INS_test, EA_8BYTE, 1, false, 7u)]
    [TestCase(INS_add, EA_2BYTE, 128, false, 5u)]
    [TestCase(INS_add, EA_4BYTE, 1, true, 6u)]
    public static void AddressImmediatesUseSignExtendedByteOrCappedDwordWidths(
        instruction ins, emitAttr attr, int value, bool relocatable, uint size)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = relocatable;
            if (relocatable)
            {
                attr |= EA_CNS_RELOC_FLG;
            }
            var id = NewAddressConstant(emitter, attr, 0, value);
            id.idIns(ins);
            id.idInsFmt(emitter.emitInsModeFormat(ins, IF_ARD_CNS));
            id.idAddr().iiaAddrMode.amBaseReg = REG_RAX;
            id.idAddr().iiaAddrMode.amIndxReg = REG_NA;

            Assert.That(emitter.emitInsSizeAM(id, insCodeMI(ins), value), Is.EqualTo(size));
            Assert.That(id.idIsCnsReloc(), Is.EqualTo(relocatable));
        });
    }

    [TestCase(INS_call)]
    [TestCase(INS_tail_i_jmp)]
    public static void CallRegisterOperandsBypassSibAndDisplacementButMemoryCallsDoNot(instruction ins)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, ins, EA_8BYTE, REG_RSP, REG_NA, 0, 0);
            id.idInsFmt(IF_ARD);
            id.idSetIsCall();
            Assert.That(emitter.emitInsSizeAM(id, insCodeMR(ins)), Is.EqualTo(3u));
            id.idAddr().iiaAddrMode.amDisp = 127;
            Assert.That(emitter.emitInsSizeAM(id, insCodeMR(ins)), Is.EqualTo(4u));
            Assert.That(CallDisplacement(emitter, id), Is.EqualTo((nint)127));

            id.idAddr().iiaAddrMode.amDisp = 0;
            id.idSetIsCallRegPtr();
            Assert.That(emitter.emitInsSizeAM(id, insCodeMR(ins)), Is.EqualTo(2u));
            Assert.That(id.idIsCallRegPtr(), Is.True);
            Assert.That(id.idIsTlsGD(), Is.False);
        });
    }

    [TestCase(0L, 2u)]
    [TestCase(128L, 6u)]
    [TestCase(long.MaxValue, 6u)]
    public static void LargeCallsReadDisplacementFromTheirGcPayload(long displacement, uint size)
    {
        WithEmitter((compiler, emitter) =>
        {
            var id = CallDescriptorFactory.LargeCall((nint)displacement);
            Assert.That(id.NativeLogicalSize, Is.EqualTo(72));
            Assert.That(emitter.emitInsSizeAM(id, insCodeMR(INS_call)), Is.EqualTo(size));
            Assert.That(CallDisplacement(emitter, id), Is.EqualTo((nint)displacement));
        });
    }

    [TestCase(IF_RWR_LABEL)]
    [TestCase(IF_MRW_CNS)]
    [TestCase(IF_MRW_RRD)]
    [TestCase(IF_MRW_SHF)]
    public static void NonAddressUnionFormatsDoNotInterpretTheirStorageAsBaseAndIndex(Emitter.insFormat format)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_add, EA_4BYTE, REG_RSP, REG_RCX, 3, 0);
            id.idInsFmt(format);
            Assert.That(emitter.emitInsSizeAM(id, insCodeRM(INS_add)), Is.EqualTo(7u));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RSP));
        });
    }

#if DEBUG
    [Test]
    public static void AbsoluteAddressDebugInformationPreservesHandleAndFlags()
    {
        WithEmitter((_, emitter) =>
        {
            var handle = unchecked((nuint)0xFEDCBA9876543210UL);
            emitter.emitIns_R_AI(INS_lea, EA_8BYTE, REG_RAX, 128, handle, GTF_ICON_HDL_MASK);
            var info = Last(emitter).idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");

            Assert.That(info.idMemCookie, Is.EqualTo(unchecked((nint)handle)));
            Assert.That(info.idFlags, Is.EqualTo(GTF_ICON_HDL_MASK));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DisassemblyRejectsBeforeRecordingEitherEntryPoint(bool data16)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                if (data16)
                {
                    emitter.emitIns_Data16();
                }
                else
                {
                    emitter.emitIns_R_AI(INS_lea, EA_8BYTE, REG_RAX, 128);
                }
            });

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Used(emitter), Is.EqualTo(used));
            Assert.That(CurrentCount(emitter), Is.Zero);
            Assert.That(CurrentSize(emitter), Is.Zero);
            Assert.That(LastInstruction(emitter), Is.Null);
        });
    }
#endif

    private static Emitter.instrDesc Address(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg, regNumber index, uint scale, nint displacement, regNumber destination = REG_RAX)
    {
        var id = NewAddress(emitter, attr, displacement);
        id.idIns(ins);
        id.idInsFmt(emitter.emitInsModeFormat(ins, IF_RRD_ARD));
        id.idReg1(destination);
        id.idAddr().iiaAddrMode.amBaseReg = reg;
        id.idAddr().iiaAddrMode.amIndxReg = index;
        id.idAddr().iiaAddrMode.amScale = scale;
        return id;
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX,
            (compiler, codeGen, _) => action(compiler, codeGen.Emitter));
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
    }

    private abstract class CallDescriptorFactory : Emitter
    {
        private CallDescriptorFactory(CodeGen codeGen) : base(codeGen)
        {
        }

        internal static instrDesc LargeCall(nint displacement)
        {
            var id = new instrDescCGCA { idcDisp = displacement };
            id.idIns(INS_call);
            id.idOpSize(EA_8BYTE);
            id.idInsFmt(IF_ARD);
            id.idSetIsLargeCall();
            id.idAddr().iiaAddrMode.amBaseReg = REG_RAX;
            id.idAddr().iiaAddrMode.amIndxReg = REG_NA;
            // Deliberately differ from the large-call displacement slot.
            id.idAddr().iiaAddrMode.amDisp = 42;
            return id;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr attr, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmdCns")]
    private static extern Emitter.instrDesc NewAddressConstant(Emitter emitter, emitAttr attr, nint displacement, int value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCIdisp")]
    private static extern nint CallDisplacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);
}
