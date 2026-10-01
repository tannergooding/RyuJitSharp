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

#if TARGET_ARM64
    public regMaskTP compHelperCallKillSet(CorInfoHelpFunc helper)
    {
        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                return new regMaskTP(SRBM_CALLEE_TRASH_WRITEBARRIER);
            }

            case CORINFO_HELP_PROF_FCN_ENTER:
            case CORINFO_HELP_PROF_FCN_LEAVE:
            case CORINFO_HELP_PROF_FCN_TAILCALL:
            {
                return new regMaskTP(
                    (SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH) &
                    ~(SRBM_ARG_REGS | SRBM_ARG_RET_BUFF | SRBM_FLTARG_REGS | SRBM_FP),
                    SRBM_MSK_CALLEE_TRASH);
            }

            case CORINFO_HELP_STOP_FOR_GC:
            {
                return SRBM_STOP_FOR_GC_TRASH;
            }

            case CORINFO_HELP_INIT_PINVOKE_FRAME:
            {
                return SRBM_INIT_PINVOKE_FRAME_TRASH;
            }

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
            {
                return new regMaskTP(SRBM_VALIDATE_INDIRECT_CALL_TRASH);
            }

            case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
            {
                return SRBM_INTERFACELOOKUP_FOR_SLOT_TRASH;
            }

            default:
            {
                return SRBM_CALLEE_TRASH;
            }
        }
    }
#elif TARGET_AMD64
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
#elif TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
    public regMaskTP compHelperCallKillSet(CorInfoHelpFunc helper)
    {
        var calleeTrash = SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH;
#if FEATURE_MASKED_HW_INTRINSICS
        calleeTrash |= SRBM_MSK_CALLEE_TRASH;
#endif

        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
#if TARGET_X86
#if FEATURE_USE_ASM_GC_WRITE_BARRIERS
                return new regMaskTP(SRBM_EAX | SRBM_EDX);
#else
                return new regMaskTP(calleeTrash);
#endif
#elif TARGET_ARM
                return new regMaskTP(SRBM_R0 | SRBM_R3 | SRBM_LR | SRBM_R12);
#elif TARGET_LOONGARCH64
                return new regMaskTP(SRBM_T0 | SRBM_T1 | SRBM_T3 | SRBM_T4 | SRBM_T7);
#else
                return new regMaskTP(SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T6);
#endif
            }

            case CORINFO_HELP_PROF_FCN_ENTER:
            case CORINFO_HELP_PROF_FCN_LEAVE:
            case CORINFO_HELP_PROF_FCN_TAILCALL:
            {
#if TARGET_X86
                return helper == CORINFO_HELP_PROF_FCN_TAILCALL
                    ? new regMaskTP(calleeTrash & ~(SRBM_ECX | SRBM_EDX))
                    : RBM_NONE;
#elif TARGET_ARM
                return helper == CORINFO_HELP_PROF_FCN_LEAVE ? new regMaskTP(SRBM_R2) : RBM_NONE;
#elif TARGET_LOONGARCH64
                return new regMaskTP(calleeTrash &
                    ~(SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7 |
                      SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 | SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7 | SRBM_FP));
#else
                return new regMaskTP(calleeTrash &
                    ~(SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7 |
                      SRBM_FA0 | SRBM_FA1 | SRBM_FA2 | SRBM_FA3 | SRBM_FA4 | SRBM_FA5 | SRBM_FA6 | SRBM_FA7 |
                      SRBM_FP));
#endif
            }

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
            {
                return new regMaskTP(SRBM_EDX);
            }
#endif

            case CORINFO_HELP_STOP_FOR_GC:
            {
#if TARGET_ARM
                return new regMaskTP(calleeTrash &
                    ~(SRBM_R0 | SRBM_R1 | SRBM_R7 | SRBM_R8 | SRBM_R11 |
                      SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 | SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7));
#else
                return new regMaskTP(calleeTrash);
#endif
            }

            case CORINFO_HELP_INIT_PINVOKE_FRAME:
            {
#if TARGET_X86
                return new regMaskTP(SRBM_EAX | SRBM_ESI);
#elif TARGET_ARM
                return new regMaskTP(calleeTrash | SRBM_R5 | SRBM_R6);
#else
                return new regMaskTP(calleeTrash);
#endif
            }

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
            {
#if TARGET_X86
                return new regMaskTP(SRBM_INT_CALLEE_TRASH & ~SRBM_ECX);
#elif TARGET_ARM
                return new regMaskTP(SRBM_INT_CALLEE_TRASH);
#else
                return new regMaskTP(SRBM_INT_CALLEE_TRASH &
                    ~(SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7 | SRBM_T3));
#endif
            }

            default:
            {
                return new regMaskTP(calleeTrash);
            }
        }
    }
#endif
}
