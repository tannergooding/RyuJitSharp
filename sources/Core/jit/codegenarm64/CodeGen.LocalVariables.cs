// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclVar(GenTreeLclVar tree)
    {
        var varNum = tree.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        var targetType = varDsc.GetRegisterType(tree);
        var isRegCandidate = varDsc.lvIsRegCandidate;

        assert((tree.Flags & GTF_VAR_DEF) == 0);

        // If this is a register candidate that has been spilled, genConsumeReg() will reload it at its use.
        // Otherwise, if it's not in a register, we load it here.
        if (!isRegCandidate && !tree.IsMultiReg && ((tree.Flags & GTF_SPILLED) == 0))
        {
            assert(targetType is not TYP_STRUCT);

            var ins = ins_Load(targetType);
            Emitter.emitIns_R_S(ins, emitActualTypeSize(targetType), tree.RegNum, varNum, 0);
            genProduceReg(tree);
        }
    }

    public void genCodeForStoreLclFld(GenTreeLclFld tree)
    {
        var targetType = tree.Type;

#if FEATURE_SIMD
        if (targetType is TYP_SIMD12)
        {
            genStoreLclTypeSimd12(tree);
            return;
        }
#endif

        var targetReg = tree.RegNum;
        var offset = tree.LclOffs;

        noway_assert(targetType is not TYP_STRUCT);
        assert(targetReg == REG_NA);

        var varNum = tree.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);

        var data = tree.Op1;
        genConsumeRegs(data);

        var dataReg = REG_NA;
        if (data.IsContainedIntOrIImmed)
        {
            assert(data.IsIntegralConst(0));
            dataReg = REG_ZR;
        }
        else if (data.IsContained)
        {
            if (data.Oper.IsCnsVec)
            {
                assert(data.IsVectorZero);
                dataReg = REG_ZR;
            }
            else
            {
                assert(data.Oper is GT_BITCAST);
                var bitcastSrc = data.AsUnOp().Op1;
                assert(!bitcastSrc.IsContained);
                dataReg = bitcastSrc.RegNum;
            }
        }
        else
        {
            assert(!data.IsContained);
            dataReg = data.RegNum;
        }

        assert(dataReg != REG_NA);

        var ins = ins_StoreFromSrc(dataReg, targetType);
        Emitter.emitIns_S_R(ins, emitActualTypeSize(targetType), dataReg, varNum, offset);

        genUpdateLife(tree);
        varDsc.RegNum = REG_STK;
    }

    public void genCodeForStoreLclVar(GenTreeLclVar lclNode)
    {
        assert(lclNode.Oper is GT_STORE_LCL_VAR);

        var data = lclNode.Op1;
        if (data.SkipCopyOrReload.IsMultiRegNode)
        {
            genMultiRegStoreToLocal(lclNode);
            return;
        }

        ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);
        if (lclNode.IsMultiReg)
        {
            assert(varTypeIsSimd(data.Type));

            var operandReg = genConsumeReg(data);
            var regCount = varDsc.lvFieldCnt;
            for (byte i = 0; i < regCount; i++)
            {
                var varReg = lclNode.GetRegByIndex(i);
                assert(varReg != REG_NA);

                var fieldLclNum = varDsc.lvFieldLclStart + i;
                ref var fieldVarDsc = ref _compiler.lvaGetDesc(fieldLclNum);
                assert(fieldVarDsc.Type == TYP_FLOAT);

                Emitter.emitIns_R_R_I(INS_dup, emitTypeSize(TYP_FLOAT), varReg, operandReg, i);
            }

            genProduceReg(lclNode);
            return;
        }

        var targetReg = lclNode.RegNum;
        var varNum = lclNode.LclNum;
        var targetType = varDsc.GetRegisterType(lclNode);

#if FEATURE_SIMD
        if (targetType is TYP_SIMD12)
        {
            genStoreLclTypeSimd12(lclNode);
            return;
        }
#endif

        genConsumeRegs(data);

        var dataReg = REG_NA;
        if (data.IsContained)
        {
            var zeroInit = data.IsIntegralConst(0) || data.IsVectorZero;
            assert(zeroInit || data.Oper is GT_BITCAST);

            if (zeroInit && varTypeIsSimd(targetType))
            {
                if (targetReg != REG_NA)
                {
                    Emitter.emitIns_R_I(INS_movi, emitActualTypeSize(targetType), targetReg, 0x00, INS_OPTS_16B);
                }
                else if (targetType is TYP_SIMD16)
                {
                    Emitter.emitIns_S_S_R_R(INS_stp, EA_8BYTE, EA_8BYTE, REG_ZR, REG_ZR, varNum, 0);
                }
                else
                {
                    assert(targetType is TYP_SIMD8);
                    Emitter.emitIns_S_R(INS_str, EA_8BYTE, REG_ZR, varNum, 0);
                }

                genUpdateLifeStore(lclNode, targetReg, ref varDsc);
                return;
            }

            if (zeroInit)
            {
                dataReg = REG_ZR;
            }
            else
            {
                var bitcastSrc = data.AsUnOp().Op1;
                assert(!bitcastSrc.IsContained);
                dataReg = bitcastSrc.RegNum;
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
            Emitter.emitIns_S_R(ins, emitActualTypeSize(targetType), dataReg, varNum, 0);
        }
        else if (varTypeIsIntegral(targetType) && genIsValidIntReg(targetReg) && genIsValidIntReg(dataReg))
        {
            inst_Mov_Extend(targetType, srcInReg: true, targetReg, dataReg, canSkip: true,
                emitActualTypeSize(targetType));
        }
        else if (TargetOS.IsUnix && data.Oper.IsCnsIntOrI && data.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL))
        {
            assert(data.AsIntCon().IconValue == 0);
            Emitter.emitIns_R(INS_mrs_tpid0, emitActualTypeSize(targetType), targetReg);
        }
        else
        {
            inst_Mov(targetType, targetReg, dataReg, canSkip: true);
        }

        genUpdateLifeStore(lclNode, targetReg, ref varDsc);
    }

#if FEATURE_SIMD
    private void genStoreLclTypeSimd12(GenTreeLclVarCommon treeNode)
    {
        assert(treeNode.Oper is GT_STORE_LCL_FLD or GT_STORE_LCL_VAR);

        var offset = treeNode.LclOffs;
        var varNum = treeNode.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        assert(varNum < _compiler.lvaCount);

        var data = treeNode.Op1;
        if (data.IsContained)
        {
            assert(data.IsIntegralConst(0) || data.IsVectorZero);

            Emitter.emitIns_S_R(ins_Store(TYP_DOUBLE), EA_8BYTE, REG_ZR, varNum, offset);
            Emitter.emitIns_S_R(ins_Store(TYP_FLOAT), EA_4BYTE, REG_ZR, varNum, offset + 8);

            genUpdateLife(treeNode);
            varDsc.RegNum = REG_STK;

            return;
        }

        var targetReg = treeNode.RegNum;
        var dataReg = genConsumeReg(data);

        if (targetReg != REG_NA)
        {
            assert(genIsValidFloatReg(targetReg));
            inst_Mov(treeNode.Type, targetReg, dataReg, canSkip: true);
        }
        else
        {
            Emitter.emitStoreSimd12ToLclOffset(unchecked((uint)varNum), offset, dataReg, treeNode);
        }

        genUpdateLifeStore(treeNode, targetReg, ref varDsc);
    }
#endif

    private void genUpdateLifeStore(GenTree tree, regNumber targetReg, ref LclVarDsc varDsc)
    {
        if (targetReg != REG_NA)
        {
            genProduceReg(tree);
        }
        else
        {
            genUpdateLife(tree);
            varDsc.RegNum = REG_STK;
        }
    }
}
#endif
