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
            assert(Arm64Emitter.isVectorRegister(targetReg));
            inst_Mov(treeNode.Type, targetReg, dataReg, canSkip: true);
        }
        else
        {
            Emitter.emitStoreSimd12ToLclOffset(unchecked((uint)varNum), offset, dataReg, treeNode);
        }

        genUpdateLifeStore(treeNode, targetReg, ref varDsc);
    }

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
#endif
}
#endif
