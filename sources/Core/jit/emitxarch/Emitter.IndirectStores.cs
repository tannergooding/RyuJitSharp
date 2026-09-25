// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_A_R_I(instruction ins, emitAttr attr, GenTreeIndir indir, regNumber reg, int imm)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Memory-register-immediate recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins));
        assert(reg != REG_NA);

        var id = emitNewInstrAmdCns(attr, indir.Offset, imm);
        id.idIns(ins);
        id.idReg1(reg);
        emitHandleMemOp(indir, id, emitInsModeFormat(ins, IF_ARD_RRD_CNS), ins);
        var size = emitInsSizeAM(id, insCodeMR(ins), imm);
        id.idCodeSize(size);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }

    public void emitInsStoreInd(instruction ins, emitAttr attr, GenTreeStoreInd mem,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Indirect-store recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(mem.Oper is GT_STOREIND);
        var addr = mem.Addr;
        var data = mem.Data;

        if ((data.Oper is GT_BSWAP or GT_BSWAP16) && data.IsContained)
        {
            assert(ins is INS_movbe or INS_movbe_apx);
            data = data.AsUnOp().Op1;
        }

        if (addr.IsContained && (addr.Oper is GT_LCL_ADDR))
        {
            var varNode = addr.AsLclVarCommon();
            var offset = varNode.LclOffs;

            if (data.IsContainedIntOrIImmed)
            {
                assert(instOptions == INS_OPTS_NONE);
                emitIns_S_I(ins, attr, varNode.LclNum, offset, unchecked((int)data.AsIntConCommon().IconValue));
            }
#if FEATURE_HW_INTRINSICS
            else if ((data.Oper is GT_HWINTRINSIC) && data.IsContained)
            {
                var intrinsic = data.AsHWIntrinsic();
                var numArgs = intrinsic.Operands.Length;
                var op1 = intrinsic.GetOp(1);

                if (numArgs == 1)
                {
                    emitIns_S_R(ins, attr, op1.RegNum, varNode.LclNum, offset, instOptions);
                }
                else
                {
                    assert(numArgs == 2);
                    assert(instOptions == INS_OPTS_NONE);
                    var icon = unchecked((int)intrinsic.GetOp(2).AsIntConCommon().IconValue);
                    emitIns_S_R_I(ins, attr, varNode.LclNum, offset, op1.RegNum, icon);
                }
            }
#endif
            else
            {
                assert(!data.IsContained);
                emitIns_S_R(ins, attr, data.RegNum, varNode.LclNum, offset, instOptions);
            }

            codeGen.genUpdateLife(mem);
            return;
        }

        var displacement = mem.Offset;
        uint size;
        instrDesc id;

        if (data.IsContainedIntOrIImmed)
        {
            assert(instOptions == INS_OPTS_NONE);
            var icon = unchecked((int)data.AsIntConCommon().IconValue);
            id = emitNewInstrAmdCns(attr, displacement, icon);
            id.idIns(ins);
            emitHandleMemOp(mem, id, emitInsModeFormat(ins, IF_ARD_CNS), ins);
            size = emitInsSizeAM(id, insCodeMI(ins), icon);
            id.idCodeSize(size);
        }
#if FEATURE_HW_INTRINSICS
        else if ((data.Oper is GT_HWINTRINSIC) && data.IsContained)
        {
            var intrinsic = data.AsHWIntrinsic();
            var numArgs = intrinsic.Operands.Length;
            var op1 = intrinsic.GetOp(1);

            if (numArgs == 1)
            {
                id = emitNewInstrAmd(attr, displacement);
                id.idIns(ins);
                emitHandleMemOp(mem, id, emitInsModeFormat(ins, IF_ARD_RRD), ins);
                id.idReg1(op1.RegNum);

                assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
                SetEvexEmbMaskIfNeeded(id, instOptions);

                size = emitInsSizeAM(id, insCodeMR(ins));
                id.idCodeSize(size);
            }
            else
            {
                assert(numArgs == 2);
                assert(instOptions == INS_OPTS_NONE);
                var icon = unchecked((int)intrinsic.GetOp(2).AsIntConCommon().IconValue);

                id = emitNewInstrAmdCns(attr, displacement, icon);
                id.idIns(ins);
                id.idReg1(op1.RegNum);
                emitHandleMemOp(mem, id, emitInsModeFormat(ins, IF_ARD_RRD_CNS), ins);
                size = emitInsSizeAM(id, insCodeMR(ins), icon);
                id.idCodeSize(size);
            }
        }
#endif
        else
        {
            assert(!data.IsContained);
            id = emitNewInstrAmd(attr, displacement);
            id.idIns(ins);
            emitHandleMemOp(mem, id, emitInsModeFormat(ins, IF_ARD_RRD), ins);
            id.idReg1(data.RegNum);

            assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
            SetEvexEmbMaskIfNeeded(id, instOptions);

            size = emitInsSizeAM(id, insCodeMR(ins));
            id.idCodeSize(size);
        }

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }
}
