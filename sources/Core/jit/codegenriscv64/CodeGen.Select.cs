// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForSelect(GenTreeOp tree)
    {
        assert(tree.Oper is GT_SELECT);
        assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zicond));
        assert(varTypeIsIntegralOrI(tree.Type));

        var select = tree.AsConditional();
        var condition = select.Cond;
        var trueValue = select.Op1;
        var falseValue = select.Op2;

        genConsumeRegs(condition);
        genConsumeRegs(trueValue);
        genConsumeRegs(falseValue);

        var targetReg = tree.RegNum;
        var conditionReg = condition.RegNum;
        var trueReg = trueValue.IsContained ? REG_ZERO : trueValue.RegNum;
        var falseReg = falseValue.IsContained ? REG_ZERO : falseValue.RegNum;

        var attr = emitActualTypeSize(tree);
        if (falseReg == REG_ZERO)
        {
            Emitter.emitIns_R_R_R(INS_czero_eqz, attr, targetReg, trueReg, conditionReg);
        }
        else if (trueReg == REG_ZERO)
        {
            Emitter.emitIns_R_R_R(INS_czero_nez, attr, targetReg, falseReg, conditionReg);
        }
        else
        {
            var tempReg = InternalRegisters.GetSingle(tree);
            Emitter.emitIns_R_R_R(INS_czero_nez, attr, tempReg, falseReg, conditionReg);
            Emitter.emitIns_R_R_R(INS_czero_eqz, attr, targetReg, trueReg, conditionReg);
            Emitter.emitIns_R_R_R(INS_add, attr, targetReg, targetReg, tempReg);
        }

        genProduceReg(tree);
    }
}
#endif
