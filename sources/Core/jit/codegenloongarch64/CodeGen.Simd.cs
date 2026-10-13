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
            throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD is unavailable for this configuration.");
        }

        NYI("unimplemented on LOONGARCH64 yet");
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD instruction options are not ported.");
    }

    public void genSimdUpperSaveLoongArch64(GenTreeIntrinsic node)
    {
        if (!Configuration.TARGET_LOONGARCH64 || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD is unavailable for this configuration.");
        }

        NYI("unimplemented on LOONGARCH64 yet");
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD upper-lane saving is not ported.");
    }

    public void genSimdUpperRestoreLoongArch64(GenTreeIntrinsic node)
    {
        if (!Configuration.TARGET_LOONGARCH64 || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD is unavailable for this configuration.");
        }

        NYI("unimplemented on LOONGARCH64 yet");
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD upper-lane restoration is not ported.");
    }
}
