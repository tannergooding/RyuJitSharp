// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class AssertionPropFlowCallback : DataFlow.ICallback
{
    private ASSERT_TP _preMergeOut = BitVecOps.UninitVal();
    private ASSERT_TP _preMergeJumpDestOut = BitVecOps.UninitVal();
    private readonly ASSERT_TP[] _jumpDestOut;
    private readonly ASSERT_TP[] _jumpDestGen;
    private readonly BitVecTraits _traits;

    public AssertionPropFlowCallback(Compiler compiler, ASSERT_TP[] jumpDestOut, ASSERT_TP[] jumpDestGen)
    {
        assert(compiler.apTraits is not null);
        _traits = compiler.apTraits;
        _jumpDestOut = jumpDestOut;
        _jumpDestGen = jumpDestGen;
    }

    public void StartMerge(BasicBlock block)
    {
        BitVecOps.Assign(_traits, ref _preMergeOut, block.bbAssertionOut);
        BitVecOps.Assign(_traits, ref _preMergeJumpDestOut, _jumpDestOut[block.bbNum]);
    }

    public void Merge(BasicBlock block, BasicBlock predecessor, int duplicateCount)
    {
        ASSERT_TP? assertionOut;
        if ((predecessor.Kind is BBJ_COND) && (predecessor.TrueTarget == block))
        {
            assertionOut = _jumpDestOut[predecessor.bbNum];
            if (duplicateCount > 1)
            {
                assert(predecessor.FalseTarget == block);

                // Native BitVec assignment copies inline single-word values but
                // aliases multi-word storage. Preserve that distinction here.
                if (BitVecTraits.GetArrSize(_traits) <= 1)
                {
                    assertionOut = BitVecOps.MakeCopy(_traits, assertionOut);
                }

                BitVecOps.IntersectionD(_traits, assertionOut, predecessor.bbAssertionOut);
            }
        }
        else
        {
            assertionOut = predecessor.bbAssertionOut;
        }

        BitVecOps.IntersectionD(_traits, block.bbAssertionIn, assertionOut);
    }

    public void MergeHandler(BasicBlock block, BasicBlock firstTryBlock, BasicBlock lastTryBlock)
    {
        // VN assertions cannot become false, so the dominating try entry's IN
        // set suffices for every possible exception point in the region.
        BitVecOps.IntersectionD(_traits, block.bbAssertionIn, firstTryBlock.bbAssertionIn);
    }

    public bool EndMerge(BasicBlock block)
    {
        BitVecOps.DataFlowD(_traits, block.bbAssertionOut, block.bbAssertionGen, block.bbAssertionIn);
        BitVecOps.DataFlowD(_traits, _jumpDestOut[block.bbNum], _jumpDestGen[block.bbNum], block.bbAssertionIn);

        return !BitVecOps.Equal(_traits, _preMergeOut, block.bbAssertionOut) ||
            !BitVecOps.Equal(_traits, _preMergeJumpDestOut, _jumpDestOut[block.bbNum]);
    }
}
