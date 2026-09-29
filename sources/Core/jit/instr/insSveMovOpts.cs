// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public enum insSveMovOpts : uint
{
    INS_SVE_MOV_OPTS_UNPRED,
    INS_SVE_MOV_OPTS_ZEROING,
    INS_SVE_MOV_OPTS_MERGING,
}
#endif
