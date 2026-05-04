// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

global using static RyuJitSharp.JitFlags;
using System.Diagnostics;

namespace RyuJitSharp;

// This class wraps the CORJIT_FLAGS type in the JIT-EE interface (in corjit.h).
// If this changes, also change spmidumphelper.cpp.
public partial struct JitFlags
{
    private ulong _jitFlags;
    private CORINFO_InstructionSetFlags _instructionSetFlags;

    // Convenience constructor to set exactly one flags.
    public JitFlags(JitFlag flag)
    {
        Set(flag);
    }

    public void Clear(JitFlag flag)
    {
        _jitFlags &= ~(1UL << (int)(flag));
    }

    public readonly CORINFO_InstructionSetFlags GetInstructionSetFlags()
    {
        return _instructionSetFlags;
    }

    public readonly bool IsEmpty() => _jitFlags is 0;

    public readonly bool IsSet(JitFlag flag) => (_jitFlags & (1UL << (int)(flag))) is not 0;

    public void Reset()
    {
        _jitFlags = 0;
    }

    public void Set(JitFlag flag)
    {
        _jitFlags |= 1UL << (int)(flag);
    }

    public unsafe void SetFromFlags(CORJIT_FLAGS flags)
    {
        // We don't want to have to check every one, so we assume it is exactly the same values as the JitFlag
        // values defined in this type.
        _jitFlags = flags.GetFlagsRaw();
        _instructionSetFlags = flags.GetInstructionSetFlags();

        assert(sizeof(JitFlags) == sizeof(CORJIT_FLAGS));

        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_SPEED_OPT, JitFlag.JIT_FLAG_SPEED_OPT);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_SIZE_OPT, JitFlag.JIT_FLAG_SIZE_OPT);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_DEBUG_CODE, JitFlag.JIT_FLAG_DEBUG_CODE);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_DEBUG_EnC, JitFlag.JIT_FLAG_DEBUG_EnC);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_DEBUG_INFO, JitFlag.JIT_FLAG_DEBUG_INFO);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_MIN_OPT, JitFlag.JIT_FLAG_MIN_OPT);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_ENABLE_CFG, JitFlag.JIT_FLAG_ENABLE_CFG);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_OSR, JitFlag.JIT_FLAG_OSR);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_ALT_JIT, JitFlag.JIT_FLAG_ALT_JIT);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_FROZEN_ALLOC_ALLOWED, JitFlag.JIT_FLAG_FROZEN_ALLOC_ALLOWED);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_PORTABLE_ENTRY_POINTS, JitFlag.JIT_FLAG_PORTABLE_ENTRY_POINTS);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_AOT, JitFlag.JIT_FLAG_AOT);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_PROF_ENTERLEAVE, JitFlag.JIT_FLAG_PROF_ENTERLEAVE);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_PROF_NO_PINVOKE_INLINE, JitFlag.JIT_FLAG_PROF_NO_PINVOKE_INLINE);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_RELOC, JitFlag.JIT_FLAG_RELOC);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_IL_STUB, JitFlag.JIT_FLAG_IL_STUB);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_PROCSPLIT, JitFlag.JIT_FLAG_PROCSPLIT);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_BBINSTR, JitFlag.JIT_FLAG_BBINSTR);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_BBINSTR_IF_LOOPS, JitFlag.JIT_FLAG_BBINSTR_IF_LOOPS);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_BBOPT, JitFlag.JIT_FLAG_BBOPT);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_FRAMED, JitFlag.JIT_FLAG_FRAMED);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_PUBLISH_SECRET_PARAM, JitFlag.JIT_FLAG_PUBLISH_SECRET_PARAM);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_USE_PINVOKE_HELPERS, JitFlag.JIT_FLAG_USE_PINVOKE_HELPERS);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_REVERSE_PINVOKE, JitFlag.JIT_FLAG_REVERSE_PINVOKE);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_TRACK_TRANSITIONS, JitFlag.JIT_FLAG_TRACK_TRANSITIONS);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_TIER0, JitFlag.JIT_FLAG_TIER0);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_TIER1, JitFlag.JIT_FLAG_TIER1);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_NO_INLINING, JitFlag.JIT_FLAG_NO_INLINING);

#if TARGET_ARM
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_RELATIVE_CODE_RELOCS, JitFlag.JIT_FLAG_RELATIVE_CODE_RELOCS);
        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_SOFTFP_ABI, JitFlag.JIT_FLAG_SOFTFP_ABI);
#endif

        FLAGS_EQUAL(CorJitFlag.CORJIT_FLAG_ASYNC, JitFlag.JIT_FLAG_ASYNC);

        [Conditional("DEBUG")]
        static void FLAGS_EQUAL(CorJitFlag a, JitFlag b)
        {
            assert((uint)a == (uint)b);
        }
    }

    public void SetInstructionSetFlags(CORINFO_InstructionSetFlags instructionSetFlags)
    {
        _instructionSetFlags = instructionSetFlags;
    }
}
