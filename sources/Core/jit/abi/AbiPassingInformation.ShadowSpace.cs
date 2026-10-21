// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// These values match registeramd64.h; regNumber only contains names for the active target.
internal enum WindowsAmd64Register : uint
{
    Rcx = 1,
    Rdx = 2,
    R8 = 8,
    R9 = 9,
    R10 = 10,
    Xmm0 = 32,
    Xmm1 = 33,
    Xmm2 = 34,
    Xmm3 = 35,
}

public partial struct AbiPassingInformation
{
    public static bool GetShadowSpaceCallerOffsetForReg(regNumber reg, out int offset)
    {
        if (!Configuration.TARGET_AMD64 || !Configuration.WINDOWS_AMD64_ABI)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Caller shadow-space offsets require Windows AMD64.");
            offset = default!;
            return default!;
        }

        switch ((WindowsAmd64Register)(uint)reg)
        {
            case WindowsAmd64Register.Rcx:
            case WindowsAmd64Register.Xmm0:
            {
                offset = 0;
                return true;
            }

            case WindowsAmd64Register.Rdx:
            case WindowsAmd64Register.Xmm1:
            {
                offset = 8;
                return true;
            }

            case WindowsAmd64Register.R8:
            case WindowsAmd64Register.Xmm2:
            {
                offset = 16;
                return true;
            }

            case WindowsAmd64Register.R9:
            case WindowsAmd64Register.Xmm3:
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
    }
}
