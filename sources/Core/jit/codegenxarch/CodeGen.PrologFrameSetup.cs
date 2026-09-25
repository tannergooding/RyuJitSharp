// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genEstablishFramePointer(int delta, bool reportUnwindData)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog frame-pointer setup requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (delta == 0)
        {
            Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_FPBASE, REG_SPBASE, canSkip: false);
        }
        else
        {
            Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_FPBASE, REG_SPBASE, delta);
        }

        if (reportUnwindData)
        {
            _compiler.unwindSetFrameReg(REG_FPBASE, (uint)delta);
        }
#endif
    }

    public void genAllocLclFrame(uint frameSize, regNumber initReg, ref bool initRegZeroed, regMaskTP maskArgRegsLiveIn)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog stack allocation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (frameSize == 0)
        {
            return;
        }

        var pageSize = _compiler.eeGetPageSize();
        if (frameSize == REGSIZE_BYTES)
        {
            Emitter.emitIns_R(INS_push, EA_PTRSIZE, REG_RAX);
            _compiler.unwindAllocStack(frameSize);
        }
        else if (frameSize < pageSize)
        {
            Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, (nint)frameSize);
            _compiler.unwindAllocStack(frameSize);
            if (frameSize + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES > pageSize)
            {
                Emitter.emitIns_R_AR(INS_test, EA_4BYTE, REG_RAX, REG_SPBASE, 0);
            }
        }
        else
        {
            assert((SRBM_STACK_PROBE_HELPER_ARG & (SRBM_SECRET_STUB_PARAM | SRBM_DEFAULT_HELPER_CALL_TARGET)) == 0);
            Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_STACK_PROBE_HELPER_ARG, REG_SPBASE,
                unchecked(-(int)frameSize));
            _regSet.verifyRegUsed(REG_STACK_PROBE_HELPER_ARG);
            genEmitHelperCall(CORINFO_HELP_STACK_PROBE, 0, EA_UNKNOWN);
            if (initReg == REG_DEFAULT_HELPER_CALL_TARGET)
            {
                initRegZeroed = false;
            }

            assert((SRBM_STACK_PROBE_HELPER_TRASH & SRBM_STACK_PROBE_HELPER_ARG) == 0);
            Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE, REG_STACK_PROBE_HELPER_ARG, canSkip: false);
            _compiler.unwindAllocStack(frameSize);
            if (initReg == REG_STACK_PROBE_HELPER_ARG)
            {
                initRegZeroed = false;
            }
        }
#endif
    }

    public void genClearAvxStateInProlog()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog AVX-state clearing requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        // Hoist clearing to the prolog when only calls, not this method's own SIMD, require it.
        if (Emitter.ContainsCallNeedingVzeroupper && !Emitter.Contains256BitOrMoreAvxInstruction)
        {
            instGen(INS_vzeroupper);
        }
#endif
    }

    public void genClearAvxStateInEpilog()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Epilog AVX-state clearing requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        if (Emitter.Contains256BitOrMoreAvxInstruction)
        {
            instGen(INS_vzeroupper);
        }
#endif
    }
}
