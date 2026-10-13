// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;
using System.Text;

namespace RyuJitSharp;

public static partial class Globals
{
    private static class RegisterDiagnosticFormats
    {
        internal static readonly CompositeFormat All = CompositeFormat.Parse(REG_MASK_ALL_FMT);
        internal static readonly CompositeFormat Int = CompositeFormat.Parse(REG_MASK_INT_FMT);
    }

    public static void printRegMask(regMaskTP mask) =>
        jitprintf(string.Format(CultureInfo.InvariantCulture, RegisterDiagnosticFormats.All, unchecked((ulong)mask.Lower)));

    public static void printRegMaskInt(regMaskTP mask)
    {
        var registers = mask.Lower & (Configuration.TARGET_AMD64 ? SRBM_ALLINT_ALL : SRBM_ALLINT);
        jitprintf(string.Format(CultureInfo.InvariantCulture, RegisterDiagnosticFormats.Int, (ulong)registers));
    }
}
