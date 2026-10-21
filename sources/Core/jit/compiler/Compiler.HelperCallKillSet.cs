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

    public regMaskTP compHelperCallKillSet(CorInfoHelpFunc helper)
    {
        if (Configuration.TARGET_ARM64)
        {
            return compHelperCallKillSetArm64(helper);
        }

        if (Configuration.TARGET_AMD64)
        {
            return compHelperCallKillSetAmd64(helper);
        }

        if (Configuration.TARGET_X86 || Configuration.TARGET_ARM ||
            Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            return compHelperCallKillSetOther(helper);
        }

        if (Configuration.TARGET_WASM)
        {
            return RBM_CALLEE_TRASH;
        }

        FatalJitException.ThrowFatalJitException("Unsupported or unset target architecture.");
        return default!;
    }

    private regMaskTP compHelperCallKillSetArm64(CorInfoHelpFunc helper)
    {
        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                return new regMaskTP(SRBM_CALLEE_TRASH_WRITEBARRIER_ARM64);
            }

            case CORINFO_HELP_PROF_FCN_ENTER:
            case CORINFO_HELP_PROF_FCN_LEAVE:
            case CORINFO_HELP_PROF_FCN_TAILCALL:
            {
                return new regMaskTP(
                    (SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH) &
                    ~(SRBM_ARG_REGS | SRBM_ARG_RET_BUFF | SRBM_FLTARG_REGS | SRBM_ARM64_FP),
                    SRBM_MSK_CALLEE_TRASH);
            }

            case CORINFO_HELP_STOP_FOR_GC:
            {
                return SRBM_STOP_FOR_GC_TRASH_ARM64;
            }

            case CORINFO_HELP_INIT_PINVOKE_FRAME:
            {
                return SRBM_INIT_PINVOKE_FRAME_TRASH_ARM64;
            }

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
            {
                return new regMaskTP(SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM64);
            }

            case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
            {
                return SRBM_INTERFACELOOKUP_FOR_SLOT_TRASH;
            }

            default:
            {
                return SRBM_CALLEE_TRASH_ARM64;
            }
        }
    }
    private regMaskTP compHelperCallKillSetAmd64(CorInfoHelpFunc helper)
    {
        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                return new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT);
            }

            case CORINFO_HELP_PROF_FCN_ENTER:
            {
                return Configuration.UNIX_AMD64_ABI
                    ? removeRegistersFromCalleeTrash(SRBM_ARG_REGS, SRBM_FLTARG_REGS, SRBM_NONE)
                    : getCalleeTrashRegisterMask();
            }

            case CORINFO_HELP_PROF_FCN_LEAVE:
            case CORINFO_HELP_PROF_FCN_TAILCALL:
            {
                return Configuration.UNIX_AMD64_ABI
                    ? removeRegistersFromCalleeTrash(
                        SRBM_INTRET_AMD64 | SRBM_INTRET_1_AMD64,
                        SRBM_FLOATRET_AMD64 | SRBM_FLOATRET_1_AMD64, SRBM_NONE)
                    : removeRegistersFromCalleeTrash(SRBM_INTRET_AMD64, SRBM_FLOATRET_AMD64, SRBM_NONE);
            }

            case CORINFO_HELP_ASSIGN_REF_EAX when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_ECX when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_EBX when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_EBP when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_ESI when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_EDI when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EAX when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_ECX when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EBX when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EBP when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_ESI when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EDI when Configuration.TARGET_X86:
            {
                return new regMaskTP(LsraGlobals.genSingleTypeRegMask(REG_X86_EDX));
            }

            case CORINFO_HELP_STOP_FOR_GC:
            {
                return Configuration.UNIX_AMD64_ABI
                    ? removeRegistersFromCalleeTrash(
                        SRBM_INTRET_AMD64 | SRBM_INTRET_1_AMD64,
                        SRBM_FLOATRET_AMD64 | SRBM_FLOATRET_1_AMD64, SRBM_NONE)
                    : removeRegistersFromCalleeTrash(SRBM_INTRET_AMD64, SRBM_FLOATRET_AMD64, SRBM_NONE);
            }

            case CORINFO_HELP_INIT_PINVOKE_FRAME:
            {
                return getCalleeTrashRegisterMask();
            }

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
            {
                return removeRegistersFromCalleeTrash(SRBM_X64_R10 | SRBM_X64_RCX, SRBM_NONE, SRBM_NONE,
                    getIntegerCalleeTrashRegisterMask());
            }

            case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
            {
                return removeRegistersFromCalleeTrash(SRBM_ARG_REGS, SRBM_FLTARG_REGS, SRBM_NONE);
            }

            default:
            {
                return getCalleeTrashRegisterMask();
            }
        }
    }

    private regMaskTP getCalleeTrashRegisterMask()
    {
        return Configuration.HAS_MORE_THAN_64_REGISTERS
            ? new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH)
            : new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH | SRBM_MSK_CALLEE_TRASH);
    }

    private regMaskTP getIntegerCalleeTrashRegisterMask() => new regMaskTP(SRBM_INT_CALLEE_TRASH);

    private regMaskTP removeRegistersFromCalleeTrash(regMask intRegisters, regMask floatRegisters, regMask maskRegisters)
        => removeRegistersFromCalleeTrash(intRegisters, floatRegisters, maskRegisters, getCalleeTrashRegisterMask());

    private static regMaskTP removeRegistersFromCalleeTrash(
        regMask intRegisters, regMask floatRegisters, regMask maskRegisters, regMaskTP killMask)
    {
        return Configuration.HAS_MORE_THAN_64_REGISTERS
            ? new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters), killMask.Upper & ~maskRegisters)
            : new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters | maskRegisters));
    }

    private regMaskTP compHelperCallKillSetOther(CorInfoHelpFunc helper)
    {
        var calleeTrash = SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH;
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            calleeTrash |= SRBM_MSK_CALLEE_TRASH;
        }

        switch (helper)
        {
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                return Configuration.TARGET_X86 ? new regMaskTP(SRBM_X86_EAX | SRBM_X86_EDX) :
                    Configuration.TARGET_ARM
                        ? new regMaskTP(SRBM_ARM32_R0 | SRBM_ARM32_R3 | SRBM_ARM32_LR | SRBM_ARM32_R12)
                        : Configuration.TARGET_LOONGARCH64
                            ? new regMaskTP(SRBM_LOONGARCH64_T0 | SRBM_LOONGARCH64_T1 |
                                SRBM_LOONGARCH64_T3 | SRBM_LOONGARCH64_T4 | SRBM_LOONGARCH64_T7)
                            : new regMaskTP(SRBM_RISCV64_T0 | SRBM_RISCV64_T1 |
                                SRBM_RISCV64_T2 | SRBM_RISCV64_T4 | SRBM_RISCV64_T6);
            }

            case CORINFO_HELP_PROF_FCN_ENTER:
            case CORINFO_HELP_PROF_FCN_LEAVE:
            case CORINFO_HELP_PROF_FCN_TAILCALL:
            {
                if (Configuration.TARGET_X86)
                {
                    return helper == CORINFO_HELP_PROF_FCN_TAILCALL
                        ? new regMaskTP(calleeTrash & ~(SRBM_X86_ECX | SRBM_X86_EDX))
                        : RBM_NONE;
                }

                if (Configuration.TARGET_ARM)
                {
                    return helper == CORINFO_HELP_PROF_FCN_LEAVE ? new regMaskTP(SRBM_ARM32_R2) : RBM_NONE;
                }

                if (Configuration.TARGET_LOONGARCH64)
                {
                    return new regMaskTP(calleeTrash &
                        ~(SRBM_LOONGARCH64_A0 | SRBM_LOONGARCH64_A1 | SRBM_LOONGARCH64_A2 | SRBM_LOONGARCH64_A3 |
                          SRBM_LOONGARCH64_A4 | SRBM_LOONGARCH64_A5 | SRBM_LOONGARCH64_A6 | SRBM_LOONGARCH64_A7 |
                          SRBM_LOONGARCH64_F0 | SRBM_LOONGARCH64_F1 | SRBM_LOONGARCH64_F2 | SRBM_LOONGARCH64_F3 |
                          SRBM_LOONGARCH64_F4 | SRBM_LOONGARCH64_F5 | SRBM_LOONGARCH64_F6 | SRBM_LOONGARCH64_F7 |
                          SRBM_LOONGARCH64_FP));
                }

                return new regMaskTP(calleeTrash &
                    ~(SRBM_RISCV64_A0 | SRBM_RISCV64_A1 | SRBM_RISCV64_A2 | SRBM_RISCV64_A3 |
                      SRBM_RISCV64_A4 | SRBM_RISCV64_A5 | SRBM_RISCV64_A6 | SRBM_RISCV64_A7 |
                      SRBM_RISCV64_FA0 | SRBM_RISCV64_FA1 | SRBM_RISCV64_FA2 | SRBM_RISCV64_FA3 |
                      SRBM_RISCV64_FA4 | SRBM_RISCV64_FA5 | SRBM_RISCV64_FA6 | SRBM_RISCV64_FA7 |
                      SRBM_RISCV64_FP));
            }

            case CORINFO_HELP_ASSIGN_REF_EAX when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_ECX when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_EBX when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_EBP when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_ESI when Configuration.TARGET_X86:
            case CORINFO_HELP_ASSIGN_REF_EDI when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EAX when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_ECX when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EBX when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EBP when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_ESI when Configuration.TARGET_X86:
            case CORINFO_HELP_CHECKED_ASSIGN_REF_EDI when Configuration.TARGET_X86:
            {
                return new regMaskTP(SRBM_X86_EDX);
            }

            case CORINFO_HELP_STOP_FOR_GC:
            {
                return Configuration.TARGET_ARM
                    ? new regMaskTP(calleeTrash &
                        ~(SRBM_ARM32_R0 | SRBM_ARM32_R1 | SRBM_ARM32_R7 | SRBM_ARM32_R8 | SRBM_ARM32_R11 |
                          SRBM_ARM32_F0 | SRBM_ARM32_F1 | SRBM_ARM32_F2 | SRBM_ARM32_F3 |
                          SRBM_ARM32_F4 | SRBM_ARM32_F5 | SRBM_ARM32_F6 | SRBM_ARM32_F7))
                    : new regMaskTP(calleeTrash);
            }

            case CORINFO_HELP_INIT_PINVOKE_FRAME:
            {
                return Configuration.TARGET_X86 ? new regMaskTP(SRBM_X86_EAX | SRBM_X86_ESI) :
                    Configuration.TARGET_ARM ? new regMaskTP(calleeTrash | SRBM_ARM32_R5 | SRBM_ARM32_R6) :
                    new regMaskTP(calleeTrash);
            }

            case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
            {
                if (Configuration.TARGET_X86)
                {
                    return new regMaskTP(SRBM_INT_CALLEE_TRASH & ~SRBM_X86_ECX);
                }

                if (Configuration.TARGET_ARM)
                {
                    return new regMaskTP(SRBM_INT_CALLEE_TRASH);
                }

                return new regMaskTP(SRBM_INT_CALLEE_TRASH &
                    ~(Configuration.TARGET_LOONGARCH64
                        ? SRBM_LOONGARCH64_A0 | SRBM_LOONGARCH64_A1 | SRBM_LOONGARCH64_A2 | SRBM_LOONGARCH64_A3 |
                            SRBM_LOONGARCH64_A4 | SRBM_LOONGARCH64_A5 | SRBM_LOONGARCH64_A6 | SRBM_LOONGARCH64_A7 |
                            SRBM_LOONGARCH64_T3
                        : SRBM_RISCV64_A0 | SRBM_RISCV64_A1 | SRBM_RISCV64_A2 | SRBM_RISCV64_A3 |
                            SRBM_RISCV64_A4 | SRBM_RISCV64_A5 | SRBM_RISCV64_A6 | SRBM_RISCV64_A7 |
                            SRBM_RISCV64_T3));
            }

            default:
            {
                return new regMaskTP(calleeTrash);
            }
        }
    }
}
