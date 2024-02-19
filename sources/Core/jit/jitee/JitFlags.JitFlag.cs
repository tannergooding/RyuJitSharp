// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace RyuJitSharp;

public partial struct JitFlags
{
    public enum JitFlag
    {
        /// <summary>Optimize for speed.</summary>
        JIT_FLAG_SPEED_OPT = 0,

        /// <summary>Optimize for code size.</summary>
        JIT_FLAG_SIZE_OPT = 1,

        /// <summary>Generate "debuggable" code (no code-mangling optimizations).</summary>
        JIT_FLAG_DEBUG_CODE = 2,

        /// <summary>We are in Edit-n-Continue mode.</summary>
        JIT_FLAG_DEBUG_EnC = 3,

        /// <summary>Generate line and local-var info.</summary>
        JIT_FLAG_DEBUG_INFO = 4,

        /// <summary>Disable all jit optimizations (not necessarily debuggable code).</summary>
        JIT_FLAG_MIN_OPT = 5,

        /// <summary>Generate CFG enabled code.</summary>
        JIT_FLAG_ENABLE_CFG = 6,

        /// <summary>Generate alternate version for On Stack Replacement.</summary>
        JIT_FLAG_OSR = 7,

        /// <summary>JIT should consider itself an ALT_JIT.</summary>
        JIT_FLAG_ALT_JIT = 8,

        /// <summary>JIT is allowed to use *_MAYBEFROZEN allocators.</summary>
        JIT_FLAG_FROZEN_ALLOC_ALLOWED = 9,

        /// <summary>Use the final code generator, i.e., not the interpreter.</summary>
        JIT_FLAG_MAKEFINALCODE = 10,

        /// <summary>Use version-resilient code generation.</summary>
        JIT_FLAG_READYTORUN = 11,

        /// <summary>Instrument prologues/epilogues.</summary>
        JIT_FLAG_PROF_ENTERLEAVE = 12,

        /// <summary>Disables PInvoke inlining.</summary>
        JIT_FLAG_PROF_NO_PINVOKE_INLINE = 13,

        /// <summary>Pre-jit is the execution engine.</summary>
        JIT_FLAG_PREJIT = 14,

        /// <summary>Generate relocatable code.</summary>
        JIT_FLAG_RELOC = 15,

        /// <summary>Method is an IL stub.</summary>
        JIT_FLAG_IL_STUB = 16,

        /// <summary>JIT should separate code into hot and cold sections.</summary>
        JIT_FLAG_PROCSPLIT = 17,

        /// <summary>Collect basic block profile information.</summary>
        JIT_FLAG_BBINSTR = 18,

        /// <summary>JIT must instrument current method if it has loops.</summary>
        JIT_FLAG_BBINSTR_IF_LOOPS = 19,

        /// <summary>Optimize method based on profile information.</summary>
        JIT_FLAG_BBOPT = 20,

        /// <summary>All methods have an EBP frame.</summary>
        JIT_FLAG_FRAMED = 21,

        /// <summary>JIT must place stub secret param into local 0.</summary>
        /// <remarks>Used by IL stubs.</remarks>
        JIT_FLAG_PUBLISH_SECRET_PARAM = 22,

        /// <summary>The JIT should use the PINVOKE_{BEGIN,END} helpers instead of emitting inline transitions.</summary>
        JIT_FLAG_USE_PINVOKE_HELPERS = 23,

        /// <summary>The JIT should insert REVERSE_PINVOKE_{ENTER,EXIT} helpers into method prolog/epilog.</summary>
        JIT_FLAG_REVERSE_PINVOKE = 24,

        /// <summary>The JIT should insert the helper variants that track transitions.</summary>
        JIT_FLAG_TRACK_TRANSITIONS = 25,

        /// <summary>This is the initial tier for tiered compilation which should generate code as quickly as possible.</summary>
        JIT_FLAG_TIER0 = 26,

        /// <summary>This is the final tier (for now) for tiered compilation which should generate high quality code.</summary>
        JIT_FLAG_TIER1 = 27,

        /// <summary>JIT should not inline any called method into this method.</summary>
        JIT_FLAG_NO_INLINING = 28,

        //
        // TARGET_ARM32
        //

        /// <summary>JIT should generate PC-relative address computations instead of EE relocation records.</summary>
        JIT_FLAG_ARM32_RELATIVE_CODE_RELOCS = 29,

        /// <summary>Enable armel calling convention.</summary>
        JIT_FLAG_ARM32_SOFTFP_ABI = 30,

        //
        // TARGET_XARCH
        //

        /// <summary>On Xarch, 512-bit vector usage may incur CPU frequency throttling.</summary>
        JIT_FLAG_XARCH_VECTOR512_THROTTLING = 31,

        // Note: the mcs tool uses the currently unused upper flags bits when outputting SuperPMI MC file flags.
        // See EXTRA_JIT_FLAGS and spmidumphelper.cpp. Currently, these are bits 56 through 63. If they overlap,
        // something needs to change.
    }
}
