// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
#if FEATURE_HW_INTRINSICS && TARGET_ARM64
    public static bool HasScalarInputVariant(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);

        return (flags & HW_Flag_HasScalarInputVariant) != 0;
    }

    public static bool BaseTypeFromValueTupleArg(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);

        return (flags & HW_Flag_BaseTypeFromValueTupleArg) != 0;
    }

    public static NamedIntrinsic GetScalarInputVariant(NamedIntrinsic id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Hardware-intrinsic scalar-input variant selection is not ported.");
    }
#endif

#if FEATURE_HW_INTRINSICS && TARGET_WASM
    public static int lookupImmUpperBound(NamedIntrinsic intrinsic, int simdSize, var_types simdBaseType)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm hardware-intrinsic immediate bounds are not ported.");
    }
#endif
}
