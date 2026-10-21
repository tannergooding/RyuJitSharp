// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private regMaskTP RBM_ALLINT
    {
        get
        {
            assert(_compiler is not null);

            if (Configuration.TARGET_AMD64)
            {
                return new regMaskTP(_compiler.SRBM_ALLINT);
            }

            if (Configuration.TARGET_ARM64)
            {
                return new regMaskTP(SRBM_ALLINT_ARM64);
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_ALLINT_ARM);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_ALLINT_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_ALLINT));
        }
    }

    private regMaskTP RBM_CALLEE_SAVED
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                var intRegisters = SRBM_X64_EBX | SRBM_X64_R12 | SRBM_X64_R13 | SRBM_X64_R14 | SRBM_X64_R15;
                if (!Configuration.UNIX_AMD64_ABI)
                {
                    intRegisters |= SRBM_X64_ESI | SRBM_X64_EDI;
                }

                intRegisters |= SRBM_ETW_FRAMED_EBP;

                var floatRegisters = SRBM_NONE;
                if (!Configuration.UNIX_AMD64_ABI)
                {
                    floatRegisters = SRBM_X64_XMM6 | SRBM_X64_XMM7 | SRBM_X64_XMM8 | SRBM_X64_XMM9 |
                        SRBM_X64_XMM10 | SRBM_X64_XMM11 | SRBM_X64_XMM12 | SRBM_X64_XMM13 |
                        SRBM_X64_XMM14 | SRBM_X64_XMM15;
                }

                return new regMaskTP(intRegisters | floatRegisters, SRBM_NONE);
            }

            if (Configuration.TARGET_ARM64)
            {
                return new regMaskTP(SRBM_CALLEE_SAVED_ARM64);
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_CALLEE_SAVED_ARM);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_CALLEE_SAVED_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_CALLEE_SAVED));
        }
    }

    private regMaskTP RBM_CALLEE_TRASH
    {
        get
        {
            if (Configuration.TARGET_XARCH)
            {
                return CallScratchRegisters;
            }

            if (Configuration.TARGET_ARM64)
            {
                return SRBM_CALLEE_TRASH_ARM64;
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_INT_CALLEE_TRASH_ARM | SRBM_FLT_CALLEE_TRASH_ARM);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_CALLEE_TRASH_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_CALLEE_TRASH));
        }
    }

    private regMaskTP RBM_INTERFACELOOKUP_FOR_SLOT_TRASH
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_CALLEE_TRASH & ~new regMaskTP(SRBM_ARG_REGS | SRBM_FLTARG_REGS);
            }

            if (Configuration.TARGET_ARM64)
            {
                return new regMaskTP(
                    (SRBM_INT_CALLEE_TRASH_ARM64 | SRBM_FLT_CALLEE_TRASH_ARM64) &
                    ~(SRBM_ARG_REGS_ARM64 | SRBM_FLTARG_REGS),
                    SRBM_MSK_CALLEE_TRASH_ARM64);
            }

            return MissingNoGCCallMask(nameof(RBM_INTERFACELOOKUP_FOR_SLOT_TRASH));
        }
    }

    private regMaskTP RBM_CALLEE_GCTRASH_WRITEBARRIER
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                var intRegisters = SRBM_X64_EAX | SRBM_X64_ECX | SRBM_X64_EDX |
                    SRBM_X64_R8 | SRBM_X64_R9 | SRBM_X64_R10 | SRBM_X64_R11;
                if (Configuration.UNIX_AMD64_ABI)
                {
                    intRegisters |= SRBM_X64_RDI | SRBM_X64_RSI;
                }

                return new regMaskTP(intRegisters);
            }

            if (Configuration.TARGET_ARM64)
            {
                return new regMaskTP(SRBM_CALLEE_GCTRASH_WRITEBARRIER_ARM64);
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_CALLEE_GCTRASH_WRITEBARRIER_ARM);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_CALLEE_GCTRASH_WRITEBARRIER_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_CALLEE_GCTRASH_WRITEBARRIER));
        }
    }

    private regMaskTP RBM_PROFILER_ENTER_TRASH
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return Configuration.UNIX_AMD64_ABI
                    ? RBM_CALLEE_TRASH & ~new regMaskTP(SRBM_ARG_REGS | SRBM_FLTARG_REGS)
                    : RBM_CALLEE_TRASH;
            }

            if (Configuration.TARGET_ARM64)
            {
                return SRBM_CALLEE_TRASH_ARM64 &
                    ~new regMaskTP(SRBM_ARG_REGS_ARM64 | SRBM_ARM64_R8 | SRBM_FLTARG_REGS | SRBM_ARM64_FP);
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_NONE);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_PROFILER_ENTER_TRASH_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_PROFILER_ENTER_TRASH));
        }
    }

    private regMaskTP RBM_PROFILER_LEAVE_TRASH
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                var returnRegisters = SRBM_FLOATRET_AMD64 | SRBM_INTRET_AMD64;
                if (Configuration.UNIX_AMD64_ABI)
                {
                    returnRegisters |= SRBM_FLOATRET_1_AMD64 | SRBM_INTRET_1_AMD64;
                }

                return RBM_CALLEE_TRASH & ~new regMaskTP(returnRegisters);
            }

            if (Configuration.TARGET_ARM64)
            {
                return RBM_PROFILER_ENTER_TRASH;
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_PROFILER_RET_SCRATCH);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_PROFILER_LEAVE_TRASH_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_PROFILER_LEAVE_TRASH));
        }
    }

    private regMaskTP RBM_PROFILER_TAILCALL_TRASH
    {
        get
        {
            if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
            {
                return RBM_PROFILER_LEAVE_TRASH;
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_NONE);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_PROFILER_TAILCALL_TRASH_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_PROFILER_TAILCALL_TRASH));
        }
    }

    private regMaskTP RBM_VALIDATE_INDIRECT_CALL_TRASH
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                assert(_compiler is not null);
                return new regMaskTP(_compiler.SRBM_INT_CALLEE_TRASH & ~(SRBM_X64_R10 | SRBM_X64_RCX));
            }

            if (Configuration.TARGET_ARM64)
            {
                return new regMaskTP(SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM64);
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_VALIDATE_INDIRECT_CALL_TRASH_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_VALIDATE_INDIRECT_CALL_TRASH));
        }
    }

    private regMaskTP RBM_CALLEE_TRASH_NOGC
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_CALLEE_TRASH;
            }

            if (Configuration.TARGET_ARM64)
            {
                return new regMaskTP(SRBM_CALLEE_TRASH_NOGC_ARM64);
            }

            if (Configuration.TARGET_ARM)
            {
                return new regMaskTP(SRBM_CALLEE_TRASH_NOGC_ARM);
            }

            if (Configuration.TARGET_RISCV64)
            {
                return new regMaskTP(SRBM_CALLEE_TRASH_NOGC_RISCV64);
            }

            return MissingNoGCCallMask(nameof(RBM_CALLEE_TRASH_NOGC));
        }
    }

    private static regMaskTP RBM_INIT_PINVOKE_FRAME_TRASH
        => MissingNoGCCallMask(nameof(RBM_INIT_PINVOKE_FRAME_TRASH));

    private static regMaskTP MissingNoGCCallMask(string mask)
    {
        FatalJitException.ThrowFatalJitException(
            CORJIT_SKIPPED, $"No-GC call register mask {mask} is not ported for this target.");
        return default!;
    }

    public static unsafe bool emitNoGChelper(CORINFO_METHOD_HANDLE methHnd)
    {
        var helper = Compiler.eeGetHelperNum(methHnd);
        if (helper == CORINFO_HELP_UNDEF)
        {
            return false;
        }

        return helper.IsNoGC;
    }

    public unsafe regMaskTP emitGetGCRegsSavedOrModified(CORINFO_METHOD_HANDLE methHnd)
    {
        assert(_compiler is not null);
        var isNoGCHelper = emitNoGChelper(methHnd);
        var helper = Compiler.eeGetHelperNum(methHnd);

        if (isNoGCHelper)
        {
            var savedSet = RBM_ALLINT & ~emitGetGCRegsKilledByNoGCCall(helper);
            if (Configuration.DEBUG && _compiler.verbose)
            {
                jitprintf("NoGC Call: savedSet=");
                printRegMaskInt(savedSet);
                emitDispRegSet(savedSet);
                jitprintf("\n");
            }
            return savedSet;
        }

        else if ((Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64) &&
                 (helper == CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT))
        {
            return RBM_ALLINT & ~RBM_INTERFACELOOKUP_FOR_SLOT_TRASH;
        }
        else
        {
            return RBM_CALLEE_SAVED;
        }
    }

    public unsafe regMaskTP emitGetGCRegsKilledByNoGCCall(CorInfoHelpFunc helper)
    {
        assert(emitNoGChelper(Compiler.eeFindHelper(helper)));
        regMaskTP killMask;

        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                killMask = RBM_CALLEE_GCTRASH_WRITEBARRIER;
                break;
            }

            case CORINFO_HELP_PROF_FCN_ENTER when !Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64:
            {
                killMask = RBM_PROFILER_ENTER_TRASH;
                break;
            }

            case CORINFO_HELP_PROF_FCN_LEAVE when !Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64:
            {
                killMask = RBM_PROFILER_LEAVE_TRASH;
                if (Configuration.TARGET_ARM)
                {
                    killMask &= ~new regMaskTP(SRBM_PROFILER_RET_SCRATCH);
                }

                break;
            }

            case CORINFO_HELP_PROF_FCN_TAILCALL when !Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64:
            {
                killMask = RBM_PROFILER_TAILCALL_TRASH;
                break;
            }

            case CORINFO_HELP_INIT_PINVOKE_FRAME when Configuration.TARGET_X86:
            {
                killMask = RBM_INIT_PINVOKE_FRAME_TRASH;
                break;
            }

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
            {
                killMask = RBM_VALIDATE_INDIRECT_CALL_TRASH;
                break;
            }

            default:
            {
                killMask = RBM_CALLEE_TRASH_NOGC;
                break;
            }
        }

        assert(_compiler is not null);
        assert((killMask & _compiler.compHelperCallKillSet(helper)) == killMask);
        return killMask;
    }
}
