// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_XARCH
    public static int instInputSize(instruction ins)
    {
        assert((uint)ins < (uint)instInfo.Length);

        return (instInfo[(int)ins] & Input_Mask) switch {
            Input_8Bit => 1,
            Input_16Bit => 2,
            Input_32Bit => 4,
            Input_64Bit => 8,
            _ => throw new System.InvalidOperationException($"Instruction {ins} has no input size."),
        };
    }
#endif
}
