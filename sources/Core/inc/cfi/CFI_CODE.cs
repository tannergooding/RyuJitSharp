// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_CFI_SUPPORT
using System.Runtime.InteropServices;

namespace RyuJitSharp;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 8)]
public struct CFI_CODE
{
    public byte CodeOffset;
    public byte CfiOpCode;
    public short DwarfReg;
    public int Offset;

    public CFI_CODE(byte codeOffset, byte cfiOpcode, short dwarfReg, int offset)
    {
        CodeOffset = codeOffset;
        CfiOpCode = cfiOpcode;
        DwarfReg = dwarfReg;
        Offset = offset;
    }
}
#endif
