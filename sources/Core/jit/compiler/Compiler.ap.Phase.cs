// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree? optAssertionProp(ASSERT_TP? assertions, GenTree tree, Statement? statement, BasicBlock? block)
    {
        switch (tree.Oper)
        {
            case GT_LCL_VAR:
            {
                return optAssertionProp_LclVar(assertions, tree.AsLclVarCommon(), statement);
            }

            case GT_LCL_FLD:
            {
                return optAssertionProp_LclFld(assertions, tree.AsLclVarCommon(), statement);
            }

            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                return optAssertionProp_LocalStore(assertions, tree.AsLclVarCommon(), statement);
            }

            case GT_STORE_BLK:
            {
                return optAssertionProp_BlockStore(assertions, tree.AsBlk(), statement);
            }

            case GT_RETURN:
            case GT_SWIFT_ERROR_RET:
            {
                return optAssertionProp_Return(assertions, tree, statement);
            }

            case GT_SUB:
            case GT_MUL:
            case GT_ADD:
            {
                return optAssertionProp_AddMulSub(assertions, tree.AsOp(), statement, block);
            }

            case GT_MOD:
            case GT_DIV:
            case GT_UMOD:
            case GT_UDIV:
            {
                return optAssertionProp_ModDiv(assertions, tree.AsOp(), statement, block);
            }

            case GT_ARR_LENGTH:
            {
                // Local AP would introduce asymmetric exception sets between
                // CSE uses and definitions, preventing bounds-check removal.
                if (!optLocalAssertionProp)
                {
                    return optAssertionProp_Ind(assertions, tree, statement);
                }

                return null;
            }

            case GT_BLK:
            case GT_IND:
            case GT_STOREIND:
            case GT_NULLCHECK:
            {
                return optAssertionProp_Ind(assertions, tree, statement);
            }

            case GT_BOUNDS_CHECK:
            {
                return optAssertionProp_BndsChk(assertions, tree, statement, block);
            }

            case GT_CAST:
            {
                return optAssertionProp_Cast(assertions, tree.AsCast(), statement, block);
            }

            case GT_CALL:
            {
                return optAssertionProp_Call(assertions, tree.AsCall(), statement);
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                if (!optLocalAssertionProp)
                {
                    optAssertionProp_HWIntrinsic(tree.AsHWIntrinsic());
                }

                return null;
            }
#endif

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GT:
            case GT_GE:
            {
                return optAssertionProp_RelOp(assertions, tree, statement, block);
            }

            case GT_JTRUE:
            {
                return block is not null ? optVNConstantPropOnJTrue(block, tree) : null;
            }

            default:
            {
                return null;
            }
        }
    }

    public PhaseStatus optAssertionPropMain()
    {
        if (fgSsaPassesCompleted == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        optAssertionInit(false);
        noway_assert(optAssertionCount == 0);
        assert(apTraits is not null);
        var madeChanges = false;

        // Earlier local AP may have used a different trait size. Do not expose
        // its stale OUT bits to VN queries before dataflow initializes this pass.
        foreach (var block in Blocks)
        {
            block.bbAssertionOut = BitVecOps.UninitVal();
        }

#if DEBUG
        var baseTreeID = compGenTreeID;
#endif
        List<BasicBlock> switchBlocks = [];
        foreach (var block in Blocks)
        {
            compCurBB = block;
            fgRemoveRestOfBlock = false;

            var statement = block.FirstStmt;
            while (statement is not null)
            {
                if (fgRemoveRestOfBlock)
                {
                    fgRemoveStmt(block, statement);
                    statement = statement.NextStmt;
                    madeChanges = true;
                    continue;
                }
                else
                {
                    var nextStatement = optVNAssertionPropCurStmt(block, statement);
                    madeChanges |= optAssertionPropagatedCurrentStmt;
#if DEBUG
                    madeChanges |= baseTreeID != compGenTreeID;
#endif
                    if (fgRemoveRestOfBlock)
                    {
                        statement = statement.NextStmt;
                        continue;
                    }

                    if (statement != nextStatement)
                    {
                        statement = nextStatement;
                        continue;
                    }
                }

                foreach (var tree in statement.TreeList)
                {
                    optAssertionGen(tree);
                }

                statement = statement.NextStmt;
            }

            if (block.Kind is BBJ_SWITCH)
            {
                switchBlocks.Add(block);
            }
        }

        foreach (var switchBlock in switchBlocks)
        {
            madeChanges |= optCreateJumpTableImpliedAssertions(switchBlock);
        }

        if (optAssertionCount == 0)
        {
            // Range analysis and CSE share these sets even when AP found no facts.
            foreach (var block in Blocks)
            {
                block.bbAssertionIn = BitVecOps.MakeEmpty(apTraits);
            }

            return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
        }

        bbJtrueAssertionOut = optInitAssertionDataflowFlags();
        var jumpDestGen = optComputeAssertionGen();
        var flow = new DataFlow(this);
        var propagation = new AssertionPropFlowCallback(this, bbJtrueAssertionOut, jumpDestGen);
        flow.ForwardAnalysis(ref propagation);

#if DEBUG
        if (verbose)
        {
            foreach (var block in Blocks)
            {
                assert(block.bbAssertionIn is not null);
                assert(block.bbAssertionOut is not null);
                jitprintf($"{FMT_BB(block.bbNum)}:\n");
                optDumpAssertionIndices(" in   = ", block.bbAssertionIn, "\n");
                optDumpAssertionIndices(" out  = ", block.bbAssertionOut, "\n");
                if (block.Kind is BBJ_COND)
                {
                    jitprintf($" {FMT_BB(block.TrueTarget.bbNum)} = ");
                    optDumpAssertionIndices("", bbJtrueAssertionOut[block.bbNum], "\n");
                }
            }

            jitprintf("\n");
        }
#endif

        var assertions = BitVecOps.MakeEmpty(apTraits);
        foreach (var block in Blocks)
        {
            BitVecOps.Assign(apTraits, ref assertions, block.bbAssertionIn);

            // Preserve native's fault-handler exclusion pending EH iteration review.
            if (block.CatchType is bbCatchType.BBCT_FAULT)
            {
                continue;
            }

            compCurBB = block;
            fgRemoveRestOfBlock = false;
            var statement = block.GetFirstNonPhiDef();
            while (statement is not null)
            {
                if (fgRemoveRestOfBlock)
                {
                    fgRemoveStmt(block, statement);
                    statement = statement.NextStmt;
                    madeChanges = true;
                    continue;
                }

                var previousStatement = statement == block.FirstStmt ? null : statement.PrevStmt;
                optAssertionPropagatedCurrentStmt = false;

                for (var tree = statement.TreeListBegin; tree is not null; tree = tree.Next)
                {
#if DEBUG
                    optDumpAssertionIndices("Propagating ", assertions, " ");
                    JITDUMP($"for {FMT_BB(block.bbNum)}, stmt {FMT_STMT(statement.Id)}, tree [{tree.TreeId:D6}]");
                    JITDUMP(", tree -> ");
                    if (verbose)
                    {
                        optPrintAssertionIndex(tree.AssertionInfo.AssertionIndex);
                    }

                    JITDUMP("\n");
#endif
                    var newTree = optAssertionProp(assertions, tree, statement, block);
                    if (newTree is not null)
                    {
                        assert(optAssertionPropagatedCurrentStmt);
                        tree = newTree;
                    }

                    if (tree.GeneratesAssertion)
                    {
                        BitVecOps.AddElemD(apTraits, assertions, tree.AssertionInfo.AssertionIndex - 1);
                    }
                }

                if (optAssertionPropagatedCurrentStmt)
                {
#if DEBUG
                    if (verbose)
                    {
                        jitprintf("Re-morphing this stmt:\n");
                        gtDispStmt(statement);
                        jitprintf("\n");
                    }
#endif
                    var wasConditional = block.Kind is BBJ_COND;
                    var trueBlock = wasConditional ? block.TrueTarget : null;
                    var falseBlock = wasConditional ? block.FalseTarget : null;
                    fgMorphBlockStmt(block, statement, message: nameof(optAssertionPropMain));
                    madeChanges = true;

                    if (wasConditional && !optLocalAssertionProp)
                    {
                        var outgoing = block.bbAssertionOut;
                        assert(outgoing is not null);

                        if (block.Kind is not BBJ_COND)
                        {
                            if ((block.UniqueSucc == trueBlock) && (trueBlock != falseBlock))
                            {
                                BitVecOps.Assign(apTraits, ref outgoing, bbJtrueAssertionOut[block.bbNum]);
                            }
                            else if ((block.UniqueSucc == falseBlock) && (trueBlock != falseBlock))
                            {
                                // The existing OUT set already describes the retained false edge.
                            }
                            else
                            {
                                BitVecOps.Assign(apTraits, ref outgoing, block.bbAssertionIn);
                            }
                        }
                        else if ((block.TrueTarget != trueBlock) || (block.FalseTarget != falseBlock))
                        {
                            // Unexpected edge changes retain only facts valid at block entry.
                            BitVecOps.Assign(apTraits, ref outgoing, block.bbAssertionIn);
                            BitVecOps.Assign(apTraits, ref bbJtrueAssertionOut[block.bbNum], block.bbAssertionIn);
                        }

                        block.bbAssertionOut = outgoing;
                    }
                }

                var nextStatement = previousStatement is null ? block.FirstStmt : previousStatement.NextStmt;
                statement = statement == nextStatement ? statement.NextStmt : nextStatement;
            }

            optAssertionPropagatedCurrentStmt = false;
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
