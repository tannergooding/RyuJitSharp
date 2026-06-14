// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTreeBlk
{
    public const BlkOpKind BlkOpKindInvalid = BlkOpKind.BlkOpKindInvalid;
    public const BlkOpKind BlkOpKindLoop = BlkOpKind.BlkOpKindLoop;
    public const BlkOpKind BlkOpKindUnroll = BlkOpKind.BlkOpKindUnroll;
    public const BlkOpKind BlkOpKindUnrollMemmove = BlkOpKind.BlkOpKindUnrollMemmove;
#if TARGET_WASM
    public const BlkOpKind BlkOpKindNativeOpcode = BlkOpKind.BlkOpKindNativeOpcode;
#endif

    public enum BlkOpKind
    {
        BlkOpKindInvalid,
        BlkOpKindLoop,
        BlkOpKindUnroll,
        BlkOpKindUnrollMemmove,
#if TARGET_WASM
        BlkOpKindNativeOpcode,
#endif
    }
}
