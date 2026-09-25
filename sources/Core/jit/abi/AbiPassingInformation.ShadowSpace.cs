// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct AbiPassingInformation
{
    public static bool GetShadowSpaceCallerOffsetForReg(regNumber reg, out int offset)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Caller shadow-space offsets require Windows AMD64.");
#else
        switch (reg)
        {
            case REG_RCX:
            case REG_XMM0:
            {
                offset = 0;
                return true;
            }

            case REG_RDX:
            case REG_XMM1:
            {
                offset = 8;
                return true;
            }

            case REG_R8:
            case REG_XMM2:
            {
                offset = 16;
                return true;
            }

            case REG_R9:
            case REG_XMM3:
            {
                offset = 24;
                return true;
            }

            default:
            {
                offset = 0;
                return false;
            }
        }
#endif
    }
}
