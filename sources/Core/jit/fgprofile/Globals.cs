// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public static int EfficientEdgeCountBlockToKey(BasicBlock block)
    {
        // EH normalization can add internal blocks without IL offsets. Their block
        // numbers occupy a separate key space selected by the high bit.
        return block.HasFlag(BBF_INTERNAL) ? block.bbNum | int.MinValue : block.bbCodeOffs;
    }
}
