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
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.InstructionInputSizeMetadataRequiresXarch);
            return default!;
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)instInfo.Length);

        switch ((instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] & Input_Mask))
        {
            case Input_8Bit:
                return 1;
            case Input_16Bit:
                return 2;
            case Input_32Bit:
                return 4;
            case Input_64Bit:
                return 8;
            default:
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.InstructionArgumentHasNoInputSize,
                        ins);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.InstructionArgumentHasNoInputSize);
                }

                return default;
        }
    }
}
