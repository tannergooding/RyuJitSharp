// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;
using System.Text;

namespace RyuJitSharp;

public static partial class Globals
{
    private static readonly CompositeFormat s_regMaskAllFormat = CompositeFormat.Parse(REG_MASK_ALL_FMT);
    private static readonly CompositeFormat s_regMaskIntFormat = CompositeFormat.Parse(REG_MASK_INT_FMT);

    public static void printRegMask(regMaskTP mask) =>
        jitprintf(string.Format(CultureInfo.InvariantCulture, s_regMaskAllFormat, (ulong)mask.Lower));

    public static void printRegMaskInt(regMaskTP mask)
    {
#if TARGET_AMD64
        var registers = mask.Lower & SRBM_ALLINT_ALL;
#else
        var registers = mask.Lower & SRBM_ALLINT;
#endif
        jitprintf(string.Format(CultureInfo.InvariantCulture, s_regMaskIntFormat, (ulong)registers));
    }
}
