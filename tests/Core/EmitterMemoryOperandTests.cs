// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterMemoryOperandTests
{
    private static CorInfoReloc s_hint;

    [TestCase(REG_RAX, REG_NA, 1, 0, 0u)]
    [TestCase(REG_RBP, REG_R8, 2, 128, 1u)]
    [TestCase(REG_RAX, REG_RCX, 4, 65536, 2u)]
    [TestCase(REG_NA, REG_R12, 8, -8191, 3u)]
    [TestCase(REG_NA, REG_NA, 1, 32, 0u)]
    public static void AddressModesPreserveConstructorDisplacementsAndRegisterScaleFields(
        regNumber baseReg, regNumber indexReg, byte scale, int offset, uint encodedScale)
    {
        WithEmitter((compiler, emitter) =>
        {
            var baseNode = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseNode.RegNum = baseReg == REG_NA ? REG_RAX : baseReg;
            GenTree? index = null;
            if (indexReg != REG_NA)
            {
                index = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
                index.RegNum = indexReg;
            }

            var address = new GenTreeAddrMode(TYP_BYREF, baseNode, index, scale, offset) { IsContained = true };
            if (baseReg == REG_NA)
            {
                address.BaseAddress = null;
            }

            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);
            var id = NewAddress(emitter, EA_4BYTE, offset);
            id.idIns(INS_mov);
            id.idReg1(REG_RDX);
            var count = CurrentCount(emitter);
            var used = Used(emitter);
            emitter.emitHandleMemOp(indir, id, IF_RRW_ARD, INS_mov);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_ARD));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(baseReg));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(indexReg));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(encodedScale));
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)offset));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(id.idCodeSize(), Is.Zero);
            Assert.That(CurrentSize(emitter), Is.Zero);
            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            Assert.That(Used(emitter), Is.EqualTo(used));
        });
    }

    [TestCase(0)]
    [TestCase(65536)]
    public static void PlainRegisterAddressesAndImmediateStorageRemainIndependent(int offset)
    {
        WithEmitter((compiler, emitter) =>
        {
            var reg = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            reg.RegNum = REG_R9;
            GenTree address = reg;
            if (offset != 0)
            {
                address = new GenTreeAddrMode(TYP_BYREF, reg, null, 0, offset) { IsContained = true };
            }

            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);
            var id = NewAddressConstant(emitter, EA_GCREF, offset, 77);
            id.idIns(INS_add);
            emitter.emitHandleMemOp(indir, id, IF_ARW_CNS, INS_add);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_ARW_CNS));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R9));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.Zero);
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)offset));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)77));
            Assert.That(id.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(id.idIsLargeCns(), Is.True);
            Assert.That(id.idIsLargeDsp(), Is.EqualTo(offset != 0));
        });
    }

    [TestCase(0L, false, false, CorInfoReloc.NONE, false)]
    [TestCase(128L, false, false, CorInfoReloc.NONE, false)]
    [TestCase(0x123456789ABCDEF0L, false, false, CorInfoReloc.RELATIVE32, true)]
    [TestCase(0x123456789ABCDEF0L, true, true, CorInfoReloc.RELATIVE32, true)]
    public static void ContainedAbsoluteAddressesRetainRelocationAndHandleMetadata(
        long address, bool relocatable, bool handle, CorInfoReloc hint, bool displacementRelocation)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = relocatable;
            var constant = new GenTreeIntCon(TYP_I_IMPL, (nint)address) { IsContained = true };
            if (handle)
            {
                constant.Flags |= GTF_ICON_FIELD_HDL;
            }
#if DEBUG
            constant.TargetHandle = unchecked((nint)0xFEDCBA9876543210UL);
#endif
            var indir = new GenTreeIndir(GT_IND, TYP_INT, constant);
            var id = NewAddress(emitter, EA_4BYTE, (nint)address);
            id.idIns(INS_mov);
            id.idReg1(REG_RAX);
            emitter.emitHandleMemOp(indir, id, IF_RWR_ARD, INS_mov);

            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.Zero);
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)address));
            Assert.That(id.idIsDspReloc(), Is.EqualTo(displacementRelocation));
            Assert.That(emitter.emitInsSizeAM(id, insCodeRM(INS_mov)), Is.EqualTo(displacementRelocation ? 6u : 7u));
#if DEBUG
            var info = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
            Assert.That(info.idFlags, Is.EqualTo(handle ? constant.Flags : GTF_EMPTY));
            Assert.That(info.idMemCookie, Is.EqualTo(handle ? constant.TargetHandle : (nint)0));
#endif
        }, hint);
    }

    [TestCase(INS_rol_N, IF_RRW_CNS, IF_RRW_SHF)]
    [TestCase(INS_ror_N, IF_MRW_CNS, IF_MRW_SHF)]
    [TestCase(INS_rcl_N, IF_SRW_CNS, IF_SRW_SHF)]
    [TestCase(INS_rcr_N, IF_ARW_CNS, IF_ARW_SHF)]
    [TestCase(INS_shl_N, IF_ARW_CNS, IF_ARW_SHF)]
    [TestCase(INS_shr_N, IF_ARW_CNS, IF_ARW_SHF)]
    [TestCase(INS_sar_N, IF_ARW_CNS, IF_ARW_SHF)]
    [TestCase(INS_mov, IF_RRW_ARD, IF_RWR_ARD)]
    [TestCase(INS_mov, IF_AWR_RRD, IF_AWR_RRD)]
    [TestCase(INS_add, IF_RRW_ARD, IF_RRW_ARD)]
    public static void InstructionFormatsRetainAllShiftClassesAndTheMovWriteException(
        instruction ins, Emitter.insFormat original, Emitter.insFormat expected)
    {
        WithEmitter((_, emitter) => Assert.That(emitter.emitMapFmtForIns(original, ins), Is.EqualTo(expected)));
    }

    [TestCase(INS_neg, EA_8BYTE, 0, IF_SRW, 4u)]
    [TestCase(INS_inc, EA_4BYTE, 16, IF_SRW, 3u)]
    [TestCase(INS_push, EA_8BYTE, 0, IF_SRD, 3u)]
    [TestCase(INS_pop, EA_8BYTE, 0, IF_SWR, 3u)]
    [TestCase(INS_push, EA_GCREF, 0, IF_SRD, 3u)]
    public static void StackOnlyInstructionsKeepFormatsSizesAndDiagnosticOffsets(
        instruction ins, emitAttr attr, int offset, Emitter.insFormat format, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            var depth = emitter.emitCntStackDepth;
            emitter.emitIns_S(ins, attr, 0, offset);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(16));
            Assert.That(id.idGCref(), Is.EqualTo(EA_IS_GCREF(attr) ? GCInfo.GCtype.GCT_GCREF : GCInfo.GCtype.GCT_NONE));
            Assert.That(emitter.emitCntStackDepth, Is.EqualTo(depth));
            CheckDebugOffset(id);
        });
    }

    [TestCase(INS_mov, EA_8BYTE, 1, 1, false, IF_SWR_CNS, 8u)]
    [TestCase(INS_add, EA_8BYTE, 127, 127, false, IF_SRW_CNS, 5u)]
    [TestCase(INS_add, EA_8BYTE, 128, 128, false, IF_SRW_CNS, 8u)]
    [TestCase(INS_test, EA_4BYTE, 1, 1, false, IF_SRD_CNS, 7u)]
    [TestCase(INS_add, EA_4BYTE, 3, 3, true, IF_SRW_CNS, 7u)]
    [TestCase(INS_add, EA_4BYTE, 16, 16, true, IF_SRW_CNS, 7u)]
    [TestCase(INS_shl_N, EA_8BYTE, -1, 127, false, IF_SRW_SHF, 5u)]
    [TestCase(INS_sar_N, EA_4BYTE, 129, 1, false, IF_SRW_SHF, 4u)]
    [TestCase(INS_rol_N, EA_4BYTE, 128, 0, false, IF_SRW_SHF, 4u)]
    [TestCase(INS_rcr_N, EA_4BYTE, 255, 127, false, IF_SRW_SHF, 4u)]
    public static void StackImmediatesRetainSignedWidthsRelocationAndNativeShiftMasking(
        instruction ins, emitAttr attr, int value, int stored, bool relocation, Emitter.insFormat format, uint size)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = relocation;
            if (relocation)
            {
                attr |= EA_CNS_RELOC_FLG;
            }

            emitter.emitIns_S_I(ins, attr, 0, 0, value);
            var id = Last(emitter);
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)stored));
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(id.idIsLargeCns(), Is.EqualTo(stored is < -16 or > 15));
            Assert.That(id.idIsCnsReloc(), Is.EqualTo(relocation));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            CheckDebugOffset(id);
        });
    }

    [TestCase(REG_XMM1, EA_16BYTE, 0, 5u)]
    [TestCase(REG_XMM8, EA_16BYTE, 0, 5u)]
    [TestCase(REG_XMM16, EA_16BYTE, 0, 7u)]
    [TestCase(REG_XMM1, EA_64BYTE, 80, 7u)]
    public static void TwoRegisterStackLoadsKeepVexAndEvexSourceAndCompressionChoices(
        regNumber source, emitAttr attr, int offset, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R_S(INS_addps, attr, REG_XMM0, source, 0, offset);
            var id = Last(emitter);
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_RRD_SRD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(id.idReg2(), Is.EqualTo(source));
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            CheckDebugOffset(id);
        });
    }

    [Test]
    public static void SharedStackLoadsPreserveMulxNddAndEmbeddedBroadcastOptions()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R_S(INS_mulx, EA_8BYTE, REG_R8, REG_R9, 0, 0);
            Assert.That(Last(emitter).idInsFmt(), Is.EqualTo(IF_RWR_RWR_SRD));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(6u));

            emitter.UseRex2Encodings = true;
            emitter.UsePromotedEvexEncodings = true;
            emitter.emitIns_R_R_S(INS_add, EA_8BYTE, REG_R16, REG_R31, 0, 0, INS_OPTS_EVEX_nd);
            Assert.That(Last(emitter).idInsFmt(), Is.EqualTo(IF_RWR_RRD_SRD));
            Assert.That(Last(emitter).idIsEvexNdContextSet(), Is.True);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(7u));

            emitter.emitIns_R_R_S(INS_addps, EA_64BYTE, REG_XMM0, REG_XMM1, 0, 0,
                INS_OPTS_EVEX_eb | INS_OPTS_EVEX_em_k3 | INS_OPTS_EVEX_em_zero);
            Assert.That(Last(emitter).idGetEvexbContext(), Is.EqualTo(3u));
            Assert.That(Last(emitter).idGetEvexAaaContext(), Is.EqualTo(3u));
            Assert.That(Last(emitter).idIsEvexZContextSet(), Is.True);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(7u));
            CheckDebugOffset(Last(emitter));
        });
    }

    [Test]
    public static void StackRecordingRetainsNegativeSpillTemporaryNumbers()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (_, codeGen, _) =>
        {
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpBeginPreAllocateTemps();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_LONG, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_LONG);
            temp.tdTempOffs = -32;
            try
            {
                var emitter = codeGen.Emitter;
                emitter.emitIns_S(INS_neg, EA_8BYTE, temp.tdTempNum, 4);
                Assert.That(Last(emitter).idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
                Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(4u));
                emitter.emitIns_S_I(INS_mov, EA_8BYTE, temp.tdTempNum, 8, 7);
                Assert.That(Last(emitter).idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
                Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(8u));
                emitter.emitIns_R_R_S(INS_mulx, EA_8BYTE, REG_R8, REG_R9, temp.tdTempNum, 0);
                Assert.That(Last(emitter).idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
                Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(6u));
                Assert.That(codeGen.RegSet.tmpGetNum(temp.tdTempNum), Is.SameAs(temp));
            }
            finally
            {
                codeGen.RegSet.tmpRlsTemp(temp);
            }
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void DisassemblyRejectsAllRecordingEntrypointsBeforeAllocation(int entrypoint)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                switch (entrypoint)
                {
                    case 0:
                    {
                        emitter.emitIns_S(INS_neg, EA_8BYTE, 0, 0);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitIns_S_I(INS_mov, EA_8BYTE, 0, 0, 1);
                        break;
                    }

                    default:
                    {
                        emitter.emitIns_R_R_S(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, 0, 0);
                        break;
                    }
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

    private static void WithEmitter(Action<Compiler, Emitter> action, CorInfoReloc hint = CorInfoReloc.NONE)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            s_hint = hint;
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
            codeGen.Emitter.emitVarRefOffs = 0x1234;
#endif
            action(compiler, codeGen.Emitter);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* _, void* address) => s_hint;

    private static void CheckDebugOffset(Emitter.instrDesc id)
    {
#if DEBUG
        var info = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
        Assert.That(info.idVarRefOffs, Is.EqualTo(0x1234u));
#else
        _ = id;
#endif
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr attr, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmdCns")]
    private static extern Emitter.instrDesc NewAddressConstant(Emitter emitter, emitAttr attr, nint displacement, int constant);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint AddressDisplacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint Constant(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);
}
