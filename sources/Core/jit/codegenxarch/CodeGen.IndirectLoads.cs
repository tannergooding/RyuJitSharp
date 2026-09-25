// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCodeForIndir(GenTreeIndir tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Indirect read generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_IND);

#if FEATURE_SIMD
        if (tree.Type is TYP_SIMD12)
        {
            genLoadIndTypeSimd12(tree);
            return;
        }
#endif
        var targetType = tree.Type;
        var addr = tree.Addr;
        if (addr.Oper.IsCnsIntOrI && addr.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL))
        {
            noway_assert((emitAttr)targetType.Size == EA_PTRSIZE);
            Emitter.emitIns_R_C(ins_Load(TYP_I_IMPL), EA_PTRSIZE, tree.RegNum, FLD_GLOBAL_GS,
                unchecked((int)addr.AsIntCon().IconValue));
        }
        else
        {
            genConsumeAddress(addr);
            var loadIns = tree.DontExtend ? INS_mov : ins_Load(targetType);
            Emitter.emitInsLoadInd(loadIns, tree.Type.EmitSize, tree.RegNum, tree);
        }

        genProduceReg(tree);
#endif
    }

#if FEATURE_SIMD
    public void genLoadIndTypeSimd12(GenTreeIndir tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 indirect reads require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_IND);
        var addr = tree.Addr;
        genConsumeAddress(addr);

        if (addr.IsContained && (addr.Oper is GT_LCL_ADDR))
        {
            genEmitLoadLclTypeSimd12(tree.RegNum, addr.AsLclFld().LclNum, addr.AsLclFld().LclOffs);
            genProduceReg(tree);
            return;
        }

        var targetReg = tree.RegNum;
        Emitter.emitInsLoadInd(INS_movsd_simd, EA_8BYTE, targetReg, tree);

        if (tree.IsIndirAddrMode)
        {
            var addrMode = addr.AsAddrMode();
            addrMode.Offset = unchecked(addrMode.Offset + 8);
        }
        else if (addr.Oper.IsCnsIntOrI && addr.IsContained)
        {
            var icon = addr.AsIntConCommon();
            assert(!icon.ImmedValNeedsReloc(_compiler));
            icon.IconValue = unchecked(icon.IconValue + 8);
        }
        else
        {
            addr = new GenTreeAddrMode(addr.Type, addr, null, 0, 8) { IsContained = true };
        }
        tree.Addr = addr;

        // Insert the upper float in lane 2 and zero lane 3 without overreading.
        var indir = indirForm(TYP_SIMD16, addr);
        Emitter.emitIns_SIMD_R_R_A_I(INS_insertps, EA_16BYTE, targetReg, targetReg, indir, 0x28,
            INS_OPTS_NONE);
        genProduceReg(tree);
#endif
    }
#endif

    public void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Indexed address generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        if (tree.IsBoundsChecked)
        {
            RequireSharedThrowHelperBlocks();
        }

        var @base = tree.Arr;
        var index = tree.Index;
        var baseReg = genConsumeReg(@base);
        var indexReg = genConsumeReg(index);
        var dstReg = tree.RegNum;

        // Address generation uses the base beyond the first emitted instruction.
        // Restore the root killed by consumption until the final LEA is recorded.
        _gcInfo.gcMarkRegPtrVal(baseReg, @base.Type);
        assert(varTypeIsIntegral(index.Type));
        var tmpReg = _internalRegisters.GetSingle(tree);

        if (tree.IsBoundsChecked)
        {
            if (index.Type is TYP_I_IMPL)
            {
                Emitter.emitIns_R_AR(INS_mov, EA_4BYTE, tmpReg, baseReg, tree.LenOffset);
                Emitter.emitIns_R_R(INS_cmp, EA_8BYTE, indexReg, tmpReg);
            }
            else
            {
                Emitter.emitIns_R_AR(INS_cmp, EA_4BYTE, indexReg, baseReg, tree.LenOffset);
            }

            genJumpToSharedThrowHlpBlk(EJ_jae, SCK_RNGCHK_FAIL);
        }

        if (index.Type is not TYP_I_IMPL)
        {
            // A 32-bit write zero-extends the index for pointer-width addressing.
            _ = Emitter.emitIns_Mov(INS_mov, EA_4BYTE, tmpReg, indexReg, canSkip: false);
            indexReg = tmpReg;
        }

        var scale = unchecked((uint)tree.ElemSize);
        switch (scale)
        {
            case 1:
            case 2:
            case 4:
            case 8:
            {
                tmpReg = indexReg;
                break;
            }

            default:
            {
                // IMUL sign-extends its immediate; the EE excludes larger elements.
                noway_assert(scale <= int.MaxValue);
                Emitter.emitIns_R_I(RyuJitSharp.Emitter.inst3opImulForReg(tmpReg), EA_PTRSIZE, indexReg,
                    unchecked((nint)scale));
                scale = 1;
                break;
            }
        }

        Emitter.emitIns_R_ARX(INS_lea, tree.Type.EmitSize, dstReg, baseReg, tmpReg, scale, tree.ElemOffset);
        _gcInfo.gcMarkRegSetNpt(@base.RegMask);
        genProduceReg(tree);
#endif
    }
}
#endif
