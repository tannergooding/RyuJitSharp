// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static int lookupIval(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ARM64IntrinsicImmediatesAreUnavailableForThisTarget);
        }

        switch (id)
        {
            case NI_Sve_Compute16BitAddresses:
            {
                return 1;
            }
            case NI_Sve_Compute32BitAddresses:
            {
                return 2;
            }
            case NI_Sve_Compute64BitAddresses:
            {
                return 3;
            }
            case NI_Sve_Compute8BitAddresses:
            {
                return 0;
            }
            default:
            {
                unreached();
                break;
            }
        }

        return -1;
    }
}
