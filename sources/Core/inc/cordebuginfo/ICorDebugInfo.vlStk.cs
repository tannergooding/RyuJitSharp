// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_STK -- Any 32 bit value which is on the stack
    // eg. [ESP+0x20], or [EBP-0x28]
    //
    // VLT_STK_BYREF -- the specified stack location contains the address of the variable
    // eg. mov EAX, [ESP+0x20]; [EAX]
    public struct vlStk
    {
        public RegNum vlsBaseReg;

        public int vlsOffset;
    }
}
