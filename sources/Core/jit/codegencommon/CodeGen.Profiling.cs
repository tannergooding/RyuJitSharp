// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genProfilingLeaveCallback(CorInfoHelpFunc helper)
    {
        if (Configuration.TARGET_ARM64)
        {
            assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
            if (!_compiler.compIsProfilerHookNeeded)
            {
                return;
            }

            _compiler.info.compProfilerCallback = true;

            const regNumber profilerFuncIdReg = REG_ARM64_R10;
            const regNumber profilerCallerSpReg = REG_ARM64_R11;
            if (_compiler.compProfilerMethHndIndirected)
            {
                instGen_Set_Reg_To_Imm(EA_PTR_DSP_RELOC, profilerFuncIdReg,
                    unchecked((nint)_compiler.compProfilerMethHnd));
                Emitter.emitIns_R_R(INS_ldr, EA_PTRSIZE, profilerFuncIdReg, profilerFuncIdReg);
            }
            else
            {
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, profilerFuncIdReg,
                    unchecked((nint)_compiler.compProfilerMethHnd));
            }

            GCInfo.gcMarkRegSetNpt(genRegMask(profilerFuncIdReg));

            var callerSPOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
            _ = genInstrWithConstant(INS_add, EA_PTRSIZE, profilerCallerSpReg, genFramePointerReg(),
                unchecked(-(nint)callerSPOffset), profilerCallerSpReg);

            GCInfo.gcMarkRegSetNpt(genRegMask(profilerCallerSpReg));

            genEmitHelperCall(helper, 0, EA_UNKNOWN);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
            if (!_compiler.compIsProfilerHookNeeded)
            {
                return;
            }

            _compiler.info.compProfilerCallback = true;

            bool r0InUse;
            if (helper is CORINFO_HELP_PROF_FCN_TAILCALL)
            {
                r0InUse = false;
            }
            else if (_compiler.info.compRetType is TYP_VOID)
            {
                r0InUse = false;
            }
            else if (varTypeIsFloating(_compiler.info.compRetType) ||
                _compiler.IsHfa(_compiler.info.compMethodInfo->args.retTypeClass))
            {
                r0InUse = _compiler.info.compIsVarArgs || _compiler.opts.compUseSoftFP;
            }
            else
            {
                r0InUse = true;
            }

            var attr = EA_UNKNOWN;

            if (r0InUse)
            {
                if (varTypeIsGC(_compiler.info.compRetNativeType))
                {
                    attr = _compiler.info.compRetNativeType.EmitActualSize;
                }
                else if (_compiler.compMethodReturnsRetBufAddr)
                {
                    attr = EA_BYREF;
                }
                else
                {
                    attr = EA_PTRSIZE;
                }

                _ = Emitter.emitIns_Mov(INS_mov, attr, REG_PROFILER_RET_SCRATCH, TargetRuntimeMetadata.Register0, canSkip: false);
                genTransferRegGCState(REG_PROFILER_RET_SCRATCH, TargetRuntimeMetadata.Register0);
                _regSet.verifyRegUsed(REG_PROFILER_RET_SCRATCH);
            }

            if (_compiler.compProfilerMethHndIndirected)
            {
                Emitter.emitIns_R_AI(INS_ldr, EA_PTR_DSP_RELOC, TargetRuntimeMetadata.Register0,
                    unchecked((nint)_compiler.compProfilerMethHnd));
            }
            else
            {
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, TargetRuntimeMetadata.Register0, unchecked((nint)_compiler.compProfilerMethHnd));
            }

            GCInfo.gcMarkRegSetNpt(TargetRuntimeMetadata.Register0Mask);
            _regSet.verifyRegUsed(TargetRuntimeMetadata.Register0);
            genEmitHelperCall(helper, 0, EA_UNKNOWN);

            if (r0InUse)
            {
                _ = Emitter.emitIns_Mov(INS_mov, attr, TargetRuntimeMetadata.Register0, REG_PROFILER_RET_SCRATCH, canSkip: false);
                genTransferRegGCState(TargetRuntimeMetadata.Register0, REG_PROFILER_RET_SCRATCH);
                GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_RET_SCRATCH));
            }

            return;
        }

        if (Configuration.TARGET_WASM)
        {
            // Wasm omits the matching profiling-entry hook in its prolog.
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
            if (!_compiler.compIsProfilerHookNeeded)
            {
                return;
            }

            _compiler.info.compProfilerCallback = true;

            var instruction = _compiler.compProfilerMethHndIndirected ? INS_ld : INS_addi;
            Emitter.emitIns_R_R_Addr(
                instruction,
                EA_PTRSIZE,
                REG_PROFILER_LEAVE_ARG_FUNC_ID_RISCV64,
                REG_PROFILER_LEAVE_ARG_FUNC_ID_RISCV64,
                _compiler.compProfilerMethHnd);

            GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_LEAVE_ARG_FUNC_ID_RISCV64));

            var callerSPOffset = unchecked(-(nint)_compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed));
            _ = genInstrWithConstant(
                INS_addi,
                EA_PTRSIZE,
                REG_PROFILER_LEAVE_ARG_CALLER_SP_RISCV64,
                genFramePointerReg(),
                callerSPOffset,
                REG_PROFILER_LEAVE_ARG_CALLER_SP_RISCV64);

            GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_LEAVE_ARG_CALLER_SP_RISCV64));

            genEmitHelperCall(helper, 0, EA_UNKNOWN);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            genProfilingLeaveCallbackLoongArch64(helper);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Profiler leave callbacks are unsupported for this target.");
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        _compiler.info.compProfilerCallback = true;
        if (Configuration.TARGET_X86)
        {
            var savedStackLevel = genStackLevel;
            if (Configuration.UNIX_X86_ABI)
            {
                Emitter.emitIns_R_I(INS_sub, EA_4BYTE, TargetRuntimeMetadata.StackPointerBaseRegister, 0xC);
                AddStackLevel(0xC);
                AddNestedAlignment(0xC);
            }
            if (_compiler.compProfilerMethHndIndirected)
            {
                Emitter.emitIns_AR_R(INS_push, EA_PTRSIZE | EA_DSP_RELOC_FLG, REG_NA, REG_NA,
                    unchecked((nint)_compiler.compProfilerMethHnd));
            }
            else
            {
                inst_IV(INS_push, unchecked((nint)_compiler.compProfilerMethHnd));
            }
            genSinglePush();

            var argSize = Configuration.UNIX_X86_ABI ? -Configuration.REGSIZE_BYTES : Configuration.REGSIZE_BYTES;
            genEmitHelperCall(helper, argSize, EA_UNKNOWN);
            if (Configuration.UNIX_X86_ABI)
            {
                Emitter.emitIns_R_I(INS_add, EA_4BYTE, TargetRuntimeMetadata.StackPointerBaseRegister, 0x10);
                SubtractStackLevel(0x10);
                SubtractNestedAlignment(0xC);
            }
            SetStackLevel(savedStackLevel);
            return;
        }

        if (Configuration.WINDOWS_AMD64_ABI)
        {
            noway_assert(_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
            noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 4 * Configuration.REGSIZE_BYTES);
            if (_compiler.lvaKeepAliveAndReportThis() && _compiler.lvaGetDesc(_compiler.info.compThisArg).lvIsInReg)
            {
                var thisReg = _compiler.lvaGetDesc(_compiler.info.compThisArg).RegNum;
                var trash = Emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_LEAVE);
                noway_assert((trash & new regMaskTP(thisReg.SingleTypeMask)).IsEmpty);
            }
        }

        // The helper preserves return registers, including GC roots, while inspecting the result.
        if (_compiler.compProfilerMethHndIndirected)
        {
            Emitter.emitIns_R_AI(INS_mov, EA_PTRSIZE | EA_DSP_RELOC_FLG, TargetRuntimeMetadata.FirstIntegerArgumentRegister,
                unchecked((nint)_compiler.compProfilerMethHnd));
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, TargetRuntimeMetadata.FirstIntegerArgumentRegister, unchecked((nint)_compiler.compProfilerMethHnd));
        }

        if (_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT)
        {
            var callerOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
            Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.SecondIntegerArgumentRegister, genFramePointerReg(), -callerOffset);
        }
        else
        {
            var local = _compiler.lvaGetDesc(0);
            NYI_IF(!local.lvIsParam, "Profiler ELT callback for a method without any params");
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.SecondIntegerArgumentRegister, 0, 0);
        }

        if (Configuration.UNIX_AMD64_ABI)
        {
            genEmitHelperCall(helper, 0, EA_UNKNOWN, TargetRuntimeMetadata.DefaultHelperCallTargetRegister);
        }
        else
        {
            genEmitHelperCall(helper, 0, EA_UNKNOWN, TargetRuntimeMetadata.ThirdIntegerArgumentRegister);
        }
    }

    public regNumber genFramePointerReg() => IsFramePointerUsed ? TargetRuntimeMetadata.FramePointerBaseRegister : TargetRuntimeMetadata.StackPointerBaseRegister;
}
