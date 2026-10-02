// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Globalization;
#if REGMASK_BITS_8
using MaskFormattingBits = System.Byte;
#elif REGMASK_BITS_16
using MaskFormattingBits = System.UInt16;
#elif REGMASK_BITS_32
using MaskFormattingBits = System.UInt32;
#else
using MaskFormattingBits = System.UInt64;
#endif

namespace RyuJitSharp;

public static partial class Globals
{
    // Managed strings replace the native arena-owned diagnostic buffers.
    public static string regMaskToString(regMaskTP mask, Compiler context)
    {
        return string.Format(CultureInfo.InvariantCulture, s_regMaskAllFormat,
            unchecked((MaskFormattingBits)mask.Lower));
    }

    public static string regMaskIntToString(regMaskTP mask, Compiler context)
    {
#if TARGET_AMD64
        var registers = mask.Lower & SRBM_ALLINT_ALL;
#else
        var registers = mask.Lower & SRBM_ALLINT;
#endif
        return string.Format(CultureInfo.InvariantCulture, s_regMaskIntFormat,
            unchecked((MaskFormattingBits)registers));
    }
}
#endif
