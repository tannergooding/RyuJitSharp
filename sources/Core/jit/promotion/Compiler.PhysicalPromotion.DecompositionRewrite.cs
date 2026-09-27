// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotiondecomposition.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed unsafe partial class PhysicalPromotionReplaceVisitor
    {
        private bool OverlappingReplacements(GenTreeLclVarCommon local, out int first, out int end)
        {
            first = end = 0;
            var aggregate = _aggregates.Lookup(local.LclNum);
            return (aggregate is not null) &&
                aggregate.OverlappingReplacements(local.LclOffs,
                    checked((int)(local.GetLayout(_compiler) ??
                        throw new InvalidOperationException("A struct local requires a layout.")).Size), out first, out end);
        }

        private void EliminateCommasInBlockOp(GenTree store, PhysicalPromotionDecompositionStatementList result)
        {
            var changed = false;
            var source = store.Data;
            if (!store.IsReverseOp && store.Oper.IsIndir && (source.Oper is GT_COMMA))
            {
                var address = store.AsIndir().Addr;
                if (((address.Flags & GTF_ALL_EFFECT) != 0) ||
                    (((source.Flags & GTF_ASG) != 0) && !address.IsInvariant) ||
                    _compiler.PhysicalPromotionHasAddressExposedLocals(address))
                {
                    var temp = _compiler.lvaGrabTemp(true, "Block morph store addr");
                    result.AddStatement(_compiler.gtNewTempStore(temp, address));
                    store.AsIndir().Addr = _compiler.gtNewLclvNode(address.Type, temp);
                    _compiler.gtUpdateNodeSideEffects(store);
                    changed = true;
                }
            }

            while (source.Oper is GT_COMMA)
            {
                result.AddStatement(source.AsOp().Op1);
                source = source.AsOp().Op2;
                changed = true;
            }

            if (changed)
            {
                store.DataRef = source;
                _compiler.gtUpdateNodeSideEffects(store);
                _madeChanges = true;
            }
        }

        private void InitFields(GenTreeLclVarCommon destination, PhysicalPromotionAggregateInfo aggregate,
            int first, int end, PhysicalPromotionDecompositionPlan plan)
        {
            for (var index = first; index < end; index++)
            {
                var replacement = aggregate.Replacements[index];
                if (!plan.CanInitPrimitive(replacement.AccessType))
                {
                    JITDUMP($"  Unsupported init of {replacement.AccessType.Name} " +
                        $"{replacement.Description}. Will init as struct and read back.\n");
                    ClearNeedsWriteBack(replacement);
                    SetNeedsReadBack(replacement);
                    plan.MarkNonRemainderUseOfStructLocal();
                    continue;
                }

#if DEBUG
                if (_compiler.verbose)
                {
                    JITDUMP($"  Init V{replacement.LclNum:D2} ({replacement.Description})" +
                        $"{LastUseString(destination, replacement)}\n");
                }
#endif

                plan.InitReplacement(replacement, replacement.Offset - destination.LclOffs);
            }
        }

#if DEBUG
        private string LastUseString(GenTreeLclVarCommon? local, PhysicalPromotionReplacement replacement)
        {
            if (local is null)
            {
                throw new InvalidOperationException("A replacement must belong to a struct local.");
            }

            var deaths = _liveness.GetDeathsForStructLocal(local);
            var aggregate = _aggregates.Lookup(local.LclNum) ??
                throw new InvalidOperationException("A replacement must belong to a promoted aggregate.");
            var index = aggregate.Replacements.IndexOf(replacement);
            assert(index >= 0);
            return deaths.IsReplacementDying(index) ? " (last use)" : "";
        }
#endif

        private void CopyBetweenFields(GenTree store, PhysicalPromotionAggregateInfo? dstAggregate,
            int dstIndex, int dstEnd, GenTree source, PhysicalPromotionAggregateInfo? srcAggregate,
            int srcIndex, int srcEnd, PhysicalPromotionDecompositionStatementList statements,
            PhysicalPromotionDecompositionPlan plan)
        {
            var dstLocal = store.Oper.IsLocalStore ? store.AsLclVarCommon() : null;
            var srcLocal = source.Oper is GT_LCL_VAR or GT_LCL_FLD ? source.AsLclVarCommon() : null;
            var dstOffset = dstLocal?.LclOffs ?? 0;
            var srcOffset = srcLocal?.LclOffs ?? 0;

            while ((dstIndex < dstEnd) || (srcIndex < srcEnd))
            {
                var dst = dstIndex < dstEnd ? dstAggregate!.Replacements[dstIndex] : null;
                var src = srcIndex < srcEnd ? srcAggregate!.Replacements[srcIndex] : null;
                if ((src is not null) && src.NeedsReadBack)
                {
                    JITDUMP($"  Source replacement V{src.LclNum:D2} ({src.Description}) " +
                        "is stale. Will read it back before copy.\n");
                    statements.AddStatement(_compiler.PhysicalPromotionCreateReadBack(srcLocal!.LclNum, src));
                    ClearNeedsReadBack(src);
                    assert(!src.NeedsWriteBack);
                }

                if ((dst is not null) && (src is not null))
                {
                    if (src.Offset - srcOffset + src.AccessType.Size <= dst.Offset - dstOffset)
                    {
                        var offset = src.Offset - srcOffset;
                        plan.CopyFromReplacement(src, offset);
#if DEBUG
                        if (_compiler.verbose)
                        {
                            JITDUMP($"  dst+{offset:D3} <- V{src.LclNum:D2} ({src.Description})" +
                                $"{LastUseString(srcLocal, src)}\n");
                        }
#endif
                        srcIndex++;
                        continue;
                    }

                    if (dst.Offset - dstOffset + dst.AccessType.Size <= src.Offset - srcOffset)
                    {
                        var offset = dst.Offset - dstOffset;
                        plan.CopyToReplacement(dst, offset);
#if DEBUG
                        if (_compiler.verbose)
                        {
                            JITDUMP($"  V{dst.LclNum:D2} ({dst.Description})" +
                                $"{LastUseString(dstLocal, dst)} <- src+{offset:D3}\n");
                        }
#endif
                        dstIndex++;
                        continue;
                    }

                    if (((dst.Offset - dstOffset) == (src.Offset - srcOffset)) &&
                        (dst.AccessType == src.AccessType))
                    {
                        plan.CopyBetweenReplacements(dst, src, dst.Offset - dstOffset);
#if DEBUG
                        if (_compiler.verbose)
                        {
                            JITDUMP($"  V{dst.LclNum:D2} ({dst.Description})" +
                                $"{LastUseString(dstLocal, dst)} <- V{src.LclNum:D2} ({src.Description})" +
                                $"{LastUseString(srcLocal, src)}\n");
                        }
#endif
                        dstIndex++;
                        srcIndex++;
                        continue;
                    }

                    statements.AddStatement(_compiler.PhysicalPromotionCreateWriteBack(srcLocal!.LclNum, src));
#if DEBUG
                    if (_compiler.verbose)
                    {
                        JITDUMP($"  Partial overlap of V{dst.LclNum:D2} ({dst.Description})" +
                            $"{LastUseString(dstLocal, dst)} <- V{src.LclNum:D2} ({src.Description})" +
                            $"{LastUseString(srcLocal, src)}. Will read source back before copy\n");
                    }
#endif
                    srcIndex++;
                    continue;
                }

                if (dst is not null)
                {
                    var offset = dst.Offset - dstOffset;
                    plan.CopyToReplacement(dst, offset);
#if DEBUG
                    if (_compiler.verbose)
                    {
                        JITDUMP($"  V{dst.LclNum:D2} ({dst.Description})" +
                            $"{LastUseString(dstLocal, dst)} <- src+{offset:D3}\n");
                    }
#endif
                    dstIndex++;
                }
                else
                {
                    var offset = src!.Offset - srcOffset;
                    plan.CopyFromReplacement(src, offset);
#if DEBUG
                    if (_compiler.verbose)
                    {
                        JITDUMP($"  dst+{offset:D3} <- V{src.LclNum:D2} ({src.Description})" +
                            $"{LastUseString(srcLocal, src)}\n");
                    }
#endif
                    srcIndex++;
                }
            }

            if ((dstLocal is not null) && (srcLocal is not null) &&
                (dstLocal.LclNum == srcLocal.LclNum) && (dstOffset > srcOffset) &&
                (dstOffset - srcOffset < (srcLocal.GetLayout(_compiler) ??
                    throw new InvalidOperationException("A struct local requires a layout.")).Size))
            {
                JITDUMP("  Reversing copy order for overlapping slices of the same local\n");
                plan.Reverse();
            }
        }

        private void HandleStructStore(ref GenTree use)
        {
            var store = use;
            assert(store.Type is TYP_STRUCT);

            var source = store.Data.EffectiveVal;
            var dstLocal = store.Oper.IsLocalStore ? store.AsLclVarCommon() : null;
            var srcLocal = source.Oper is GT_LCL_VAR or GT_LCL_FLD ? source.AsLclVarCommon() : null;
            var dstFirst = 0;
            var dstEnd = 0;
            var srcFirst = 0;
            var srcEnd = 0;
            var dstInvolves = (dstLocal is not null) &&
                OverlappingReplacements(dstLocal, out dstFirst, out dstEnd);
            var srcInvolves = (srcLocal is not null) &&
                OverlappingReplacements(srcLocal, out srcFirst, out srcEnd);
            if (!dstInvolves && !srcInvolves)
            {
                return;
            }

#if DEBUG
            JITDUMP($"Processing block operation [{store.TreeId:D6}] that involves replacements\n");
#endif
            if ((source.Oper is GT_LCL_VAR or GT_LCL_FLD or GT_BLK) || source.IsCnsInitVal)
            {
                var result = new PhysicalPromotionDecompositionStatementList();
                EliminateCommasInBlockOp(store, result);
                source = store.Data;
                var plan = new PhysicalPromotionDecompositionPlan(_compiler, this, _aggregates,
                    _liveness, store, source, dstInvolves, srcInvolves);
                var dstAggregate = dstInvolves ? _aggregates.Lookup(dstLocal!.LclNum)! : null;
                var srcAggregate = srcInvolves ? _aggregates.Lookup(srcLocal!.LclNum)! : null;
                if (dstInvolves)
                {
                    var start = dstLocal!.LclOffs;
                    var end = start + checked((int)(dstLocal.GetLayout(_compiler) ??
                        throw new InvalidOperationException("A struct local requires a layout.")).Size);
                    if (dstAggregate!.Replacements[dstFirst].Offset < start)
                    {
                        var replacement = dstAggregate.Replacements[dstFirst++];
                        JITDUMP("*** Block operation partially overlaps with start replacement " +
                            $"of destination V{replacement.LclNum:D2} ({replacement.Description})\n");
                        if (replacement.NeedsWriteBack)
                        {
                            result.AddStatement(_compiler.PhysicalPromotionCreateWriteBack(dstLocal.LclNum, replacement));
                            ClearNeedsWriteBack(replacement);
                        }

                        SetNeedsReadBack(replacement);
                        plan.MarkNonRemainderUseOfStructLocal();
                    }

                    if ((dstFirst < dstEnd) &&
                        (dstAggregate.Replacements[dstEnd - 1].Offset +
                         dstAggregate.Replacements[dstEnd - 1].AccessType.Size > end))
                    {
                        var replacement = dstAggregate.Replacements[--dstEnd];
                        JITDUMP("*** Block operation partially overlaps with end replacement " +
                            $"of destination V{replacement.LclNum:D2} ({replacement.Description})\n");
                        if (replacement.NeedsWriteBack)
                        {
                            result.AddStatement(_compiler.PhysicalPromotionCreateWriteBack(dstLocal.LclNum, replacement));
                            ClearNeedsWriteBack(replacement);
                        }

                        SetNeedsReadBack(replacement);
                        plan.MarkNonRemainderUseOfStructLocal();
                    }
                }

                if (srcInvolves)
                {
                    var start = srcLocal!.LclOffs;
                    var end = start + checked((int)(srcLocal.GetLayout(_compiler) ??
                        throw new InvalidOperationException("A struct local requires a layout.")).Size);
                    if (srcAggregate!.Replacements[srcFirst].Offset < start)
                    {
                        var replacement = srcAggregate.Replacements[srcFirst++];
                        JITDUMP("*** Block operation partially overlaps with start replacement " +
                            $"of source V{replacement.LclNum:D2} ({replacement.Description})\n");
                        if (replacement.NeedsWriteBack)
                        {
                            result.AddStatement(_compiler.PhysicalPromotionCreateWriteBack(srcLocal.LclNum, replacement));
                            ClearNeedsWriteBack(replacement);
                        }
                    }

                    if ((srcFirst < srcEnd) &&
                        (srcAggregate.Replacements[srcEnd - 1].Offset +
                         srcAggregate.Replacements[srcEnd - 1].AccessType.Size > end))
                    {
                        var replacement = srcAggregate.Replacements[--srcEnd];
                        JITDUMP("*** Block operation partially overlaps with end replacement " +
                            $"of source V{replacement.LclNum:D2} ({replacement.Description})\n");
                        if (replacement.NeedsWriteBack)
                        {
                            result.AddStatement(_compiler.PhysicalPromotionCreateWriteBack(srcLocal.LclNum, replacement));
                            ClearNeedsWriteBack(replacement);
                        }
                    }
                }

                if (source.IsCnsInitVal)
                {
                    InitFields(dstLocal!, dstAggregate!, dstFirst, dstEnd, plan);
                }
                else
                {
                    CopyBetweenFields(store, dstAggregate, dstFirst, dstEnd, source,
                        srcAggregate, srcFirst, srcEnd, result, plan);
                }

                plan.Finalize(result);
                use = result.ToCommaTree(_compiler);
                _madeChanges = true;
            }
            else
            {
                if (store.Data.Oper is GT_LCL_VAR or GT_LCL_FLD)
                {
                    var local = store.Data.AsLclVarCommon();
                    ref var data = ref store.DataRef;
                    WriteBackBeforeUse(ref data, local.LclNum, local.LclOffs,
                        checked((int)(local.GetLayout(_compiler) ??
                            throw new InvalidOperationException("A struct local requires a layout.")).Size));
                }

                if (dstLocal is not null)
                {
                    MarkForReadBack(dstLocal, checked((int)(dstLocal.GetLayout(_compiler) ??
                        throw new InvalidOperationException("A struct local requires a layout.")).Size),
                        "cannot decompose store");
                }
            }
        }
    }
}
