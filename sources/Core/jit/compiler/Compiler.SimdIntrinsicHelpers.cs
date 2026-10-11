// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    internal static TSimd NarrowAndDuplicateSimdLong<TSimd>(var_types baseType, in TSimd value)
        where TSimd : unmanaged
    {
        TSimd result = default;
        var source = value;
        var sourceWords = MemoryMarshal.Cast<TSimd, ulong>(MemoryMarshal.CreateReadOnlySpan(ref source, 1));
        var destination = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));

        switch (baseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                for (var index = 0; index < destination.Length; index++)
                {
                    destination[index] = (byte)Math.Min(sourceWords[index / 8], byte.MaxValue);
                }
                break;
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                var elements = MemoryMarshal.Cast<byte, ushort>(destination);
                for (var index = 0; index < elements.Length; index++)
                {
                    elements[index] = (ushort)Math.Min(sourceWords[index / 4], ushort.MaxValue);
                }
                break;
            }

            case TYP_FLOAT:
            case TYP_INT:
            case TYP_UINT:
            {
                var elements = MemoryMarshal.Cast<byte, uint>(destination);
                for (var index = 0; index < elements.Length; index++)
                {
                    elements[index] = (uint)Math.Min(sourceWords[index / 2], uint.MaxValue);
                }
                break;
            }

            case TYP_DOUBLE:
            case TYP_LONG:
            case TYP_ULONG:
            {
                result = value;
                break;
            }

            default:
            {
                unreached();
                throw new System.Diagnostics.UnreachableException();
            }
        }

        return result;
    }

    internal static uint ReverseArm64Bits(uint value)
    {
        // Reverse each byte's 1-, 2-, and 4-bit groups, then reverse byte order.
        value = ((value & 0x5555_5555u) << 1) | ((value >> 1) & 0x5555_5555u);
        value = ((value & 0x3333_3333u) << 2) | ((value >> 2) & 0x3333_3333u);
        value = ((value & 0x0F0F_0F0Fu) << 4) | ((value >> 4) & 0x0F0F_0F0Fu);
        return BinaryPrimitives.ReverseEndianness(value);
    }

    internal static ulong ReverseArm64Bits(ulong value)
    {
        value = ((value & 0x5555_5555_5555_5555UL) << 1) | ((value >> 1) & 0x5555_5555_5555_5555UL);
        value = ((value & 0x3333_3333_3333_3333UL) << 2) | ((value >> 2) & 0x3333_3333_3333_3333UL);
        value = ((value & 0x0F0F_0F0F_0F0F_0F0FUL) << 4) | ((value >> 4) & 0x0F0F_0F0F_0F0F_0F0FUL);
        return BinaryPrimitives.ReverseEndianness(value);
    }
}
