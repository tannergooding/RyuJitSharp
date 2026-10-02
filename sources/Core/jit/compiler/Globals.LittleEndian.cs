// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;

namespace RyuJitSharp;

public static unsafe partial class Globals
{
    public static byte getU1LittleEndian(byte* ptr)
    {
        return *ptr;
    }

    public static ushort getU2LittleEndian(byte* ptr)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(ptr, sizeof(ushort)));
    }

    public static uint getU4LittleEndian(byte* ptr)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(ptr, sizeof(uint)));
    }

    public static sbyte getI1LittleEndian(byte* ptr)
    {
        return unchecked((sbyte)*ptr);
    }

    public static short getI2LittleEndian(byte* ptr)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(new ReadOnlySpan<byte>(ptr, sizeof(short)));
    }

    public static int getI4LittleEndian(byte* ptr)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(ptr, sizeof(int)));
    }

    public static long getI8LittleEndian(byte* ptr)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(ptr, sizeof(long)));
    }

    public static float getR4LittleEndian(byte* ptr)
    {
        var value = getI4LittleEndian(ptr);

        return BitConverter.Int32BitsToSingle(value);
    }

    public static double getR8LittleEndian(byte* ptr)
    {
        var value = getI8LittleEndian(ptr);

        return BitConverter.Int64BitsToDouble(value);
    }
}
