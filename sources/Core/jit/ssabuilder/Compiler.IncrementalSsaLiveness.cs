// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool IsInsertedSsaLiveIn(BasicBlock block, int lclNum)
    {
        assert(lvaGetDesc(lclNum).lvInSsa);

        return (_insertedSsaLocalsLiveIn is not null) &&
            _insertedSsaLocalsLiveIn.ContainsKey(new BasicBlockLocalPair(block, lclNum));
    }

    public bool AddInsertedSsaLiveIn(BasicBlock block, int lclNum)
    {
        assert(block != fgFirstBB);

        _insertedSsaLocalsLiveIn ??= [];
        if (!_insertedSsaLocalsLiveIn.TryAdd(new BasicBlockLocalPair(block, lclNum), true))
        {
            return false;
        }

        JITDUMP($"Marked V{lclNum:D2} as live into {FMT_BB(block.bbNum)}\n");
        return true;
    }
}
