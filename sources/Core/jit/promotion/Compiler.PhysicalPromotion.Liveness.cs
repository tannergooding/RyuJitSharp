// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotionliveness.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    internal readonly struct PhysicalPromotionStructDeaths(
        BitVec deaths, PhysicalPromotionAggregateInfo aggregate, BitVecTraits traits)
    {
        public bool IsRemainderDying()
        {
            return (aggregate.UnpromotedMax <= aggregate.UnpromotedMin) ||
                   BitVecOps.IsMember(traits, deaths, 0);
        }

        public bool IsReplacementDying(int index)
        {
            assert(index < aggregate.Replacements.Count);
            return BitVecOps.IsMember(traits, deaths, 1 + index);
        }
    }

    internal sealed class PhysicalPromotionLiveness
    {
        private sealed class BlockInfo(BitVecTraits traits)
        {
            public BitVec VarUse = BitVecOps.MakeEmpty(traits);
            public BitVec VarDef = BitVecOps.MakeEmpty(traits);
            public BitVec LiveIn = BitVecOps.MakeEmpty(traits);
            public BitVec LiveOut = BitVecOps.MakeEmpty(traits);
        }

        private struct DefinitionSizeVisitor(Compiler compiler, GenTreeLclVarCommon local) : ILocalDefVisitor
        {
            public int Size = -1;

            public GenTree.VisitResult Visit<TDef>(TDef definition) where TDef : struct, ILocalDef
            {
                if (!ReferenceEquals(definition.DefNode, local))
                {
                    return GenTree.VisitResult.Continue;
                }

                var size = definition.GetStoreSize(compiler);
                if (!size.IsExact)
                {
                    throw new InvalidOperationException("A promoted retbuffer definition must have an exact size.");
                }

                Size = checked((int)size.ExactSize);
                return GenTree.VisitResult.Abort;
            }
        }

        private readonly Compiler _compiler;
        private readonly PhysicalPromotionAggregateInfoMap _aggregates;
        private readonly Dictionary<GenTreeLclVarCommon, BitVec> _aggregateDeaths = [];
        private readonly int[] _structLclToTrackedIndex;
        private readonly BitVecTraits _traits;
        private readonly BlockInfo?[] _blockInfo;
        private readonly BitVec _liveIn;
        private readonly BitVec _ehLiveVars;

        public PhysicalPromotionLiveness(Compiler compiler, PhysicalPromotionAggregateInfoMap aggregates)
        {
            _compiler = compiler;
            _aggregates = aggregates;
            _structLclToTrackedIndex = new int[compiler.lvaCount];

            var trackedIndex = 0;
            foreach (var aggregate in aggregates.Aggregates)
            {
                _structLclToTrackedIndex[aggregate.LclNum] = trackedIndex;
                trackedIndex += 1 + aggregate.Replacements.Count;
#if DEBUG
                compiler.lvaGetDesc(aggregate.LclNum).lvTrackedWithoutIndex = true;
                foreach (var replacement in aggregate.Replacements)
                {
                    compiler.lvaGetDesc(replacement.LclNum).lvTrackedWithoutIndex = true;
                }
#endif
            }

            _traits = new BitVecTraits(compiler, trackedIndex);
            _blockInfo = new BlockInfo[compiler.fgBBNumMax + 1];
            _liveIn = BitVecOps.MakeEmpty(_traits);
            _ehLiveVars = BitVecOps.MakeEmpty(_traits);
        }

        public void Run()
        {
            JITDUMP($"Computing liveness for {BitVecTraits.GetSize(_traits)} remainders/fields\n\n");
            ComputeUseDefSets();
            InterBlockLiveness();
            FillInLiveness();
        }

        public bool IsReplacementLiveIn(BasicBlock block, int structLcl, int replacementIndex)
        {
            return BitVecOps.IsMember(_traits, GetBlockInfo(block).LiveIn,
                _structLclToTrackedIndex[structLcl] + 1 + replacementIndex);
        }

        public bool IsReplacementLiveOut(BasicBlock block, int structLcl, int replacementIndex)
        {
            return BitVecOps.IsMember(_traits, GetBlockInfo(block).LiveOut,
                _structLclToTrackedIndex[structLcl] + 1 + replacementIndex);
        }

        public PhysicalPromotionStructDeaths GetDeathsForStructLocal(GenTreeLclVarCommon local)
        {
            var aggregate = _aggregates.Lookup(local.LclNum);
            assert(aggregate is not null && ((local.Type is TYP_STRUCT) ||
                ((local.Oper is GT_LCL_ADDR) && ((local.Flags & GTF_VAR_DEF) != 0))));

            var deaths = _aggregateDeaths[local];
            var traits = new BitVecTraits(_compiler, aggregate.Replacements.Count + 1);
            return new PhysicalPromotionStructDeaths(deaths, aggregate, traits);
        }

        private BlockInfo GetBlockInfo(BasicBlock block)
        {
            return _blockInfo[block.bbNum] ??
                throw new InvalidOperationException("Physical promotion liveness has not initialized this block.");
        }

        private void ComputeUseDefSets()
        {
            foreach (var block in _compiler.Blocks)
            {
                var info = _blockInfo[block.bbNum] = new BlockInfo(_traits);
                foreach (var statement in block.Statements)
                {
                    var qmark = _compiler.compQmarkUsed
                        ? _compiler.fgGetTopLevelQmark(statement.RootNode, out _) : null;
                    foreach (var local in statement.LocalsTreeList)
                    {
                        if ((qmark is null) || ((local.Flags & GTF_VAR_DEF) == 0))
                        {
                            MarkUseDef(statement, local, info.VarUse, info.VarDef);
                        }
                    }
                }

#if DEBUG
                if (_compiler.verbose)
                {
                    var allVars = BitVecOps.Union(_traits, info.VarUse, info.VarDef);
                    jitprintf($"{FMT_BB(block.bbNum)} USE({BitVecOps.Count(_traits, info.VarUse)})=");
                    DumpVarSet(info.VarUse, allVars);
                    jitprintf($"\n{FMT_BB(block.bbNum)} DEF({BitVecOps.Count(_traits, info.VarDef)})=");
                    DumpVarSet(info.VarDef, allVars);
                    jitprintf("\n\n");
                }
#endif
            }
        }

        private void MarkUseDef(Statement statement, GenTreeLclVarCommon local,
                                BitVec useSet, BitVec defSet)
        {
            var aggregate = _aggregates.Lookup(local.LclNum);
            if (aggregate is null)
            {
                return;
            }

            var isDef = (local.Flags & GTF_VAR_DEF) != 0;
            var isUse = !isDef;
            var baseIndex = _structLclToTrackedIndex[local.LclNum];
            var replacements = aggregate.Replacements;
            if ((local.Type is TYP_STRUCT) || (local.Oper is GT_LCL_ADDR))
            {
                if (local.Oper.IsScalarLocal)
                {
                    for (var index = 0; index <= replacements.Count; index++)
                    {
                        MarkIndex(baseIndex + index, isUse, isDef, useSet, defSet);
                    }
                }
                else
                {
                    var offset = local.LclOffs;
                    var size = GetSizeOfStructLocal(statement, local);
                    var index = FirstOverlappingReplacement(replacements, offset, size);
                    while ((index < replacements.Count) && (replacements[index].Offset < offset + size))
                    {
                        var replacement = replacements[index];
                        var isFullFieldDef = isDef && (offset <= replacement.Offset) &&
                            (offset + size >= replacement.Offset + replacement.AccessType.Size);
                        MarkIndex(baseIndex + 1 + index, isUse, isFullFieldDef, useSet, defSet);
                        index++;
                    }

                    var isFullRemainderDef = isDef && (aggregate.UnpromotedMin >= offset) &&
                        (aggregate.UnpromotedMax <= offset + size);
                    var isRemainderUse = isUse && aggregate.Unpromoted.Intersects(
                        new SegmentList.Segment(offset, offset + size));
                    MarkIndex(baseIndex, isRemainderUse, isFullRemainderDef, useSet, defSet);
                }
            }
            else
            {
                var offset = local.LclOffs;
                var index = LowerBound(replacements, offset, static rep => rep.Offset);
                if ((index == replacements.Count) || (replacements[index].Offset != offset))
                {
                    var isFullRemainderDef = isDef && (aggregate.UnpromotedMin >= offset) &&
                        (aggregate.UnpromotedMax <= offset + local.Type.Size);
                    MarkIndex(baseIndex, isUse, isFullRemainderDef, useSet, defSet);
                }
                else
                {
                    MarkIndex(baseIndex + 1 + index, isUse, isDef, useSet, defSet);
                }
            }
        }

        private static int FirstOverlappingReplacement(
            List<PhysicalPromotionReplacement> replacements, int offset, int size)
        {
            var index = LowerBound(replacements, offset, static rep => rep.Offset);
            if ((index > 0) && replacements[index - 1].Overlaps(offset, size))
            {
                index--;
            }

            return index;
        }

        private int GetSizeOfStructLocal(Statement statement, GenTreeLclVarCommon local)
        {
            if (local.Oper is not GT_LCL_ADDR)
            {
                return checked((int)local.GetLayout(_compiler)!.Size);
            }

            var parent = _compiler.gtFindLink(statement, local).parent;
            assert(parent is GenTreeCall);
            var visitor = new DefinitionSizeVisitor(_compiler, local);
            var result = parent.VisitLogicalLocalDefs(_compiler, ref visitor);
            assert(result is GenTree.VisitResult.Abort);
            return visitor.Size >= 0 ? visitor.Size :
                throw new InvalidOperationException("The call must define its retbuffer local.");
        }

        private void MarkIndex(int index, bool isUse, bool isDef, BitVec useSet, BitVec defSet)
        {
            if (isUse && !BitVecOps.IsMember(_traits, defSet, index))
            {
                BitVecOps.AddElemD(_traits, useSet, index);
            }

            if (isDef)
            {
                BitVecOps.AddElemD(_traits, defSet, index);
            }
        }

        private void InterBlockLiveness()
        {
            var dfs = _compiler._dfsTree ??
                throw new InvalidOperationException("Physical promotion requires the current DFS tree.");
            bool changed;
            do
            {
                changed = false;
                for (var index = 0; index < dfs.PostOrderCount; index++)
                {
                    changed |= PerBlockLiveness(dfs.GetPostOrder(index));
                }
            }
            while (changed && dfs.HasCycle);

#if DEBUG
            if (_compiler.verbose)
            {
                foreach (var block in _compiler.Blocks)
                {
                    var info = GetBlockInfo(block);
                    var allVars = BitVecOps.Union(_traits, info.LiveIn, info.LiveOut);
                    jitprintf($"{FMT_BB(block.bbNum)} IN ({BitVecOps.Count(_traits, info.LiveIn)})=");
                    DumpVarSet(info.LiveIn, allVars);
                    jitprintf($"\n{FMT_BB(block.bbNum)} OUT({BitVecOps.Count(_traits, info.LiveOut)})=");
                    DumpVarSet(info.LiveOut, allVars);
                    jitprintf("\n\n");
                }
            }
#endif
        }

        private bool PerBlockLiveness(BasicBlock block)
        {
            assert(!block.EndsWithJmpMethod(_compiler));
            var info = GetBlockInfo(block);
            BitVecOps.ClearD(_traits, info.LiveOut);
            _ = block.VisitRegularSuccs(_compiler, successor => {
                BitVecOps.UnionD(_traits, info.LiveOut, GetBlockInfo(successor).LiveIn);
                return BasicBlockVisit.Continue;
            });

            BitVecOps.LivenessD(_traits, _liveIn, info.VarDef, info.VarUse, info.LiveOut);
            if (block.HasPotentialEHSuccs(_compiler))
            {
                BitVecOps.ClearD(_traits, _ehLiveVars);
                AddHandlerLiveVars(block, _ehLiveVars);
                BitVecOps.UnionD(_traits, _liveIn, _ehLiveVars);
                BitVecOps.UnionD(_traits, info.LiveOut, _ehLiveVars);
            }

            var changed = !BitVecOps.Equal(_traits, info.LiveIn, _liveIn);
            if (changed)
            {
                BitVecOps.Assign(_traits, ref info.LiveIn, _liveIn);
            }

            return changed;
        }

        private void AddHandlerLiveVars(BasicBlock block, BitVec vars)
        {
            _ = block.VisitEHSuccs(_compiler, successor => {
                BitVecOps.UnionD(_traits, vars, GetBlockInfo(successor).LiveIn);
                return BasicBlockVisit.Continue;
            });
        }

        private void FillInLiveness()
        {
            var life = BitVecOps.MakeEmpty(_traits);
            var volatileVars = BitVecOps.MakeEmpty(_traits);
            foreach (var block in _compiler.Blocks)
            {
                if (block.FirstStmt is null)
                {
                    continue;
                }

                BitVecOps.ClearD(_traits, volatileVars);
                if (block.HasPotentialEHSuccs(_compiler))
                {
                    AddHandlerLiveVars(block, volatileVars);
                }

                BitVecOps.Assign(_traits, ref life, GetBlockInfo(block).LiveOut);
                var statement = block.LastStmt!;
                while (true)
                {
                    var qmark = _compiler.compQmarkUsed
                        ? _compiler.fgGetTopLevelQmark(statement.RootNode, out _) : null;
                    for (var node = statement.TreeListEnd; node is not null; node = node.Prev)
                    {
                        assert(node.Oper.IsAnyLocal);
                        if ((qmark is null) || ((node.Flags & GTF_VAR_DEF) == 0))
                        {
                            FillInLiveness(life, volatileVars, statement, node.AsLclVarCommon());
                        }
                    }

                    if (statement == block.FirstStmt)
                    {
                        break;
                    }

                    statement = statement.PrevStmt!;
                }
            }
        }

        private void FillInLiveness(BitVec life, BitVec volatileVars, Statement statement,
                                    GenTreeLclVarCommon local)
        {
            var aggregate = _aggregates.Lookup(local.LclNum);
            if (aggregate is null)
            {
                return;
            }

            var isDef = (local.Flags & GTF_VAR_DEF) != 0;
            var isUse = !isDef;
            var baseIndex = _structLclToTrackedIndex[local.LclNum];
            var replacements = aggregate.Replacements;
            if ((local.Type is TYP_STRUCT) || (local.Oper is GT_LCL_ADDR))
            {
                var aggregateTraits = new BitVecTraits(_compiler, replacements.Count + 1);
                var deaths = BitVecOps.MakeEmpty(aggregateTraits);
                if (local.Oper.IsScalarLocal)
                {
                    for (var index = 0; index <= replacements.Count; index++)
                    {
                        MarkDeath(life, volatileVars, deaths, aggregateTraits,
                            baseIndex + index, index, isUse, isDef);
                    }
                }
                else
                {
                    var offset = local.LclOffs;
                    var size = GetSizeOfStructLocal(statement, local);
                    var index = FirstOverlappingReplacement(replacements, offset, size);
                    while ((index < replacements.Count) && (replacements[index].Offset < offset + size))
                    {
                        var replacement = replacements[index];
                        var fullDef = isDef && (offset <= replacement.Offset) &&
                            (offset + size >= replacement.Offset + replacement.AccessType.Size);
                        MarkDeath(life, volatileVars, deaths, aggregateTraits,
                            baseIndex + 1 + index, 1 + index, isUse, fullDef);
                        index++;
                    }

                    var fullRemainderDef = isDef && (aggregate.UnpromotedMin >= offset) &&
                        (aggregate.UnpromotedMax <= offset + size);
                    var remainderUse = isUse && aggregate.Unpromoted.Intersects(
                        new SegmentList.Segment(offset, offset + size));
                    MarkDeath(life, volatileVars, deaths, aggregateTraits,
                        baseIndex, 0, remainderUse, fullRemainderDef);
                }

                _aggregateDeaths[local] = deaths;
            }
            else
            {
                var offset = local.LclOffs;
                var index = LowerBound(replacements, offset, static rep => rep.Offset);
                var isRemainder = (index == replacements.Count) || (replacements[index].Offset != offset);
                var varIndex = baseIndex + (isRemainder ? 0 : 1 + index);
                var fullDef = isDef && (!isRemainder ||
                    ((aggregate.UnpromotedMin >= offset) &&
                     (aggregate.UnpromotedMax <= offset + local.Type.Size)));
                if (BitVecOps.IsMember(_traits, life, varIndex))
                {
                    local.Flags &= ~GTF_VAR_DEATH;
                    if (fullDef && !BitVecOps.IsMember(_traits, volatileVars, varIndex))
                    {
                        BitVecOps.RemoveElemD(_traits, life, varIndex);
                    }
                }
                else
                {
                    local.Flags |= GTF_VAR_DEATH;
                    if (isUse)
                    {
                        BitVecOps.AddElemD(_traits, life, varIndex);
                    }
                }
            }
        }

        private void MarkDeath(BitVec life, BitVec volatileVars, BitVec deaths, BitVecTraits deathTraits,
                               int varIndex, int deathIndex, bool isUse, bool isFullDef)
        {
            if (BitVecOps.IsMember(_traits, life, varIndex))
            {
                if (isFullDef && !BitVecOps.IsMember(_traits, volatileVars, varIndex))
                {
                    BitVecOps.RemoveElemD(_traits, life, varIndex);
                }
            }
            else
            {
                BitVecOps.AddElemD(deathTraits, deaths, deathIndex);
                if (isUse)
                {
                    BitVecOps.AddElemD(_traits, life, varIndex);
                }
            }
        }

#if DEBUG
        private void DumpVarSet(BitVec set, BitVec allVars)
        {
            jitprintf("{");
            var separator = "";
            foreach (var aggregate in _aggregates.Aggregates)
            {
                for (var index = 0; index <= aggregate.Replacements.Count; index++)
                {
                    var trackedIndex = _structLclToTrackedIndex[aggregate.LclNum] + index;
                    if (BitVecOps.IsMember(_traits, set, trackedIndex))
                    {
                        if (index == 0)
                        {
                            jitprintf($"{separator}V{aggregate.LclNum:D2}(remainder)");
                        }
                        else
                        {
                            var replacement = aggregate.Replacements[index - 1];
                            jitprintf($"{separator}V{aggregate.LclNum:D2}." +
                                $"[{replacement.Offset:D3}..{replacement.Offset + replacement.AccessType.Size:D3})");
                        }

                        separator = " ";
                    }
                    else if (BitVecOps.IsMember(_traits, allVars, trackedIndex))
                    {
                        jitprintf($"{separator}              ");
                        separator = " ";
                    }
                }
            }

            jitprintf("}");
        }
#endif
    }
}
