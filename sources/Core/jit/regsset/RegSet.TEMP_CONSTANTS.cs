// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
    private const int MAX_TEMP_SIZE = 64;

    // The shared layout reserves space for the largest vector temp and ARM64's unknown-size slots.
    public const int TEMP_SLOT_COUNT = (MAX_TEMP_SIZE / sizeof(int)) + 2;

    public static int TEMP_MAX_SIZE
    {
        get
        {
            if (!Configuration.FEATURE_SIMD)
            {
                return sizeof(double);
            }

            if (Configuration.TARGET_XARCH)
            {
                return 64;
            }

            if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
            {
                return 16;
            }

            throw new FatalJitException(CORJIT_IMPLLIMITATION, "SIMD spill temps are unsupported for this target.");
        }
    }
}
