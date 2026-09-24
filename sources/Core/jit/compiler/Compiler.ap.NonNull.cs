// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp1Kind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    public enum AssertVisit
    {
        Continue,
        Abort,
    }

    public bool optAssertionIsNonNull(GenTree op, ASSERT_TP? assertions)
    {
        if ((op.Oper is GT_ADD) && op.AsOp().Op2.Oper.IsCnsIntOrI &&
            !fgIsBigOffset(op.AsOp().Op2.AsIntCon().IconValue))
        {
            op = op.AsOp().Op1;
        }

        if (!optLocalAssertionProp)
        {
            assert(vnStore is not null);
            if (vnStore.IsKnownNonNull(op._vnPair.Conservative))
            {
                return true;
            }
        }

        op = op.EffectiveVal;
        if (op.Oper is not GT_LCL_VAR)
        {
            return false;
        }

        if (!optLocalAssertionProp)
        {
            assert(vnStore is not null);
            return optAssertionVNIsNonNull(vnStore.VNNormalValue(op._vnPair.Conservative), assertions);
        }

        if (BitVecOps.MaybeUninit(assertions))
        {
            return false;
        }

        assert(apTraits is not null);
        var local = op.AsLclVarCommon().LclNum;
        var dependent = BitVecOps.Intersection(apTraits, GetAssertionDep(local), assertions);
        var result = false;
        _ = BitVecOps.VisitBits(apTraits, dependent, bitIndex => {
            var assertion = optGetAssertion(GetAssertionIndex((ushort)bitIndex));
            if (assertion.KindIs(OAK_NOT_EQUAL) && assertion.Op1.KindIs(O1K_LCLVAR) &&
                assertion.Op2.KindIs(O2K_CONST_INT) && (assertion.Op1.LclNum == local) && (assertion.Op2.IntConstant == 0))
            {
                result = true;
                return false;
            }

            return true;
        });

        return result;
    }

    public bool optAssertionVNIsNonNull(ValueNum vn, ASSERT_TP? assertions, int budget = 10)
    {
        if (vn == ValueNumStore.NoVN)
        {
            return false;
        }

        assert(vnStore is not null);
        if (vnStore.IsKnownNonNull(vn))
        {
            return true;
        }

        var baseVN = vn;
        vnStore.PeelOffsets(ref baseVN, out var offset);
        if ((offset < 0) || fgIsBigOffset(unchecked((nint)offset)))
        {
            // Such offsets cannot inherit the base's non-null proof, but an
            // assertion about the full address can still prove it non-null.
            baseVN = vn;
        }

        if (!BitVecOps.MaybeUninit(assertions))
        {
            assert(apTraits is not null);
            var result = false;
            _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex => {
                var assertion = optGetAssertion(GetAssertionIndex((ushort)bitIndex));
                if (assertion.CanPropNonNull && ((assertion.Op1.VN == vn) || (assertion.Op1.VN == baseVN)))
                {
                    result = true;
                    return false;
                }

                return true;
            });
            if (result)
            {
                return true;
            }
        }

        if (budget <= 0)
        {
            return false;
        }

        AssertVisit Visit(ValueNum reachingVN, ASSERT_TP? reachingAssertions)
        {
            return optAssertionVNIsNonNull(reachingVN, reachingAssertions, budget - 1) ? AssertVisit.Continue : AssertVisit.Abort;
        }

        if (optVisitReachingAssertions(vn, Visit) is AssertVisit.Continue)
        {
            return true;
        }

        return (baseVN != vn) && (optVisitReachingAssertions(baseVN, Visit) is AssertVisit.Continue);
    }

    public ASSERT_TP? optGetEdgeAssertions(BasicBlock block, BasicBlock predecessor)
    {
        assert(apTraits is not null);
        if (predecessor.Kind is BBJ_COND)
        {
            if (predecessor.TrueTarget == block)
            {
                return bbJtrueAssertionOut is not null
                    ? bbJtrueAssertionOut[predecessor.bbNum]
                    : BitVecOps.MakeEmpty(apTraits);
            }

            // A stale PHI argument must not import assertions from another edge.
            return predecessor.FalseTarget == block ? predecessor.bbAssertionOut : BitVecOps.MakeEmpty(apTraits);
        }

        foreach (var pred in block.PredBlocks)
        {
            if (pred == predecessor)
            {
                return predecessor.bbAssertionOut;
            }
        }

        return BitVecOps.MakeEmpty(apTraits);
    }

    public AssertVisit optVisitReachingAssertions(ValueNum vn, Func<ValueNum, ASSERT_TP?, AssertVisit> visitor)
    {
        assert(vnStore is not null);
        VNPhiDef phiDef = default;
        if (!vnStore.GetPhiDef(vn, ref phiDef))
        {
            return AssertVisit.Abort;
        }

        var definition = lvaGetDesc(phiDef.LclNum).GetPerSsaData(phiDef.SsaDef);
        var node = definition.DefNode;
        var block = definition.Block;
        assert((node is not null) && node.IsPhiDefn);
        assert(block is not null);
        var traits = new BitVecTraits(this, fgBBNumMax + 1);
        var visited = BitVecOps.MakeEmpty(traits);
        var actualPreds = BitVecOps.MakeEmpty(traits);
        foreach (var predecessor in block.PredBlocks)
        {
            BitVecOps.AddElemD(traits, actualPreds, predecessor.bbNum);
        }

        foreach (var use in node.Data.AsPhi().Uses)
        {
            var arg = use.Node.AsPhiArg();
            var predecessor = arg.PredBB;
            // Normal predecessor lists exclude EH flow; handler entries are not
            // dead merely because they have no normal predecessors.
            if ((predecessor.bbPreds is null) && (predecessor != fgFirstBB) && !bbIsHandlerBeg(predecessor))
            {
                JITDUMP($"... optVisitReachingAssertions in {FMT_BB(block.bbNum)}: phi-pred {FMT_BB(predecessor.bbNum)} is unreachable, ignoring\n");
                BitVecOps.AddElemD(traits, visited, predecessor.bbNum);
                continue;
            }

            if (!BitVecOps.IsMember(traits, actualPreds, predecessor.bbNum))
            {
                JITDUMP($"... optVisitReachingAssertions in {FMT_BB(block.bbNum)}: phi-pred {FMT_BB(predecessor.bbNum)} not a block pred\n");
                return AssertVisit.Abort;
            }

            // A loop-varying argument may deliberately have NoVN on the tree.
            // Read its SSA definition without back-patching the argument slot.
            var argDefinition = lvaGetDesc(arg.LclNum).GetPerSsaData(arg.SsaNum);
            var argVN = vnStore.VNNormalValue(argDefinition._vnPair.Conservative);
            if (visitor(argVN, optGetEdgeAssertions(block, predecessor)) is AssertVisit.Abort)
            {
                return AssertVisit.Abort;
            }
            BitVecOps.AddElemD(traits, visited, predecessor.bbNum);
        }

        foreach (var predecessor in block.PredBlocks)
        {
            if (!BitVecOps.IsMember(traits, visited, predecessor.bbNum))
            {
                JITDUMP($"... optVisitReachingAssertions in {FMT_BB(block.bbNum)}: pred {FMT_BB(predecessor.bbNum)} not a phi-pred\n");
                return AssertVisit.Abort;
            }
        }

        return AssertVisit.Continue;
    }
}
