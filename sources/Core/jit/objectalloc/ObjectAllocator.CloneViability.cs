// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private bool CloneOverlaps(CloneInfo info)
    {
        var traits = new BitVecTraits(CompilerInstance, _initialMaxBlockID);

        foreach (var other in _cloneMap.Values)
        {
            if ((other == info) || !other.WillClone)
            {
                continue;
            }

            if (BitVecOps.IsEmptyIntersection(traits, info.Blocks, other.Blocks))
            {
                continue;
            }

            JITDUMP("Cloned blocks for");
#if DEBUG
            if (CompilerInstance.verbose)
            {
                DumpIndex(info.PseudoIndex);
            }
#endif
            JITDUMP(" overlap with those for");
#if DEBUG
            if (CompilerInstance.verbose)
            {
                DumpIndex(other.PseudoIndex);
            }
#endif
            JITDUMP(" unable to clone\n");
            return true;
        }

        return false;
    }

    private bool ShouldClone(CloneInfo info)
    {
        var sizeConfig = JitConfig.JitCloneLoopsSizeLimit;
        var sizeLimit = sizeConfig >= 0 ? (uint)sizeConfig : uint.MaxValue;
        var size = 0u;
        var blocksToClone = info.BlocksToClone ?? throw new InvalidOperationException("Clone extent was not computed.");

        foreach (var block in blocksToClone)
        {
            var slack = unchecked(sizeLimit - size);
            if (CloneBlockComplexityExceeds(block, slack, ref size))
            {
                JITDUMP("Rejecting");
#if DEBUG
                if (CompilerInstance.verbose)
                {
                    DumpIndex(info.PseudoIndex);
                }
#endif
                JITDUMP($" cloning: exceeds size limit {sizeLimit}\n");
                return false;
            }
        }

        JITDUMP("Accepting");
#if DEBUG
        if (CompilerInstance.verbose)
        {
            DumpIndex(info.PseudoIndex);
        }
#endif
        JITDUMP($" cloning: size {size} does not exceed size limit {sizeLimit}\n");
        return true;
    }

    private bool CloneBlockComplexityExceeds(BasicBlock block, uint limit, ref uint size)
    {
        // This is BasicBlock::ComplexityExceeds (compiler.hpp) for the node-count callback.
        var compiler = CompilerInstance;
        var localCount = 0u;
        foreach (var stmt in block.Statements)
        {
            var slack = unchecked(limit - localCount);
            var exceeded = compiler.gtComplexityExceeds(stmt.RootNode, slack, _ => {
                localCount++;
                return 1;
            });
            if (exceeded)
            {
                size = unchecked(size + localCount);
                return true;
            }
        }

        size = unchecked(size + localCount);
        return false;
    }

    private bool CanClone(CloneInfo info)
    {
        if (!info.CheckedCanClone)
        {
            _ = CheckCanClone(info);
            info.CheckedCanClone = true;
        }

        return info.CanClone;
    }

    private static unsafe GenTree? IsGuard(BasicBlock block, GuardInfo info)
    {
        if (block.Kind is not BBJ_COND)
        {
            JITDUMP("... not BBJ_COND\n");
            return null;
        }

        var stmt = block.LastStmt;
        if (stmt is null)
        {
            JITDUMP("... no stmt\n");
            return null;
        }

        var jumpTree = stmt.RootNode;
        if (jumpTree.Oper is not GT_JTRUE)
        {
            JITDUMP("... no JTRUE\n");
            return null;
        }

        var tree = jumpTree.AsOp().Op1;
        if (tree.Oper is not (GT_NE or GT_EQ))
        {
            JITDUMP("... not NE/EQ\n");
            return null;
        }

        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;
        if (op1.Oper is not GT_IND)
        {
            (op1, op2) = (op2, op1);
        }

        if (op1.Oper is not GT_IND)
        {
            JITDUMP("... no JTRUE(cmp(ind, ...))\n");
            return null;
        }

        if (op1.Type is not TYP_I_IMPL)
        {
            JITDUMP("... no JTRUE(cmp(ind:int, ...))\n");
            return null;
        }

        var addr = op1.AsIndir().Addr;
        if (addr.Type is not TYP_REF)
        {
            JITDUMP("... no JTRUE(cmp(ind:int(*:ref), ...))\n");
            return null;
        }

        if (addr.Oper is not GT_LCL_VAR)
        {
            JITDUMP("... no JTRUE(cmp(ind:int(lcl:ref), ...))\n");
            return null;
        }

        if (!op2.Oper.IsCnsIntOrI || !op2.AsIntCon().IsIconHandle(GTF_ICON_CLASS_HDL))
        {
            JITDUMP("... no JTRUE(cmp(ind:int(lcl:ref), clsHnd))\n");
            return null;
        }

        info.Local = addr.AsLclVar().LclNum;
        info.Type = (CORINFO_CLASS_HANDLE)op2.AsIntCon().CompileTimeHandle;
        info.Block = block;
        info.Stmt = stmt;
        info.Relop = tree;
        JITDUMP($"... {FMT_BB(block.bbNum)} is guard for V{info.Local:D2}\n");

        return tree;
    }

    private static Statement LatestStatement(Statement first, Statement second)
    {
        if (first == second)
        {
            return first;
        }

        var cursor1 = first.NextStmt;
        var cursor2 = second.NextStmt;
        while (true)
        {
            if ((cursor1 == second) || (cursor2 is null))
            {
                return second;
            }

            if ((cursor2 == first) || (cursor1 is null))
            {
                return first;
            }

            cursor1 = cursor1.NextStmt;
            cursor2 = cursor2.NextStmt;
        }
    }
}
