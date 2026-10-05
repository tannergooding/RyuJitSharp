// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
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
        if ((uint)ins < (uint)CodeGen.instInfo.Length)
        {
            return (CodeGen.instInfo[(int)ins] & Arm64InstInfoCompare) != 0;
        }

        return false;
    }

    private static bool emitInsIsVectorLongArm64(instruction ins)
    {
        if ((uint)ins < (uint)CodeGen.instInfo.Length)
        {
            return (CodeGen.instInfo[(int)ins] & Arm64InstInfoLong) != 0;
        }

        return false;
    }

    private static bool emitInsIsVectorNarrowArm64(instruction ins)
    {
        if ((uint)ins < (uint)CodeGen.instInfo.Length)
        {
            return (CodeGen.instInfo[(int)ins] & Arm64InstInfoNarrow) != 0;
        }

        return false;
    }

    private static bool emitInsIsVectorWideArm64(instruction ins)
    {
        if ((uint)ins < (uint)CodeGen.instInfo.Length)
        {
            return (CodeGen.instInfo[(int)ins] & Arm64InstInfoWide) != 0;
        }

        return false;
    }
}
#endif
