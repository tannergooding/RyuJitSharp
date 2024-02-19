// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_FPSTK -- enregisterd TYP_DOUBLE (on the FP stack)
    // eg. ST(3). Actually it is ST("FPstkHeight - vpFpStk")
    public struct vlFPstk
    {
        public uint vlfReg;
    }
}
