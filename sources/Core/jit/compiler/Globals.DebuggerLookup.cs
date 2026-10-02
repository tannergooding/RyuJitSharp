// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
#if DEBUG
    // Debugger lookups publish these references for watch windows and data breakpoints.
    public static GenTree? dbTree;
    public static Statement? dbStmt;
    public static BasicBlock? dbTreeBlock;
    public static BasicBlock? dbBlock;
    public static FlowGraphNaturalLoop? dbLoop;

    public static GenTree? dFindTreeInTree(GenTree? tree, uint id)
    {
        if (tree is null)
        {
            return null;
        }

        if (unchecked((uint)tree.TreeId) == id)
        {
            dbTree = tree;
            return tree;
        }

        GenTree? child = null;
        _ = tree.VisitOperands(operand =>
        {
            // The pinned native implementation recurses on child, not operand.
            child = dFindTreeInTree(child, id);
            return child is not null ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue;
        });

        return child;
    }

    public static GenTree? dFindTree(uint id)
    {
        var comp = GetDebuggerCompiler();
        dbTreeBlock = null;
        dbTree = null;

        foreach (var block in comp.Blocks)
        {
            foreach (var stmt in block.Statements)
            {
                var tree = dFindTreeInTree(stmt.RootNode, id);
                if (tree is not null)
                {
                    dbTreeBlock = block;
                    dbTree = tree;
                    return tree;
                }
            }
        }

        return null;
    }

    public static Statement? dFindStmt(uint id)
    {
        var comp = GetDebuggerCompiler();
        dbStmt = null;

        uint stmtId = 0;
        foreach (var block in comp.Blocks)
        {
            foreach (var stmt in block.Statements)
            {
                stmtId = unchecked(stmtId + 1);
                if (stmtId == id)
                {
                    dbStmt = stmt;
                    return stmt;
                }
            }
        }

        return null;
    }

    public static BasicBlock? dFindBlock(uint bbNum)
    {
        var comp = GetDebuggerCompiler();
        dbBlock = null;

        foreach (var block in comp.Blocks)
        {
            if (unchecked((uint)block.bbNum) == bbNum)
            {
                dbBlock = block;
                return block;
            }
        }

        return null;
    }

    public static FlowGraphNaturalLoop? dFindLoop(uint index)
    {
        var comp = GetDebuggerCompiler();
        dbLoop = null;

        if ((comp._loops is null) || (index >= (uint)comp._loops.NumLoops))
        {
            jitprintf($"Index {index} out of range\n");
            return null;
        }

        dbLoop = comp._loops.GetLoopByIndex((int)index);
        return dbLoop;
    }
#endif
}
