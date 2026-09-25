// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    internal bool killGCRefs(GenTree tree)
    {
        if (tree.Oper.IsCall)
        {
            var call = tree.AsCall();
            if (call.IsUnmanaged)
            {
                return true;
            }

            if (call.IsHelperCall(CORINFO_HELP_JIT_PINVOKE_BEGIN))
            {
                assert(opts.ShouldUsePInvokeHelpers);
                return true;
            }
        }
        else if (tree.Oper is GT_START_PREEMPTGC)
        {
            return true;
        }

        return false;
    }

#if TARGET_AMD64
    public regMaskTP compHelperCallKillSet(CorInfoHelpFunc helper)
    {
        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
                return new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT);

            case CORINFO_HELP_PROF_FCN_ENTER:
#if UNIX_AMD64_ABI
                return removeRegistersFromCalleeTrash(SRBM_ARG_REGS, SRBM_FLTARG_REGS, SRBM_NONE);
#else
                return getCalleeTrashRegisterMask();
#endif

            case CORINFO_HELP_PROF_FCN_LEAVE:
            case CORINFO_HELP_PROF_FCN_TAILCALL:
#if UNIX_AMD64_ABI
                return removeRegistersFromCalleeTrash(
                    SRBM_INTRET | SRBM_INTRET_1, SRBM_FLOATRET | SRBM_FLOATRET_1, SRBM_NONE);
#else
                return removeRegistersFromCalleeTrash(SRBM_INTRET, SRBM_FLOATRET, SRBM_NONE);
#endif

#if TARGET_X86
            case CORINFO_HELP_ASSIGN_REF_EAX:
            case CORINFO_HELP_ASSIGN_REF_ECX:
            case CORINFO_HELP_ASSIGN_REF_EBX:
            case CORINFO_HELP_ASSIGN_REF_EBP:
            case CORINFO_HELP_ASSIGN_REF_ESI:
            case CORINFO_HELP_ASSIGN_REF_EDI:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EAX:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_ECX:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EBX:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EBP:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_ESI:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EDI:
                return new regMaskTP(LsraGlobals.genSingleTypeRegMask(REG_EDX));
#endif

            case CORINFO_HELP_STOP_FOR_GC:
#if UNIX_AMD64_ABI
                return removeRegistersFromCalleeTrash(
                    SRBM_INTRET | SRBM_INTRET_1, SRBM_FLOATRET | SRBM_FLOATRET_1, SRBM_NONE);
#else
                return removeRegistersFromCalleeTrash(SRBM_INTRET, SRBM_FLOATRET, SRBM_NONE);
#endif

            case CORINFO_HELP_INIT_PINVOKE_FRAME:
                return getCalleeTrashRegisterMask();

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
                return removeRegistersFromCalleeTrash(SRBM_R10 | SRBM_RCX, SRBM_NONE, SRBM_NONE,
                    getIntegerCalleeTrashRegisterMask());

            case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
                return removeRegistersFromCalleeTrash(SRBM_ARG_REGS, SRBM_FLTARG_REGS, SRBM_NONE);

            default:
                return getCalleeTrashRegisterMask();
        }
    }

    private regMaskTP getCalleeTrashRegisterMask()
    {
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH);
#else
        return new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH | SRBM_MSK_CALLEE_TRASH);
#endif
    }

    private regMaskTP getIntegerCalleeTrashRegisterMask() => new regMaskTP(SRBM_INT_CALLEE_TRASH);

    private regMaskTP removeRegistersFromCalleeTrash(regMask intRegisters, regMask floatRegisters, regMask maskRegisters)
        => removeRegistersFromCalleeTrash(intRegisters, floatRegisters, maskRegisters, getCalleeTrashRegisterMask());

    private static regMaskTP removeRegistersFromCalleeTrash(
        regMask intRegisters, regMask floatRegisters, regMask maskRegisters, regMaskTP killMask)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters), killMask.Upper & ~maskRegisters);
#else
        return new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters | maskRegisters));
#endif
    }
#endif
}
