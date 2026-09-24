// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if EMIT_GENERATE_GCINFO && !JIT32_GCENCODER
using System;

namespace RyuJitSharp;

public partial struct GCInfo
{
    private readonly struct RegSlotIdKey : IEquatable<RegSlotIdKey>
    {
        public readonly ushort m_regNum;
        public readonly ushort m_flags;

        public RegSlotIdKey(ushort regNum, uint flags)
        {
            m_regNum = regNum;
            m_flags = unchecked((ushort)flags);
            assert(m_flags == flags);
        }

        public bool Equals(RegSlotIdKey other)
        {
            return (m_regNum == other.m_regNum) && (m_flags == other.m_flags);
        }

        public override bool Equals(object? obj)
        {
            return obj is RegSlotIdKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(((uint)m_flags << 16) + m_regNum));
        }
    }

    private readonly struct StackSlotIdKey : IEquatable<StackSlotIdKey>
    {
        public readonly int m_offset;
        public readonly bool m_fpRel;
        public readonly ushort m_flags;

        public StackSlotIdKey(int offset, bool fpRel, uint flags)
        {
            m_offset = offset;
            m_fpRel = fpRel;
            m_flags = unchecked((ushort)flags);
            assert(m_flags == flags);
        }

        public bool Equals(StackSlotIdKey other)
        {
            return (m_offset == other.m_offset) && (m_fpRel == other.m_fpRel) && (m_flags == other.m_flags);
        }

        public override bool Equals(object? obj)
        {
            return obj is StackSlotIdKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(((uint)m_flags << 16) ^ (uint)m_offset ^ (m_fpRel ? 0x1000000u : 0u)));
        }
    }
}
#endif
