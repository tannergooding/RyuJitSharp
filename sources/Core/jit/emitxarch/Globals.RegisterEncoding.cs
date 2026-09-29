// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
#if TARGET_XARCH
    public static regNumber AbsRegNumber(regNumber reg)
    {
        assert(reg < REG_STK);
        if ((uint)reg >= KBASE)
        {
            return (regNumber)((uint)reg - KBASE);
        }
        else if ((uint)reg >= XMMBASE)
        {
            return (regNumber)((uint)reg - XMMBASE);
        }

        return reg;
    }

    public static bool IsXMMReg(regNumber reg)
    {
#if TARGET_AMD64
        return (reg >= REG_XMM0) && (reg <= REG_XMM31);
#else
        return (reg >= REG_XMM0) && (reg <= REG_XMM7);
#endif
    }

    public static uint HighAwareRegEncoding(regNumber reg)
    {
        assert(((uint)REG_XMM0 & 0x7) == 0);
        return (uint)AbsRegNumber(reg) & 0xF;
    }

    public static uint RegEncoding(regNumber reg)
    {
        assert(((uint)REG_XMM0 & 0x7) == 0);
        return (uint)AbsRegNumber(reg) & 0x7;
    }
#endif
}
