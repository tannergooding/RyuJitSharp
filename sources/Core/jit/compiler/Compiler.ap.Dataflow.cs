// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public ASSERT_TP[] optComputeAssertionGen()
    {
        assert(apTraits is not null);
        var jumpDestGen = new ASSERT_TP[fgBBNumMax + 1];

        foreach (var block in Blocks)
        {
            var valueGen = BitVecOps.MakeEmpty(apTraits);
            GenTree? jtrue = null;

            foreach (var statement in block.Statements)
            {
                foreach (var tree in statement.TreeList)
                {
                    if (tree.Oper is GT_JTRUE)
                    {
                        assert((tree.Next is null) && (statement.NextStmt is null));
                        jtrue = tree;
                        break;
                    }

                    if (tree.GeneratesAssertion)
                    {
                        BitVecOps.AddElemD(apTraits, valueGen, tree.AssertionInfo.AssertionIndex - 1);
                    }
                }
            }

            if (jtrue is not null)
            {
                var jumpDestValueGen = BitVecOps.MakeCopy(apTraits, valueGen);
                if (jtrue.GeneratesAssertion)
                {
                    var info = jtrue.AssertionInfo;
                    AssertionIndex valueAssertionIndex;
                    AssertionIndex jumpDestAssertionIndex;

                    if (info.AssertionHoldsOnFalseEdge)
                    {
                        valueAssertionIndex = info.AssertionIndex;
                        jumpDestAssertionIndex = optFindComplementary(info.AssertionIndex);
                    }
                    else
                    {
                        jumpDestAssertionIndex = info.AssertionIndex;
                        valueAssertionIndex = optFindComplementary(jumpDestAssertionIndex);
                    }

                    if (valueAssertionIndex != NO_ASSERTION_INDEX)
                    {
                        BitVecOps.AddElemD(apTraits, valueGen, valueAssertionIndex - 1);
                    }

                    if (jumpDestAssertionIndex != NO_ASSERTION_INDEX)
                    {
                        BitVecOps.AddElemD(apTraits, jumpDestValueGen, jumpDestAssertionIndex - 1);
                    }
                }

                jumpDestGen[block.bbNum] = jumpDestValueGen;
            }
            else
            {
                jumpDestGen[block.bbNum] = BitVecOps.MakeEmpty(apTraits);
            }

            block.bbAssertionGen = valueGen;

#if DEBUG
            if (verbose)
            {
                if (block == fgFirstBB)
                {
                    jitprintf("\n");
                }

                jitprintf($"{FMT_BB(block.bbNum)} valueGen = ");
                optPrintAssertionIndices(block.bbAssertionGen);
                if (block.Kind is BBJ_COND)
                {
                    jitprintf($" => {FMT_BB(block.TrueTarget.bbNum)} valueGen = ");
                    optPrintAssertionIndices(jumpDestGen[block.bbNum]);
                }

                jitprintf("\n");
                if (block == fgLastBB)
                {
                    jitprintf("\n");
                }
            }
#endif
        }

        return jumpDestGen;
    }

    public ASSERT_TP[] optInitAssertionDataflowFlags()
    {
        assert(apTraits is not null);
        assert(fgFirstBB is not null);
        var jumpDestOut = new ASSERT_TP[fgBBNumMax + 1];

        // Unreachable blocks are not visited by dataflow. Their initial sets must
        // contain only real assertions, not the unused capacity of the table.
        var validFull = BitVecOps.MakeEmpty(apTraits);
        for (var index = 1; index <= optAssertionCount; index++)
        {
            BitVecOps.AddElemD(apTraits, validFull, index - 1);
        }

        foreach (var block in Blocks)
        {
            block.bbAssertionIn = BitVecOps.MakeCopy(apTraits, validFull);
            block.bbAssertionGen = BitVecOps.MakeEmpty(apTraits);
            block.bbAssertionOut = BitVecOps.MakeCopy(apTraits, validFull);
            jumpDestOut[block.bbNum] = BitVecOps.MakeCopy(apTraits, validFull);
        }

        BitVecOps.ClearD(apTraits, fgFirstBB.bbAssertionIn);
        return jumpDestOut;
    }
}
