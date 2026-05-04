// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public interface ICodeGen
{
    Compiler Compiler { get; }

#if LATE_DISASM
    Disassembler Disassembler { get; }
#endif

    Emitter Emitter { get; }

    /// <summary>true if we've determined that the current method is to be fully interruptible.</summary>
    bool Interruptible { get; set; }

    /// <summary>Indicates whether the current method requires an explicit stack frame, and all arguments and locals to be accessible relative to the Frame Pointer.</summary>
    /// <remarks>Prohibits double alignment of the stack.</remarks>
    bool IsFramePointerRequired { get; set; }

    /// <summary>Indicates whether the current method sets up an explicit stack frame or not.</summary>
    bool IsFramePointerUsed { get; set; }

    /// <summary>Indicates whether the current method requires an explicit frame.</summary>
    /// <remarks>Does not prohibit double alignment of the stack.</remarks>
    bool IsFrameRequired { get; set; }

    //  The following will be set to true if we've determined that we need to
    //  generate a full-blown pointer register map for the current method.
    //  Currently it is equal to (GetInterruptible() || !isFramePointerUsed())
    //  (i.e. We generate the full-blown map for EBP-less methods and
    //        for fully interruptible methods)
    bool IsFullPtrRegMapRequired { get; set; }

#if TARGET_AMD64
    regMaskFlt RBM_ALLFLOAT { get; }

    regMaskInt RBM_ALLINT { get; }

    regMaskFlt RBM_FLT_CALLEE_TRASH { get; }

    regMaskInt RBM_INT_CALLEE_TRASH { get; }

    regNumber REG_INT_LAST { get; }
#endif

#if TARGET_XARCH
    regMaskMsk RBM_ALLMASK { get; }

    regMaskMsk RBM_MSK_CALLEE_TRASH { get; }
#endif

    /// <summary>indicates whether to align loops.</summary>
    /// <remarks>Used to avoid effects of loop alignment when diagnosing perf issues.</remarks>
    bool ShouldAlignLoops { get; set; }

#if DEBUG
    /// <summary>used to make sure the value of 'GetInterruptible()' isn't changed after it's been used by any logic that depends on its value.</summary>
    bool IsGcTypeFixed { get; }

    bool Verbose { get; set; }
#endif

#if TARGET_XARCH
    /// <summary>Call this function after the equivalent fields in Compiler have been initialized.</summary>
    void CopyRegisterInfo();
#endif

    unsafe void genGenerateCode(out void* codePtr, out uint nativeSizeOfCode);
}
