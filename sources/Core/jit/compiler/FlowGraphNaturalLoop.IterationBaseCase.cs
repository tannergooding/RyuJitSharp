// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class FlowGraphNaturalLoop
{
    private bool FindConstInit(BasicBlock preheader, NaturalLoopIterInfo info)
    {
        var compiler = _dfsTree.GetCompiler();
        for (var block = preheader; block is not null; block = block.GetUniquePred(compiler))
        {
            var stmt = block.LastStmt;
            while (stmt is not null)
            {
                var tree = stmt.RootNode;
                if (tree.Oper is GT_STORE_LCL_VAR)
                {
                    var store = tree.AsLclVarCommon();
                    var data = store.Data;
                    if (store.LclNum == info.IterVar && data.Oper.IsCnsIntOrI && data.Type is TYP_INT)
                    {
                        info.HasConstInit = true;
                        info.ConstInitValue = unchecked((int)data.AsIntCon().IconValue);
#if DEBUG
                        info.InitTree = tree;
#endif
                        return true;
                    }
                }

                if (compiler.gtTreeHasLocalStore(tree, info.IterVar))
                {
                    return false;
                }

                if (stmt == block.FirstStmt)
                {
                    break;
                }
                stmt = stmt.PrevStmt;
            }
        }
        return false;
    }

    private static bool EvaluateRelop(int left, int right, genTreeOps oper)
    {
        return oper switch
        {
            GT_EQ => left == right,
            GT_NE => left != right,
            GT_LT => left < right,
            GT_LE => left <= right,
            GT_GT => left > right,
            GT_GE => left >= right,
            _ => throw new FatalJitException($"Unexpected loop comparison: {oper}"),
        };
    }

    private static bool EvaluateRelop(uint left, uint right, genTreeOps oper)
    {
        return oper switch
        {
            GT_EQ => left == right,
            GT_NE => left != right,
            GT_LT => left < right,
            GT_LE => left <= right,
            GT_GT => left > right,
            GT_GE => left >= right,
            _ => throw new FatalJitException($"Unexpected loop comparison: {oper}"),
        };
    }

    private bool CheckLoopConditionBaseCase(BasicBlock preheader, NaturalLoopIterInfo info)
    {
        if (info.HasConstInit && info.HasConstLimit)
        {
            var init = info.ConstInitValue;
            var limit = info.ConstLimit();
            assert(info.TestTree is not null && info.TestTree.AsOp().Op1.Type.ActualType is TYP_INT);
            var isTrue = info.TestTree.AsOp().IsUnsigned
                ? EvaluateRelop(unchecked((uint)init), unchecked((uint)limit), info.TestOper())
                : EvaluateRelop(init, limit, info.TestOper());
            if (isTrue)
            {
                JITDUMP($"  Condition is trivially true on entry ({init} " +
                    $"{(info.TestTree.AsOp().IsUnsigned ? "(uns)" : "")}{info.TestOper()} {limit})\n");
                return true;
            }
        }
        return HasZeroTripTest(preheader, info);
    }

    private bool HasZeroTripTest(BasicBlock preheader, NaturalLoopIterInfo info)
    {
        assert(preheader.Kind is not BBJ_COND);
        var compiler = _dfsTree.GetCompiler();
        var block = preheader;
        while (true)
        {
            foreach (var stmt in block.Statements)
            {
                var tree = stmt.RootNode;
                if (compiler.gtTreeHasLocalStore(tree, info.IterVar) ||
                    (info.LimitVar != BAD_VAR_NUM && compiler.gtTreeHasLocalStore(tree, info.LimitVar)))
                {
#if DEBUG
                    JITDUMP($"  Iterator or limit modified by [{tree.TreeId:D6}] in {FMT_BB(block.bbNum)}\n");
#endif
                    return false;
                }
            }

            var previous = block;
            block = block.GetUniquePred(compiler);
            if (block is null)
            {
                return false;
            }
            if (block.Kind is BBJ_COND && block.FalseTarget != block.TrueTarget &&
                IsZeroTripTest(block, block.TrueTarget == previous, info))
            {
                return true;
            }
        }
    }

    private bool IsZeroTripTest(BasicBlock guardBlock, bool entersWhenTrue, NaturalLoopIterInfo info)
    {
        assert(guardBlock.Kind is BBJ_COND);
        var enteringJTrue = guardBlock.LastStmt?.RootNode
            ?? throw new FatalJitException("A loop entry guard requires a condition.");
        assert(enteringJTrue.Oper is GT_JTRUE);
        var relop = enteringJTrue.AsUnOp().Op1;
        if (!relop.Oper.IsCmpCompare)
        {
            return false;
        }
#if DEBUG
        JITDUMP($"  Guard block {FMT_BB(guardBlock.bbNum)} enters the loop when condition " +
            $"[{relop.TreeId:D6}] evaluates to {(entersWhenTrue ? "true" : "false")}\n");
#endif

        GenTree limitCandidate;
        genTreeOps oper;
        var operands = relop.AsOp();
        if (operands.Op1.Oper.IsScalarLocal && operands.Op1.AsLclVarCommon().LclNum == info.IterVar)
        {
            JITDUMP("    op1 is the iteration variable\n");
            oper = relop.Oper;
            limitCandidate = operands.Op2;
        }
        else if (operands.Op2.Oper.IsScalarLocal && operands.Op2.AsLclVarCommon().LclNum == info.IterVar)
        {
            JITDUMP("    op2 is the iteration variable\n");
            oper = relop.Oper.SwapRelop;
            limitCandidate = operands.Op1;
        }
        else
        {
            JITDUMP("    Relop does not involve iteration variable\n");
            return false;
        }
        if (!entersWhenTrue)
        {
            oper = oper.ReverseRelop;
        }

        assert(info.TestTree is not null);
        if (relop.AsOp().IsUnsigned != info.TestTree.AsOp().IsUnsigned || oper != info.TestOper() ||
            !GenTree.Compare(limitCandidate, info.Limit()))
        {
#if DEBUG
            JITDUMP($"    Condition guarantees V{info.IterVar:D2} {(relop.AsOp().IsUnsigned ? "(uns) " : "")}" +
                $"{oper} [{limitCandidate.TreeId:D6}], but invariant requires V{info.IterVar:D2} " +
                $"{(info.TestTree.AsOp().IsUnsigned ? "(uns) " : "")}{info.TestOper()} [{info.Limit().TreeId:D6}]\n");
#endif
            return false;
        }
#if DEBUG
        JITDUMP($"  Condition is established before entry at [{relop.TreeId:D6}]\n");
#endif
        return true;
    }
}
