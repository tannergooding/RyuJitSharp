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
#if DEBUG
        string? reasonText = null;
#endif

#if ETW_EBP_FRAMED
        if (!result && opts.OptimizationDisabled())
        {
#if DEBUG
            reasonText = "Debug Code";
#endif
            result = true;
        }
        if (!result && (info.compILCodeSize > DEFAULT_MAX_INLINE_SIZE))
        {
#if DEBUG
            reasonText = "IL Code Size";
#endif
            result = true;
        }
        if (!result && (fgBBcount > 3))
        {
#if DEBUG
            reasonText = "BasicBlock Count";
#endif
            result = true;
        }
        if (!result && fgHasLoops)
        {
#if DEBUG
            reasonText = "Method has Loops";
#endif
            result = true;
        }
        if (!result && (optCallCount >= optFastTailCallCount + 2))
        {
#if DEBUG
            reasonText = "Call Count";
#endif
            result = true;
        }
        if (!result && (optIndirectCallCount >= optIndirectFastTailCallCount + 1))
        {
#if DEBUG
            reasonText = "Indirect Call";
#endif
            result = true;
        }
#endif

        // The VM identifies an InlinedCallFrame's containing frame through the frame register.
        if (!result && (optNativeCallCount != 0))
        {
#if DEBUG
            reasonText = "Uses PInvoke";
#endif
            result = true;
        }

#if TARGET_ARM64
        if (!result)
        {
#if DEBUG
            reasonText = "Temporary ARM64 force frame pointer";
#endif
            result = true;
        }
#endif

#if TARGET_LOONGARCH64
        if (!result)
        {
#if DEBUG
            reasonText = "Temporary LOONGARCH64 force frame pointer";
#endif
            result = true;
        }
#endif

#if TARGET_RISCV64
        if (!result)
        {
#if DEBUG
            reasonText = "Temporary RISCV64 force frame pointer";
#endif
            result = true;
        }
#endif

#if DEBUG
        reason = result ? reasonText : null;
#else
        reason = null;
#endif
        return result;
    }
}
