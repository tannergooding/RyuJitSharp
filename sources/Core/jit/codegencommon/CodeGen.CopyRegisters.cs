// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genRegCopy(GenTree treeNode)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Register copy generation outside Windows AMD64 is not implemented.");
#else
        assert(treeNode.Oper is GT_COPY);
        var op1 = treeNode.AsUnOp().Op1;

        if (op1.IsMultiRegNode)
        {
            // Copy before reloading the next field: that reload can overwrite this field's source.
            // The child supplies the full count; COPY only counts through its last assigned register.
            var regCount = op1.GetMultiRegCount(_compiler);
            assert(regCount <= MAX_MULTIREG_COUNT);
            var busyRegs = RBM_NONE;

            for (byte i = 0; i < regCount; i++)
            {
                if ((op1.GetRegSpillFlagByIdx(i) & GTF_SPILLED) == 0)
                {
                    var reg = op1.GetRegByIndex(i);
                    busyRegs |= regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
                }
            }

            for (byte i = 0; i < regCount; i++)
            {
                var sourceReg = op1.GetRegByIndex(i);
                var copiedReg = genRegCopy(treeNode, i);
                var copiedMask = regMaskTP.CreateFromRegNum(copiedReg, copiedReg.SingleTypeMask);
                if (copiedReg != sourceReg)
                {
                    assert((busyRegs & copiedMask).IsEmpty);
                    busyRegs &= ~regMaskTP.CreateFromRegNum(sourceReg, sourceReg.SingleTypeMask);
                }
                busyRegs |= copiedMask;
            }

            return;
        }

        var srcReg = genConsumeReg(op1);
        var targetType = treeNode.Type;
        var targetReg = treeNode.RegNum;
        assert(srcReg != REG_NA);
        assert(targetReg != REG_NA);
        assert(targetType != TYP_STRUCT);
        inst_Mov(targetType, targetReg, srcReg, canSkip: false);

        if (op1.Oper.IsLocal)
        {
            var lcl = op1.AsLclVarCommon();
            assert((lcl.Flags & GTF_VAR_DEF) == 0);

            // A dying source or temporary copy does not move the local's permanent home.
            if (((lcl.Flags & GTF_VAR_DEATH) == 0) && ((treeNode.Flags & GTF_VAR_DEATH) == 0))
            {
                ref var varDsc = ref _compiler.lvaGetDesc(lcl.LclNum);
                if (varDsc.RegNum != REG_STK)
                {
                    genUpdateRegLife(in varDsc, isBorn: false, isDying: true
#if DEBUG
                        , op1
#endif
                        );
                    _gcInfo.gcMarkRegSetNpt(op1.RegMask);
                    genUpdateVarReg(ref varDsc, treeNode);
                    getVariableLiveKeeper().siUpdateVariableLiveRange(in varDsc, lcl.LclNum);
                    genUpdateRegLife(in varDsc, isBorn: true, isDying: false
#if DEBUG
                        , treeNode
#endif
                        );
                }
            }
        }

        genProduceReg(treeNode);
#endif
    }

    public regNumber genRegCopy(GenTree treeNode, byte multiRegIndex)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Indexed register copies outside Windows AMD64 are not implemented.");
#else
        assert(treeNode.Oper is GT_COPY);
        var op1 = treeNode.AsUnOp().Op1;
        assert(op1.IsMultiRegNode);
        var copyNode = treeNode.AsCopyOrReload();
        assert(copyNode.RegCount <= MAX_MULTIREG_COUNT);
        _ = genConsumeReg(op1, multiRegIndex);

        var sourceReg = op1.GetRegByIndex(multiRegIndex);
        var targetReg = copyNode.GetRegNumByIdx(multiRegIndex);
        if (targetReg != REG_NA)
        {
            assert(sourceReg != targetReg);
            var_types type;

            if (op1.IsMultiRegLclVar)
            {
                ref var parentVarDsc = ref _compiler.lvaGetDesc(op1.AsLclVar().LclNum);
                var fieldVarNum = parentVarDsc.lvFieldLclStart + multiRegIndex;
                ref var fieldVarDsc = ref _compiler.lvaGetDesc(fieldVarNum);
                type = fieldVarDsc.Type;
                inst_Mov(type, targetReg, sourceReg, canSkip: false);

                if (!op1.AsLclVar().IsLastUse(multiRegIndex) && (fieldVarDsc.RegNum != REG_STK))
                {
                    genUpdateRegLife(in fieldVarDsc, isBorn: false, isDying: true
#if DEBUG
                        , op1
#endif
                        );
                    _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(sourceReg, sourceReg.SingleTypeMask));
                    genUpdateVarReg(ref fieldVarDsc, treeNode);
                    getVariableLiveKeeper().siUpdateVariableLiveRange(in fieldVarDsc, fieldVarNum);
                    genUpdateRegLife(in fieldVarDsc, isBorn: true, isDying: false
#if DEBUG
                        , treeNode
#endif
                        );
                }
            }
            else
            {
                type = op1.GetRegTypeByIndex(multiRegIndex);
                inst_Mov(type, targetReg, sourceReg, canSkip: false);
                _gcInfo.gcMarkRegPtrVal(targetReg, type);
            }

            return targetReg;
        }

        return sourceReg;
#endif
    }
}
