// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genTableBasedSwitchArm(GenTree tree)
    {
        var operands = tree.AsOp();
        genConsumeOperands(operands);
        var indexReg = operands.Op1.RegNum;
        var baseNode = operands.Op2;
        assert(baseNode is not null);
        var baseReg = baseNode.RegNum;

        Emitter.emitIns_R_ARX(INS_ldr, EA_4BYTE, REG_ARM32_PC, baseReg, indexReg,
            (uint)Configuration.TARGET_POINTER_SIZE, 0);
    }

    public void genJumpTableArm(GenTree tree)
    {
        var tableBase = genEmitJumpTable(tree, relativeAddr: false);
        genMov32RelocatableDataLabel(tableBase, tree.RegNum);
        genProduceReg(tree);
    }
}
