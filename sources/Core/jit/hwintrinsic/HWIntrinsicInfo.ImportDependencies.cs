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
        switch (intrinsic)
        {
            case NI_PackedSimd_ExtractScalar:
            case NI_PackedSimd_ReplaceScalar:
            case NI_PackedSimd_LoadScalarAndInsert:
            case NI_PackedSimd_StoreSelectedScalar:
            {
                return Compiler.getSIMDVectorLength(unchecked((uint)simdSize), simdBaseType) - 1;
            }

            default:
            {
                unreached();
                return 0;
            }
        }
    }

    public static void GetImmOpsPositions(NamedIntrinsic id, out int first, out int second)
    {
        first = -1;
        second = -1;

        switch (id)
        {
            case NI_PackedSimd_ExtractScalar:
            case NI_PackedSimd_ReplaceScalar:
            {
                first = 2;
                break;
            }

            case NI_PackedSimd_StoreSelectedScalar:
            case NI_PackedSimd_LoadScalarAndInsert:
            {
                first = 3;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }
#endif
}
