// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, optimizer.cpp and gentree.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTreeBoundsChk optRemoveRangeCheck(GenTreeBoundsChk check, GenTree? comma, Statement statement)
    {
        noway_assert((comma is not null && (comma.Oper is GT_COMMA) && (comma.AsOp().Op1 == check)) ||
            ((check.Oper is GT_BOUNDS_CHECK) && (comma is null)));
        noway_assert(check.Oper is GT_BOUNDS_CHECK);
        var tree = comma ?? check;

#if DEBUG
        if (verbose)
        {
            jitprintf("Before optRemoveRangeCheck:\n");
            gtDispTree(tree);
        }
#endif

        // A proved bounds check also proves the length load does not fault.
        GenTree? sideEffects = null;
        gtExtractSideEffList(check.ArrayLength, ref sideEffects, GTF_ASG);
        gtExtractSideEffList(check.Index, ref sideEffects);

        if (sideEffects is not null)
        {
            if (comma is not null)
            {
                comma.AsOp().Op1 = sideEffects;
            }
            else
            {
                statement.RootNode = sideEffects;
                tree = sideEffects;
            }
        }
        else
        {
            check.BashToNOP();
        }

        if (tree.Oper is GT_COMMA)
        {
            tree.Flags |= GTF_DONT_CSE;
        }

        gtUpdateSideEffects(statement, tree);

#if DEBUG
        if (verbose)
        {
            jitprintf($"After optRemoveRangeCheck for [{tree.TreeId:D6}]:\n");
            gtDispTree(statement.RootNode);
        }
#endif
        return check;
    }

    public void gtUpdateSideEffects(Statement statement, GenTree tree)
    {
        if (fgNodeThreading is NodeThreading.AllTrees)
        {
            gtUpdateTreeAncestorsSideEffects(statement, tree);
        }
        else
        {
            assert(fgNodeThreading is not NodeThreading.LIR);
            gtUpdateStmtSideEffects(statement);
        }
    }

    private void gtUpdateTreeAncestorsSideEffects(Statement statement, GenTree tree)
    {
        while (true)
        {
            gtUpdateNodeSideEffects(tree);
            var parent = gtFindLink(statement, tree).parent;
            if (parent is null)
            {
                noway_assert(tree == statement.RootNode);
                return;
            }

            tree = parent;
        }
    }
}
