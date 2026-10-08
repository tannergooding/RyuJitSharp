// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.varRefKinds;
using System;

namespace RyuJitSharp;

[Flags]
public enum varRefKinds
{
    VR_INVARIANT = 0x00,
    VR_NONE = 0x00,
    VR_IND_REF = 0x01,
    VR_IND_SCL = 0x02,
    VR_GLB_VAR = 0x04,
}
