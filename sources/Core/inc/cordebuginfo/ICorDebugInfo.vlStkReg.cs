// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_STK_REG -- Partly enregistered TYP_LONG
    // eg { LowerDWord=[ESP+0x8] UpperDWord=EAX }
    public struct vlStkReg
    {
        public VlsrStk vlsrStk;

        public RegNum vlsrReg;

        public struct VlsrStk
        {
            public RegNum vlsrsBaseReg;

            public int vlsrsOffset;
        }
    }
}
