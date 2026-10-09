// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class AsyncMutationDataFlowCallback(
        Compiler compiler, VARSET_TP[] mutatedVars, VARSET_TP[] mutatedVarsIn) : DataFlow.ICallback
    {
        private VARSET_TP _preMergeIn = VarSetOps.UninitVal();

        public void StartMerge(BasicBlock block)
        {
            VarSetOps.Assign(compiler, ref _preMergeIn, mutatedVarsIn[block.bbNum]);
        }

        public void Merge(BasicBlock block, BasicBlock predecessor, int duplicateCount)
        {
            VarSetOps.UnionD(compiler, mutatedVarsIn[block.bbNum], mutatedVarsIn[predecessor.bbNum]);
            VarSetOps.UnionD(compiler, mutatedVarsIn[block.bbNum], mutatedVars[predecessor.bbNum]);
        }

        public void MergeHandler(BasicBlock block, BasicBlock firstTryBlock, BasicBlock lastTryBlock)
        {
            for (var predecessor = compiler.BlockPredsWithEH(block); predecessor is not null;
                 predecessor = predecessor.NextPredEdge)
            {
                Merge(block, predecessor.SourceBlock, predecessor.DupCount);
            }
        }

        public bool EndMerge(BasicBlock block) =>
            !VarSetOps.Equal(compiler, _preMergeIn, mutatedVarsIn[block.bbNum]);
    }

    private struct AsyncMutationVisitor(Compiler compiler, VARSET_TP mutated) : ILocalDefVisitor
    {
        public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            MarkAsyncIfTracked(compiler, def.LclNum, mutated);
            return GenTree.VisitResult.Continue;
        }
    }

    private static void MarkAsyncIfTracked(Compiler compiler, int lclNum, VARSET_TP mutated)
    {
        ref var descriptor = ref compiler.lvaGetDesc(lclNum);
        if (descriptor.lvTracked)
        {
            VarSetOps.AddElemD(compiler, mutated, descriptor._varIndex);
        }
    }

    private static void MarkAsyncMutatedVarDsc(Compiler compiler, int lclNum, VARSET_TP mutated)
    {
        ref var descriptor = ref compiler.lvaGetDesc(lclNum);
        if (descriptor.lvPromoted)
        {
            for (var i = 0; i < descriptor.lvFieldCnt; i++)
            {
                MarkAsyncIfTracked(compiler, descriptor.lvFieldLclStart + i, mutated);
            }
        }
        else
        {
            MarkAsyncIfTracked(compiler, lclNum, mutated);
        }
    }

    private static void MarkAsyncMutatedLocals(Compiler compiler, GenTree node, VARSET_TP mutated)
    {
        if (node.Oper is GT_LCL_ADDR)
        {
            MarkAsyncMutatedVarDsc(compiler, node.AsLclVarCommon().LclNum, mutated);
        }
        else if ((node.Oper is GT_LCL_VAR or GT_LCL_FLD) &&
                 compiler.lvaIsImplicitByRefLocal(node.AsLclVarCommon().LclNum))
        {
            MarkAsyncMutatedVarDsc(compiler, node.AsLclVarCommon().LclNum, mutated);
        }
        else
        {
            var visitor = new AsyncMutationVisitor(compiler, mutated);
            _ = node.VisitLogicalLocalDefs(compiler, ref visitor);
        }
    }

    internal static bool IsAsyncDefaultValue(GenTree node)
    {
        return node.IsIntegralConst(0) || node.IsFloatPositiveZero || node.IsVectorZero;
    }

    private static void UpdateAsyncMutatedLocals(Compiler compiler, GenTree node, VARSET_TP mutated)
    {
        if (node.Oper.IsLocalStore && IsAsyncDefaultValue(node.AsLclVarCommon().Data) &&
            !compiler.fgVarNeedsExplicitZeroInit(node.AsLclVarCommon().LclNum, bbInALoop: false, bbIsReturn: false))
        {
            return;
        }

        if (node.Oper is GT_LCL_ADDR)
        {
            MarkAsyncMutatedVarDsc(compiler, node.AsLclVarCommon().LclNum, mutated);
            return;
        }

        var visitor = new AsyncMutationVisitor(compiler, mutated);
        _ = node.VisitLogicalLocalDefs(compiler, ref visitor);
    }

    internal sealed class AsyncDefaultValueAnalysis(Compiler compiler)
    {
#if DEBUG
        private static ConfigMethodRange s_range;
#endif
        private VARSET_TP[]? _mutatedVarsIn;

        public unsafe void Run()
        {
#if DEBUG
            s_range.EnsureInit(JitConfig.JitAsyncDefaultValueAnalysisRange);
            if (!s_range.Contains(compiler.info.compMethodHash()))
            {
                JITDUMP("Default value analysis disabled because of method range\n");
                _mutatedVarsIn = CreateSets(compiler);
                foreach (var block in compiler.Blocks)
                {
                    VarSetOps.AssignNoCopy(compiler, ref _mutatedVarsIn[block.bbNum], VarSetOps.MakeFull(compiler));
                }

                return;
            }
#endif
            var mutatedVars = CreateSets(compiler);
            foreach (var block in compiler.Blocks)
            {
                foreach (var node in block)
                {
                    UpdateAsyncMutatedLocals(compiler, node, mutatedVars[block.bbNum]);
                }
            }

#if DEBUG
            JITDUMP("Default value analysis: per-block mutated vars\n");
            if (compiler.verbose)
            {
                foreach (var block in compiler.Blocks)
                {
                    if (!VarSetOps.IsEmpty(compiler, mutatedVars[block.bbNum]))
                    {
                        JITDUMP($"  {FMT_BB(block.bbNum)} mutated: {AsyncAnalysis.PrintVarSet(compiler, mutatedVars[block.bbNum])}\n");
                    }
                }
            }
#endif
            _mutatedVarsIn = CreateSets(compiler);
            var firstBlock = compiler.fgFirstBB ?? throw new InvalidOperationException("Async analysis requires an entry block");
            var trackedToVar = compiler.lvaTrackedToVarNum ??
                throw new InvalidOperationException("Async analysis requires tracked locals");

            for (var i = 0; i < compiler.lvaTrackedCount; i++)
            {
                var lclNum = trackedToVar[i];
                ref var descriptor = ref compiler.lvaGetDesc(lclNum);
                if (descriptor.lvIsParam || descriptor.lvIsParamRegTarget || descriptor.lvIsOSRLocal)
                {
                    VarSetOps.AddElemD(compiler, _mutatedVarsIn[firstBlock.bbNum], descriptor._varIndex);
                }
            }

            var callback = new AsyncMutationDataFlowCallback(compiler, mutatedVars, _mutatedVarsIn);
            new DataFlow(compiler).ForwardAnalysis(ref callback);

#if DEBUG
            JITDUMP("Default value analysis: per-block mutated vars on entry\n");
            if (compiler.verbose)
            {
                foreach (var block in compiler.Blocks)
                {
                    var values = _mutatedVarsIn[block.bbNum];
                    JITDUMP($"  {FMT_BB(block.bbNum)} mutated on entry: " +
                        $"{(VarSetOps.IsEmpty(compiler, values) ? "<none>" : AsyncAnalysis.PrintVarSet(compiler, values))}\n");
                }
            }
#endif
        }

        public VARSET_TP GetMutatedVarsIn(BasicBlock block)
        {
            assert(_mutatedVarsIn is not null);
            return _mutatedVarsIn[block.bbNum];
        }
    }

    internal sealed class AsyncPreservedValueAnalysis(Compiler compiler)
    {
#if DEBUG
        private static ConfigMethodRange s_range;
#endif
        private HashSet<int> _awaitBlocks = [];
        private HashSet<int> _resumeReachableBlocks = [];
        private VARSET_TP[]? _mutatedVarsIn;

        public unsafe void Run(IReadOnlyList<BasicBlock> awaitBlocks)
        {
#if DEBUG
            s_range.EnsureInit(JitConfig.JitAsyncPreservedValueAnalysisRange);
            if (!s_range.Contains(compiler.info.compMethodHash()))
            {
                JITDUMP("Preserved value analysis disabled because of method range\n");
                _mutatedVarsIn = CreateSets(compiler);
                foreach (var block in compiler.Blocks)
                {
                    VarSetOps.AssignNoCopy(compiler, ref _mutatedVarsIn[block.bbNum], VarSetOps.MakeFull(compiler));
                    _ = _awaitBlocks.Add(block.bbNum);
                    _ = _resumeReachableBlocks.Add(block.bbNum);
                }

                return;
            }
#endif
            var worklist = new Stack<BasicBlock>();
            foreach (var block in awaitBlocks)
            {
                _ = _awaitBlocks.Add(block.bbNum);
                worklist.Push(block);
            }

#if DEBUG
            JITDUMP("Preserved value analysis: blocks containing awaits\n");
            DumpBlocks("  Await blocks:", _awaitBlocks);
#endif
            while (worklist.Count != 0)
            {
                var block = worklist.Pop();
                _ = block.VisitAllSuccs(compiler, successor => {
                    if (_resumeReachableBlocks.Add(successor.bbNum))
                    {
                        worklist.Push(successor);
                    }

                    return BasicBlockVisit.Continue;
                });
            }

#if DEBUG
            JITDUMP("Preserved value analysis: blocks reachable after resuming\n");
            DumpBlocks("  Resume-reachable blocks:", _resumeReachableBlocks);
#endif
            var mutatedVars = CreateSets(compiler);
            foreach (var block in compiler.Blocks)
            {
                var isResumeReachable = _resumeReachableBlocks.Contains(block.bbNum);
                if (!isResumeReachable && !_awaitBlocks.Contains(block.bbNum))
                {
                    continue;
                }

                var node = block.FirstLIRNode;
                if (!isResumeReachable)
                {
                    while ((node is not GenTreeCall call) || !call.IsAsync)
                    {
                        assert(node is not null);
                        node = node.Next;
                    }
                }

                while (node is not null)
                {
                    MarkAsyncMutatedLocals(compiler, node, mutatedVars[block.bbNum]);
                    node = node.Next;
                }
            }

#if DEBUG
            JITDUMP("Preserved value analysis: per-block mutated vars after resumption\n");
            if (compiler.verbose)
            {
                foreach (var block in compiler.Blocks)
                {
                    if (!VarSetOps.IsEmpty(compiler, mutatedVars[block.bbNum]))
                    {
                        JITDUMP($"  {FMT_BB(block.bbNum)} mutated: {AsyncAnalysis.PrintVarSet(compiler, mutatedVars[block.bbNum])}\n");
                    }
                }
            }
#endif
            _mutatedVarsIn = CreateSets(compiler);
            var callback = new AsyncMutationDataFlowCallback(compiler, mutatedVars, _mutatedVarsIn);
            new DataFlow(compiler).ForwardAnalysis(ref callback);

#if DEBUG
            JITDUMP("Preserved value analysis: per-block mutated vars on entry\n");
            if (compiler.verbose)
            {
                foreach (var block in compiler.Blocks)
                {
                    var values = _mutatedVarsIn[block.bbNum];
                    var description = VarSetOps.IsEmpty(compiler, values) ? "<none>" :
                        VarSetOps.Equal(compiler, values, VarSetOps.MakeFull(compiler)) ? "<all>" :
                        AsyncAnalysis.PrintVarSet(compiler, values);
                    JITDUMP($"  {FMT_BB(block.bbNum)} mutated since resumption on entry: {description}\n");
                }
            }
#endif
        }

#if DEBUG
        private void DumpBlocks(string label, HashSet<int> numbers)
        {
            if (!compiler.verbose)
            {
                return;
            }

            var result = new StringBuilder(label);
            var separator = " ";
            foreach (var block in compiler.Blocks)
            {
                if (numbers.Contains(block.bbNum))
                {
                    _ = result.Append(separator).Append(FMT_BB(block.bbNum));
                    separator = ", ";
                }
            }

            JITDUMP($"{result}\n");
        }
#endif
        public VARSET_TP GetMutatedVarsIn(BasicBlock block)
        {
            assert(_mutatedVarsIn is not null);
            return _mutatedVarsIn[block.bbNum];
        }

        public bool IsResumeReachable(BasicBlock block) => _resumeReachableBlocks.Contains(block.bbNum);
    }

    private static VARSET_TP[] CreateSets(Compiler compiler)
    {
        var sets = new VARSET_TP[compiler.fgBBNumMax + 1];
        for (var i = 0; i < sets.Length; i++)
        {
            sets[i] = VarSetOps.MakeEmpty(compiler);
        }

        return sets;
    }

    internal sealed class AsyncAnalysis
    {
#if DEBUG
        public static string PrintVarSet(Compiler compiler, VARSET_TP set)
        {
            var trackedToVar = compiler.lvaTrackedToVarNum ??
                throw new InvalidOperationException("Async analysis requires tracked locals");
            var result = new StringBuilder();
            for (var index = 0; index < compiler.lvaTrackedCount; index++)
            {
                if (VarSetOps.IsMember(compiler, set, index))
                {
                    if (result.Length != 0)
                    {
                        _ = result.Append(' ');
                    }

                    _ = result.Append(System.Globalization.CultureInfo.InvariantCulture, $"V{trackedToVar[index]:D2}");
                }
            }

            return result.ToString();
        }
#endif
        private readonly Compiler _compiler;
        private readonly TreeLifeUpdater _updater;
        private readonly int _numVars;
        private readonly AsyncDefaultValueAnalysis _defaultValues;
        private readonly AsyncPreservedValueAnalysis _preservedValues;
        private VARSET_TP _mutatedValues;
        private VARSET_TP _mutatedSinceResumption;
        private bool _resumeReachable;

        public AsyncAnalysis(Compiler compiler, AsyncDefaultValueAnalysis defaultValues,
            AsyncPreservedValueAnalysis preservedValues)
        {
            _compiler = compiler;
            _updater = new TreeLifeUpdater(compiler, forCodeGen: false);
            _numVars = compiler.lvaCount;
            _defaultValues = defaultValues;
            _preservedValues = preservedValues;
            _mutatedValues = VarSetOps.MakeEmpty(compiler);
            _mutatedSinceResumption = VarSetOps.MakeEmpty(compiler);
        }

        public bool ResumeReachable => _resumeReachable;
        public VARSET_TP MutatedSinceResumption => _mutatedSinceResumption;

        public void StartBlock(BasicBlock block)
        {
            VarSetOps.Assign(_compiler, ref _compiler.compCurLife, block.bbLiveIn);
            VarSetOps.Assign(_compiler, ref _mutatedValues, _defaultValues.GetMutatedVarsIn(block));
            VarSetOps.Assign(_compiler, ref _mutatedSinceResumption, _preservedValues.GetMutatedVarsIn(block));
            _resumeReachable = _preservedValues.IsResumeReachable(block);
        }

        public void Update(GenTree node)
        {
            _updater.UpdateLife(node, generalLclAddrHandling: true);
            UpdateAsyncMutatedLocals(_compiler, node, _mutatedValues);

            _resumeReachable |= node is GenTreeCall call && call.IsAsync;
            if (_resumeReachable)
            {
                MarkAsyncMutatedLocals(_compiler, node, _mutatedSinceResumption);
            }
        }

        public void GetLiveLocals(AsyncContinuationLayoutBuilder layoutBuilder, Func<int, bool> includeLocal)
        {
            for (var lclNum = 0; lclNum < _numVars; lclNum++)
            {
                if (includeLocal(lclNum) && IsLive(lclNum))
                {
                    layoutBuilder.AddLocal(lclNum);
                }
            }
        }

        public bool IsLive(int lclNum)
        {
#if FEATURE_FIXED_OUT_ARGS
            if (lclNum == _compiler.lvaOutgoingArgSpaceVar)
            {
                return false;
            }
#endif

            if ((lclNum == _compiler.info.compRetBuffArg) ||
                (lclNum == _compiler.lvaGSSecurityCookie) ||
                (lclNum == _compiler.info.compLvFrameListRoot) ||
                (lclNum == _compiler.lvaInlinedPInvokeFrameVar) ||
                (lclNum == _compiler.lvaRetAddrVar) ||
                (lclNum == _compiler.lvaAsyncContinuationArg))
            {
                return false;
            }

            ref var descriptor = ref _compiler.lvaGetDesc(lclNum);
            if ((descriptor.Type is TYP_BYREF) && !descriptor.IsImplicitByRef)
            {
                return false;
            }

            if ((descriptor.Type is TYP_STRUCT) || descriptor.IsImplicitByRef)
            {
                var layout = descriptor.Layout ??
                    throw new InvalidOperationException("Struct local has no layout");
                if (layout.HasGCByRef())
                {
                    return false;
                }
            }

            if (_compiler.opts.compDbgCode && (lclNum < _compiler.info.compLocalsCount))
            {
                return true;
            }

            if (descriptor.lvRefCnt(RCS_NORMAL) == 0)
            {
                return false;
            }

            var promotionType = _compiler.lvaGetPromotionType(in descriptor);
            if (promotionType is PROMOTION_TYPE_INDEPENDENT)
            {
                return false;
            }

            if (promotionType is PROMOTION_TYPE_DEPENDENT)
            {
                var anyLive = false;
                var anyMutated = false;
                for (var i = 0; i < descriptor.lvFieldCnt; i++)
                {
                    ref var field = ref _compiler.lvaGetDesc(descriptor.lvFieldLclStart + i);
                    anyLive |= !field.lvTracked || VarSetOps.IsMember(_compiler, _compiler.compCurLife, field._varIndex);
                    anyMutated |= !field.lvTracked || VarSetOps.IsMember(_compiler, _mutatedValues, field._varIndex);
                }

                return anyLive && anyMutated;
            }

            if (descriptor.lvIsStructField &&
                (_compiler.lvaGetParentPromotionType(in descriptor) is PROMOTION_TYPE_DEPENDENT))
            {
                return false;
            }

            return !descriptor.lvTracked ||
                (VarSetOps.IsMember(_compiler, _compiler.compCurLife, descriptor._varIndex) &&
                 VarSetOps.IsMember(_compiler, _mutatedValues, descriptor._varIndex));
        }
    }
}
