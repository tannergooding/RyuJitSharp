// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class treeStmtLst
{
    public treeStmtLst? tslNext;
    public GenTree tslTree;
    public Statement tslStmt;
    public BasicBlock tslBlock;

    public treeStmtLst(GenTree tree, Statement stmt, BasicBlock block)
    {
        tslTree = tree;
        tslStmt = stmt;
        tslBlock = block;
    }
}

public sealed class CSEdsc
{
    public CSEdsc? csdNextInBucket;
    public nuint csdHashKey;
    public nint csdConstDefValue;
    public ValueNum csdConstDefVN;
    public int csdIndex;
    public ushort csdDefCount;
    public ushort csdUseCount;
    public weight_t csdDefWtCnt;
    public weight_t csdUseWtCnt;
    public treeStmtLst csdTreeList;
    public treeStmtLst csdTreeLast;
    public ValueNum defExcSetPromise;
    public ValueNum defExcSetCurrent;
    public ushort numDistinctLocals;
    public ushort numLocalOccurrences;
    public bool csdIsSharedConst;
    public bool csdLiveAcrossCall;

    public CSEdsc(GenTree tree, Statement stmt, BasicBlock block)
    {
        csdTreeList = new treeStmtLst(tree, stmt, block);
        csdTreeLast = csdTreeList;
    }

    public bool IsViable()
    {
        if (defExcSetPromise == ValueNumStore.NoVN)
        {
            return false;
        }

        if ((csdDefCount == 0) || (csdUseCount == 0))
        {
            return false;
        }

        if ((csdDefWtCnt <= 0) || (csdUseWtCnt <= 0))
        {
            return false;
        }

        return true;
    }

    public void ComputeNumLocals(Compiler compiler)
    {
        var visitor = new LocalCountingVisitor();
        var tree = csdTreeList.tslTree;
        _ = visitor.WalkTree(ref tree, null);
        numDistinctLocals = visitor.Count;
        numLocalOccurrences = visitor.Occurrences;
    }

    private struct LocalCountingVisitor : IGenTreeVisitor<LocalCountingVisitor>
    {
        private const int MaxLocals = 8;

        private readonly GenTreeStack _ancestors = [];
        private readonly int[] _locals = new int[MaxLocals];
        private readonly int[] _counts = new int[MaxLocals];

        public ushort Count;
        public ushort Occurrences;

        public static bool DoPreOrder => true;
        public static bool DoLclVarsOnly => true;

        public LocalCountingVisitor()
        {
        }

        public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var localNumber = use.AsLclVarCommon().LclNum;
            Occurrences++;

            for (var i = 0; i < Count; i++)
            {
                if (_locals[i] == localNumber)
                {
                    _counts[i]++;
                    return Compiler.WALK_CONTINUE;
                }
            }

            if (Count >= MaxLocals)
            {
                return Compiler.WALK_ABORT;
            }

            _locals[Count] = localNumber;
            _counts[Count]++;
            Count++;
            return Compiler.WALK_CONTINUE;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
            => Compiler.WALK_CONTINUE;

        // WalkTree must mutate this visitor to retain the counts after traversal.
#pragma warning disable IDE0251
        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<LocalCountingVisitor>.WalkTree(ref this, ref use, user, _ancestors);
#pragma warning restore IDE0251
    }
}
