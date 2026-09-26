// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, forwardsub.cpp.

using System.Runtime.CompilerServices;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool fgIsCheapReorderableAddressTree(GenTree tree)
    {
        if (tree.Type is not TYP_BYREF and not TYP_I_IMPL)
        {
            return false;
        }
        if ((tree.Flags & GTF_ALL_EFFECT & ~GTF_EXCEPT) != 0)
        {
            return false;
        }

        var baseAddress = gtPeelFieldAddrs(tree);
        return baseAddress.Oper is GT_LCL_VAR or GT_LCL_ADDR;
    }

    private bool fgForwardSubMultiUse(Statement nextStmt, int lclNum, GenTree fwdSubNode)
    {
        const int maxUses = 4;
        var visitor = new ForwardSubCollectVisitor(lclNum);
        _ = visitor.WalkTree(ref nextStmt.RootNodeRef, null);
        if (visitor.Bail || visitor.Uses.Count is < 2 or > maxUses)
        {
            return false;
        }

        var lastIndex = visitor.Uses.Count - 1;
        for (var i = 0; i < lastIndex; i++)
        {
            var link = gtFindLink(nextStmt, visitor.Uses[i]);
            if (Unsafe.IsNullRef(ref link.result))
            {
                throw new FatalJitException("A collected forward-substitution use lost its owner.");
            }
            link.result = gtCloneExpr(fwdSubNode)
                ?? throw new FatalJitException("A cheap forward-substitution address could not be cloned.");
        }

        var lastLink = gtFindLink(nextStmt, visitor.Uses[lastIndex]);
        if (Unsafe.IsNullRef(ref lastLink.result))
        {
            throw new FatalJitException("The final forward-substitution use lost its owner.");
        }
        lastLink.result = fwdSubNode;

        var lastUse = gtPeelFieldAddrs(fwdSubNode).AsLclVarCommon();
        fgSequenceLocals(nextStmt);
        fgForwardSubUpdateLiveness(lastUse, lastUse);
        gtUpdateStmtSideEffects(nextStmt);
        return true;
    }
}
