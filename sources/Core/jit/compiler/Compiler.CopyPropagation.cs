// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, copyprop.cpp.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private static readonly ConditionalWeakTable<LclNumToLiveDefsMap, CopyPropKeyOrder> s_copyPropKeyOrders = [];

    private sealed class CopyPropKeyOrder
    {
        // JitHashTable hashes unsigned locals by their number and visits buckets in order,
        // following head-inserted chains (jithashtable.h).
        private static ReadOnlySpan<int> PrimeSizes =>
        [
            9, 23, 59, 131, 239, 433, 761, 1399, 2473, 4327, 7499,
            12973, 22433, 46559, 96581, 200341, 415517, 861719,
            1787021, 3705617, 7684087, 15933877, 33040633, 68513161,
            142069021, 294594427, 733045421,
        ];

        private List<int>?[] _buckets = [];
        private uint _count;
        private uint _max;

        public void Add(int local)
        {
            if (_count == _max)
            {
                Grow();
            }

            var bucket = _buckets[unchecked((uint)local) % (uint)_buckets.Length] ??= [];
            assert(!bucket.Contains(local));
            bucket.Insert(0, local);
            _count++;
        }

        public void Remove(int local)
        {
            var bucket = _buckets[unchecked((uint)local) % (uint)_buckets.Length];
            assert(bucket is not null);
            if (!bucket.Remove(local))
            {
                throw new InvalidOperationException("Copy propagation lost a live definition.");
            }
            _count--;
        }

        public IEnumerable<int> Keys(LclNumToLiveDefsMap names)
        {
            if (_count != names.Count)
            {
                throw new InvalidOperationException("Copy propagation requires definitions pushed through its stack helper.");
            }

            foreach (var bucket in _buckets)
            {
                if (bucket is not null)
                {
                    foreach (var local in bucket)
                    {
                        yield return local;
                    }
                }
            }
        }

        private void Grow()
        {
            var targetSize = unchecked(((_count * 3) / 2 * 4) / 3);
            targetSize = Math.Max(targetSize, 7u);
            if (targetSize < _count)
            {
                Globals.NOMEM();
            }

            var size = 0;
            foreach (var prime in PrimeSizes)
            {
                if ((uint)prime >= targetSize)
                {
                    size = prime;
                    break;
                }
            }
            if (size == 0)
            {
                Globals.NOMEM();
            }

            var buckets = new List<int>?[size];
            foreach (var bucket in _buckets)
            {
                if (bucket is not null)
                {
                    foreach (var local in bucket)
                    {
                        var next = buckets[unchecked((uint)local) % (uint)size] ??= [];
                        next.Insert(0, local);
                    }
                }
            }
            _buckets = buckets;
            _max = (uint)size * 3 / 4;
        }
    }

    public void optBlockCopyPropPopStacks(BasicBlock block, LclNumToLiveDefsMap curSsaName)
    {
        var order = s_copyPropKeyOrders.GetValue(curSsaName, static _ => new CopyPropKeyOrder());
        foreach (var statement in block.Statements)
        {
            foreach (var tree in statement.TreeList)
            {
                if (!tree.Oper.IsSsaDef)
                {
                    continue;
                }

                var visitor = new CopyPropPopDefVisitor(curSsaName, this, order);
                _ = tree.VisitLogicalLocalDefs(this, ref visitor);
            }
        }
    }

    private struct CopyPropPopDefVisitor(LclNumToLiveDefsMap names, Compiler compiler, CopyPropKeyOrder order)
        : ILocalDefVisitor
    {
        public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            if ((def.GetSsaNum(compiler) != SsaConfig.RESERVED_SSA_NUM) &&
                names.TryGetValue(def.LclNum, out var stack))
            {
                _ = stack.Pop();
                if (stack.Count == 0)
                {
                    _ = names.Remove(def.LclNum);
                    order.Remove(def.LclNum);
                }
            }
            return GenTree.VisitResult.Continue;
        }
    }

#if DEBUG
    public void optDumpCopyPropStack(LclNumToLiveDefsMap curSsaName)
    {
        JITDUMP("{ ");
        foreach (var local in s_copyPropKeyOrders.GetValue(curSsaName, static _ => new CopyPropKeyOrder()).Keys(curSsaName))
        {
            var definition = curSsaName[local].Peek();
            var number = definition.SsaNum == SsaConfig.RESERVED_SSA_NUM ? "NA" : $"{definition.SsaNum}";
            JITDUMP($"[{definition.GetDefNode().TreeId:D6}]:V{local:D2}/{number} ");
        }
        JITDUMP("}\n\n");
    }
#endif

    public bool optCopyProp(BasicBlock block, Statement statement, GenTreeLclVarCommon tree,
        int lclNum, LclNumToLiveDefsMap curSsaName)
    {
        assert((tree.Flags & GTF_VAR_DEF) == 0);
        assert(tree.LclNum == lclNum);

        ref var varDsc = ref lvaGetDesc(lclNum);
        ref var varSsaDsc = ref varDsc.GetPerSsaData(tree.SsaNum);
        var lclDefVN = varSsaDsc._vnPair.Conservative;
        assert(lclDefVN != ValueNumStore.NoVN);

        foreach (var newLclNum in s_copyPropKeyOrders.GetValue(curSsaName, static _ => new CopyPropKeyOrder()).Keys(curSsaName))
        {
            if (lclNum == newLclNum)
            {
                continue;
            }

            var newLclDef = curSsaName[newLclNum].Peek();
            ref var newLclSsaDef = ref newLclDef.GetSsaDef();
            var newLclDefVN = newLclSsaDef._vnPair.Conservative;
            assert(newLclDefVN != ValueNumStore.NoVN);
            if (newLclDefVN != lclDefVN)
            {
#if DEBUG
                JITDUMP($"orig [{tree.TreeId:D6}] copy [{newLclDef.GetDefNode().TreeId:D6}] VNs proved equivalent\n");
#endif
                continue;
            }

            ref var newLclVarDsc = ref lvaGetDesc(newLclNum);
            var enregOld = !varDsc.lvDoNotEnregister &&
                (!varDsc.IsLiveInOutOfHandler || IsEHVarARegCandidate(in varDsc));
            var enregNew = !newLclVarDsc.lvDoNotEnregister &&
                (!newLclVarDsc.IsLiveInOutOfHandler || IsEHVarARegCandidate(in newLclVarDsc));
            if (enregOld != enregNew)
            {
                continue;
            }

            if (varDsc.lvOnlyUsedOnSynchronousPath || newLclVarDsc.lvOnlyUsedOnSynchronousPath)
            {
                continue;
            }

            if (optCopyProp_LclVarScore(in varDsc, in newLclVarDsc, true) <= 0)
            {
                continue;
            }

            assert(newLclVarDsc.lvTracked);
            if ((newLclNum != info.compThisArg) &&
                !VarSetOps.IsMember(this, compCurLife, newLclVarDsc._varIndex))
            {
                continue;
            }

            var newLclType = newLclVarDsc.Type;
            if (!newLclVarDsc.lvNormalizeOnLoad)
            {
                newLclType = newLclType.ActualType;
            }
            var oldLclType = tree.Oper is GT_LCL_VAR ? tree.Type : varDsc.Type;
            if (newLclType != oldLclType)
            {
                continue;
            }

#if DEBUG
            if (verbose)
            {
                JITDUMP($"VN based copy assertion for [{tree.TreeId:D6}] V{lclNum:D2} ${lclDefVN:x} by " +
                    $"[{newLclDef.GetDefNode().TreeId:D6}] V{newLclNum:D2} ${newLclDefVN:x}.\n");
                DISPNODE(tree);
            }
#endif
            var newSsaNum = newLclDef.SsaNum;
            assert(newSsaNum != SsaConfig.RESERVED_SSA_NUM);
            tree.LclNum = newLclNum;
            tree.SsaNum = newSsaNum;

            if (newLclDefVN != lclDefVN)
            {
                assert(vnStore is not null);
                tree._vnPair = newLclSsaDef._vnPair;
                var parent = gtFindLink(statement, tree).parent;
                while ((parent is not null) && (parent.Oper is GT_COMMA))
                {
#if DEBUG
                    JITDUMP($" Updating COMMA parent VN [{parent.TreeId:D6}]\n");
#endif
                    var op1Exceptions = vnStore.VNPExceptionSet(parent.AsOp().Op1._vnPair);
                    parent._vnPair = vnStore.VNPWithExc(parent.AsOp().Op2._vnPair, op1Exceptions);
                    parent = gtFindLink(statement, parent).parent;
                }
            }

            gtUpdateSideEffects(statement, tree);
            newLclSsaDef.AddUse(block);

#if DEBUG
            if (verbose)
            {
                JITDUMP("copy propagated to:\n");
                DISPNODE(tree);
            }
#endif
            return true;
        }
        return false;
    }

    public void optCopyPropPushDef(GenTreeLclVarCommon lclNode, int lclNum,
        int ssaNum, LclNumToLiveDefsMap curSsaName)
    {
        var nodeLclNum = lclNode.LclNum;
        if ((gsShadowVarInfo is not null) && lvaGetDesc(nodeLclNum).lvIsParam &&
            (gsShadowVarInfo[nodeLclNum].ShadowCopy != BAD_VAR_NUM))
        {
            assert(!curSsaName.ContainsKey(nodeLclNum));
            return;
        }

        if (ssaNum == SsaConfig.RESERVED_SSA_NUM)
        {
            return;
        }

        if (!curSsaName.TryGetValue(lclNum, out var stack))
        {
            stack = new CopyPropSsaDefStack();
            curSsaName.Add(lclNum, stack);
            s_copyPropKeyOrders.GetValue(curSsaName, static _ => new CopyPropKeyOrder()).Add(lclNum);
        }
        stack.Push(new CopyPropSsaDef(this, lclNum, ssaNum, lclNode));
    }

    private struct CopyPropPushDefVisitor(Compiler compiler, LclNumToLiveDefsMap names) : ILocalDefVisitor
    {
        public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            compiler.optCopyPropPushDef(def.DefNode, def.LclNum, def.GetSsaNum(compiler), names);
            return GenTree.VisitResult.Continue;
        }
    }

    public bool optBlockCopyProp(BasicBlock block, LclNumToLiveDefsMap curSsaName)
    {
#if DEBUG
        JITDUMP($"Copy Assertion for {FMT_BB(block.bbNum)}\n");
        if (verbose)
        {
            JITDUMP("  curSsaName stack: ");
            optDumpCopyPropStack(curSsaName);
        }
#endif
        var lifeUpdater = new TreeLifeUpdater(this, forCodeGen: false);
        var madeChanges = false;

        compCurLifeTree = null;
        VarSetOps.Assign(this, ref compCurLife, block.bbLiveIn);
        foreach (var statement in block.Statements)
        {
            foreach (var tree in statement.TreeList)
            {
                lifeUpdater.UpdateLife(tree);
                if (tree.Oper.IsSsaDef)
                {
                    var visitor = new CopyPropPushDefVisitor(this, curSsaName);
                    _ = tree.VisitLogicalLocalDefs(this, ref visitor);
                }
                else if ((tree.Oper is GT_LCL_VAR or GT_LCL_FLD) && tree.AsLclVarCommon().HasSsaName)
                {
                    var local = tree.AsLclVarCommon();
                    var localNum = local.LclNum;
                    if ((lvaGetDesc(localNum).lvIsParam || (localNum == info.compThisArg)) &&
                        !curSsaName.ContainsKey(localNum))
                    {
                        optCopyPropPushDef(local, localNum, local.SsaNum, curSsaName);
                    }

                    if (block.CatchType is BBCT_FINALLY or BBCT_FAULT)
                    {
                        continue;
                    }
                    madeChanges |= optCopyProp(block, statement, local, localNum, curSsaName);
                }
            }
        }
        return madeChanges;
    }

    private struct CopyPropDomTreeVisitor(Compiler compiler) : IDomTreeVisitor<CopyPropDomTreeVisitor>
    {
        private readonly Compiler _compiler = compiler;
        private readonly LclNumToLiveDefsMap _curSsaName = [];
        public bool MadeChanges { get; private set; }

        public readonly void Begin()
        {
        }

        public void PreOrderVisit(BasicBlock block)
            => MadeChanges |= _compiler.optBlockCopyProp(block, _curSsaName);

        public readonly void PostOrderVisit(BasicBlock block)
            => _compiler.optBlockCopyPropPopStacks(block, _curSsaName);

        public readonly void End()
        {
#if DEBUG
            foreach (var local in s_copyPropKeyOrders.GetValue(_curSsaName, static _ => new CopyPropKeyOrder()).Keys(_curSsaName))
            {
                assert(_compiler.lvaGetDesc(local).lvIsParam || local == _compiler.info.compThisArg);
                assert(_curSsaName[local].Count == 1);
            }
#endif
        }

        public void WalkTree(FlowGraphDominatorTree tree)
            => IDomTreeVisitor<CopyPropDomTreeVisitor>.WalkTree(ref this, _compiler, tree);
    }

    public PhaseStatus optVnCopyProp()
    {
        if (fgSsaPassesCompleted == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        VarSetOps.AssignNoCopy(this, ref compCurLife, VarSetOps.MakeEmpty(this));
        var visitor = new CopyPropDomTreeVisitor(this);
        visitor.WalkTree(_domTree ?? throw new InvalidOperationException("Copy propagation requires dominators."));

        // Tracked variable count increases after copy propagation; do not retain a shorter array.
        VarSetOps.AssignNoCopy(this, ref compCurLife, VarSetOps.UninitVal());

        return visitor.MadeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
