// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class Lowering
{
    private const int POST_INDEXED_ADDRESSING_MAX_DISTANCE = 16;

    private bool TryMoveAddSubRMWAfterIndir(GenTreeLclVarCommon store)
    {
        if (store.Oper is not GT_STORE_LCL_VAR)
        {
            return false;
        }

        var localNumber = store.LclNum;
        if (CompilerInstance.lvaGetDesc(localNumber).lvDoNotEnregister)
        {
            return false;
        }

        var data = store.Op1;
        if ((data.Oper is not (GT_ADD or GT_SUB)) || data.HasOverflowCheck)
        {
            return false;
        }

        var op1 = data.AsOp().Op1;
        var op2 = data.AsOp().Op2;
        if ((op1.Oper is not GT_LCL_VAR) || !op2.IsContainedIntOrIImmed)
        {
            return false;
        }
        if (op1.AsLclVarCommon().LclNum != localNumber)
        {
            return false;
        }

        var maxCount = int.Min(_blockIndirs.Count, POST_INDEXED_ADDRESSING_MAX_DISTANCE / 2);
        for (var i = 0; i < maxCount; i++)
        {
            var savedIndex = _blockIndirs.Count - 1 - i;
            var previous = _blockIndirs[savedIndex];
            if ((previous.AddrBase.LclNum != localNumber) || (previous.Offset != 0))
            {
                continue;
            }

            var prevIndir = previous.Indir;
            if ((prevIndir is null) || (prevIndir.Next is null))
            {
                continue;
            }

#if DEBUG
            JITDUMP($"[{store.TreeId:D6}] is an an RMW ADD/SUB on local V{localNumber:D2} " +
                $"which is used as the address to [{prevIndir.TreeId:D6}]. Trying to make them adjacent.\n");
#endif
            if (TryMakeIndirAndStoreAdjacent(prevIndir, store))
            {
                previous.Indir = null;
                _blockIndirs[savedIndex] = previous;
                return true;
            }
        }

        return false;
    }

    private bool TryMakeIndirAndStoreAdjacent(GenTreeIndir prevIndir, GenTreeLclVarCommon store)
    {
        GenTree? current = prevIndir;
        for (var i = 0; i < POST_INDEXED_ADDRESSING_MAX_DISTANCE; i++)
        {
            assert(current is not null);
            assert((current._lirFlags & LIR.Flags.Mark) == 0);
            current = current.Next;
            if (current == store)
            {
                break;
            }
        }

        if (current != store)
        {
            JITDUMP("  Too far separated, giving up\n");
            return false;
        }

        JITDUMP("  They are close. Trying to move the following range (where * are nodes part of the data flow):\n\n");
#if DEBUG
        var startDumpNode = BlockRange().GetTreeRange(prevIndir, out _).FirstNode;
        var endDumpNode = store.Next;
        void DumpWithMarks()
        {
            if (!CompilerInstance.verbose)
            {
                return;
            }

            for (var node = startDumpNode; node != endDumpNode; node = node.Next)
            {
                assert(node is not null);
                var prefix = node == prevIndir ? "1. " :
                    node == store ? "2. " : (node._lirFlags & LIR.Flags.Mark) != 0 ? "*  " : "   ";
                CompilerInstance.gtDispLIRNode(node, prefix);
            }
        }
#endif

        try
        {
            MarkTree(store);
#if DEBUG
            DumpWithMarks();
#endif
            JITDUMP("\n");
            assert((prevIndir._lirFlags & LIR.Flags.Mark) == 0);
            _scratchSideEffects.Clear();
            for (var node = prevIndir.Next; node != store; node = node.Next)
            {
                assert(node is not null);
                if ((node._lirFlags & LIR.Flags.Mark) != 0)
                {
                    if (_scratchSideEffects.InterferesWith(CompilerInstance, node, true))
                    {
#if DEBUG
                        JITDUMP($"Giving up due to interference with [{node.TreeId:D6}]\n");
#endif
                        return false;
                    }
                }
                else
                {
                    _scratchSideEffects.AddNode(CompilerInstance, node);
                }
            }

            if (_scratchSideEffects.InterferesWith(CompilerInstance, store, true))
            {
                JITDUMP("Have interference. Giving up.\n");
                return false;
            }

#if DEBUG
            JITDUMP($"Interference checks passed. Moving nodes that are not part of data flow of [{store.TreeId:D6}]\n\n");
#endif
            GenTree previous = prevIndir;
            for (var node = prevIndir.Next; ;)
            {
                assert(node is not null);
                var next = node.Next;
                if ((node._lirFlags & LIR.Flags.Mark) != 0)
                {
                    BlockRange().Remove(node);
                    BlockRange().InsertAfter(previous, node);
                    previous = node;
                }

                if (node == store)
                {
                    break;
                }

                node = next;
            }

            JITDUMP("Result:\n\n");
#if DEBUG
            DumpWithMarks();
#endif
            JITDUMP("\n");
            return true;
        }
        finally
        {
            UnmarkTree(store);
        }
    }
}
#endif
