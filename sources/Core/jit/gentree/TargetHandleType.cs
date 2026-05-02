// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
global using static RyuJitSharp.TargetHandleType;

namespace RyuJitSharp;

// TargetHandleTypes are used to determine the type of handle present inside GenTreeIntCon node.
// The values are such that they don't overlap with helper's or user function's handle.
public enum TargetHandleType : byte
{
    THT_Unknown = 2,
    THT_GSCookieCheck = 4,
    THT_SetGSCookie = 6,
    THT_InitializeArrayIntrinsics = 8
}
#endif
