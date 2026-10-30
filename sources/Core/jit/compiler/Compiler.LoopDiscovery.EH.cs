// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    public ushort ehTrueEnclosingTryIndex(ushort regionIndex)
    {
        assert(regionIndex != EHblkDsc.NO_ENCLOSING_INDEX);
        assert(fgImportDone);

        ref var root = ref ehGetDsc(regionIndex);
        for (;;)
        {
            regionIndex = ehGetDsc(regionIndex).ebdEnclosingTryIndex;
            if (regionIndex == EHblkDsc.NO_ENCLOSING_INDEX)
            {
                break;
            }

            if (!EHblkDsc.ebdIsSameTry(root, ehGetDsc(regionIndex)))
            {
                break;
            }
        }

        return regionIndex;
    }

    public BasicBlock fgNewBBatTryRegionEnd(BBKinds jumpKind, ushort tryIndex)
    {
        ref var clause = ref ehGetDsc(tryIndex);
        var oldTryBeg = clause.ebdTryBeg;
        var oldTryLast = clause.ebdTryLast;
        var newBlock = fgNewBBafter(jumpKind, oldTryLast, false);
        newBlock.TryIndex = tryIndex;
        newBlock.copyHndIndex(oldTryBeg);

        for (var index = (int)tryIndex; index < compHndBBtabCount; index++)
        {
            ref var current = ref ehGetDsc((ushort)index);
            if (current.ebdTryLast != oldTryLast)
            {
                break;
            }

            assert((index == tryIndex) || (index == ehGetEnclosingTryIndex((ushort)(index - 1))));
            fgSetTryEnd(ref current, newBlock);
        }

        assert(newBlock.TryIndex == tryIndex);
        assert(BasicBlock.sameHndRegion(newBlock, oldTryBeg));
        return newBlock;
    }

    public void fgSetEHRegionForNewPreheaderOrExit(BasicBlock block)
    {
        var next = block.Next;
        assert(next is not null);
        if (bbIsTryBeg(next))
        {
            assert(next.hasTryIndex);
            var newTryIndex = ehTrueEnclosingTryIndex(next.TryIndex);
            if (newTryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
            {
                block.clearTryIndex();
            }
            else
            {
                block.TryIndex = newTryIndex;
            }

            block.copyHndIndex(next);
        }
        else
        {
            fgExtendEHRegionBefore(next);
        }
    }
}
