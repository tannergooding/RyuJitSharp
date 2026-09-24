// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public void dumpRegMask(SingleTypeRegSet regs, var_types type)
    {
#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeIsMask(type))
        {
#if HAS_MORE_THAN_64_REGISTERS
            dumpRegMask(new regMaskTP(SRBM_NONE, regs));
#else
            dumpRegMask(new regMaskTP(regs));
#endif
            return;
        }
#endif

        assert(varTypeUsesIntReg(type) || varTypeUsesFloatReg(type));
        dumpRegMask(new regMaskTP(regs));
    }

    public void dumpRegMask(regMaskTP regs)
    {
#if TARGET_AMD64
        // AMD64 aliases RBM_ALLDOUBLE to the current RBM_ALLFLOAT set.
        var allDoubleRegs = new regMaskTP(SRBM_ALLFLOAT);
        if (regs == new regMaskTP(SRBM_ALLINT))
        {
            jitprintf("[allInt]");
        }
        else if (regs == new regMaskTP(SRBM_ALLINT & ~SRBM_FPBASE))
        {
            jitprintf("[allIntButFP]");
        }
        else if (regs == new regMaskTP(SRBM_ALLFLOAT))
        {
            jitprintf("[allFloat]");
        }
        else if (regs == allDoubleRegs)
        {
            jitprintf("[allDouble]");
        }
#if FEATURE_MASKED_HW_INTRINSICS
        else if (regs == new regMaskTP(SRBM_NONE, SRBM_ALLMASK))
        {
            jitprintf("[allMask]");
        }
#endif
        else
        {
            dspRegMask(regs);
        }
#else
        NYI("Compiler.dumpRegMask outside AMD64");
        fatal(CORJIT_IMPLLIMITATION);
#endif
    }
#endif
}
