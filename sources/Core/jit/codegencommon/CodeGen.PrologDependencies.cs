// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private regMaskTP genStackAllocRegisterMask(uint frameSize, regMaskTP maskCalleeSavedFloat)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32StackAllocationRegisterSelectionIsNotSupportedOnThisTarget
    );
            return default!;
        }

        return genStackAllocRegisterMaskArmCore(frameSize, maskCalleeSavedFloat);
    }
}
