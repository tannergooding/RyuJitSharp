// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void SetSaveFpLrWithAllCalleeSavedRegisters(bool value)
    {
        if (!Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.ARM64FramePolicyIsNotSupportedForThisTarget);
            return;
        }

        JITDUMP($"Setting genSaveFpLrWithAllCalleeSavedRegisters to {dspBool(value)}\n");
        genSaveFpLrWithAllCalleeSavedRegisters = value;
    }
}
