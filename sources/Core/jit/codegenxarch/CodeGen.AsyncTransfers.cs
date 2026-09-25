// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genReturnSuspend(GenTreeUnOp tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Suspension return generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var op = tree.Op1;
        assert(op.Type == TYP_REF);
        var reg = genConsumeReg(op);
        inst_Mov(TYP_REF, REG_ASYNC_CONTINUATION_RET, reg, canSkip: true);
        _gcInfo.gcMarkRegPtrVal(REG_ASYNC_CONTINUATION_RET, TYP_REF);

        var descriptor = _compiler.compRetTypeDesc;
        var count = descriptor.ReturnRegCount;
        for (byte i = 0; i < count; i++)
        {
            if (varTypeIsGC(descriptor.GetReturnRegType(i)))
            {
                var returnReg = descriptor.GetAbiReturnReg(i, _compiler.info.compCallConv);
                instGen_Set_Reg_To_Zero(EA_PTRSIZE, returnReg);
            }
        }
        genMarkReturnGCInfo();
#endif
    }

    public void genCodeForAsyncContinuation(GenTree tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Async continuation generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_ASYNC_CONTINUATION);
        inst_Mov(tree.Type, tree.RegNum, REG_ASYNC_CONTINUATION_RET, canSkip: true);
        genTransferRegGCState(tree.RegNum, REG_ASYNC_CONTINUATION_RET);
        genProduceReg(tree);
#endif
    }

    public void genNonLocalJmp(GenTreeUnOp tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Nonlocal jump generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        genConsumeOperands(tree);
        inst_TT(INS_i_jmp, EA_PTRSIZE, tree.Op1);
#endif
    }

    public void genFtnEntry(GenTree tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Function-entry address generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE | EA_DSP_RELOC_FLG, Emitter.emitGetFirstPrologIG(), tree.RegNum);
        genProduceReg(tree);
#endif
    }

    public void genPatchpoint(GenTreeUnOp tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Patchpoint generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_PATCHPOINT or GT_PATCHPOINT_FORCED);
        genConsumeOperands(tree);
        genCopyRegIfNeeded(tree.Op1, REG_ARG_0);
        if (tree.Oper == GT_PATCHPOINT)
        {
            genCopyRegIfNeeded(tree.AsOp().Op2, REG_ARG_1);
        }
        var helper = tree.Oper == GT_PATCHPOINT ? CORINFO_HELP_PATCHPOINT : CORINFO_HELP_PATCHPOINT_FORCED;
        genEmitHelperCall(helper, 0, EA_UNKNOWN);

        // A tail-jump prefix would falsely tell the Windows unwinder that the
        // epilog has restored RSP and callee-saved registers.
        Emitter.emitIns_R(INS_i_jmp, EA_PTRSIZE, REG_INTRET);
#endif
    }
}
