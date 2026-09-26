// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class SsaCheckVisitor
    {
        private sealed class SsaInfo
        {
            private BasicBlock? _defBlock;
            private BasicBlock? _useBlock;
            private uint _useCount;
            private bool _hasPhiUse;
            private bool _hasGlobalUse;
            private bool _hasMultipleDef;

            public BasicBlock? DefBlock => _defBlock;
            public int NumUses => _useCount > ushort.MaxValue ? ushort.MaxValue : (int)_useCount;
            public bool HasPhiUse => _hasPhiUse;
            public bool HasGlobalUse => _hasGlobalUse;
            public bool HasMultipleDef => _hasMultipleDef;

            public void AddUse(BasicBlock block)
            {
                // Uses can precede the definition in this traversal.
                if (_defBlock is null)
                {
                    if (_useBlock is null)
                    {
                        _useBlock = block;
                    }
                    else if (_useBlock != block)
                    {
                        _hasGlobalUse = true;
                    }
                }
                else if (_defBlock != block)
                {
                    _hasGlobalUse = true;
                }

                _useCount = unchecked(_useCount + 1);
            }

            public void AddPhiUse(BasicBlock block)
            {
                _hasPhiUse = true;
                AddUse(block);
            }

            public void AddDef(BasicBlock block)
            {
                if (_defBlock is null)
                {
                    if ((_useBlock is not null) && (_useBlock != block))
                    {
                        _hasGlobalUse = true;
                    }

                    _defBlock = block;
                }
                else
                {
                    _hasMultipleDef = true;
                }
            }
        }

        private readonly struct DefVisitor : ILocalDefVisitor
        {
            private readonly SsaCheckVisitor _checker;

            public DefVisitor(SsaCheckVisitor checker)
            {
                _checker = checker;
            }

            public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
            {
                var defNode = def.DefNode;
                var isUse = (defNode.Flags & GTF_VAR_USEASG) != 0;
                var lclNum = def.LclNum;
                ref var varDsc = ref _checker._compiler.lvaGetDesc(lclNum);

                assert(def.IsEntire(_checker._compiler) || isUse);

                var ssaNum = def.GetSsaNum(_checker._compiler);
                _checker.ProcessDef(defNode, lclNum, ssaNum);

                if (!def.IsEntire(_checker._compiler))
                {
                    assert(isUse);
                    var useSsaNum = SsaConfig.RESERVED_SSA_NUM;
                    if (ssaNum != SsaConfig.RESERVED_SSA_NUM)
                    {
                        useSsaNum = varDsc.GetPerSsaData(ssaNum).UseDefSsaNum;
                    }

                    _checker.ProcessUse(defNode, lclNum, useSsaNum);
                }

                return GenTree.VisitResult.Continue;
            }
        }

        private struct TreeWalker : IGenTreeVisitor<TreeWalker>
        {
            public static bool DoPreOrder => true;

            private readonly SsaCheckVisitor _checker;
            private readonly GenTreeStack _ancestors;

            public TreeWalker(SsaCheckVisitor checker)
            {
                _checker = checker;
                _ancestors = [];
            }

            public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
            {
                var tree = use;
                if (tree.Oper.IsSsaDef)
                {
                    _checker.ProcessDefs(tree);
                }
                else if (tree.Oper is GT_LCL_VAR or GT_LCL_FLD or GT_PHI_ARG)
                {
                    _checker.ProcessUses(tree.AsLclVarCommon());
                }

                return WALK_CONTINUE;
            }

            public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

            public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
                => IGenTreeVisitor<TreeWalker>.WalkTree(ref this, ref use, user, _ancestors);
        }

        private readonly Compiler _compiler;
        private readonly Dictionary<(int Local, int Ssa), SsaInfo> _infoMap = [];
        private BasicBlock? _block;
        private bool _hasErrors;

        public bool HasErrors => _hasErrors;

        public SsaCheckVisitor(Compiler compiler)
        {
            _compiler = compiler;
        }

        public void WalkTree(Statement statement)
        {
            var walker = new TreeWalker(this);
            _ = walker.WalkTree(ref statement.RootNodeRef, null);
        }

        private SsaInfo GetInfo(int lclNum, int ssaNum)
        {
            var key = (lclNum, ssaNum);
            if (!_infoMap.TryGetValue(key, out var info))
            {
                info = new SsaInfo();
                _infoMap.Add(key, info);
            }

            return info;
        }

        private void ProcessDef(GenTree tree, int lclNum, int ssaNum)
        {
            if (!_compiler.lvaGetDesc(lclNum).lvInSsa)
            {
                if (ssaNum != SsaConfig.RESERVED_SSA_NUM)
                {
                    SetHasErrors();
                    JITDUMP($"[error] Unexpected SSA number on def [{tree.TreeId:D6}] (V{lclNum:D2})\n");
                }
                return;
            }

            if (ssaNum == SsaConfig.RESERVED_SSA_NUM)
            {
                SetHasErrors();
                JITDUMP($"[error] Missing SSA number on def [{tree.TreeId:D6}] (V{lclNum:D2})\n");
                return;
            }

            assert(_block is not null);
            GetInfo(lclNum, ssaNum).AddDef(_block);
        }

        private void ProcessDefs(GenTree tree)
        {
            var visitor = new DefVisitor(this);
            _ = tree.VisitLogicalLocalDefs(_compiler, ref visitor);
        }

        private void ProcessUse(GenTreeLclVarCommon tree, int lclNum, int ssaNum)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
            if (!varDsc.lvInSsa)
            {
                if (ssaNum != SsaConfig.RESERVED_SSA_NUM)
                {
                    SetHasErrors();
                    JITDUMP($"[error] Unexpected SSA number on [{tree.TreeId:D6}] (V{lclNum:D2})\n");
                }
                return;
            }

            if (ssaNum == SsaConfig.RESERVED_SSA_NUM)
            {
                if (varDsc.lvPerSsaData.Count > 0)
                {
                    SetHasErrors();
                    JITDUMP($"[error] Missing SSA number on use [{tree.TreeId:D6}] (V{lclNum:D2})\n");
                }
                return;
            }

            assert(_block is not null);
            var info = GetInfo(lclNum, ssaNum);
            if (tree.Oper is GT_PHI_ARG)
            {
                info.AddPhiUse(_block);
            }
            else
            {
                info.AddUse(_block);
            }
        }

        private void ProcessUses(GenTreeLclVarCommon tree)
        {
            var lclNum = tree.LclNum;
            if (tree.HasCompositeSsaName)
            {
                SetHasErrors();
                JITDUMP($"[error] Composite SSA number on use [{tree.TreeId:D6}] (V{lclNum:D2})\n");
                return;
            }

            ProcessUse(tree, lclNum, tree.SsaNum);
        }

        private void SetHasErrors()
        {
            if (!_hasErrors)
            {
                JITDUMP("fgDebugCheckSsa: errors found\n");
                _hasErrors = true;
            }
        }

        public void SetBlock(BasicBlock block)
        {
            _block = block;
            CheckPhis(block);
        }

        private void CheckPhis(BasicBlock block)
        {
            Statement? nonPhiStmt = null;
            foreach (var statement in block.Statements)
            {
                if (!statement.IsPhiDefnStmt)
                {
                    nonPhiStmt ??= statement;
                    continue;
                }

                if (nonPhiStmt is not null)
                {
                    SetHasErrors();
                    JITDUMP($"[error] {FMT_BB(block.bbNum)} PhiDef {FMT_STMT(statement.Id)} appears after " +
                            $"non-PhiDef {FMT_STMT(nonPhiStmt.Id)}\n");
                }

                var phiDefNode = statement.RootNode.AsLclVar();
                var phi = phiDefNode.Data.AsPhi();
                assert(phiDefNode.IsPhiDefn);

                var bitVecTraits = new BitVecTraits(_compiler, _compiler.fgBBNumMax + 1);
                var phiPreds = BitVecOps.MakeEmpty(bitVecTraits);

                foreach (var use in phi.Uses)
                {
                    var phiArgNode = use.Node.AsPhiArg();
                    if (phiArgNode.LclNum != phiDefNode.LclNum)
                    {
                        SetHasErrors();
                        JITDUMP($"[error] Wrong local V{phiArgNode.LclNum:D2} in PhiArg [{phiArgNode.TreeId:D6}] " +
                                $"-- expected V{phiDefNode.LclNum:D2}\n");
                    }

                    if (_compiler.bbIsHandlerBeg(block))
                    {
                        // Handler PHIs allow repeated and implicit predecessors.
                        continue;
                    }

                    var phiArgBlock = phiArgNode.PredBB;
                    if (phiArgBlock is not null)
                    {
                        if (BitVecOps.IsMember(bitVecTraits, phiPreds, phiArgBlock.bbNum))
                        {
                            SetHasErrors();
                            JITDUMP($"[error] {FMT_BB(block.bbNum)} [{phi.TreeId:D6}]: multiple PhiArgs " +
                                    $"for predBlock {FMT_BB(phiArgBlock.bbNum)}\n");
                        }

                        BitVecOps.AddElemD(bitVecTraits, phiPreds, phiArgBlock.bbNum);
                        if (_compiler.fgGetPredForBlock(block, phiArgBlock) is null)
                        {
                            JITDUMP($"[info] {FMT_BB(block.bbNum)} [{phi.TreeId:D6}]: stale PhiArg " +
                                    $"[{phiArgNode.TreeId:D6}] pred block {FMT_BB(phiArgBlock.bbNum)}\n");
                        }
                    }
                }
            }
        }

        public void DoChecks()
        {
            for (var lclNum = 0; lclNum < _compiler.lvaCount; lclNum++)
            {
                ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
                if (!varDsc.lvInSsa)
                {
                    continue;
                }

                ref var ssaDefs = ref varDsc.lvPerSsaData;
                for (var index = 0; index < ssaDefs.Count; index++)
                {
                    ref var ssaVarDsc = ref ssaDefs.GetSsaDefByIndex(index);
                    var ssaNum = ssaDefs.GetSsaNum(in ssaVarDsc);
                    if (!_infoMap.TryGetValue((lclNum, ssaNum), out var info))
                    {
                        continue;
                    }

                    var infoDefBlock = info.DefBlock;
                    var ssaDefBlock = ssaVarDsc.Block;
                    if (infoDefBlock != ssaDefBlock)
                    {
                        // Native bookkeeping tolerates either origin for initial parameter/local values.
                        var initialValOfParamOrLocal = (lclNum < _compiler.lvaCount) &&
                                                       (ssaNum == SsaConfig.FIRST_SSA_NUM);
                        var noDefBlockOrFirstBB = (infoDefBlock is null) &&
                                                  (ssaDefBlock == _compiler.fgFirstBB);
                        if (!(initialValOfParamOrLocal && noDefBlockOrFirstBB))
                        {
                            JITDUMP($"[error] Wrong def block for V{lclNum:D2}.{ssaNum} : IR " +
                                    $"{FMT_BB(infoDefBlock?.bbNum ?? 0)} SSA {FMT_BB(ssaDefBlock?.bbNum ?? 0)}\n");
                            SetHasErrors();
                        }
                    }

                    var infoUses = info.NumUses;
                    var ssaUses = ssaVarDsc.NumUses;
                    if (infoUses > ssaUses)
                    {
                        JITDUMP($"[error] NumUses underestimated for V{lclNum:D2}.{ssaNum}: " +
                                $"IR {infoUses} SSA {ssaUses}\n");
                        SetHasErrors();
                    }
                    else if (infoUses < ssaUses)
                    {
                        JITDUMP($"[info] NumUses overestimated for V{lclNum:D2}.{ssaNum}: " +
                                $"IR {infoUses} SSA {ssaUses}\n");
                    }

                    if (info.HasPhiUse && !ssaVarDsc.HasPhiUse)
                    {
                        JITDUMP($"[error] HasPhiUse underestimated for V{lclNum:D2}.{ssaNum}\n");
                        SetHasErrors();
                    }
                    else if (!info.HasPhiUse && ssaVarDsc.HasPhiUse)
                    {
                        JITDUMP($"[info] HasPhiUse overestimated for V{lclNum:D2}.{ssaNum}\n");
                    }

                    if (info.HasGlobalUse && !ssaVarDsc.HasGlobalUse)
                    {
                        JITDUMP($"[error] HasGlobalUse underestimated for V{lclNum:D2}.{ssaNum}\n");
                        SetHasErrors();
                    }
                    else if (!info.HasGlobalUse && ssaVarDsc.HasGlobalUse)
                    {
                        JITDUMP($"[info] HasGlobalUse overestimated for V{lclNum:D2}.{ssaNum}\n");
                    }

                    if (info.HasMultipleDef)
                    {
                        JITDUMP($"[error] HasMultipleDef for V{lclNum:D2}.{ssaNum}\n");
                        SetHasErrors();
                    }
                }
            }
        }
    }

    public void fgDebugCheckSsa()
    {
        if (!fgSsaValid)
        {
            return;
        }

        assert(fgSsaPassesCompleted > 0);
        assert(_dfsTree is not null);

        var visitor = new SsaCheckVisitor(this);
        for (var index = 0; index < _dfsTree.PostOrderCount; index++)
        {
            var block = _dfsTree.GetPostOrder(index);
            visitor.SetBlock(block);
            foreach (var statement in block.Statements)
            {
                visitor.WalkTree(statement);
            }
        }

        visitor.DoChecks();
        if (visitor.HasErrors)
        {
            assert(false, "SSA check failures");
        }
        else
        {
            JITDUMP("SSA checks completed successfully\n");
        }
    }
}
#endif
