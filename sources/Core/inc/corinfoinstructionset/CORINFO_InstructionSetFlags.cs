// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public unsafe struct CORINFO_InstructionSetFlags
{
    private const int FlagsFieldCount = 2;
    private const int BitsPerFlagsField = sizeof(ulong) * 8;

    private _flags_e__FixedBuffer _flags;

    private static uint GetFlagsFieldIndex(CORINFO_InstructionSet instructionSet)
    {
        uint bitIndex = (uint)instructionSet;
        return bitIndex / BitsPerFlagsField;
    }

    private static ulong GetRelativeBitMask(CORINFO_InstructionSet instructionSet)
    {
        return 1UL << (int)instructionSet;
    }

    public readonly int GetInstructionFlagsFieldCount()
    {
        return FlagsFieldCount;
    }

    public void AddInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        uint index = GetFlagsFieldIndex(instructionSet);
        _flags[(int)index] |= GetRelativeBitMask(instructionSet);
    }

    public void RemoveInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        uint index = GetFlagsFieldIndex(instructionSet);
        ulong bitIndex = GetRelativeBitMask(instructionSet);
        _flags[(int)index] &= ~bitIndex;
    }

    public readonly bool HasInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        uint index = GetFlagsFieldIndex(instructionSet);
        ulong bitIndex = GetRelativeBitMask(instructionSet);
        return ((_flags[(int)index] & bitIndex) != 0);
    }

    public readonly bool Equals(CORINFO_InstructionSetFlags other)
    {
        ReadOnlySpan<ulong> flags = _flags;
        return flags.SequenceEqual(other._flags);
    }

    public void Add(CORINFO_InstructionSetFlags other)
    {
        for (int i = 0; i < FlagsFieldCount; i++)
        {
            _flags[i] |= other._flags[i];
        }
    }

    public readonly bool IsEmpty()
    {
        ReadOnlySpan<ulong> flags = _flags;
        return !flags.ContainsAnyExcept(0UL);
    }

    public void Reset()
    {
        Span<ulong> flags = _flags;
        flags.Clear();
    }

    public void Set64BitInstructionSetVariants()
    {
        if (TARGET_ARM64)
        {
            if (HasInstructionSet(InstructionSet_ARMARCH_ArmBase))
            {
                AddInstructionSet(InstructionSet_ARMARCH_ArmBase_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
            {
                AddInstructionSet(InstructionSet_ARMARCH_AdvSimd_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_Aes))
            {
                AddInstructionSet(InstructionSet_ARMARCH_Aes_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_Crc32))
            {
                AddInstructionSet(InstructionSet_ARMARCH_Crc32_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_Dp))
            {
                AddInstructionSet(InstructionSet_ARMARCH_Dp_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_Rdm))
            {
                AddInstructionSet(InstructionSet_ARMARCH_Rdm_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_Sha1))
            {
                AddInstructionSet(InstructionSet_ARMARCH_Sha1_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_Sha256))
            {
                AddInstructionSet(InstructionSet_ARMARCH_Sha256_Arm64);
            }

            if (HasInstructionSet(InstructionSet_ARMARCH_Sve))
            {
                AddInstructionSet(InstructionSet_ARMARCH_Sve_Arm64);
            }
        }

        if (TARGET_X64)
        {
            if (HasInstructionSet(InstructionSet_XARCH_X86Base))
            {
                AddInstructionSet(InstructionSet_XARCH_X86Base_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_SSE))
            {
                AddInstructionSet(InstructionSet_XARCH_SSE_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_SSE2))
            {
                AddInstructionSet(InstructionSet_XARCH_SSE2_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_SSE3))
            {
                AddInstructionSet(InstructionSet_XARCH_SSE3_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_SSSE3))
            {
                AddInstructionSet(InstructionSet_XARCH_SSSE3_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_SSE41))
            {
                AddInstructionSet(InstructionSet_XARCH_SSE41_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_SSE42))
            {
                AddInstructionSet(InstructionSet_XARCH_SSE42_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX2))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX2_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AES))
            {
                AddInstructionSet(InstructionSet_XARCH_AES_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_BMI1))
            {
                AddInstructionSet(InstructionSet_XARCH_BMI1_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_BMI2))
            {
                AddInstructionSet(InstructionSet_XARCH_BMI2_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_FMA))
            {
                AddInstructionSet(InstructionSet_XARCH_FMA_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_LZCNT))
            {
                AddInstructionSet(InstructionSet_XARCH_LZCNT_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_PCLMULQDQ))
            {
                AddInstructionSet(InstructionSet_XARCH_PCLMULQDQ_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_POPCNT))
            {
                AddInstructionSet(InstructionSet_XARCH_POPCNT_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVXVNNI))
            {
                AddInstructionSet(InstructionSet_XARCH_AVXVNNI_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_MOVBE))
            {
                AddInstructionSet(InstructionSet_XARCH_MOVBE_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_X86Serialize))
            {
                AddInstructionSet(InstructionSet_XARCH_X86Serialize_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512F))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512F_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512F_VL))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512F_VL_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512BW))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512BW_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512BW_VL_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512CD))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512CD_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512CD_VL_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512DQ))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512DQ_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512DQ_VL_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512VBMI))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512VBMI_X64);
            }

            if (HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL))
            {
                AddInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL_X64);
            }
        }
    }

    [UnscopedRef]
    public Span<ulong> GetFlagsRaw()
    {
        return _flags;
    }

    [InlineArray(FlagsFieldCount)]
    private struct _flags_e__FixedBuffer
    {
        public ulong e0;
    }
}
