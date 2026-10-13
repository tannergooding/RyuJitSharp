// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    // These bits are the ARM64 instruction-info flags in CodeGen.InstructionFlags.generated.cs.
    private const byte Arm64InstInfoCompare = 4;
    private const byte Arm64InstInfoWide = 16;
    private const byte Arm64InstInfoLong = 32;
    private const byte Arm64InstInfoNarrow = 64;

    private static bool emitInsIsCompareArm64(instruction ins)
    {
        RequireArm64Target();
        if (InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length)
        {
            return (((byte)CodeGen.instInfo[nativeOrdinal] & Arm64InstInfoCompare) != 0);
        }

        return false;
    }

    private static bool emitInsIsVectorLongArm64(instruction ins)
    {
        RequireArm64Target();
        if (InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length)
        {
            return (((byte)CodeGen.instInfo[nativeOrdinal] & Arm64InstInfoLong) != 0);
        }

        return false;
    }

    private static bool emitInsIsVectorNarrowArm64(instruction ins)
    {
        RequireArm64Target();
        if (InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length)
        {
            return (((byte)CodeGen.instInfo[nativeOrdinal] & Arm64InstInfoNarrow) != 0);
        }

        return false;
    }

    private static bool emitInsIsVectorWideArm64(instruction ins)
    {
        RequireArm64Target();
        if (InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length)
        {
            return (((byte)CodeGen.instInfo[nativeOrdinal] & Arm64InstInfoWide) != 0);
        }

        return false;
    }
}
