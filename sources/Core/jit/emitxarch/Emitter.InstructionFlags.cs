// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    public static bool DoesWriteZeroFlagForResult(instruction ins)
    {
        // BSF/BSR set ZF from the source rather than the result.
        if (ins is INS_bsf or INS_bsr)
        {
            return false;
        }

        var flags = CodeGen.instInfo[(int)ins];

        return (flags & Writes_ZF) != 0;
    }
#endif
}
