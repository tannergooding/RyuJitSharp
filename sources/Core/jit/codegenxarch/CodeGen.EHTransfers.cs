// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCallFinally(BasicBlock block)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Finally-call generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(block.Kind == BBJ_CALLFINALLY);
        var nextBlock = block.Next;

        if (block.HasFlag(BBF_RETLESS_CALL))
        {
            Emitter.emitIns_J(INS_call, block.Target);

            // The unreachable breakpoint keeps the call's return address inside
            // its EH region for unwind lookup, including at the end of the code.
            if ((nextBlock is null) || !BasicBlock.sameEHRegion(block, nextBlock))
            {
                instGen(INS_int3);
            }
        }
        else
        {
            // Liveness after the finally call cannot describe last uses in the
            // handler. Keep the call and its continuation instruction non-GC.
            Emitter.emitDisableGC();
            Emitter.emitIns_J(INS_call, block.Target);

            assert(nextBlock is not null);
            assert(nextBlock.Kind == BBJ_CALLFINALLYRET);
            var finallyContinuation = nextBlock.Target;
            if ((nextBlock.Next == finallyContinuation) && !_compiler.fgInDifferentRegions(nextBlock, finallyContinuation))
            {
                // Stack walking from the handler still needs a return address
                // in this special EH region even when the continuation follows.
                instGen(INS_nop);
            }
            else
            {
                inst_JMP(EJ_jmp, finallyContinuation);
            }

            Emitter.emitEnableGC();
        }
#endif
    }

    public void genEHCatchRet(BasicBlock block)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Catch-return generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE | EA_DSP_RELOC_FLG, block.Target, REG_INTRET);
#endif
    }
}
