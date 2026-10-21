// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static regNumber AbsRegNumber(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException("Absolute xarch register numbering is not available for this target.");
            return default!;
        }

        assert(Globals.genIsValidReg(reg));
        if (Configuration.TARGET_AMD64 && (uint)reg >= (uint)REG_X64_K0)
        {
            return (regNumber)((uint)reg - (uint)REG_X64_K0);
        }

        if (Configuration.TARGET_X86 && (uint)reg >= (uint)REG_X86_K0)
        {
            return (regNumber)((uint)reg - (uint)REG_X86_K0);
        }

        var floatingRegisterFirst = TargetRuntimeMetadata.FloatingRegisterFirst;
        if ((uint)reg >= (uint)floatingRegisterFirst)
        {
            return (regNumber)((uint)reg - (uint)floatingRegisterFirst);
        }

        return reg;
    }

    public static bool IsXMMReg(regNumber reg)
    {
        return Configuration.TARGET_XARCH &&
            (reg >= TargetRuntimeMetadata.FloatingRegisterFirst) &&
            (reg <= TargetRuntimeMetadata.FloatingRegisterLast);
    }

    public static uint HighAwareRegEncoding(regNumber reg)
    {
        assert(((uint)TargetRuntimeMetadata.FloatingRegisterFirst & 0x7) == 0);
        return (uint)AbsRegNumber(reg) & 0xF;
    }

    public static uint RegEncoding(regNumber reg)
    {
        assert(((uint)TargetRuntimeMetadata.FloatingRegisterFirst & 0x7) == 0);
        return (uint)AbsRegNumber(reg) & 0x7;
    }
}
