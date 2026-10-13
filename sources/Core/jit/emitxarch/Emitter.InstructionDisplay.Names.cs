// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    private ReadOnlySpan<char> emitDisplayName(instrDesc id) => codeGen.genInsDisplayName(id);

    private static string emitSizeString(emitAttr attr)
    {
        if (attr == EA_GCREF)
        {
            return "gword ptr ";
        }

        if (attr == EA_BYREF)
        {
            return "bword ptr ";
        }

        return attr switch
        {
            EA_UNKNOWN => "",
            EA_1BYTE => "byte  ptr ",
            EA_2BYTE => "word  ptr ",
            EA_4BYTE => "dword ptr ",
            EA_8BYTE => "qword ptr ",
            EA_16BYTE => "xmmword ptr ",
            EA_32BYTE => "ymmword ptr ",
            EA_64BYTE => "zmmword ptr ",
            _ when EA_IS_DSP_RELOC(attr) => "rword ptr ",
            _ => throw new FatalJitException("Invalid display operand size."),
        };
    }
}
