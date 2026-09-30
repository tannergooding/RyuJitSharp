// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private static readonly string[] s_sizeNames =
    [
        "byte  ptr ",
        "word  ptr ",
        "dword ptr ",
        "qword ptr ",
        "xmmword ptr ",
        "ymmword ptr ",
        "zmmword ptr ",
    ];

    public static string genSizeStr(emitAttr attr)
    {
        var size = (uint)EA_SIZE(attr);
        assert((size == 0 || BitOperations.IsPow2(size)) && size <= 64);

        if ((emitAttr)size == attr)
        {
            return size > 0 ? s_sizeNames[BitOperations.TrailingZeroCount(size)] : "";
        }
        else if (attr == EA_GCREF)
        {
            return "gword ptr ";
        }
        else if (attr == EA_BYREF)
        {
            return "bword ptr ";
        }
        else if (EA_IS_DSP_RELOC(attr))
        {
            return "rword ptr ";
        }
        else
        {
            assert(false, "Unexpected");
            return "unknw ptr ";
        }
    }
}
