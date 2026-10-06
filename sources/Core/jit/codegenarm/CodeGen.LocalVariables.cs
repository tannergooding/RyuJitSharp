// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclVar(GenTreeLclVar tree)
    {
        assert((tree.Flags & GTF_VAR_DEF) == 0);

        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);
        if (!varDsc.lvIsRegCandidate && !tree.IsMultiReg && ((tree.Flags & GTF_SPILLED) == 0))
        {
            var type = varDsc.GetRegisterType(tree);
            Emitter.emitIns_R_S(ins_Load(type), type.EmitSize, tree.RegNum, tree.LclNum, 0);
            genProduceReg(tree);
        }
    }

    public void genCodeForStoreLclFld(GenTreeLclFld tree)
    {
        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        var offset = tree.LclOffs;

        noway_assert(targetType is not TYP_STRUCT);
        noway_assert(targetReg == REG_NA);

        var varNum = tree.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        var data = tree.Op1;
        genConsumeRegs(data);

        regNumber dataReg;
        if (data.IsContained)
        {
            assert(data.Oper is GT_BITCAST);
            var bitcastSrc = data.AsUnOp().Op1;
            assert(!bitcastSrc.IsContained);
            dataReg = bitcastSrc.RegNum;
        }
        else
        {
            dataReg = data.RegNum;
        }
        assert(dataReg != REG_NA);

        if (tree.IsOffsetMisaligned)
        {
            var addr = InternalRegisters.Extract(tree);
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, addr, varNum, offset);
            if (targetType is TYP_FLOAT)
            {
                var floatAsInt = InternalRegisters.GetSingle(tree);
                _ = Emitter.emitIns_Mov(INS_vmov_f2i, EA_4BYTE, floatAsInt, dataReg, canSkip: false);
                Emitter.emitIns_R_R(INS_str, EA_4BYTE, floatAsInt, addr);
            }
            else
            {
                var halfDoubleAsInt1 = InternalRegisters.Extract(tree);
                var halfDoubleAsInt2 = InternalRegisters.GetSingle(tree);
                Emitter.emitIns_R_R_R(INS_vmov_d2i, EA_8BYTE, halfDoubleAsInt1, halfDoubleAsInt2, dataReg);
                Emitter.emitIns_R_R_I(INS_str, EA_4BYTE, halfDoubleAsInt1, addr, 0, INS_FLAGS_DONT_CARE);
                Emitter.emitIns_R_R_I(INS_str, EA_4BYTE, halfDoubleAsInt2, addr, 4, INS_FLAGS_DONT_CARE);
            }
        }
        else
        {
            var attr = targetType.EmitSize;
            var ins = ins_StoreFromSrc(dataReg, targetType);
            Emitter.emitIns_S_R(ins, attr, dataReg, varNum, offset);
        }

        genUpdateLife(tree);
        varDsc.RegNum = REG_STK;
    }

    public void genCodeForStoreLclVar(GenTreeLclVar tree)
    {
        var data = tree.Op1;
        if (data.SkipCopyOrReload.IsMultiRegNode)
        {
            genMultiRegStoreToLocal(tree);
            return;
        }

        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);
        var targetType = varDsc.GetRegisterType(tree);
        if (targetType is TYP_LONG)
        {
            genStoreLongLclVar(tree);
            return;
        }

        genConsumeRegs(data);

        regNumber dataReg;
        if (data.IsContained)
        {
            assert(data.Oper is GT_BITCAST);
            var bitcastSrc = data.AsUnOp().Op1;
            assert(!bitcastSrc.IsContained);
            dataReg = bitcastSrc.RegNum;
        }
        else
        {
            dataReg = data.RegNum;
        }
        assert(dataReg != REG_NA);

        var targetReg = tree.RegNum;
        if (targetReg == REG_NA)
        {
            inst_set_SV_var(tree);
            var ins = ins_StoreFromSrc(dataReg, targetType);
            Emitter.emitIns_S_R(ins, targetType.EmitSize, dataReg, tree.LclNum, 0);
        }
        else if (varTypeIsIntegral(targetType) &&
            Emitter.isGeneralRegister(targetReg) && Emitter.isGeneralRegister(dataReg))
        {
            inst_Mov_Extend(targetType, srcInReg: true, targetReg, dataReg, canSkip: true,
                targetType.EmitActualSize);
        }
        else
        {
            inst_Mov(targetType, targetReg, dataReg, canSkip: true);
        }

        genUpdateLifeStore(tree, targetReg, ref varDsc);
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
}
#endif
