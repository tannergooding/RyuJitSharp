// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace RyuJitSharp;

// This class wraps the CORJIT_FLAGS type in the JIT-EE interface (in corjit.h).
// If this changes, also change spmidumphelper.cpp.
public partial struct JitFlags
{
    private long _jitFlags;
    private CORINFO_InstructionSetFlags _instructionSetFlags;

    // Convenience constructor to set exactly one flags.
    public JitFlags(JitFlag flag)
    {
        Set(flag);
    }

    public void Clear(JitFlag flag)
    {
        _jitFlags &= ~(1L << (int)(flag));
    }

    public readonly CORINFO_InstructionSetFlags GetInstructionSetFlags()
    {
        return _instructionSetFlags;
    }

    public readonly bool IsEmpty() => _jitFlags == 0;

    public readonly bool IsSet(JitFlag flag) => (_jitFlags & (1L << (int)(flag))) != 0;

    public void Reset()
    {
        _jitFlags = 0;
    }

    public void Set(JitFlag flag)
    {
        _jitFlags |= 1L << (int)(flag);
    }

    public unsafe void SetFromFlags(CORJIT_FLAGS flags)
    {
        // We don't want to have to check every one, so we assume it is exactly the same values as the JitFlag
        // values defined in this type.
        _jitFlags = flags.GetFlagsRaw();
        _instructionSetFlags = flags.GetInstructionSetFlags();

        assert(sizeof(JitFlags) == sizeof(CORJIT_FLAGS));

        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_SPEED_OPT, JIT_FLAG_SPEED_OPT);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_SIZE_OPT, JIT_FLAG_SIZE_OPT);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_DEBUG_CODE, JIT_FLAG_DEBUG_CODE);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_DEBUG_EnC, JIT_FLAG_DEBUG_EnC);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_DEBUG_INFO, JIT_FLAG_DEBUG_INFO);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_MIN_OPT, JIT_FLAG_MIN_OPT);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_ENABLE_CFG, JIT_FLAG_ENABLE_CFG);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_OSR, JIT_FLAG_OSR);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_ALT_JIT, JIT_FLAG_ALT_JIT);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_FROZEN_ALLOC_ALLOWED, JIT_FLAG_FROZEN_ALLOC_ALLOWED);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_PORTABLE_ENTRY_POINTS, JIT_FLAG_PORTABLE_ENTRY_POINTS);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_AOT, JIT_FLAG_AOT);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_PROF_ENTERLEAVE, JIT_FLAG_PROF_ENTERLEAVE);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_PROF_NO_PINVOKE_INLINE, JIT_FLAG_PROF_NO_PINVOKE_INLINE);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_RELOC, JIT_FLAG_RELOC);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_IL_STUB, JIT_FLAG_IL_STUB);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_PROCSPLIT, JIT_FLAG_PROCSPLIT);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_BBINSTR, JIT_FLAG_BBINSTR);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_BBINSTR_IF_LOOPS, JIT_FLAG_BBINSTR_IF_LOOPS);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_BBOPT, JIT_FLAG_BBOPT);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_FRAMED, JIT_FLAG_FRAMED);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_PUBLISH_SECRET_PARAM, JIT_FLAG_PUBLISH_SECRET_PARAM);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_USE_PINVOKE_HELPERS, JIT_FLAG_USE_PINVOKE_HELPERS);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_REVERSE_PINVOKE, JIT_FLAG_REVERSE_PINVOKE);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_TRACK_TRANSITIONS, JIT_FLAG_TRACK_TRANSITIONS);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_TIER0, JIT_FLAG_TIER0);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_TIER1, JIT_FLAG_TIER1);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_NO_INLINING, JIT_FLAG_NO_INLINING);

#if TARGET_ARM
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_RELATIVE_CODE_RELOCS, JitFlags.JIT_FLAG_RELATIVE_CODE_RELOCS);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_SOFTFP_ABI, JitFlags.JIT_FLAG_SOFTFP_ABI);
#endif

        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_ASYNC, JIT_FLAG_ASYNC);
        FLAGS_EQUAL(CORJIT_FLAGS.CORJIT_FLAG_USE_DISPATCH_HELPERS, JIT_FLAG_USE_DISPATCH_HELPERS);

        [Conditional("DEBUG")]
        static void FLAGS_EQUAL(CORJIT_FLAGS.CorJitFlag a, JitFlag b)
        {
            assert((int)(a) == (int)(b));
        }
    }

    public void SetInstructionSetFlags(CORINFO_InstructionSetFlags instructionSetFlags)
    {
        _instructionSetFlags = instructionSetFlags;
    }
}
