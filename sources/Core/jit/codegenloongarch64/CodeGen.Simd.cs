// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public insOpts genGetSimdInsOptLoongArch64(emitAttr size, var_types elementType)
    {
        if (!Configuration.TARGET_LOONGARCH64 || !Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64SIMDIsUnavailableForThisConfiguration
    );
            return default!;
        }

        NYI("unimplemented on LOONGARCH64 yet");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64SIMDInstructionOptionsAreNotPorted
    );
        return default!;
    }

    public void genSimdUpperSaveLoongArch64(GenTreeIntrinsic node)
    {
        if (!Configuration.TARGET_LOONGARCH64 || !Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64SIMDIsUnavailableForThisConfiguration
    );
            return;
        }

        NYI("unimplemented on LOONGARCH64 yet");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.LoongArch64SIMDUpperLaneSavingIsNotPorted);
        return;
    }

    public void genSimdUpperRestoreLoongArch64(GenTreeIntrinsic node)
    {
        if (!Configuration.TARGET_LOONGARCH64 || !Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64SIMDIsUnavailableForThisConfiguration
    );
            return;
        }

        NYI("unimplemented on LOONGARCH64 yet");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64SIMDUpperLaneRestorationIsNotPorted
    );
        return;
    }
}
