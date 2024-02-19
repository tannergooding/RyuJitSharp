// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct CORJIT_FLAGS
{
    // No number should be re-used between different target conditions, so platform-independent code can know uniquely which number corresponds to which flag.
    public enum CorJitFlag
    {
        /// <summary>Indicates that the JIT should retrieve flags in the form of a pointer to a <see cref="CORJIT_FLAGS" /> value via <see cref="ICorJitInfo.getJitFlags" />.</summary>
        CORJIT_FLAG_CALL_GETJITFLAGS = unchecked((int)0xFFFF_FFFF),

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

        /// <summary>Use the final code generator, i.e., not the interpreter.</summary>
        CORJIT_FLAG_MAKEFINALCODE = 10,

        /// <summary>Use version-resilient code generation.</summary>
        CORJIT_FLAG_READYTORUN = 11,

        /// <summary>Instrument prologues/epilogues.</summary>
        CORJIT_FLAG_PROF_ENTERLEAVE = 12,

        /// <summary>Disables PInvoke inlining.</summary>
        CORJIT_FLAG_PROF_NO_PINVOKE_INLINE = 13,

        /// <summary>Pre-jit is the execution engine.</summary>
        CORJIT_FLAG_PREJIT = 14,

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

        //
        // TARGET_ARM
        //

        /// <summary>JIT should generate PC-relative address computations instead of EE relocation records.</summary>
        CORJIT_FLAG_ARM_RELATIVE_CODE_RELOCS = 29,

        /// <summary>Enable armel calling convention.</summary>
        CORJIT_FLAG_ARM_SOFTFP_ABI = 30,

        //
        // TARGET_XARCH
        //

        /// <summary>On x86/x64, 512-bit vector usage may incur CPU frequency throttling.</summary>
        CORJIT_FLAG_XARCH_VECTOR512_THROTTLING = 31,
    }
}
