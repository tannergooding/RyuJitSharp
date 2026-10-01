// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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

    private unsafe void emitDispInsAddr(byte* code)
    {
#if DEBUG
        assert(_compiler is not null);
        if (_compiler.opts.disAddr)
        {
            // FMT_ADDR/DBG_ADDR print the raw address, not the diffable FMT_PTR representation.
            var address = unchecked((nuint)code);
#if HOST_64BIT
            jitprintf($" {unchecked((uint)(address >> 32)):x8}`{unchecked((uint)address):x8} ");
#else
            jitprintf($" {unchecked((uint)address):x8} ");
#endif
        }
#endif
    }

    private void emitDispInsOffs(uint offset, bool display)
    {
        jitprintf(display ? $"{offset:X6}" : "      ");
    }
}
