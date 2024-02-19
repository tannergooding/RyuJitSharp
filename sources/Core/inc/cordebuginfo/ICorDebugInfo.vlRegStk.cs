// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_REG_STK -- Partly enregistered TYP_LONG
    // eg { LowerDWord=EAX UpperDWord=[ESP+0x8] }
    public struct vlRegStk
    {
        public RegNum vlrsReg;

        public VlrsStk vlrsStk;

        public struct VlrsStk
        {
            public RegNum vlrssBaseReg;

            public int vlrssOffset;
        }
    }
}
