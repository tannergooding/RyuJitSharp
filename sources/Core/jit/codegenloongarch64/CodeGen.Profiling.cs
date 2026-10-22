// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genProfilingEnterCallbackLoongArch64(regNumber initReg, ref bool initRegZeroed)
    {
        if (!Configuration.TARGET_LOONGARCH64 || !Configuration.PROFILING_SUPPORTED)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.LoongArch64ProfilingCallbacksAreUnavailable);
            return;
        }

        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        assert(!_compiler.compProfilerMethHndIndirected);
        instGen_Set_Reg_To_Imm(
            EA_PTRSIZE,
            REG_PROFILER_ENTER_ARG_FUNC_ID_LOONGARCH64,
            unchecked((nint)_compiler.compProfilerMethHnd));

        var callerStackOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
        _ = genInstrWithConstantLoongArch64(
            INS_addi_d,
            EA_PTRSIZE,
            REG_PROFILER_ENTER_ARG_CALLER_SP_LOONGARCH64,
            genFramePointerReg(),
            unchecked(-(nint)callerStackOffset),
            REG_PROFILER_ENTER_ARG_CALLER_SP_LOONGARCH64);

        genEmitHelperCallLoongArch64(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN);

        var clobberedRegisters = new regMaskTP(SRBM_PROFILER_ENTER_TRASH_LOONGARCH64)
            | new regMaskTP(SRBM_PROFILER_ENTER_ARG_FUNC_ID_LOONGARCH64)
            | new regMaskTP(SRBM_PROFILER_ENTER_ARG_CALLER_SP_LOONGARCH64);
        if ((clobberedRegisters & genRegMask(initReg)).IsNonEmpty)
        {
            initRegZeroed = false;
        }
    }

    private unsafe void genProfilingLeaveCallbackLoongArch64(
        CorInfoHelpFunc helper = CORINFO_HELP_PROF_FCN_LEAVE)
    {
        if (!Configuration.TARGET_LOONGARCH64 || !Configuration.PROFILING_SUPPORTED)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.LoongArch64ProfilingCallbacksAreUnavailable);
            return;
        }

        assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        _compiler.info.compProfilerCallback = true;
        assert(!_compiler.compProfilerMethHndIndirected);

        instGen_Set_Reg_To_Imm(
            EA_PTRSIZE,
            REG_PROFILER_LEAVE_ARG_FUNC_ID_LOONGARCH64,
            unchecked((nint)_compiler.compProfilerMethHnd));

        GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_LEAVE_ARG_FUNC_ID_LOONGARCH64));

        var callerStackOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
        _ = genInstrWithConstantLoongArch64(
            INS_addi_d,
            EA_PTRSIZE,
            REG_PROFILER_LEAVE_ARG_CALLER_SP_LOONGARCH64,
            genFramePointerReg(),
            unchecked(-(nint)callerStackOffset),
            REG_PROFILER_LEAVE_ARG_CALLER_SP_LOONGARCH64);

        GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_PROFILER_LEAVE_ARG_CALLER_SP_LOONGARCH64));
        genEmitHelperCallLoongArch64(helper, 0, EA_UNKNOWN);
    }
}
