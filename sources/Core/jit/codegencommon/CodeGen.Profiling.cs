// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if PROFILING_SUPPORTED
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genProfilingLeaveCallback(CorInfoHelpFunc helper)
    {
#if TARGET_ARM64
        assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        _compiler.info.compProfilerCallback = true;

        const regNumber profilerFuncIdReg = REG_R10;
        const regNumber profilerCallerSpReg = REG_R11;
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
        genInstrWithConstant(INS_add, EA_PTRSIZE, profilerCallerSpReg, genFramePointerReg(),
            unchecked(-(nint)callerSPOffset), profilerCallerSpReg);

        GCInfo.gcMarkRegSetNpt(genRegMask(profilerCallerSpReg));

        genEmitHelperCall(helper, 0, EA_UNKNOWN);
#elif TARGET_ARM
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
#if CONFIGURABLE_ARM_ABI
            r0InUse = _compiler.info.compIsVarArgs || _compiler.opts.compUseSoftFP;
#else
            r0InUse = _compiler.info.compIsVarArgs || Compiler.Options.compUseSoftFP;
#endif
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

            Emitter.emitIns_Mov(INS_mov, attr, REG_PROFILER_RET_SCRATCH, REG_R0, canSkip: false);
            genTransferRegGCState(REG_PROFILER_RET_SCRATCH, REG_R0);
            _regSet.verifyRegUsed(REG_PROFILER_RET_SCRATCH);
        }

        if (_compiler.compProfilerMethHndIndirected)
        {
            Emitter.emitIns_R_AI(INS_ldr, EA_PTR_DSP_RELOC, REG_R0,
                unchecked((nint)_compiler.compProfilerMethHnd));
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, REG_R0, unchecked((nint)_compiler.compProfilerMethHnd));
        }

        GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_R0));
        _regSet.verifyRegUsed(REG_R0);
        genEmitHelperCall(helper, 0, EA_UNKNOWN);

        if (r0InUse)
        {
            Emitter.emitIns_Mov(INS_mov, attr, REG_R0, REG_PROFILER_RET_SCRATCH, canSkip: false);
            genTransferRegGCState(REG_R0, REG_PROFILER_RET_SCRATCH);
            GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_RET_SCRATCH));
        }
#elif TARGET_WASM
        // Wasm omits the matching profiling-entry hook in its prolog.
#elif TARGET_RISCV64
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
            REG_PROFILER_LEAVE_ARG_FUNC_ID,
            REG_PROFILER_LEAVE_ARG_FUNC_ID,
            _compiler.compProfilerMethHnd);

        GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_LEAVE_ARG_FUNC_ID));

        var callerSPOffset = unchecked(-(nint)_compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed));
        genInstrWithConstant(
            INS_addi,
            EA_PTRSIZE,
            REG_PROFILER_LEAVE_ARG_CALLER_SP,
            genFramePointerReg(),
            callerSPOffset,
            REG_PROFILER_LEAVE_ARG_CALLER_SP);

        GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_LEAVE_ARG_CALLER_SP));

        genEmitHelperCall(helper, 0, EA_UNKNOWN);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Profiler leave callbacks require xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        _compiler.info.compProfilerCallback = true;
#if TARGET_X86
        var savedStackLevel = genStackLevel;
#if UNIX_X86_ABI
        Emitter.emitIns_R_I(INS_sub, EA_4BYTE, REG_SPBASE, 0xC);
        AddStackLevel(0xC);
        AddNestedAlignment(0xC);
#endif
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

#if UNIX_X86_ABI
        var argSize = -REGSIZE_BYTES;
#else
        var argSize = REGSIZE_BYTES;
#endif
        genEmitHelperCall(helper, argSize, EA_UNKNOWN);
#if UNIX_X86_ABI
        Emitter.emitIns_R_I(INS_add, EA_4BYTE, REG_SPBASE, 0x10);
        SubtractStackLevel(0x10);
        SubtractNestedAlignment(0xC);
#endif
        SetStackLevel(savedStackLevel);
#else
#if WINDOWS_AMD64_ABI
        noway_assert(_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 4 * REGSIZE_BYTES);
        if (_compiler.lvaKeepAliveAndReportThis() && _compiler.lvaGetDesc(_compiler.info.compThisArg).lvIsInReg)
        {
            var thisReg = _compiler.lvaGetDesc(_compiler.info.compThisArg).RegNum;
            var trash = Emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_LEAVE);
            noway_assert((trash & new regMaskTP(thisReg.SingleTypeMask)).IsEmpty);
        }
#endif

        // The helper preserves return registers, including GC roots, while inspecting the result.
        if (_compiler.compProfilerMethHndIndirected)
        {
            Emitter.emitIns_R_AI(INS_mov, EA_PTRSIZE | EA_DSP_RELOC_FLG, REG_ARG_0,
                unchecked((nint)_compiler.compProfilerMethHnd));
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, REG_ARG_0, unchecked((nint)_compiler.compProfilerMethHnd));
        }

        if (_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT)
        {
            var callerOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
            Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_ARG_1, genFramePointerReg(), -callerOffset);
        }
        else
        {
            var local = _compiler.lvaGetDesc(0);
            NYI_IF(!local.lvIsParam, "Profiler ELT callback for a method without any params");
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, REG_ARG_1, 0, 0);
        }

#if UNIX_AMD64_ABI
        genEmitHelperCall(helper, 0, EA_UNKNOWN, REG_DEFAULT_PROFILER_CALL_TARGET);
#else
        genEmitHelperCall(helper, 0, EA_UNKNOWN, REG_ARG_2);
#endif
#endif
#endif
    }

    public regNumber genFramePointerReg() => IsFramePointerUsed ? REG_FPBASE : REG_SPBASE;
}
#endif
