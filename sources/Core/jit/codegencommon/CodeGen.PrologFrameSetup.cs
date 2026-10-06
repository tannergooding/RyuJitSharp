// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genEstablishFramePointer(int delta, bool reportUnwindData)
    {
#if TARGET_ARM64
        genEstablishFramePointerArm64(delta, reportUnwindData);
#elif TARGET_ARM
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(arm_Valid_Imm_For_Add_SP(delta));
        Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, REG_FPBASE, REG_SPBASE, delta, INS_FLAGS_DONT_CARE);

        if (reportUnwindData)
        {
            _compiler.unwindPadding();
        }
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Frame pointer establishment is not ported for this target.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (delta == 0)
        {
            _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_FPBASE, REG_SPBASE, canSkip: false);
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

#if !TARGET_WASM
    public void genAllocLclFrame(uint frameSize, regNumber initReg, ref bool initRegZeroed, regMaskTP maskArgRegsLiveIn)
    {
#if TARGET_ARM64
        genAllocLclFrameArm64(frameSize, initReg, ref initRegZeroed, maskArgRegsLiveIn);
#elif TARGET_ARM
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        if (frameSize == 0)
        {
            return;
        }

        var pageSize = unchecked((uint)_compiler.eeGetPageSize());
        assert(!_compiler.compHasSecretStubArgument() || (REG_SECRET_STUB_PARAM != initReg));

        if (frameSize < pageSize)
        {
            Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, unchecked((nint)frameSize));
        }
        else
        {
            genInstrWithConstant(INS_sub, EA_PTRSIZE, REG_STACK_PROBE_HELPER_ARG, REG_SPBASE,
                unchecked((nint)frameSize), REG_STACK_PROBE_HELPER_ARG, INS_FLAGS_DONT_CARE);
            _regSet.verifyRegUsed(REG_STACK_PROBE_HELPER_ARG);
            genEmitHelperCall(CORINFO_HELP_STACK_PROBE, 0, EA_UNKNOWN, REG_STACK_PROBE_HELPER_CALL_TARGET);
            _regSet.verifyRegUsed(REG_STACK_PROBE_HELPER_CALL_TARGET);
            _compiler.unwindPadding();
            _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE, REG_STACK_PROBE_HELPER_ARG, canSkip: false);

            var clobberMask = new regMaskTP(SRBM_STACK_PROBE_HELPER_ARG | SRBM_STACK_PROBE_HELPER_CALL_TARGET |
                SRBM_STACK_PROBE_HELPER_TRASH);
            if ((genRegMask(initReg) & clobberMask).IsNonEmpty)
            {
                initRegZeroed = false;
            }
        }

        _compiler.unwindAllocStack(frameSize);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Local frame allocation is not ported for this target.");
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
#if TARGET_X86
            var spOffset = unchecked(-(int)frameSize);

            if (_compiler.compHasSecretStubArgument())
            {
                Emitter.emitIns_R(INS_push, EA_PTRSIZE, REG_SECRET_STUB_PARAM);
                spOffset += REGSIZE_BYTES;
            }

            Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_STACK_PROBE_HELPER_ARG, REG_SPBASE, spOffset);
            _regSet.verifyRegUsed(REG_STACK_PROBE_HELPER_ARG);
            genEmitHelperCall(CORINFO_HELP_STACK_PROBE, 0, EA_UNKNOWN);

            if (_compiler.compHasSecretStubArgument())
            {
                Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_SECRET_STUB_PARAM);
                Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, (nint)frameSize);
            }
            else
            {
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE, REG_STACK_PROBE_HELPER_ARG, canSkip: false);
            }
#else
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
            _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE, REG_STACK_PROBE_HELPER_ARG, canSkip: false);
#endif
            _compiler.unwindAllocStack(frameSize);
            if (initReg == REG_STACK_PROBE_HELPER_ARG)
            {
                initRegZeroed = false;
            }
        }
#endif
    }
#endif

    public void genClearAvxStateInProlog()
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog AVX-state clearing requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        // Hoist clearing to the prolog when only calls, not this method's own SIMD, require it.
        if (Emitter.ContainsCallNeedingVzeroupper && !Emitter.Contains256BitOrMoreAvxInstruction)
        {
            instGen(INS_vzeroupper);
        }
#endif
    }

    public void genClearAvxStateInEpilog()
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Epilog AVX-state clearing requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        if (Emitter.Contains256BitOrMoreAvxInstruction)
        {
            instGen(INS_vzeroupper);
        }
#endif
    }
}
