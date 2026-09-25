// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.EmitCallType;

namespace RyuJitSharp;

public enum EmitCallType
{
    EC_FUNC_TOKEN,
#if TARGET_XARCH
    EC_FUNC_TOKEN_INDIR,
#endif
    EC_INDIR_R,
#if TARGET_XARCH
    EC_INDIR_ARD,
#endif
    EC_COUNT,
}
