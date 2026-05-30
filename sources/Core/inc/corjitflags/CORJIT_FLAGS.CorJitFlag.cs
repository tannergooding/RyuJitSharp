// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct CORJIT_FLAGS
{
    public const CorJitFlag CORJIT_FLAG_CALL_GETJITFLAGS = CorJitFlag.CORJIT_FLAG_CALL_GETJITFLAGS;
    public const CorJitFlag CORJIT_FLAG_SPEED_OPT = CorJitFlag.CORJIT_FLAG_SPEED_OPT;
    public const CorJitFlag CORJIT_FLAG_SIZE_OPT = CorJitFlag.CORJIT_FLAG_SIZE_OPT;
    public const CorJitFlag CORJIT_FLAG_DEBUG_CODE = CorJitFlag.CORJIT_FLAG_DEBUG_CODE;
    public const CorJitFlag CORJIT_FLAG_DEBUG_EnC = CorJitFlag.CORJIT_FLAG_DEBUG_EnC;
    public const CorJitFlag CORJIT_FLAG_DEBUG_INFO = CorJitFlag.CORJIT_FLAG_DEBUG_INFO;
    public const CorJitFlag CORJIT_FLAG_MIN_OPT = CorJitFlag.CORJIT_FLAG_MIN_OPT;
    public const CorJitFlag CORJIT_FLAG_ENABLE_CFG = CorJitFlag.CORJIT_FLAG_ENABLE_CFG;
    public const CorJitFlag CORJIT_FLAG_OSR = CorJitFlag.CORJIT_FLAG_OSR;
    public const CorJitFlag CORJIT_FLAG_ALT_JIT = CorJitFlag.CORJIT_FLAG_ALT_JIT;
    public const CorJitFlag CORJIT_FLAG_FROZEN_ALLOC_ALLOWED = CorJitFlag.CORJIT_FLAG_FROZEN_ALLOC_ALLOWED;
    public const CorJitFlag CORJIT_FLAG_PORTABLE_ENTRY_POINTS = CorJitFlag.CORJIT_FLAG_PORTABLE_ENTRY_POINTS;
    public const CorJitFlag CORJIT_FLAG_AOT = CorJitFlag.CORJIT_FLAG_AOT;
    public const CorJitFlag CORJIT_FLAG_PROF_ENTERLEAVE = CorJitFlag.CORJIT_FLAG_PROF_ENTERLEAVE;
    public const CorJitFlag CORJIT_FLAG_PROF_NO_PINVOKE_INLINE = CorJitFlag.CORJIT_FLAG_PROF_NO_PINVOKE_INLINE;
    public const CorJitFlag CORJIT_FLAG_ASYNC = CorJitFlag.CORJIT_FLAG_ASYNC;
    public const CorJitFlag CORJIT_FLAG_RELOC = CorJitFlag.CORJIT_FLAG_RELOC;
    public const CorJitFlag CORJIT_FLAG_IL_STUB = CorJitFlag.CORJIT_FLAG_IL_STUB;
    public const CorJitFlag CORJIT_FLAG_PROCSPLIT = CorJitFlag.CORJIT_FLAG_PROCSPLIT;
    public const CorJitFlag CORJIT_FLAG_BBINSTR = CorJitFlag.CORJIT_FLAG_BBINSTR;
    public const CorJitFlag CORJIT_FLAG_BBINSTR_IF_LOOPS = CorJitFlag.CORJIT_FLAG_BBINSTR_IF_LOOPS;
    public const CorJitFlag CORJIT_FLAG_BBOPT = CorJitFlag.CORJIT_FLAG_BBOPT;
    public const CorJitFlag CORJIT_FLAG_FRAMED = CorJitFlag.CORJIT_FLAG_FRAMED;
    public const CorJitFlag CORJIT_FLAG_PUBLISH_SECRET_PARAM = CorJitFlag.CORJIT_FLAG_PUBLISH_SECRET_PARAM;
    public const CorJitFlag CORJIT_FLAG_USE_PINVOKE_HELPERS = CorJitFlag.CORJIT_FLAG_USE_PINVOKE_HELPERS;
    public const CorJitFlag CORJIT_FLAG_REVERSE_PINVOKE = CorJitFlag.CORJIT_FLAG_REVERSE_PINVOKE;
    public const CorJitFlag CORJIT_FLAG_TRACK_TRANSITIONS = CorJitFlag.CORJIT_FLAG_TRACK_TRANSITIONS;
    public const CorJitFlag CORJIT_FLAG_TIER0 = CorJitFlag.CORJIT_FLAG_TIER0;
    public const CorJitFlag CORJIT_FLAG_TIER1 = CorJitFlag.CORJIT_FLAG_TIER1;
    public const CorJitFlag CORJIT_FLAG_NO_INLINING = CorJitFlag.CORJIT_FLAG_NO_INLINING;

#if TARGET_ARM
    public const CorJitFlag CORJIT_FLAG_RELATIVE_CODE_RELOCS = CorJitFlag.CORJIT_FLAG_RELATIVE_CODE_RELOCS;
    public const CorJitFlag CORJIT_FLAG_SOFTFP_ABI = CorJitFlag.CORJIT_FLAG_SOFTFP_ABI;
#endif

    public const CorJitFlag CORJIT_FLAG_USE_DISPATCH_HELPERS = CorJitFlag.CORJIT_FLAG_USE_DISPATCH_HELPERS;

    // No number should be re-used between different target conditions, so platform-independent code can know uniquely which number corresponds to which flag.
    public enum CorJitFlag
    {
        /// <summary>Indicates that the JIT should retrieve flags in the form of a pointer to a <see cref="CORJIT_FLAGS" /> value via <see cref="ICorJitInfo.getJitFlags" />.</summary>
        CORJIT_FLAG_CALL_GETJITFLAGS = -1,

        /// <summary>Optimize for speed.</summary>
        CORJIT_FLAG_SPEED_OPT = 0,

        /// <summary>Optimize for code size.</summary>
        CORJIT_FLAG_SIZE_OPT = 1,

        /// <summary>Generate "debuggable" code (no code-mangling optimizations).</summary>
        CORJIT_FLAG_DEBUG_CODE = 2,

        /// <summary>We are in Edit-n-Continue mode.</summary>
        CORJIT_FLAG_DEBUG_EnC = 3,

        /// <summary>Generate line and local-var info.</summary>
        CORJIT_FLAG_DEBUG_INFO = 4,

        /// <summary>Disable all jit optimizations (not necessarily debuggable code).</summary>
        CORJIT_FLAG_MIN_OPT = 5,

        /// <summary>Generate CFG enabled code.</summary>
        CORJIT_FLAG_ENABLE_CFG = 6,

        /// <summary>Generate alternate version for On Stack Replacement.</summary>
        CORJIT_FLAG_OSR = 7,

        /// <summary>JIT should consider itself an ALT_JIT.</summary>
        CORJIT_FLAG_ALT_JIT = 8,

        /// <summary>JIT is allowed to use *_MAYBEFROZEN allocators.</summary>
        CORJIT_FLAG_FROZEN_ALLOC_ALLOWED = 9,

        /// <summary>Use portable entrypoints for managed calling convention (see clr-abi.md for details)</summary>
        CORJIT_FLAG_PORTABLE_ENTRY_POINTS = 10,

        /// <summary>Do ahead-of-time code generation (ReadyToRun or NativeAOT)</summary>
        CORJIT_FLAG_AOT = 11,

        /// <summary>Instrument prologues/epilogues.</summary>
        CORJIT_FLAG_PROF_ENTERLEAVE = 12,

        /// <summary>Disables PInvoke inlining.</summary>
        CORJIT_FLAG_PROF_NO_PINVOKE_INLINE = 13,

        /// <summary>Generate code for use as an async function</summary>
        CORJIT_FLAG_ASYNC = 14,

        /// <summary>Generate relocatable code.</summary>
        CORJIT_FLAG_RELOC = 15,

        /// <summary>Method is an IL stub.</summary>
        CORJIT_FLAG_IL_STUB = 16,

        /// <summary>JIT should separate code into hot and cold sections.</summary>
        CORJIT_FLAG_PROCSPLIT = 17,

        /// <summary>Collect basic block profile information.</summary>
        CORJIT_FLAG_BBINSTR = 18,

        /// <summary>JIT must instrument current method if it has loops.</summary>
        CORJIT_FLAG_BBINSTR_IF_LOOPS = 19,

        /// <summary>Optimize method based on profile information.</summary>
        CORJIT_FLAG_BBOPT = 20,

        /// <summary>All methods have an EBP frame.</summary>
        CORJIT_FLAG_FRAMED = 21,

        /// <summary>JIT must place stub secret param into local 0.</summary>
        /// <remarks>Used by IL stubs.</remarks>
        CORJIT_FLAG_PUBLISH_SECRET_PARAM = 22,

        /// <summary>The JIT should use the PINVOKE_{BEGIN,END} helpers instead of emitting inline transitions.</summary>
        CORJIT_FLAG_USE_PINVOKE_HELPERS = 23,

        /// <summary>The JIT should insert REVERSE_PINVOKE_{ENTER,EXIT} helpers into method prolog/epilog.</summary>
        CORJIT_FLAG_REVERSE_PINVOKE = 24,

        /// <summary>The JIT should insert the helper variants that track transitions.</summary>
        CORJIT_FLAG_TRACK_TRANSITIONS = 25,

        /// <summary>This is the initial tier for tiered compilation which should generate code as quickly as possible.</summary>
        CORJIT_FLAG_TIER0 = 26,

        /// <summary>This is the final tier (for now) for tiered compilation which should generate high quality code.</summary>
        CORJIT_FLAG_TIER1 = 27,

        /// <summary>JIT should not inline any called method into this method.</summary>
        CORJIT_FLAG_NO_INLINING = 28,

#if TARGET_ARM
        /// <summary>JIT should generate PC-relative address computations instead of EE relocation records.</summary>
        CORJIT_FLAG_RELATIVE_CODE_RELOCS = 29,

        /// <summary>Enable armel calling convention.</summary>
        CORJIT_FLAG_SOFTFP_ABI = 30,
#endif

        /// <summary>The JIT should use helpers for interface dispatch instead of virtual stub dispatch</summary>
        CORJIT_FLAG_USE_DISPATCH_HELPERS = 31,
    }
}
