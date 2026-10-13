// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static sbyte encodeRegAsIval(regNumber opReg)
    {
        assert(isLowSimdReg(opReg) || isMaskReg(opReg));
        var ival = (nint)opReg;
        assert((ival >= 0) && (ival <= 0xFF));

        return unchecked((sbyte)ival);
    }
}
