// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
global using static RyuJitSharp.insFlags;

namespace RyuJitSharp;

public enum insFlags : uint
{
    INS_FLAGS_NOT_SET = 0x00,
    INS_FLAGS_SET = 0x01,
    INS_FLAGS_DONT_CARE = 0x02,
}
#endif
