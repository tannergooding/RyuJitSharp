// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/// <summary>Represents classification information for a struct.</summary>
public struct SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR
{
    public SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR()
    {
        Initialize();
    }

    /// <summary>Whether the struct is passable/passed (this includes struct returning) in registers.</summary>
    public bool passedInRegisters;

    /// <summary>Number of eight-bytes for this struct.</summary>
    public byte eightByteCount;

    private _eightByteClassifications_e__FixedBuffer _eightByteClassifications;

    /// <summary>The eight-bytes type classification.</summary>
    [UnscopedRef]
    public Span<SystemVClassificationType> eightByteClassifications => _eightByteClassifications;

    private _eightByteSizes_e__FixedBuffer _eightByteSizes;

    /// <summary>The size of the eight-bytes.</summary>
    /// <remarks>An eight-byte could include padding. This represents the no padding size of the eight-byte.</remarks>
    [UnscopedRef]
    public Span<byte> eightByteSizes => _eightByteSizes;

    private _eightByteOffsets_e__FixedBuffer _eightByteOffsets;

    /// <summary>The start offset of the eight-bytes (in bytes).</summary>
    [UnscopedRef]
    public Span<byte> eightByteOffsets => _eightByteOffsets;

    /// <summary>Copies a struct classification into this one.</summary>
    /// <param name="copyFrom">The struct classification to copy from.</param>
    public void CopyFrom(in SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR copyFrom)
    {
        passedInRegisters = copyFrom.passedInRegisters;
        eightByteCount = copyFrom.eightByteCount;

        for (int i = 0; i < CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS; i++)
        {
            eightByteClassifications[i] = copyFrom.eightByteClassifications[i];
            eightByteSizes[i] = copyFrom.eightByteSizes[i];
            eightByteOffsets[i] = copyFrom.eightByteOffsets[i];
        }
    }

    /// <summary>Returns whether the eight-byte at slotIndex is of integral type.</summary>
    /// <param name="slotIndex">The slot number we are determining if it is of integral type.</param>
    /// <returns><c>true</c> if we the eight-byte at index slotIndex is of integral type.</returns>
    public readonly bool IsIntegralSlot(uint slotIndex)
    {
        return _eightByteClassifications[(int)slotIndex] is SystemVClassificationTypeInteger
                                                         or SystemVClassificationTypeIntegerReference
                                                         or SystemVClassificationTypeIntegerByRef;
    }

    /// <summary>Returns whether the eight-byte at <paramref name="slotIndex" /> is SSE type.</summary>
    /// <param name="slotIndex">The slot number we are determining if it is of SSE type.</param>
    /// <returns><c>true</c> if we the eight-byte at index slotIndex is of SSE type.</returns>
    /// <remarks>Follows the rules of the AMD64 System V ABI specification at https://software.intel.com/sites/default/files/article/402129/mpx-linux64-abi.pdf. Please refer to it for definitions/examples.</remarks>
    public readonly bool IsSseSlot(uint slotIndex)
    {
        return _eightByteClassifications[(int)slotIndex] == SystemVClassificationTypeSSE;
    }

    private void Initialize()
    {
        passedInRegisters = false;
        eightByteCount = 0;

        for (int i = 0; i < CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS; i++)
        {
            eightByteClassifications[i] = SystemVClassificationTypeUnknown;
            eightByteSizes[i] = 0;
            eightByteOffsets[i] = 0;
        }
    }

    [InlineArray(CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS)]
    private struct _eightByteClassifications_e__FixedBuffer
    {
        public SystemVClassificationType e0;
    }

    [InlineArray(CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS)]
    private struct _eightByteSizes_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS)]
    private struct _eightByteOffsets_e__FixedBuffer
    {
        public byte e0;
    }
}
