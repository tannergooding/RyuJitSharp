// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

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
        var tempReg = _internalRegisters.GetSingle(tree);

        // Load the 32-bit offset relative to the method's first block.
        Emitter.emitIns_R_R_R(INS_ldr, EA_4BYTE, baseReg, baseReg, indexReg, INS_OPTS_LSL);

        // Add it to the absolute address of the first block.
        var firstBlock = _compiler.fgFirstBB;
        assert(firstBlock is not null);
        genEmitSwitchBlockAddress(firstBlock, tempReg);
        Emitter.emitIns_R_R_R(INS_add, EA_PTRSIZE, baseReg, baseReg, tempReg);

        // Branch to the selected case.
        Emitter.emitIns_R(INS_br, emitActualTypeSize(TYP_I_IMPL), baseReg);
    }

    public unsafe void genJumpTable(GenTree tree)
    {
        var tableBase = genEmitJumpTable(tree, relativeAddr: true);
        // The handle identifies inline data, not an EE static field.
        Emitter.emitIns_R_C(INS_adr, EA_PTRSIZE, tree.RegNum, REG_NA,
            Compiler.eeFindJitDataOffs(tableBase), 0);
        genProduceReg(tree);
    }

    private void genEmitSwitchBlockAddress(BasicBlock target, regNumber targetReg)
    {
        throw new FatalJitException(CORJIT_SKIPPED,
            "ARM64 block-relative address recording is not ported.");
    }
}
#endif
