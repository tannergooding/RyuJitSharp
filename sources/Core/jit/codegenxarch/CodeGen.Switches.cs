// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genTableBasedSwitch(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Table-switch generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        genConsumeOperands(tree.AsOp());
        var indexReg = tree.AsOp().Op1.RegNum;
        var baseNode = tree.AsOp().Op2;
        assert(baseNode is not null);
        var baseReg = baseNode.RegNum;
        var tempReg = _internalRegisters.GetSingle(tree);
        assert(_compiler.fgFirstBB is not null);

        // Entries are 32-bit offsets from the method's first block, not from the table.
        Emitter.emitIns_R_ARX(INS_mov, EA_4BYTE, baseReg, baseReg, indexReg, 4, 0);
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE | EA_DSP_RELOC_FLG, _compiler.fgFirstBB, tempReg);
        Emitter.emitIns_R_R(INS_add, EA_PTRSIZE, baseReg, tempReg);
        Emitter.emitIns_R(INS_i_jmp, TYP_I_IMPL.EmitSize, baseReg);
#endif
    }

    public unsafe void genJumpTable(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Jump-table address generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var tableBase = genEmitJumpTable(tree, relativeAddr: true);
        Emitter.emitIns_R_C(INS_lea, TYP_I_IMPL.EmitSize, tree.RegNum,
            Compiler.eeFindJitDataOffs(tableBase), 0);
        genProduceReg(tree);
#endif
    }
}
#endif
