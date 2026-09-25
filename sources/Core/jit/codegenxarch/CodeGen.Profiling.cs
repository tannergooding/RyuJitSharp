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
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Profiler leave callbacks require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(helper is CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL);
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        _compiler.info.compProfilerCallback = true;
        noway_assert(_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 4 * REGSIZE_BYTES);
        if (_compiler.lvaKeepAliveAndReportThis() && _compiler.lvaGetDesc(_compiler.info.compThisArg).lvIsInReg)
        {
            var thisReg = _compiler.lvaGetDesc(_compiler.info.compThisArg).RegNum;
            var trash = Emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_LEAVE);
            noway_assert((trash & new regMaskTP(thisReg.SingleTypeMask)).IsEmpty);
        }

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

        genEmitHelperCall(helper, 0, EA_UNKNOWN, REG_ARG_2);
#endif
    }

    public regNumber genFramePointerReg() => IsFramePointerUsed ? REG_FPBASE : REG_SPBASE;
}
#endif
