// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.idAddrUnionTag;

namespace RyuJitSharp;

public enum idAddrUnionTag
{
    iaut_ALIGNED_POINTER = 0,
    iaut_DATA_OFFSET = 1,
    iaut_INST_COUNT = 2,
    iaut_UNUSED_TAG = 3,
    iaut_MASK = 3,
    iaut_SHIFT = 2,
}
