// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    internal GenTree PhysicalPromotionCreateWriteBack(int structLclNum, PhysicalPromotionReplacement replacement)
    {
        var value = gtNewLclVarNode(replacement.AccessType, replacement.LclNum);
        var store = gtNewStoreLclFldNode(replacement.AccessType, structLclNum,
            checked((ushort)replacement.Offset), value);
        if (!lvaGetDesc(structLclNum).lvDoNotEnregister)
        {
            lvaSetVarDoNotEnregister(structLclNum, DoNotEnregisterReason.LocalField);
        }

        return store;
    }

    internal GenTree PhysicalPromotionCreateReadBack(int structLclNum, PhysicalPromotionReplacement replacement)
    {
        var value = gtNewLclFldNode(replacement.AccessType, structLclNum,
            checked((ushort)replacement.Offset));
        var store = gtNewStoreLclVarNode(replacement.LclNum, value);
        if (!lvaGetDesc(structLclNum).lvDoNotEnregister)
        {
            lvaSetVarDoNotEnregister(structLclNum, DoNotEnregisterReason.LocalField);
        }

        return store;
    }

    private void PhysicalPromotionExplicitlyZeroInitReplacementLocals(
        int lclNum, List<PhysicalPromotionReplacement> replacements, ref Statement? previousStatement)
    {
        foreach (var replacement in replacements)
        {
            if (!fgVarNeedsExplicitZeroInit(replacement.LclNum, bbInALoop: false, bbIsReturn: false))
            {
                lvaGetDesc(replacement.LclNum).lvSuppressedZeroInit = true;
                continue;
            }

            var value = gtNewZeroConNode(replacement.AccessType);
            var store = gtNewStoreLclVarNode(replacement.LclNum, value);
            PhysicalPromotionInsertInitStatement(ref previousStatement, store);
        }
    }

    private void PhysicalPromotionInsertInitStatement(ref Statement? previousStatement, GenTree tree)
    {
        var statement = fgNewStmtFromTree(tree);
        if (previousStatement is not null)
        {
            fgInsertStmtAfter(fgFirstBB!, previousStatement, statement);
        }
        else
        {
            fgInsertStmtAtBeg(fgFirstBB!, statement);
        }

        previousStatement = statement;
    }
}
