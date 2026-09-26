// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public int optIsLoopIncrTree(GenTree incr)
    {
        if (incr.Oper is not GT_STORE_LCL_VAR)
        {
            return BAD_VAR_NUM;
        }

        var store = incr.AsLclVarCommon();
        var data = store.Data;
        if (!data.Oper.IsBinary)
        {
            return BAD_VAR_NUM;
        }

        var op = data.AsOp();
        if ((op.Op1 is null) || (op.Op2 is null) ||
            (op.Op1.Oper is not GT_LCL_VAR) || (op.Op1.AsLclVarCommon().LclNum != store.LclNum))
        {
            return BAD_VAR_NUM;
        }

        if (data.Oper is not (GT_ADD or GT_SUB or GT_MUL or GT_RSH or GT_LSH) ||
            op.Op2.Oper is not GT_CNS_INT || op.Op2.Type is not TYP_INT)
        {
            return BAD_VAR_NUM;
        }

        return store.LclNum;
    }

    private bool optIsLoopTestEvalIntoTemp(Statement testStmt, out Statement? newTestStmt)
    {
        newTestStmt = null;
        var test = testStmt.RootNode;
        if (test.Oper is not GT_JTRUE)
        {
            return false;
        }

        var relop = test.AsUnOp().Op1;
        noway_assert(relop.Oper.IsCompare);
        var operands = relop.AsOp();

        if (relop.Oper is GT_NE && operands.Op1.Oper is GT_LCL_VAR &&
            operands.Op2.Oper is GT_CNS_INT && operands.Op2.IsIntegralConst(0))
        {
            var previous = testStmt.PrevStmt;
            if (previous is null)
            {
                return false;
            }
            var tree = previous.RootNode;
            if (tree.Oper is GT_STORE_LCL_VAR &&
                tree.AsLclVarCommon().LclNum == operands.Op1.AsLclVarCommon().LclNum &&
                tree.AsLclVarCommon().Data.Oper.IsCompare)
            {
                newTestStmt = previous;
                return true;
            }
        }

        return false;
    }

    public bool optExtractTestIncr(BasicBlock cond, out GenTree? test, out GenTree? increment)
    {
        test = null;
        increment = null;
        var testStmt = cond.LastStmt;
        noway_assert(testStmt is not null && testStmt.NextStmt is null);
        if (optIsLoopTestEvalIntoTemp(testStmt, out var newTestStmt))
        {
            assert(newTestStmt is not null);
            testStmt = newTestStmt;
        }

        var firstStmt = cond.FirstStmt;
        var condInTry = cond.hasTryIndex;
        uint budget = 100;
        if (testStmt != firstStmt)
        {
            for (var stmt = testStmt.PrevStmt; stmt is not null; stmt = stmt.PrevStmt)
            {
                if (budget == 0)
                {
                    JITDUMP($"optExtractTestIncr: budget exhausted in {FMT_BB(cond.bbNum)}\n");
                    return false;
                }
                budget--;

                var candidateVar = optIsLoopIncrTree(stmt.RootNode);
                if ((candidateVar != BAD_VAR_NUM) && !lvaGetDesc(candidateVar).IsAddressExposed &&
                    gtTreeHasLocalRead(testStmt.RootNode, candidateVar))
                {
                    var intermediateUse = false;
                    for (var between = stmt.NextStmt; between != testStmt; between = between.NextStmt)
                    {
                        assert(between is not null);
                        if (budget == 0)
                        {
                            JITDUMP($"optExtractTestIncr: budget exhausted in {FMT_BB(cond.bbNum)}\n");
                            return false;
                        }
                        budget--;

                        var root = between.RootNode;
                        if (gtTreeHasLocalRead(root, candidateVar) || (condInTry && (root.Flags & GTF_EXCEPT) != 0))
                        {
                            intermediateUse = true;
                            break;
                        }
                    }

                    if (!intermediateUse)
                    {
                        test = testStmt.RootNode;
                        increment = stmt.RootNode;
                        return true;
                    }
                }

                if (stmt == firstStmt)
                {
                    break;
                }
            }
        }

        return false;
    }
}
