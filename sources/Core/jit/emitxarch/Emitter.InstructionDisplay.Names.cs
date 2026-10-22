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

        switch (attr)
        {
            case EA_UNKNOWN:
            {
                return "";
            }

            case EA_1BYTE:
            {
                return "byte  ptr ";
            }

            case EA_2BYTE:
            {
                return "word  ptr ";
            }

            case EA_4BYTE:
            {
                return "dword ptr ";
            }

            case EA_8BYTE:
            {
                return "qword ptr ";
            }

            case EA_16BYTE:
            {
                return "xmmword ptr ";
            }

            case EA_32BYTE:
            {
                return "ymmword ptr ";
            }

            case EA_64BYTE:
            {
                return "zmmword ptr ";
            }

            case var _ when EA_IS_DSP_RELOC(attr):
            {
                return "rword ptr ";
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidDisplayOperandSize);
                return default!;
            }
        }
    }
}
