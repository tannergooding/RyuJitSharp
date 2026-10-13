// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Globalization;

namespace RyuJitSharp;

public static partial class Globals
{
    // Managed strings replace the native arena-owned diagnostic buffers.
    public static string regMaskToString(regMaskTP mask, Compiler context)
    {
        return string.Format(CultureInfo.InvariantCulture, RegisterDiagnosticFormats.All,
            Configuration.REGMASK_BITS_32 ? (object)unchecked((uint)mask.Lower) : unchecked((ulong)mask.Lower));
    }

    public static string regMaskIntToString(regMaskTP mask, Compiler context)
    {
        var registers = mask.Lower & (Configuration.TARGET_AMD64 ? SRBM_ALLINT_ALL : SRBM_ALLINT);
        return string.Format(CultureInfo.InvariantCulture, RegisterDiagnosticFormats.Int,
            Configuration.REGMASK_BITS_32 ? (object)unchecked((uint)registers) : unchecked((ulong)registers));
    }
}
#endif
