// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>iterate through arguments tree and spill ret_expr to local variables.</summary>
public struct SpillRetExprHelper : IGenTreeVisitor<SpillRetExprHelper>
{
    public static bool DoPreOrder => true;

    public static bool UseExecutionOrder => true;

    private readonly GenTreeStack _ancestors;
    private Compiler _compiler;

    public SpillRetExprHelper(Compiler compiler)
    {
        _ancestors = [];
        _compiler = compiler;
    }

    public void StoreRetExprResultsInArgs(GenTreeCall call)
    {
        foreach (var arg in call.Args.Args)
        {
            _ = WalkTree(ref arg.EarlyNodeRef, user: null);
        }
    }

    private readonly unsafe void StoreRetExprAsLocalVar(ref GenTree use)
    {
        var retExpr = use;
        assert(retExpr.Oper is GT_RET_EXPR);

        var compiler = _compiler;
        var tmp = compiler.lvaGrabTemp(shortLifetime: true, "spilling ret_expr");

        JITDUMP($"Storing return expression [{retExpr.TreeId:D6}] to a local var V{tmp:D2}.\n");
        compiler.impStoreToTemp(tmp, retExpr, Compiler.CHECK_SPILL_NONE);
        use = compiler.gtNewLclvNode(retExpr.Type, tmp);

        assert(!compiler.lvaTable[tmp].lvSingleDef);
        compiler.lvaTable[tmp].lvSingleDef = true;
        JITDUMP($"Marked V{tmp:D2} as a single def temp\n");

        if (retExpr.Type is TYP_REF)
        {
            var retClsHnd = compiler.gtGetClassHandle(retExpr, out var isExact, out _);

            if (retClsHnd is not null)
            {
                compiler.lvaSetClass(tmp, retClsHnd, isExact);
            }
            else
            {
                JITDUMP($"Could not deduce class from [{retExpr.TreeId:D6}]");
            }
        }
    }

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        var tree = use;

        if ((tree.Flags & GTF_CALL) is 0)
        {
            // Trees with ret_expr are marked as GTF_CALL.
            return Compiler.WALK_SKIP_SUBTREES;
        }

        if (tree.Oper is GT_RET_EXPR)
        {
            StoreRetExprAsLocalVar(ref use);
        }
        return Compiler.WALK_CONTINUE;
    }

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<SpillRetExprHelper>.WalkTree(ref this, ref use, user, _ancestors);
}
