// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
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
            NYI_LOONGARCH64("genCodeForStoreLclVar : unimplemented on LoongArch64 yet");

            var operandReg = genConsumeReg(data);
            var regCount = varDsc.lvFieldCnt;
            for (byte i = 0; i < regCount; i++)
            {
                var varReg = lclNode.GetRegByIndex(i);
                assert(varReg != REG_NA);

                var fieldLclNum = varDsc.lvFieldLclStart + i;
                ref var fieldVarDsc = ref _compiler.lvaGetDesc(fieldLclNum);
                assert(fieldVarDsc.TypeIs(TYP_FLOAT));

                Emitter.emitIns_R_R_I(INS_st_d, emitTypeSize(TYP_FLOAT), varReg, operandReg, i);
            }
            genProduceReg(lclNode);
            return;
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
            // TODO-LOONGARCH64-CQ: supporting the SIMD.
            assert(!varTypeIsSIMD(targetType));

            if (zeroInit)
            {
                dataReg = REG_R0;
            }
            else if (data.IsIntegralConst())
            {
                var immediate = data.AsIntConCommon().IconValue;
                Emitter.emitIns_I_la(EA_PTRSIZE, REG_R21, immediate);
                dataReg = REG_R21;
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
                Emitter.emitIns_R_R_I(INS_ori, targetType.EmitActualSize, targetReg, REG_TP, 0);
            }
            else if (dataReg != targetReg)
            {
                inst_Mov(targetType, targetReg, dataReg, true, targetType.EmitActualSize);
            }

            genProduceReg(lclNode);
        }
    }

#if FEATURE_SIMD
    private void genStoreLclTypeSimd12(GenTreeLclVarCommon tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD12 local-store recording is not ported.");
    }
#endif
}
#endif
