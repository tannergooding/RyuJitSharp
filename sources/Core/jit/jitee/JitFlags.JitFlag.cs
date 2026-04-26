// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace RyuJitSharp;

public partial struct JitFlags
{
    public enum JitFlag
    {
        /// <summary>optimize for speed</summary>
        JIT_FLAG_SPEED_OPT = 0,

        /// <summary>optimize for code size</summary>
        JIT_FLAG_SIZE_OPT = 1,

        /// <summary>generate "debuggable" code (no code-mangling optimizations)</summary>
        JIT_FLAG_DEBUG_CODE = 2,

        /// <summary>We are in Edit-n-Continue mode</summary>
        JIT_FLAG_DEBUG_EnC = 3,

        /// <summary>generate line and local-var info</summary>
        JIT_FLAG_DEBUG_INFO = 4,

        /// <summary>disable all jit optimizations (not necessarily debuggable code)</summary>
        JIT_FLAG_MIN_OPT = 5,

        /// <summary>generate CFG enabled code</summary>
        JIT_FLAG_ENABLE_CFG = 6,

        /// <summary>Generate alternate version for On Stack Replacement</summary>
        JIT_FLAG_OSR = 7,

        /// <summary>JIT should consider itself an ALT_JIT</summary>
        JIT_FLAG_ALT_JIT = 8,

        /// <summary>JIT is allowed to use *_MAYBEFROZEN allocators</summary>
        JIT_FLAG_FROZEN_ALLOC_ALLOWED = 9,

        /// <summary>Use portable entrypoints for managed calling convention (see clr-abi.md for details)</summary>
        JIT_FLAG_PORTABLE_ENTRY_POINTS = 10,

        /// <summary>Do ahead-of-time code generation (ReadyToRun or NativeAOT)</summary>
        JIT_FLAG_AOT = 11,

        /// <summary>Instrument prologues/epilogues</summary>
        JIT_FLAG_PROF_ENTERLEAVE = 12,

        /// <summary>Disables PInvoke inlining</summary>
        JIT_FLAG_PROF_NO_PINVOKE_INLINE = 13,

        /// <summary>Generate code for use as an async function</summary>
        JIT_FLAG_ASYNC = 14,

        /// <summary>Generate relocatable code</summary>
        JIT_FLAG_RELOC = 15,

        /// <summary>method is an IL stub</summary>
        JIT_FLAG_IL_STUB = 16,

        /// <summary>JIT should separate code into hot and cold sections</summary>
        JIT_FLAG_PROCSPLIT = 17,

        /// <summary>Collect basic block profile information</summary>
        JIT_FLAG_BBINSTR = 18,

        /// <summary>JIT must instrument current method if it has loops</summary>
        JIT_FLAG_BBINSTR_IF_LOOPS = 19,

        /// <summary>Optimize method based on profile information</summary>
        JIT_FLAG_BBOPT = 20,

        /// <summary>All methods have an EBP frame</summary>
        JIT_FLAG_FRAMED = 21,

        /// <summary>JIT must place stub secret param into local 0.  (used by IL stubs)</summary>
        JIT_FLAG_PUBLISH_SECRET_PARAM = 22,

        /// <summary>The JIT should use the PINVOKE_{BEGIN,END} helpers instead of emitting inline transitions</summary>
        JIT_FLAG_USE_PINVOKE_HELPERS = 23,

        /// <summary>The JIT should insert REVERSE_PINVOKE_{ENTER,EXIT} helpers into method prolog/epilog</summary>
        JIT_FLAG_REVERSE_PINVOKE = 24,

        /// <summary>The JIT should insert the helper variants that track transitions.</summary>
        JIT_FLAG_TRACK_TRANSITIONS = 25,

        /// <summary>This is the initial tier for tiered compilation which should generate code as quickly as possible</summary>
        JIT_FLAG_TIER0 = 26,

        /// <summary>This is the final tier (for now) for tiered compilation which should generate high quality code</summary>
        JIT_FLAG_TIER1 = 27,

        /// <summary>JIT should not inline any called method into this method</summary>
        JIT_FLAG_NO_INLINING = 28,

#if TARGET_ARM
        /// <summary>JIT should generate PC-relative address computations instead of EE relocation records</summary>
        JIT_FLAG_RELATIVE_CODE_RELOCS = 29,

        /// <summary>Enable armel calling convention</summary>
        JIT_FLAG_SOFTFP_ABI = 30,
#endif

        // Note: the mcs tool uses the currently unused upper flags bits when outputting SuperPMI MC file flags.
        // See EXTRA_JIT_FLAGS and spmidumphelper.cpp. Currently, these are bits 56 through 63. If they overlap,
        // something needs to change.
    }
}
