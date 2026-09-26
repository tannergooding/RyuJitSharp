// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool optCreateJumpTableImpliedAssertions(BasicBlock switchBlock)
    {
        assert(!optLocalAssertionProp);
        assert(switchBlock.Kind is BBJ_SWITCH);
        assert(switchBlock.LastStmt is not null);
        assert(vnStore is not null);

        var modified = false;
        var switchTree = switchBlock.LastStmt.RootNode.EffectiveVal;
        assert(switchTree.Oper is GT_SWITCH);

        var operandVN = optConservativeNormalVN(switchTree.AsUnOp().Op1);
        if (operandVN == ValueNumStore.NoVN)
        {
            return modified;
        }

        if (vnStore.TypeOfVN(operandVN) is not TYP_INT)
        {
            return modified;
        }

        vnStore.PeelOffsetsI32(ref operandVN, out var offset);

        var targets = switchBlock.SwitchTargets;
        var jumpTable = targets.Cases;
        var jumpCount = jumpTable.Length;
        var hasDefault = targets.HasDefaultCase;

        for (var index = 0; index < jumpCount; index++)
        {
            if (!CheckedOps.TrySub(index, offset, out var value))
            {
                continue;
            }

            var edge = jumpTable[index];
            var target = edge.DestinationBlock;
            if (target.GetUniquePred(this) != switchBlock)
            {
                continue;
            }

            if (edge.DupCount > 1)
            {
                continue;
            }

            AssertionInfo assertion;
            if (hasDefault && (index == jumpCount - 1))
            {
                if ((offset == 0) && (value > 0) && !vnStore.IsVNConstant(operandVN))
                {
                    var relation = vnStore.IsVNNeverNegative(operandVN) ? VNF_GE : VNF_GE_UN;
                    var descriptor = AssertionDsc.CreateConstantBound(this, relation, operandVN,
                        vnStore.VNForIntCon(value));
                    assertion = new(optAddAssertion(descriptor));
                }
                else
                {
                    continue;
                }
            }
            else
            {
                var valueVN = vnStore.VNForIntCon(value);
                assertion = new(optAddAssertion(AssertionDsc.CreateConstLclVarAssertion(
                    this, BAD_VAR_NUM, operandVN, value, valueVN, true)));
            }

            if (assertion.HasAssertion)
            {
                var tree = gtNewNothingNode();
                fgInsertStmtAtBeg(target, fgNewStmtFromTree(tree));
                modified = true;
                tree.AssertionInfo = assertion;
            }
        }

        return modified;
    }
}
