// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclVar(GenTreeLclVar tree)
    {
        var varNum = tree.LclNum;
        assert((uint)varNum < (uint)_compiler.lvaCount);
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        var isRegCandidate = varDsc.lvIsRegCandidate;

        assert((tree.Flags & GTF_VAR_DEF) == 0);

        if (!isRegCandidate && !tree.IsMultiReg && ((tree.Flags & GTF_SPILLED) == 0))
        {
            var targetType = varDsc.GetRegisterType(tree);
            assert(targetType is not TYP_STRUCT);

            var ins = ins_Load(targetType);
            Emitter.emitIns_R_S(ins, targetType.EmitSize, tree.RegNum, varNum, 0);
            genProduceReg(tree);
        }
    }

    public void genCodeForStoreLclFld(GenTreeLclFld tree)
    {
        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        noway_assert(targetType is not TYP_STRUCT);

#if FEATURE_SIMD
        if (targetType is TYP_SIMD12)
        {
            genStoreLclTypeSimd12(tree);
            return;
        }
#endif

        var offset = tree.LclOffs;
        noway_assert(targetReg == REG_NA);

        var varNum = tree.LclNum;
        assert((uint)varNum < (uint)_compiler.lvaCount);
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        assert(!varDsc.lvNormalizeOnStore || (targetType == genActualType(varDsc.Type)));

        var data = tree.Op1;
        genConsumeRegs(data);

        regNumber dataReg;
        if (data.IsContainedIntOrIImmed)
        {
            assert(data.IsIntegralConst(0));
            dataReg = REG_R0;
        }
        else if (data.IsContained)
        {
            assert(data.Oper is GT_BITCAST);
            var bitCastSrc = data.AsUnOp().Op1;
            assert(!bitCastSrc.IsContained);
            dataReg = bitCastSrc.RegNum;
        }
        else
        {
            assert(!data.IsContained);
            dataReg = data.RegNum;
        }
        assert(dataReg != REG_NA);

        var ins = ins_StoreFromSrc(dataReg, targetType);
        Emitter.emitIns_S_R(ins, targetType.EmitSize, dataReg, varNum, offset);

        genUpdateLife(tree);
        varDsc.RegNum = REG_STK;
    }

    public void genCodeForStoreLclVar(GenTreeLclVar lclNode)
    {
        var data = lclNode.Op1;
        if (data.SkipCopyOrReload.IsMultiRegNode)
        {
            genMultiRegStoreToLocal(lclNode);
            return;
        }

        ref var varDsc = ref _compiler.lvaGetDesc(lclNode);
        if (lclNode.IsMultiReg)
        {
            NYI_RISCV64("genCodeForStoreLclVar-----unimplemented on RISCV64 yet----");
            throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 multi-register local stores are not ported.");
        }

        var targetReg = lclNode.RegNum;
        var varNum = lclNode.LclNum;
        var targetType = varDsc.GetRegisterType(lclNode);

#if FEATURE_SIMD
        if (lclNode.TypeIs(TYP_SIMD12))
        {
            genStoreLclTypeSimd12(lclNode);
            return;
        }
#endif

        genConsumeRegs(data);

        regNumber dataReg = REG_NA;
        if (data.IsContained)
        {
            // Contained store operands are zero-inits, constants, or bitcasts.
            var zeroInit = data.IsIntegralConst(0);
            // TODO-RISCV64-CQ: supporting the SIMD.
            assert(!varTypeIsSIMD(targetType));

            if (zeroInit)
            {
                dataReg = REG_R0;
            }
            else if (data.IsIntegralConst())
            {
                var immediate = data.AsIntConCommon().IconValue;
                dataReg = (targetReg == REG_NA) ? rsGetRsvdReg() : targetReg; // Use tempReg if spilled

                if (data.IsIconHandle() && data.AsIntCon().FitsInAddrBase(_compiler) &&
                    data.AsIntCon().AddrNeedsReloc(_compiler))
                {
                    // Support PC-relative addresses for handles hoisted by constant CSE.
                    var attr = targetType.EmitActualSize | EA_DSP_RELOC_FLG;
                    genEmitRiscvRelocatableImmediate(INS_addi, attr, dataReg, dataReg, immediate);
                }
                else
                {
                    genEmitRiscvLoadImmediate(true, EA_PTRSIZE, dataReg, immediate);
                }
            }
            else
            {
                assert(data.Oper is GT_BITCAST);
                var bitCastSrc = data.AsUnOp().Op1;
                assert(!bitCastSrc.IsContained);
                dataReg = bitCastSrc.RegNum;
            }
        }
        else
        {
            assert(!data.IsContained);
            dataReg = data.RegNum;
        }
        assert(dataReg != REG_NA);

        if (targetReg == REG_NA)
        {
            inst_set_SV_var(lclNode);

            var ins = ins_StoreFromSrc(dataReg, targetType);
            Emitter.emitIns_S_R(ins, targetType.EmitActualSize, dataReg, varNum, 0);

            genUpdateLife(lclNode);
            varDsc.SetRegNum(REG_STK);
        }
        else
        {
            if (data.IsIconHandle(GTF_ICON_TLS_HDL))
            {
                assert(data.AsIntCon().IconValue == 0);
                // Load the address from the thread pointer register.
                Emitter.emitIns_R_R(INS_mov, targetType.EmitActualSize, targetReg, REG_TP);
            }
            else
            {
                inst_Mov(targetType, targetReg, dataReg, true);
            }

            genProduceReg(lclNode);
        }
    }

    private void genEmitRiscvRelocatableImmediate(instruction ins, emitAttr attr, regNumber dataReg, regNumber addrReg,
        nint displacement)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 relocatable immediate instruction recording is not ported.");
    }

    private int genEmitRiscvLoadImmediate(bool doEmit, emitAttr attr, regNumber reg, nint immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 immediate materialization is not ported.");
    }

#if FEATURE_SIMD
    private void genStoreLclTypeSimd12(GenTreeLclVarCommon tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V SIMD12 local-store recording is not ported.");
    }
#endif
}
#endif
