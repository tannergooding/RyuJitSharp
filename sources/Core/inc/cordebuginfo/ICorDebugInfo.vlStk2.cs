// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_STK2 -- Any 64 bit value which is on the stack, in 2 successive dwords.
    // eg 2 DWords at [ESP+0x10]
    public struct vlStk2
    {
        public RegNum vls2BaseReg;

        public int vls2Offset;
    }
}
