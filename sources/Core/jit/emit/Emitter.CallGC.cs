// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private regMaskTP RBM_ALLINT
    {
        get
        {
            assert(_compiler is not null);
            return new regMaskTP(_compiler.SRBM_ALLINT);
        }
    }

    private static regMaskTP RBM_CALLEE_SAVED
        => new(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED, SRBM_MSK_CALLEE_SAVED);

    private regMaskTP CallScratchRegisters
    {
        get
        {
            assert(_compiler is not null);
            return new regMaskTP(
                _compiler.SRBM_INT_CALLEE_TRASH | _compiler.SRBM_FLT_CALLEE_TRASH, _compiler.SRBM_MSK_CALLEE_TRASH);
        }
    }

    private regMaskTP RBM_CALLEE_TRASH => CallScratchRegisters;

    private regMaskTP RBM_INTERFACELOOKUP_FOR_SLOT_TRASH
        => RBM_CALLEE_TRASH & ~new regMaskTP(SRBM_ARG_REGS | SRBM_FLTARG_REGS);

    private static regMaskTP RBM_CALLEE_GCTRASH_WRITEBARRIER => new(SRBM_INT_CALLEE_TRASH_INIT);

#if UNIX_AMD64_ABI
    private regMaskTP RBM_PROFILER_ENTER_TRASH
        => RBM_CALLEE_TRASH & ~new regMaskTP(SRBM_ARG_REGS | SRBM_FLTARG_REGS);
#else
    private regMaskTP RBM_PROFILER_ENTER_TRASH => RBM_CALLEE_TRASH;
#endif

#if UNIX_AMD64_ABI
    private regMaskTP RBM_PROFILER_LEAVE_TRASH
        => RBM_CALLEE_TRASH & ~new regMaskTP(SRBM_FLOATRET | SRBM_INTRET | SRBM_FLOATRET_1 | SRBM_INTRET_1);
#else
    private regMaskTP RBM_PROFILER_LEAVE_TRASH
        => RBM_CALLEE_TRASH & ~new regMaskTP(SRBM_FLOATRET | SRBM_INTRET);
#endif

    private regMaskTP RBM_PROFILER_TAILCALL_TRASH => RBM_PROFILER_LEAVE_TRASH;

    private regMaskTP RBM_VALIDATE_INDIRECT_CALL_TRASH
    {
        get
        {
            assert(_compiler is not null);
            return new regMaskTP(_compiler.SRBM_INT_CALLEE_TRASH & ~(SRBM_R10 | SRBM_RCX));
        }
    }

    private regMaskTP RBM_CALLEE_TRASH_NOGC => RBM_CALLEE_TRASH;
#elif TARGET_ARM64
    private static regMaskTP RBM_ALLINT => new(SRBM_ALLINT);
    private static regMaskTP RBM_CALLEE_SAVED => new(SRBM_CALLEE_SAVED);
    private static regMaskTP RBM_INTERFACELOOKUP_FOR_SLOT_TRASH => SRBM_INTERFACELOOKUP_FOR_SLOT_TRASH;
    private static regMaskTP RBM_CALLEE_GCTRASH_WRITEBARRIER => new(SRBM_CALLEE_GCTRASH_WRITEBARRIER);
    private static regMaskTP RBM_PROFILER_ENTER_TRASH
        => SRBM_CALLEE_TRASH & ~new regMaskTP(SRBM_ARG_REGS | SRBM_ARG_RET_BUFF | SRBM_FLTARG_REGS | SRBM_FP);
    private static regMaskTP RBM_PROFILER_LEAVE_TRASH => RBM_PROFILER_ENTER_TRASH;
    private static regMaskTP RBM_PROFILER_TAILCALL_TRASH => RBM_PROFILER_LEAVE_TRASH;
    private static regMaskTP RBM_VALIDATE_INDIRECT_CALL_TRASH => new(SRBM_VALIDATE_INDIRECT_CALL_TRASH);
    private static regMaskTP RBM_CALLEE_TRASH_NOGC => new(SRBM_CALLEE_TRASH_NOGC);
#else
    private static regMaskTP RBM_ALLINT => MissingNoGCCallMask(nameof(RBM_ALLINT));
    private static regMaskTP RBM_CALLEE_SAVED => MissingNoGCCallMask(nameof(RBM_CALLEE_SAVED));
    private static regMaskTP RBM_CALLEE_GCTRASH_WRITEBARRIER
        => MissingNoGCCallMask(nameof(RBM_CALLEE_GCTRASH_WRITEBARRIER));
    private static regMaskTP RBM_PROFILER_ENTER_TRASH => MissingNoGCCallMask(nameof(RBM_PROFILER_ENTER_TRASH));
    private static regMaskTP RBM_PROFILER_LEAVE_TRASH => MissingNoGCCallMask(nameof(RBM_PROFILER_LEAVE_TRASH));
    private static regMaskTP RBM_PROFILER_TAILCALL_TRASH => MissingNoGCCallMask(nameof(RBM_PROFILER_TAILCALL_TRASH));
    private static regMaskTP RBM_VALIDATE_INDIRECT_CALL_TRASH
        => MissingNoGCCallMask(nameof(RBM_VALIDATE_INDIRECT_CALL_TRASH));
    private static regMaskTP RBM_CALLEE_TRASH_NOGC => MissingNoGCCallMask(nameof(RBM_CALLEE_TRASH_NOGC));

    private static regMaskTP MissingNoGCCallMask(string mask)
        => throw new FatalJitException(CORJIT_SKIPPED, $"No-GC call register mask {mask} is not ported for this target.");
#endif

#if TARGET_ARM
    private static regMaskTP RBM_PROFILER_RET_SCRATCH
        => MissingNoGCCallMask(nameof(RBM_PROFILER_RET_SCRATCH));
#endif

#if TARGET_X86
    private static regMaskTP RBM_INIT_PINVOKE_FRAME_TRASH
        => MissingNoGCCallMask(nameof(RBM_INIT_PINVOKE_FRAME_TRASH));
#endif

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
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("NoGC Call: savedSet=");
                printRegMaskInt(savedSet);
                emitDispRegSet(savedSet);
                jitprintf("\n");
            }
#endif
            return savedSet;
        }

#if TARGET_AMD64 || TARGET_ARM64
        else if (helper == CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT)
        {
            return RBM_ALLINT & ~RBM_INTERFACELOOKUP_FOR_SLOT_TRASH;
        }
#endif
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

#if !TARGET_LOONGARCH64 && !TARGET_RISCV64
            case CORINFO_HELP_PROF_FCN_ENTER:
            {
                killMask = RBM_PROFILER_ENTER_TRASH;
                break;
            }

            case CORINFO_HELP_PROF_FCN_LEAVE:
            {
                killMask = RBM_PROFILER_LEAVE_TRASH;
#if TARGET_ARM
                killMask &= ~RBM_PROFILER_RET_SCRATCH;
#endif
                break;
            }

            case CORINFO_HELP_PROF_FCN_TAILCALL:
            {
                killMask = RBM_PROFILER_TAILCALL_TRASH;
                break;
            }
#endif

#if TARGET_X86
            case CORINFO_HELP_INIT_PINVOKE_FRAME:
            {
                killMask = RBM_INIT_PINVOKE_FRAME_TRASH;
                break;
            }
#endif

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
