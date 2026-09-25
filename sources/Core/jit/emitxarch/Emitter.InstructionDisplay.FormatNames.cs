// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    public static string emitIfName(insFormat format) => emitIfName((uint)format);

    public static string emitIfName(uint format)
    {
        if (format < (uint)insFormat.IF_COUNT)
        {
            return ((insFormat)format).ToString();
        }

        return $"??{format.ToString(CultureInfo.InvariantCulture)}??";
    }
}
#endif
