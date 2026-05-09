// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private EHNodeDsc[] ehnNodes = [];

    public EHNodeDsc ehnNode(int id)
    {
        ref var node = ref ehnNodes[id];
        node ??= new EHNodeDsc();
        return node;
    }

    public int ehnNextId;
    public EHNodeDsc? ehnTree;

    public bool ehTableFinalized;

    /// <summary>Returns true if value is between [start..end).</summary>
    /// <param name="value"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public static bool jitIsBetween(int value, int start, int end)
        => (start <= value) && (value < end);

    /// <summary>Returns true if value is between [start..end].</summary>
    /// <param name="value"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public static bool jitIsBetweenInclusive(int value, int start, int end)
        => (start <= value) && (value <= end);

#if DEBUG
    public void dispIncomingEHClause(ushort num, in CORINFO_EH_CLAUSE clause)
    {
        jitprintf($"EH clause #{num}:\n");
        jitprintf($"  Flags:         0x{clause.Flags:X}");

        // Note: the flags field is kind of weird. It should be compared for equality to determine the type of clause, even though it looks like a bitfield.
        // In particular, CORINFO_EH_CLAUSE_NONE is zero, so you can't use "&" to check it.
        const CORINFO_EH_CLAUSE_FLAGS CORINFO_EH_CLAUSE_TYPE_MASK = (CORINFO_EH_CLAUSE_FLAGS)(0x7);

        switch (clause.Flags & CORINFO_EH_CLAUSE_TYPE_MASK)
        {
            case CORINFO_EH_CLAUSE_NONE:
            {
                jitprintf(" (catch)");
                break;
            }

            case CORINFO_EH_CLAUSE_FILTER:
            {
                jitprintf(" (filter)");
                break;
            }

            case CORINFO_EH_CLAUSE_FINALLY:
            {
                jitprintf(" (finally)");
                break;
            }

            case CORINFO_EH_CLAUSE_FAULT:
            {
                jitprintf(" (fault)");
                break;
            }

            default:
            {
                jitprintf($" (UNKNOWN type {clause.Flags & CORINFO_EH_CLAUSE_TYPE_MASK}!)");
                break;
            }
        }

        if ((clause.Flags & ~CORINFO_EH_CLAUSE_TYPE_MASK) != 0)
        {
            jitprintf($" (extra unknown bits: 0x{clause.Flags & ~CORINFO_EH_CLAUSE_TYPE_MASK:X})");
        }
        jitprintf("\n");

        jitprintf($"  TryOffset:     0x{clause.TryOffset:X}\n");
        jitprintf($"  TryLength:     0x{clause.TryLength:X}\n");
        jitprintf($"  HandlerOffset: 0x{clause.HandlerOffset:X}\n");
        jitprintf($"  HandlerLength: 0x{clause.HandlerLength:X}\n");

        if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
        {
            jitprintf($"  FilterOffset:  0x{clause.FilterOffset:X}\n");
        }
        else
        {
            jitprintf($"  ClassToken:    0x{clause.ClassToken:X}\n");
        }
    }
#endif

    /// <summary>Return the EH descriptor for the most nested filter or handler region this BasicBlock is a member of (or null if this block is not in a filter or handler region).</summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public ref EHblkDsc ehGetBlockHndDsc(BasicBlock block)
        => ref (block.hasHndIndex ? ref ehGetDsc(block.HndIndex) : ref Unsafe.NullRef<EHblkDsc>());

    /// <summary>Return the EH descriptor for the most nested 'try' region this BasicBlock is a member of (or null if this block is not in a 'try' region).</summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public ref EHblkDsc ehGetBlockTryDsc(BasicBlock block)
        => ref (block.hasTryIndex ? ref ehGetDsc(block.TryIndex) : ref Unsafe.NullRef<EHblkDsc>());

    /// <summary>Return the EH descriptor for the given region index.</summary>
    /// <param name="regionIndex"></param>
    /// <returns></returns>
    public ref EHblkDsc ehGetDsc(int regionIndex)
    {
        assert(regionIndex < compHndBBtabCount);
        return ref compHndBBtab[regionIndex];
    }

    /// <summary>Return the EH index given a region descriptor</summary>
    /// <param name="ehDsc"></param>
    /// <returns></returns>
    public int ehGetIndex(in EHblkDsc ehDsc)
    {
        assert(Unsafe.IsAddressLessThanOrEqualTo(in compHndBBtab[0], in ehDsc) && Unsafe.IsAddressLessThan(in ehDsc, in compHndBBtab[compHndBBtabCount]));
        var index = (int)(Unsafe.ByteOffset(in compHndBBtab[0], in ehDsc) / Unsafe.SizeOf<EHblkDsc>());

        assert(Unsafe.AreSame(in ehDsc, in compHndBBtab[index]));
        return index;
    }

    public ref EHblkDsc ehInitHndRange(BasicBlock blk, out IL_OFFSET hndBeg, out IL_OFFSET hndEnd, out bool inFilter)
    {
        ref var hndTab = ref ehGetBlockHndDsc(blk);

        if (!Unsafe.IsNullRef(in hndTab))
        {
            if (hndTab.InFilterRegionILRange(blk))
            {
                hndBeg = hndTab.ebdFilterBegOffs;
                hndEnd = hndTab.ebdFilterEndOffs;
                inFilter = true;
            }
            else
            {
                hndBeg = hndTab.ebdHndBegOffs;
                hndEnd = hndTab.ebdHndEndOffs;
                inFilter = false;
            }
        }
        else
        {
            hndBeg = 0;
            hndEnd = info.compILCodeSize;
            inFilter = false;
        }
        return ref hndTab;
    }

    public ref EHblkDsc ehInitTryRange(BasicBlock blk, out IL_OFFSET tryBeg, out IL_OFFSET tryEnd)
    {
        ref var tryTab = ref ehGetBlockTryDsc(blk);

        if (!Unsafe.IsNullRef(in tryTab))
        {
            tryBeg = tryTab.ebdTryBegOffs;
            tryEnd = tryTab.ebdTryEndOffs;
        }
        else
        {
            tryBeg = 0;
            tryEnd = info.compILCodeSize;
        }
        return ref tryTab;
    }

    // ToEHHandlerType: Convert a CORINFO_EH_CLAUSE_FLAGS value obtained from the VM in the EH clause structure
    // to the internal EHHandlerType type.
    public EHHandlerType ToEHHandlerType(CORINFO_EH_CLAUSE_FLAGS flags)
    {
        if ((flags & CORINFO_EH_CLAUSE_FAULT) != 0)
        {
            return EH_HANDLER_FAULT;
        }
        else if ((flags & CORINFO_EH_CLAUSE_FINALLY) != 0)
        {
            return EH_HANDLER_FINALLY;
        }
        else if ((flags & CORINFO_EH_CLAUSE_FILTER) != 0)
        {
            return EH_HANDLER_FILTER;
        }
        else
        {
            // If it's none of the others, assume it is a try/catch.
            // The VM (and apparently VC) stick in extra bits in the flags field. We ignore any flags we don't know about.
            return EH_HANDLER_CATCH;
        }
    }

    // Checks the following two conditions:
    // 1) If block A contains block B, A should also contain B's try/filter/handler.
    // 2) A block cannot contain its related try/filter/handler.
    // Both these conditions are checked by making sure that all the blocks for an
    // exception clause are at the same level.
    // The algorithm is: for each exception clause, determine the first block and
    // search through the next links for its corresponding try/handler/filter as the
    // case may be. If not found, then fail.
    public unsafe void verCheckNestingLevel(int initRootId)
    {
        var ehnNodeId = initRootId;

        for (var XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            var p1 = ehnNode(ehnNodeId++);
            var p2 = ehnNode(ehnNodeId++);

            // we are relying on the fact that ehn nodes are allocated sequentially.
            noway_assert(p1.ehnHandlerNode == p2);
            noway_assert(p2.ehnTryNode == p1);

            // arrange p1 and p2 in sequential order
            if (p1.ehnStartOffset == p2.ehnStartOffset)
            {
                BADCODE("shared exception handler");
            }

            if (p1.ehnStartOffset > p2.ehnStartOffset)
            {
                (p1, p2) = (p2, p1);
            }

            var temp = p1.ehnNext;
            var numSiblings = 0;
            var search = p2;

            if (search.ehnEquivalent is not null)
            {
                search = search.ehnEquivalent;
            }

            do
            {
                if (temp == search)
                {
                    numSiblings++;
                    break;
                }
                if (temp is not null)
                {
                    temp = temp.ehnNext;
                }
            } while (temp is not null);

            CORINFO_EH_CLAUSE clause;
            info.compCompHnd->getEHinfo(info.compMethodHnd, XTnum, &clause);

            if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
            {
                var p3 = ehnNode(ehnNodeId++);

                noway_assert(p3.ehnTryNode == p1 || p3.ehnTryNode == p2);
                noway_assert(p1.ehnFilterNode == p3 || p2.ehnFilterNode == p3);

                if (p3.ehnStartOffset < p1.ehnStartOffset)
                {
                    temp   = p3;
                    search = p1;
                }
                else if (p3.ehnStartOffset < p2.ehnStartOffset)
                {
                    temp   = p1;
                    search = p3;
                }
                else
                {
                    temp   = p2;
                    search = p3;
                }

                if (search.ehnEquivalent is not null)
                {
                    search = search.ehnEquivalent;
                }

                do
                {
                    if (temp == search)
                    {
                        numSiblings++;
                        break;
                    }
                    temp = temp.ehnNext;
                } while (temp is not null);
            }
            else
            {
                numSiblings++;
            }

            if (numSiblings != 2)
            {
                BADCODE("Outer block does not contain all code in inner handler");
            }
        }
    }

    // The following code checks the following rules for the EH table:
    //  1. Overlapping of try blocks not allowed.
    //  2. Handler blocks cannot be shared between different try blocks.
    //  3. Try blocks with Finally or Fault blocks cannot have other handlers.
    //  4. If block A contains block B, A should also contain B's try/filter/handler.
    //  5. A block cannot contain it's related try/filter/handler.
    //  6. Nested block must appear before containing block
    public void verInitEHTree(int numEHClauses)
    {
        ehnNodes = new EHNodeDsc[numEHClauses * 3];
        ehnNextId = 0;
        ehnTree = null;
    }

    /// <summary>Inserts the try, handler and filter (optional) clause information in a tree structure in order to catch incorrect eh formatting (e.g. illegal overlaps, incorrect order)</summary>
    /// <param name="clause"></param>
    /// <param name="handlerTab"></param>
    public void verInsertEhNode(in CORINFO_EH_CLAUSE clause, ref EHblkDsc handlerTab)
    {
        var tryNode = ehnNode(ehnNextId++);
        var handlerNode = ehnNode(ehnNextId++);
        var filterNode = null as EHNodeDsc;

        tryNode.ehnSetTryNodeType();
        tryNode.ehnStartOffset = clause.TryOffset;
        tryNode.ehnEndOffset = clause.TryOffset + clause.TryLength - 1;
        tryNode.ehnHandlerNode = handlerNode;

        if ((clause.Flags & CORINFO_EH_CLAUSE_FINALLY) != 0)
        {
            handlerNode.ehnSetFinallyNodeType();
        }
        else if ((clause.Flags & CORINFO_EH_CLAUSE_FAULT) != 0)
        {
            handlerNode.ehnSetFaultNodeType();
        }
        else
        {
            handlerNode.ehnSetHandlerNodeType();
        }

        handlerNode.ehnStartOffset = clause.HandlerOffset;
        handlerNode.ehnEndOffset = clause.HandlerOffset + clause.HandlerLength - 1;
        handlerNode.ehnTryNode = tryNode;

        if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
        {
            filterNode = ehnNode(ehnNextId++);
            filterNode.ehnStartOffset = clause.FilterOffset;

            var blk = handlerTab.BBFilterLast;
            assert(blk is not null);
            filterNode.ehnEndOffset = blk.bbCodeOffsEnd - 1;

            noway_assert(filterNode.ehnEndOffset != 0);
            filterNode.ehnSetFilterNodeType();
            filterNode.ehnTryNode = tryNode;
            tryNode.ehnFilterNode = filterNode;
        }

        verInsertEhNodeInTree(ref ehnTree, tryNode);
        verInsertEhNodeInTree(ref ehnTree, handlerNode);

        if (filterNode is not null)
        {
            verInsertEhNodeInTree(ref ehnTree, filterNode);
        }
    }

    public void verInsertEhNodeInTree(ref EHNodeDsc? root, EHNodeDsc node)
    {
        // The root node could be changed by this method.
        //
        // node is inserted to
        //   (a) right       of root (root.right       <-- node)
        //   (b) left        of root (node.right       <-- root; node becomes root)
        //   (c) child       of root (root.child       <-- node)
        //   (d) parent      of root (node.child       <-- root; node becomes root)
        //   (e) equivalent  of root (root.equivalent  <-- node)
        //
        // such that siblings are ordered from left to right
        // child parent relationship and equivalence relationship are not violated
        //
        //
        //  Here is a list of all possible cases
        //
        //  Case 1 2 3 4 5 6 7 8 9 10 11 12 13
        //
        //       | | | | |
        //       | | | | |
        //  .......|.|.|.|..................... [ root start ] .....
        //  |        | | | |             |  |
        //  |        | | | |             |  |
        // r|        | | | |          |  |  |
        // o|          | | |          |     |
        // o|          | | |          |     |
        // t|          | | |          |     |
        //  |          | | | |     |  |     |
        //  |          | | | |     |        |
        //  |..........|.|.|.|.....|........|.. [ root end ] ........
        //               | | | |
        //               | | | | |
        //               | | | | |
        //
        //      |<-- - - - n o d e - - - -->|
        //
        //
        // Case Operation
        // --------------
        //  1    (b)
        //  2    Error
        //  3    Error
        //  4    (d)
        //  5    (d)
        //  6    (d)
        //  7    Error
        //  8    Error
        //  9    (a)
        //  10   (c)
        //  11   (c)
        //  12   (c)
        //  13   (e)

        var nStart = node.ehnStartOffset;
        var nEnd = node.ehnEndOffset;

        if (nStart > nEnd)
        {
            BADCODE("start offset greater or equal to end offset");
        }

        node.ehnNext = null;
        node.ehnChild = null;
        node.ehnEquivalent = null;

        while (true)
        {
            if (root is null)
            {
                root = node;
                break;
            }

            var rStart = root.ehnStartOffset;
            var rEnd = root.ehnEndOffset;

            if (nStart < rStart)
            {
                // Case 1
                if (nEnd < rStart)
                {
                    // Left sibling
                    node.ehnNext = root;
                    root = node;
                    return;
                }

                // Case 2, 3
                if (nEnd < rEnd)
                {
                    // [Error]
                    BADCODE("Overlapping try regions");
                }

                // Case 4, 5: [Parent]
                verInsertEhNodeParent(ref root, node);
                return;
            }

            // Cases 6-13 (nStart >= rStart)

            if (nEnd > rEnd)
            {
                // Case 9
                if (nStart > rEnd)
                {
                    // [RightSibling]
                    // Recurse with Root.Sibling as the new root

                    root = ref root.ehnNext;
                    continue;
                }

                // Case 6
                if (nStart == rStart)
                {
                    // [Parent]
                    if (node.ehnIsTryBlock || root.ehnIsTryBlock)
                    {
                        verInsertEhNodeParent(ref root, node);
                        return;
                    }

                    // non try blocks are not allowed to start at the same offset
                    BADCODE("Handlers start at the same offset");
                }

                // Case 7, 8
                BADCODE("Overlapping try regions");
            }

            // Case 10-13 (nStart >= rStart && nEnd <= rEnd)
            if ((nStart != rStart) || (nEnd != rEnd))
            {
                // Cases 10-12: [Child]
                if (root.ehnIsTryBlock)
                {
                    BADCODE("Inner try appears after outer try in exception handling table");
                }
                else
                {
                    // We have an EH clause nested within a handler, but the parent
                    // handler clause came first in the table. The rest of the compiler
                    // doesn't expect this, so sort the EH table.

                    fgNeedToSortEHTable = true;

                    // Case 12 (nStart == rStart)
                    // non try blocks are not allowed to start at the same offset
                    if ((nStart == rStart) && !node.ehnIsTryBlock)
                    {
                        BADCODE("Handlers start at the same offset");
                    }

                    // check this!
                    root = ref root.ehnChild;
                    continue;
                }
            }

            // Case 13: [Equivalent]
            if (!node.ehnIsTryBlock && !root.ehnIsTryBlock)
            {
                BADCODE("Handlers cannot be shared");
            }

            if (!node.ehnIsTryBlock || !root.ehnIsTryBlock)
            {
                // Equivalent is only allowed for try bodies
                // If one is a handler, this means the nesting is wrong
                BADCODE("Handler and try with the same offset");
            }

            node.ehnNext = root;
            node.ehnEquivalent = root;

            // check that the corresponding handler is either a catch handler or a filter

            var nodeHandlerNode = node.ehnHandlerNode;
            assert(nodeHandlerNode is not null);

            var rootHandlerNode = root.ehnHandlerNode;
            assert(rootHandlerNode is not null);

            if (nodeHandlerNode.ehnIsFaultBlock || nodeHandlerNode.ehnIsFinallyBlock ||
                rootHandlerNode.ehnIsFaultBlock || rootHandlerNode.ehnIsFinallyBlock)
            {
                BADCODE("Try block with multiple non-filter/non-handler blocks");
            }

            break;
        }
    }

    /// <summary>Make node the parent of root</summary>
    /// <param name="root"></param>
    /// <param name="node"></param>
    /// <remarks>All siblings of root that are fully or partially nested in node remain siblings of root</remarks>
    public void verInsertEhNodeParent(ref EHNodeDsc root, EHNodeDsc node)
    {
        noway_assert(node.ehnNext is null);
        noway_assert(node.ehnChild is null);

        // Root is nested in Node
        noway_assert(node.ehnStartOffset <= root.ehnStartOffset);
        noway_assert(node.ehnEndOffset >= root.ehnEndOffset);

        // Root is not the same as Node
        noway_assert(node.ehnStartOffset != root.ehnStartOffset || node.ehnEndOffset != root.ehnEndOffset);

        if (node.ehnIsFilterBlock)
        {
            BADCODE("Protected block appearing within filter block");
        }

        var lastChild = null as EHNodeDsc;
        var sibling = root.ehnNext;

        while (sibling is not null)
        {
            // siblings are ordered left to right, largest right.
            // nodes have a width of at least one.
            // Hence sibling start will always be after Node start.

            noway_assert(sibling.ehnStartOffset > node.ehnStartOffset);

            // (1): disjoint
            if (sibling.ehnStartOffset > node.ehnEndOffset)
            {
                break;
            }

            // (2): partial containment.
            if (sibling.ehnEndOffset > node.ehnEndOffset)
            {
                BADCODE("Overlapping try regions");
            }

            // else full containment (follows from (1) and (2))
            lastChild = sibling;
            sibling = sibling.ehnNext;
        }

        // All siblings of Root up to and including lastChild will continue to be
        // siblings of Root (and children of Node). The node to the right of
        // lastChild will become the first sibling of Node.

        if (lastChild is not null)
        {
            // Node has more than one child including Root
            node.ehnNext = lastChild.ehnNext;
            lastChild.ehnNext = null;
        }
        else
        {
            // Root is the only child of Node
            node.ehnNext = root.ehnNext;
            root.ehnNext = null;
        }

        node.ehnChild = root;
        root = node;
    }
}
