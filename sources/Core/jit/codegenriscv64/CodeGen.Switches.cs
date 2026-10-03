// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genTableBasedSwitch(GenTree tree)
    {
        var operands = tree.AsOp();
        genConsumeOperands(operands);

        var indexNode = operands.Op1;
        var indexReg = indexNode.RegNum;
        var baseNode = operands.Op2;
        assert(baseNode is not null);
        assert(baseNode.Type is TYP_I_IMPL);
        var baseReg = baseNode.RegNum;
        var tempReg = InternalRegisters.GetSingle(tree);

        if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Zba))
        {
            var indexSize = indexNode.Type.EmitSize;
            var sh2add = indexSize is EA_4BYTE ? INS_sh2add_uw : INS_sh2add;
            Emitter.emitIns_R_R_R(sh2add, indexSize, baseReg, indexReg, baseReg);
        }
        else
        {
            assert(indexNode.Type is TYP_I_IMPL);
            Emitter.emitIns_R_R_I(INS_slli, EA_8BYTE, tempReg, indexReg, 2);
            Emitter.emitIns_R_R_R(INS_add, EA_8BYTE, baseReg, baseReg, tempReg);
        }

        // The table entries are 32-bit offsets relative to the method's first block.
        Emitter.emitIns_R_R_I(INS_lw, EA_4BYTE, baseReg, baseReg, 0);

        var firstBlock = _compiler.fgFirstBB;
        assert(firstBlock is not null);
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE, firstBlock, tempReg);
        Emitter.emitIns_R_R_R(INS_add, EA_PTRSIZE, baseReg, baseReg, tempReg);
        Emitter.emitIns_R_R_I(INS_jalr, emitActualTypeSize(TYP_I_IMPL), REG_R0, baseReg, 0);
    }
}
