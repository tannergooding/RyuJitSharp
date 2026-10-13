// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static int instInputSize(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Instruction input size metadata requires xarch.");
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)instInfo.Length);

        return (instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] & Input_Mask) switch {
            Input_8Bit => 1,
            Input_16Bit => 2,
            Input_32Bit => 4,
            Input_64Bit => 8,
            _ => throw new System.InvalidOperationException($"Instruction {ins} has no input size."),
        };
    }
}
