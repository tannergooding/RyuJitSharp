// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgInDifferentRegions(BasicBlock blk1, BasicBlock blk2)
    {
        noway_assert(blk1 is not null);
        noway_assert(blk2 is not null);

        if (fgFirstColdBlock is null)
        {
            return false;
        }

        return blk1.HasFlag(BBF_COLD) != blk2.HasFlag(BBF_COLD);
    }
}
