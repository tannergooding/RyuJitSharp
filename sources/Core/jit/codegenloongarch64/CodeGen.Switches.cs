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

        var indexReg = operands.Op1.RegNum;
        var baseNode = operands.Op2;
        assert(baseNode is not null);
        var baseReg = baseNode.RegNum;
        var tempReg = InternalRegisters.GetSingle(tree);

        // The table entries are 32-bit offsets relative to the method's first block.
        Emitter.emitIns_R_R_I(INS_slli_d, EA_8BYTE, REG_R21, indexReg, 2);
        Emitter.emitIns_R_R_R(INS_add_d, EA_8BYTE, baseReg, baseReg, REG_R21);
        Emitter.emitIns_R_R_I(INS_ld_w, EA_4BYTE, baseReg, baseReg, 0);

        var firstBlock = _compiler.fgFirstBB;
        assert(firstBlock is not null);
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE, firstBlock, tempReg);
        Emitter.emitIns_R_R_R(INS_add_d, EA_PTRSIZE, baseReg, baseReg, tempReg);
        Emitter.emitIns_R_R_I(INS_jirl, emitActualTypeSize(TYP_I_IMPL), REG_R0, baseReg, 0);
    }
}
