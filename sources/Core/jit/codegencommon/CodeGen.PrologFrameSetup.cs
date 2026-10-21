// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genEstablishFramePointer(int delta, bool reportUnwindData)
    {
        if (Configuration.TARGET_ARM64)
        {
            genEstablishFramePointerArm64(delta, reportUnwindData);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genEstablishFramePointerLoongArch64(delta, reportUnwindData);
        }
        else if (Configuration.TARGET_ARM)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            assert(Emitter.emitIns_valid_imm_for_add_sp(delta));
            Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, TargetRuntimeMetadata.FramePointerBaseRegister, TargetRuntimeMetadata.StackPointerBaseRegister, delta, INS_FLAGS_DONT_CARE);

            if (reportUnwindData)
            {
                _compiler.unwindPadding();
            }
        }
        else if (Configuration.TARGET_RISCV64)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            assert(Emitter.isValidSimm12(delta));
            Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, TargetRuntimeMetadata.FramePointerBaseRegister, TargetRuntimeMetadata.StackPointerBaseRegister, delta);

            if (reportUnwindData)
            {
                _compiler.unwindSetFrameReg(TargetRuntimeMetadata.FramePointerBaseRegister, unchecked((uint)delta));
            }
        }
        else if (Configuration.TARGET_XARCH)
        {
            Emitter.RequireSupportedInstructionRecording();
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            if (delta == 0)
            {
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, TargetRuntimeMetadata.FramePointerBaseRegister, TargetRuntimeMetadata.StackPointerBaseRegister, canSkip: false);
            }
            else
            {
                Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.FramePointerBaseRegister, TargetRuntimeMetadata.StackPointerBaseRegister, delta);
            }

            if (reportUnwindData)
            {
                _compiler.unwindSetFrameReg(TargetRuntimeMetadata.FramePointerBaseRegister, (uint)delta);
            }
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Frame pointer establishment is not ported for this target.");
            return;
        }
    }

    public void genAllocLclFrame(uint frameSize, regNumber initReg, ref bool initRegZeroed, regMaskTP maskArgRegsLiveIn)
    {
        if (Configuration.TARGET_WASM)
        {
            genAllocLclFrameWasm(frameSize, initReg, ref initRegZeroed, maskArgRegsLiveIn);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            genAllocLclFrameArm64(frameSize, initReg, ref initRegZeroed, maskArgRegsLiveIn);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genAllocLclFrameLoongArch64(frameSize, initReg, ref initRegZeroed, maskArgRegsLiveIn);
        }
        else if (Configuration.TARGET_ARM)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());

            if (frameSize == 0)
            {
                return;
            }

            var pageSize = unchecked((uint)_compiler.eeGetPageSize());
            assert(!_compiler.compHasSecretStubArgument() || (REG_SECRET_STUB_PARAM_ARM != initReg));

            if (frameSize < pageSize)
            {
                Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, unchecked((nint)frameSize));
            }
            else
            {
                _ = genInstrWithConstant(INS_sub, EA_PTRSIZE, TargetRuntimeMetadata.StackProbeHelperArgumentRegister, TargetRuntimeMetadata.StackPointerBaseRegister,
                    unchecked((nint)frameSize), TargetRuntimeMetadata.StackProbeHelperArgumentRegister, INS_FLAGS_DONT_CARE);
                _regSet.verifyRegUsed(TargetRuntimeMetadata.StackProbeHelperArgumentRegister);
                genEmitHelperCall(CORINFO_HELP_STACK_PROBE, 0, EA_UNKNOWN, REG_STACK_PROBE_HELPER_CALL_TARGET);
                _regSet.verifyRegUsed(REG_STACK_PROBE_HELPER_CALL_TARGET);
                _compiler.unwindPadding();
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, TargetRuntimeMetadata.StackProbeHelperArgumentRegister, canSkip: false);

                var clobberMask = new regMaskTP(SRBM_STACK_PROBE_HELPER_ARG_ARM |
                    SRBM_STACK_PROBE_HELPER_CALL_TARGET | SRBM_STACK_PROBE_HELPER_TRASH_ARM);
                if ((genRegMask(initReg) & clobberMask).IsNonEmpty)
                {
                    initRegZeroed = false;
                }
            }

            _compiler.unwindAllocStack(frameSize);
        }
        else if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Local frame allocation is not ported for this target.");
            return;
        }
        else
        {
            Emitter.RequireSupportedInstructionRecording();
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            if (frameSize == 0)
            {
                return;
            }

            var pageSize = _compiler.eeGetPageSize();
            var scratchReg = Configuration.TARGET_X86 ? REG_X86_EAX : REG_X64_RAX;
            if (frameSize == Configuration.REGSIZE_BYTES)
            {
                Emitter.emitIns_R(INS_push, EA_PTRSIZE, scratchReg);
            }
            else if (frameSize < pageSize)
            {
                Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, (nint)frameSize);
                if ((long)frameSize + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES > (long)pageSize)
                {
                    Emitter.emitIns_R_AR(INS_test, EA_4BYTE, scratchReg, TargetRuntimeMetadata.StackPointerBaseRegister, 0);
                }
            }
            else if (Configuration.TARGET_X86)
            {
                var spOffset = unchecked(-(int)frameSize);

                if (_compiler.compHasSecretStubArgument())
                {
                    Emitter.emitIns_R(INS_push, EA_PTRSIZE, REG_SECRET_STUB_PARAM_X86);
                    spOffset += Configuration.REGSIZE_BYTES;
                }

                Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.StackProbeHelperArgumentRegister, TargetRuntimeMetadata.StackPointerBaseRegister, spOffset);
                _regSet.verifyRegUsed(TargetRuntimeMetadata.StackProbeHelperArgumentRegister);
                genEmitHelperCall(CORINFO_HELP_STACK_PROBE, 0, EA_UNKNOWN);

                if (_compiler.compHasSecretStubArgument())
                {
                    Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_SECRET_STUB_PARAM_X86);
                    Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, (nint)frameSize);
                }
                else
                {
                    _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, TargetRuntimeMetadata.StackProbeHelperArgumentRegister, canSkip: false);
                }
            }
            else
            {
                assert((SRBM_STACK_PROBE_HELPER_ARG_AMD64 &
                    (SRBM_SECRET_STUB_PARAM_AMD64 | SRBM_DEFAULT_HELPER_CALL_TARGET_AMD64)) == 0);
                Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.StackProbeHelperArgumentRegister, TargetRuntimeMetadata.StackPointerBaseRegister,
                    unchecked(-(int)frameSize));
                _regSet.verifyRegUsed(TargetRuntimeMetadata.StackProbeHelperArgumentRegister);
                genEmitHelperCall(CORINFO_HELP_STACK_PROBE, 0, EA_UNKNOWN);
                if (initReg == TargetRuntimeMetadata.DefaultHelperCallTargetRegister)
                {
                    initRegZeroed = false;
                }

                assert((SRBM_STACK_PROBE_HELPER_TRASH_AMD64 & SRBM_STACK_PROBE_HELPER_ARG_AMD64) == 0);
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, TargetRuntimeMetadata.StackProbeHelperArgumentRegister, canSkip: false);
            }

            _compiler.unwindAllocStack(frameSize);
            if (initReg == TargetRuntimeMetadata.StackProbeHelperArgumentRegister)
            {
                initRegZeroed = false;
            }
        }
    }

    public void genClearAvxStateInProlog()
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Prolog AVX-state clearing requires xarch.");
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        // Hoist clearing to the prolog when only calls, not this method's own SIMD, require it.
        if (Emitter.ContainsCallNeedingVzeroupper && !Emitter.Contains256BitOrMoreAvxInstruction)
        {
            instGen(INS_vzeroupper);
        }
    }

    public void genClearAvxStateInEpilog()
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Epilog AVX-state clearing requires xarch.");
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        if (Emitter.Contains256BitOrMoreAvxInstruction)
        {
            instGen(INS_vzeroupper);
        }
    }
}
