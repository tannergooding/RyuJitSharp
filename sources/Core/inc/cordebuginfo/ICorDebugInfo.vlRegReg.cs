// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_REG_REG -- TYP_LONG with both uint32_ts enregistred
    // eg. RBM_EAXEDX
    public struct vlRegReg
    {
        public RegNum vlrrReg1;

        public RegNum vlrrReg2;
    }
}
