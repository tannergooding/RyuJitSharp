// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal bool rpMustCreateEBPFrame(out string? reason)
    {
        var result = false;
        string? reasonText = null;

        if (Configuration.ETW_EBP_FRAMED && !result && opts.OptimizationDisabled)
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Debug Code";
            }

            result = true;
        }
        if (Configuration.ETW_EBP_FRAMED && !result && (info.compILCodeSize > DEFAULT_MAX_INLINE_SIZE))
        {
            if (Configuration.DEBUG)
            {
                reasonText = "IL Code Size";
            }

            result = true;
        }
        if (Configuration.ETW_EBP_FRAMED && !result && (fgBBcount > 3))
        {
            if (Configuration.DEBUG)
            {
                reasonText = "BasicBlock Count";
            }

            result = true;
        }
        if (Configuration.ETW_EBP_FRAMED && !result && fgHasLoops)
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Method has Loops";
            }

            result = true;
        }
        if (Configuration.ETW_EBP_FRAMED && !result && (optCallCount >= optFastTailCallCount + 2))
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Call Count";
            }

            result = true;
        }
        if (Configuration.ETW_EBP_FRAMED && !result && (optIndirectCallCount >= optIndirectFastTailCallCount + 1))
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Indirect Call";
            }

            result = true;
        }

        // The VM identifies an InlinedCallFrame's containing frame through the frame register.
        if (!result && (optNativeCallCount != 0))
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Uses PInvoke";
            }

            result = true;
        }

        if (Configuration.TARGET_ARM64 && !result)
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Temporary ARM64 force frame pointer";
            }

            result = true;
        }

        if (Configuration.TARGET_LOONGARCH64 && !result)
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Temporary LOONGARCH64 force frame pointer";
            }

            result = true;
        }

        if (Configuration.TARGET_RISCV64 && !result)
        {
            if (Configuration.DEBUG)
            {
                reasonText = "Temporary RISCV64 force frame pointer";
            }

            result = true;
        }

        reason = reasonText;
        return result;
    }
}
