// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public partial class Compiler
{
    [Conditional("DEBUG")]
    public void dumpRegMask(SingleTypeRegSet regs, var_types type)
    {
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS && varTypeIsMask(type))
        {
            dumpRegMask(Configuration.HAS_MORE_THAN_64_REGISTERS
                ? new regMaskTP(SRBM_NONE, regs) : new regMaskTP(regs));
            return;
        }

        assert(varTypeUsesIntReg(type) || varTypeUsesFloatReg(type));
        dumpRegMask(new regMaskTP(regs));
    }

    [Conditional("DEBUG")]
    public void dumpRegMask(regMaskTP regs)
    {
        if (!Configuration.TARGET_AMD64)
        {
            NYI("Compiler.dumpRegMask outside AMD64");
            fatal(CORJIT_IMPLLIMITATION);
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.CompilerDumpRegMaskOutsideAMD64);
            return;
        }

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
        else if (Configuration.FEATURE_MASKED_HW_INTRINSICS && (regs == new regMaskTP(SRBM_NONE, SRBM_ALLMASK)))
        {
            jitprintf("[allMask]");
        }
        else
        {
            dspRegMask(regs);
        }
    }
}
