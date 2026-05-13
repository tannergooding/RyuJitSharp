// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public struct CORINFO_InstructionSetFlags
{
    private const int FlagsFieldCount = 2;
    private const int BitsPerFlagsField = sizeof(long) * 8;

    private InlineArray2<long> _flags;

    public void Add(CORINFO_InstructionSetFlags other)
    {
        for (var i = 0; i < FlagsFieldCount; i++)
        {
            _flags[i] |= other._flags[i];
        }
    }

    public void AddInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        var index = GetFlagsFieldIndex(instructionSet);
        _flags[index] |= GetRelativeBitMask(instructionSet);
    }

    public readonly bool Equals(CORINFO_InstructionSetFlags other)
    {
        ReadOnlySpan<long> flags = _flags;
        return flags.SequenceEqual(other._flags);
    }

    private static int GetFlagsFieldIndex(CORINFO_InstructionSet instructionSet)
    {
        var bitIndex = (int)(instructionSet);
        return bitIndex / BitsPerFlagsField;
    }

    [UnscopedRef]
    public Span<long> GetFlagsRaw() => _flags;

    public readonly int GetInstructionFlagsFieldCount() => FlagsFieldCount;

    private static long GetRelativeBitMask(CORINFO_InstructionSet instructionSet)
    {
        return 1L << (int)(instructionSet);
    }

    public readonly bool HasInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        var index = GetFlagsFieldIndex(instructionSet);
        var bitIndex = GetRelativeBitMask(instructionSet);
        return ((_flags[index] & bitIndex) != 0);
    }

    public readonly bool IsEmpty() => !((ReadOnlySpan<long>)(_flags)).ContainsAnyExcept(0);

    public void RemoveInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        var index = GetFlagsFieldIndex(instructionSet);
        var bitIndex = GetRelativeBitMask(instructionSet);
        _flags[index] &= ~bitIndex;
    }

    public void Reset()
    {
        Span<long> flags = _flags;
        flags.Clear();
    }

    public void Set64BitInstructionSetVariants()
    {
#if TARGET_ARM64
        if (HasInstructionSet(InstructionSet_ArmBase))
        {
            AddInstructionSet(InstructionSet_ArmBase_Arm64);
        }

        if (HasInstructionSet(InstructionSet_AdvSimd))
        {
            AddInstructionSet(InstructionSet_AdvSimd_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Aes))
        {
            AddInstructionSet(InstructionSet_Aes_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Crc32))
        {
            AddInstructionSet(InstructionSet_Crc32_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Dp))
        {
            AddInstructionSet(InstructionSet_Dp_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Rdm))
        {
            AddInstructionSet(InstructionSet_Rdm_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sha1))
        {
            AddInstructionSet(InstructionSet_Sha1_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sha256))
        {
            AddInstructionSet(InstructionSet_Sha256_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sve))
        {
            AddInstructionSet(InstructionSet_Sve_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sve2))
        {
            AddInstructionSet(InstructionSet_Sve2_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sha3))
        {
            AddInstructionSet(InstructionSet_Sha3_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sm4))
        {
            AddInstructionSet(InstructionSet_Sm4_Arm64);
        }

        if (HasInstructionSet(InstructionSet_SveAes))
        {
            AddInstructionSet(InstructionSet_SveAes_Arm64);
        }

        if (HasInstructionSet(InstructionSet_SveSha3))
        {
            AddInstructionSet(InstructionSet_SveSha3_Arm64);
        }

        if (HasInstructionSet(InstructionSet_SveSm4))
        {
            AddInstructionSet(InstructionSet_SveSm4_Arm64);
        }
#elif TARGET_AMD64
        if (HasInstructionSet(InstructionSet_X86Base))
        {
            AddInstructionSet(InstructionSet_X86Base_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX))
        {
            AddInstructionSet(InstructionSet_AVX_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX2))
        {
            AddInstructionSet(InstructionSet_AVX2_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512))
        {
            AddInstructionSet(InstructionSet_AVX512_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512v2))
        {
            AddInstructionSet(InstructionSet_AVX512v2_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512v3))
        {
            AddInstructionSet(InstructionSet_AVX512v3_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX10v1))
        {
            AddInstructionSet(InstructionSet_AVX10v1_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX10v2))
        {
            AddInstructionSet(InstructionSet_AVX10v2_X64);
        }

        if (HasInstructionSet(InstructionSet_AES))
        {
            AddInstructionSet(InstructionSet_AES_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512VP2INTERSECT))
        {
            AddInstructionSet(InstructionSet_AVX512VP2INTERSECT_X64);
        }

        if (HasInstructionSet(InstructionSet_AVXIFMA))
        {
            AddInstructionSet(InstructionSet_AVXIFMA_X64);
        }

        if (HasInstructionSet(InstructionSet_AVXVNNI))
        {
            AddInstructionSet(InstructionSet_AVXVNNI_X64);
        }

        if (HasInstructionSet(InstructionSet_GFNI))
        {
            AddInstructionSet(InstructionSet_GFNI_X64);
        }

        if (HasInstructionSet(InstructionSet_SHA))
        {
            AddInstructionSet(InstructionSet_SHA_X64);
        }

        if (HasInstructionSet(InstructionSet_WAITPKG))
        {
            AddInstructionSet(InstructionSet_WAITPKG_X64);
        }

        if (HasInstructionSet(InstructionSet_X86Serialize))
        {
            AddInstructionSet(InstructionSet_X86Serialize_X64);
        }
#endif // TARGET_AMD64
    }
}
