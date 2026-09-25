// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForReturnTrap(GenTreeUnOp tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Return-trap generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_RETURNTRAP);
        var data = tree.Op1;
        genConsumeRegs(data);
        var zero = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
        _ = Emitter.emitInsBinary(INS_cmp, EA_4BYTE, data, zero);

        var skipLabel = genCreateTempLabel();
        inst_JMP(EJ_je, skipLabel);
        var tempReg = _internalRegisters.GetSingle(tree, new regMaskTP(_compiler.SRBM_ALLINT));
        assert(genIsValidIntReg(tempReg));
        genEmitHelperCall(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN, tempReg);
        genDefineTempLabel(skipLabel);
#endif
    }
}
