// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using static RyuJitSharp.CORJIT_FLAGS.CorJitFlag;
using static RyuJitSharp.JitFlags.JitFlag;

namespace RyuJitSharp;

// This class wraps the CORJIT_FLAGS type in the JIT-EE interface (in corjit.h).
// If this changes, also change spmidumphelper.cpp.
public unsafe partial struct JitFlags
{
    // Convenience constructor to set exactly one flags.
    public JitFlags(JitFlag flag)
    {
        Set(flag);
    }

    public void Reset()
    {
        m_jitFlags = 0;
    }

    public readonly CORINFO_InstructionSetFlags GetInstructionSetFlags()
    {
        return m_instructionSetFlags;
    }

    public void SetInstructionSetFlags(CORINFO_InstructionSetFlags instructionSetFlags)
    {
        m_instructionSetFlags = instructionSetFlags;
    }

    public void Set(JitFlag flag)
    {
        m_jitFlags |= 1UL << (int)flag;
    }

    public void Clear(JitFlag flag)
    {
        m_jitFlags &= ~(1UL << (int)flag);
    }

    public readonly bool IsSet(JitFlag flag) => (m_jitFlags & (1UL << (int)flag)) != 0;

    public readonly bool IsEmpty() => m_jitFlags == 0;

    public void SetFromFlags(CORJIT_FLAGS flags)
    {
        // We don't want to have to check every one, so we assume it is exactly the same values as the JitFlag
        // values defined in this type.
        m_jitFlags = flags.GetFlagsRaw();
        m_instructionSetFlags = flags.GetInstructionSetFlags();

        Debug.Assert(sizeof(JitFlags) == sizeof(CORJIT_FLAGS));

        FLAGS_EQUAL(CORJIT_FLAG_SPEED_OPT, JIT_FLAG_SPEED_OPT);
        FLAGS_EQUAL(CORJIT_FLAG_SIZE_OPT, JIT_FLAG_SIZE_OPT);
        FLAGS_EQUAL(CORJIT_FLAG_DEBUG_CODE, JIT_FLAG_DEBUG_CODE);
        FLAGS_EQUAL(CORJIT_FLAG_DEBUG_EnC, JIT_FLAG_DEBUG_EnC);
        FLAGS_EQUAL(CORJIT_FLAG_DEBUG_INFO, JIT_FLAG_DEBUG_INFO);
        FLAGS_EQUAL(CORJIT_FLAG_MIN_OPT, JIT_FLAG_MIN_OPT);
        FLAGS_EQUAL(CORJIT_FLAG_ENABLE_CFG, JIT_FLAG_ENABLE_CFG);
        FLAGS_EQUAL(CORJIT_FLAG_OSR, JIT_FLAG_OSR);
        FLAGS_EQUAL(CORJIT_FLAG_ALT_JIT, JIT_FLAG_ALT_JIT);
        FLAGS_EQUAL(CORJIT_FLAG_FROZEN_ALLOC_ALLOWED, JIT_FLAG_FROZEN_ALLOC_ALLOWED);
        FLAGS_EQUAL(CORJIT_FLAG_MAKEFINALCODE, JIT_FLAG_MAKEFINALCODE);
        FLAGS_EQUAL(CORJIT_FLAG_READYTORUN, JIT_FLAG_READYTORUN);
        FLAGS_EQUAL(CORJIT_FLAG_PROF_ENTERLEAVE, JIT_FLAG_PROF_ENTERLEAVE);
        FLAGS_EQUAL(CORJIT_FLAG_PROF_NO_PINVOKE_INLINE, JIT_FLAG_PROF_NO_PINVOKE_INLINE);
        FLAGS_EQUAL(CORJIT_FLAG_PREJIT, JIT_FLAG_PREJIT);
        FLAGS_EQUAL(CORJIT_FLAG_RELOC, JIT_FLAG_RELOC);
        FLAGS_EQUAL(CORJIT_FLAG_IL_STUB, JIT_FLAG_IL_STUB);
        FLAGS_EQUAL(CORJIT_FLAG_PROCSPLIT, JIT_FLAG_PROCSPLIT);
        FLAGS_EQUAL(CORJIT_FLAG_BBINSTR, JIT_FLAG_BBINSTR);
        FLAGS_EQUAL(CORJIT_FLAG_BBINSTR_IF_LOOPS, JIT_FLAG_BBINSTR_IF_LOOPS);
        FLAGS_EQUAL(CORJIT_FLAG_BBOPT, JIT_FLAG_BBOPT);
        FLAGS_EQUAL(CORJIT_FLAG_FRAMED, JIT_FLAG_FRAMED);
        FLAGS_EQUAL(CORJIT_FLAG_PUBLISH_SECRET_PARAM, JIT_FLAG_PUBLISH_SECRET_PARAM);
        FLAGS_EQUAL(CORJIT_FLAG_USE_PINVOKE_HELPERS, JIT_FLAG_USE_PINVOKE_HELPERS);
        FLAGS_EQUAL(CORJIT_FLAG_REVERSE_PINVOKE, JIT_FLAG_REVERSE_PINVOKE);
        FLAGS_EQUAL(CORJIT_FLAG_TRACK_TRANSITIONS, JIT_FLAG_TRACK_TRANSITIONS);
        FLAGS_EQUAL(CORJIT_FLAG_TIER0, JIT_FLAG_TIER0);
        FLAGS_EQUAL(CORJIT_FLAG_TIER1, JIT_FLAG_TIER1);
        FLAGS_EQUAL(CORJIT_FLAG_NO_INLINING, JIT_FLAG_NO_INLINING);

        if (TARGET_ARM32)
        {
            FLAGS_EQUAL(CORJIT_FLAG_ARM32_RELATIVE_CODE_RELOCS, JIT_FLAG_ARM32_RELATIVE_CODE_RELOCS);
            FLAGS_EQUAL(CORJIT_FLAG_ARM32_SOFTFP_ABI, JIT_FLAG_ARM32_SOFTFP_ABI);
        }
        else if (TARGET_XARCH)
        {
            FLAGS_EQUAL(CORJIT_FLAG_XARCH_VECTOR512_THROTTLING, JIT_FLAG_XARCH_VECTOR512_THROTTLING);
        }

        [Conditional("DEBUG")]
        static void FLAGS_EQUAL(CORJIT_FLAGS.CorJitFlag a, JitFlag b)
        {
            Debug.Assert((uint)a == (uint)b);
        }
    }

    private ulong m_jitFlags;
    private CORINFO_InstructionSetFlags m_instructionSetFlags;
}
