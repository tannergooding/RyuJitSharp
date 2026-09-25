// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64 && !UNIX_AMD64_ABI
    private regMaskTP CallScratchRegisters
    {
        get
        {
            assert(_compiler is not null);
            return new regMaskTP(
                _compiler.SRBM_INT_CALLEE_TRASH | _compiler.SRBM_FLT_CALLEE_TRASH, _compiler.SRBM_MSK_CALLEE_TRASH);
        }
    }

    public static unsafe bool emitNoGChelper(CORINFO_METHOD_HANDLE methHnd)
    {
        var helper = Compiler.eeGetHelperNum(methHnd);
        assert(helper < CORINFO_HELP_COUNT);

        return helper.IsNoGC;
    }

    public unsafe regMaskTP emitGetGCRegsSavedOrModified(CORINFO_METHOD_HANDLE methHnd)
    {
        assert(_compiler is not null);
        if (emitNoGChelper(methHnd))
        {
            var helper = Compiler.eeGetHelperNum(methHnd);
            var savedSet = new regMaskTP(_compiler.SRBM_ALLINT) & ~emitGetGCRegsKilledByNoGCCall(helper);
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
        else if (Compiler.eeGetHelperNum(methHnd) == CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT)
        {
            var trash = CallScratchRegisters & ~new regMaskTP(SRBM_ARG_REGS | SRBM_FLTARG_REGS);
            return new regMaskTP(_compiler.SRBM_ALLINT) & ~trash;
        }
        else
        {
            return new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED);
        }
    }

    public unsafe regMaskTP emitGetGCRegsKilledByNoGCCall(CorInfoHelpFunc helper)
    {
        assert(_compiler is not null);
        assert(emitNoGChelper(Compiler.eeFindHelper(helper)));
        regMaskTP killMask;

        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                killMask = new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT);
                break;
            }

            case CORINFO_HELP_PROF_FCN_ENTER:
            {
                killMask = CallScratchRegisters;
                break;
            }

            case CORINFO_HELP_PROF_FCN_LEAVE:
            case CORINFO_HELP_PROF_FCN_TAILCALL:
            {
                killMask = CallScratchRegisters & ~new regMaskTP(SRBM_FLOATRET | SRBM_INTRET);
                break;
            }

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
            {
                killMask = new regMaskTP(_compiler.SRBM_INT_CALLEE_TRASH & ~(SRBM_R10 | SRBM_RCX));
                break;
            }

            default:
            {
                killMask = CallScratchRegisters;
                break;
            }
        }

        assert((killMask & _compiler.compHelperCallKillSet(helper)) == killMask);
        return killMask;
    }
#else
    public static unsafe bool emitNoGChelper(CORINFO_METHOD_HANDLE methHnd)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Call GC classification is implemented only for Windows AMD64.");
    }

    public unsafe regMaskTP emitGetGCRegsSavedOrModified(CORINFO_METHOD_HANDLE methHnd)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Call GC register preservation is implemented only for Windows AMD64.");
    }

    public regMaskTP emitGetGCRegsKilledByNoGCCall(CorInfoHelpFunc helper)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Call GC register killing is implemented only for Windows AMD64.");
    }
#endif
}
