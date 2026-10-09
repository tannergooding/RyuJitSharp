// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private static ConfigMethodRange s_physicalPromotionRange;
#endif

    private unsafe bool PhysicalPromotionIsEnabled()
    {
        if (!opts.OptEnabled(CLFLG_STRUCTPROMOTE) || fgNoStructPromotion)
        {
            return false;
        }

        if ((JitConfig.JitEnablePhysicalPromotion == 0) &&
            !compStressCompile(STRESS_PHYSICAL_PROMOTION, 25))
        {
            return false;
        }

#if DEBUG
        s_physicalPromotionRange.EnsureInit(JitConfig.JitEnablePhysicalPromotionRange);
        if (!s_physicalPromotionRange.Contains(info.compMethodHash()))
        {
            return false;
        }
#endif
        return true;
    }

    private bool PhysicalPromotionHaveCandidateLocals()
    {
        for (var lclNum = 0; lclNum < lvaCount; lclNum++)
        {
            if (PhysicalPromotionIsCandidate(lvaGetDesc(lclNum)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PhysicalPromotionIsCandidate(LclVarDsc descriptor)
    {
        return (descriptor.Type is TYP_STRUCT) && !descriptor.lvPromoted && !descriptor.IsAddressExposed;
    }

    internal static SegmentList PhysicalPromotionCopyNonPadding(SegmentList nonPadding)
    {
        var copy = new SegmentList();
        foreach (var segment in nonPadding)
        {
            copy.Add(segment);
        }

        return copy;
    }

    private bool PhysicalPromotionMapsToParameterRegister(int lclNum, int offset, var_types accessType)
    {
        assert(lclNum < info.compArgsCount);
        if (opts.IsOSR)
        {
            return false;
        }

        ref readonly var abiInfo = ref lvaGetParameterAbiInfo(lclNum);
        if (abiInfo.IsPassedByReference || abiInfo.HasAnyStackSegment)
        {
            return false;
        }

        foreach (ref readonly var segment in abiInfo.Segments)
        {
            if ((offset < segment.Offset) ||
                (offset + accessType.Size > segment.Offset + segment.Size))
            {
                continue;
            }

            if (!genIsValidIntReg(segment.Register) && varTypeUsesFloatReg(accessType))
            {
                continue;
            }

            if (genIsValidFloatReg(segment.Register) && (offset != segment.Offset))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // Selection is kept private to the inactive packet: creating replacement locals
    // without subsequently running liveness and rewriting would change the method.
    private PhysicalPromotionAggregateInfoMap? PhysicalPromotionSelectCandidates()
    {
        if (!PhysicalPromotionHaveCandidateLocals())
        {
            return null;
        }

        var visitor = new PhysicalPromotionUsesVisitor(this, lvaCount);
        foreach (var block in Blocks)
        {
            visitor.SetBB(block);
            foreach (var statement in block.Statements)
            {
                _ = statement.VisitLogicalLocalOccurrencesViaLocalsTreeList(occurrence => {
                    if (PhysicalPromotionIsCandidate(lvaGetDesc(occurrence.LclNum)))
                    {
                        _ = visitor.WalkTree(ref statement.RootNodeRef, null);
                        return GenTree.VisitResult.Abort;
                    }

                    return GenTree.VisitResult.Continue;
                });
            }
        }

        var aggregates = new PhysicalPromotionAggregateInfoMap(lvaCount);
        if (!visitor.PickPromotions(aggregates))
        {
            return null;
        }

        return aggregates;
    }

    private unsafe struct PhysicalPromotionUsesVisitor : IGenTreeVisitor<PhysicalPromotionUsesVisitor>
    {
        private readonly Compiler _compiler;
        private readonly PhysicalPromotionLocalUses?[] _uses;
        private readonly List<(GenTreeLclVarCommon Store, BasicBlock Block)> _candidateStores = [];
        private readonly GenTreeStack _ancestors = [];
        private BasicBlock _block = null!;

        public PhysicalPromotionUsesVisitor(Compiler compiler, int localCount)
        {
            _compiler = compiler;
            _uses = new PhysicalPromotionLocalUses[localCount];
        }

        public static bool DoPreOrder => true;
        public static bool ComputeStack => true;

        public void SetBB(BasicBlock block) => _block = block;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<PhysicalPromotionUsesVisitor>.WalkTree(ref this, ref use, user, _ancestors);

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (!use.Oper.IsAnyLocal)
            {
                return WALK_CONTINUE;
            }

            var local = use.AsLclVarCommon();
            ref var descriptor = ref _compiler.lvaGetDesc(local.LclNum);
            var isCandidate = PhysicalPromotionIsCandidate(descriptor);

            if (isCandidate)
            {
                var accessType = local.Type;
                ClassLayout? layout;
                PhysicalPromotionAccessKindFlags flags;
                if (local.Oper is GT_LCL_ADDR)
                {
#if DEBUG
                    assert(user is GenTreeCall && descriptor.IsDefinedViaAddress);
#endif
                    accessType = TYP_STRUCT;
                    layout = _compiler.typGetObjLayout(user!.AsCall().RetClsHnd);
                    flags = PhysicalPromotionAccessKindFlags.IsCallRetBuf;
                }
                else
                {
                    var effectiveUser = user is not null && (user.Oper is GT_COMMA)
                        ? EffectiveUser() : user;
                    layout = accessType is TYP_STRUCT ? local.GetLayout(_compiler) : null;
                    flags = ClassifyLocalAccess(local, effectiveUser);
                }

#if DEBUG
                if ((flags & (PhysicalPromotionAccessKindFlags.IsCallRetBuf |
                              PhysicalPromotionAccessKindFlags.IsStoreDestination)) != 0)
                {
                    assert(!IsInsideQmarkArm());
                }
#endif
                GetOrCreateUses(local.LclNum).RecordAccess(local.LclOffs, accessType, layout,
                    flags, _block.getBBWeight(_compiler));
            }

            if (local.Oper.IsLocalStore && (local.Type is TYP_STRUCT))
            {
                var data = local.Data.EffectiveVal;
                if (data.Oper.IsLocalRead &&
                    (isCandidate || PhysicalPromotionIsCandidate(_compiler.lvaGetDesc(data.AsLclVarCommon().LclNum))))
                {
                    _candidateStores.Add((local, _block));
                }
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        private readonly PhysicalPromotionLocalUses GetOrCreateUses(int lclNum)
        {
            return _uses[lclNum] ??= new PhysicalPromotionLocalUses();
        }

        private readonly GenTree? EffectiveUser()
        {
            var ancestors = _ancestors.ToArray();
            for (var index = 1; index < ancestors.Length; index++)
            {
                var parent = ancestors[index];
                var child = ancestors[index - 1];
                if (parent.Oper is not GT_COMMA)
                {
                    return parent;
                }

                if (ReferenceEquals(parent.AsOp().Op1, child))
                {
                    return null;
                }
            }

            return null;
        }

#if DEBUG
        private readonly bool IsInsideQmarkArm()
        {
            var ancestors = _ancestors.ToArray();
            for (var index = 1; index < ancestors.Length; index++)
            {
                if (ancestors[index].Oper is GT_COLON)
                {
                    return true;
                }
            }

            return false;
        }
#endif

        private readonly PhysicalPromotionAccessKindFlags ClassifyLocalAccess(GenTreeLclVarCommon local, GenTree? user)
        {
            var flags = PhysicalPromotionAccessKindFlags.None;
            if (local.Oper.IsLocalStore)
            {
#if DEBUG
                flags |= PhysicalPromotionAccessKindFlags.IsStoreDestination;
#endif
                if (local.Data.EffectiveVal.Oper.IsCall)
                {
                    flags |= PhysicalPromotionAccessKindFlags.IsStoredFromCall;
                }
            }

            if (user is GenTreeCall call)
            {
                foreach (var arg in call.Args.Args)
                {
                    if (!ReferenceEquals(arg.Node.EffectiveVal, local))
                    {
                        continue;
                    }

                    flags |= PhysicalPromotionAccessKindFlags.IsCallArg;
                    if (!call.Args.IsAbiInformationDetermined)
                    {
                        call.Args.DetermineAbiInfo(_compiler, call);
                    }

                    if (!arg.AbiInfo.HasAnyStackSegment && !arg.AbiInfo.IsPassedByReference)
                    {
                        flags |= PhysicalPromotionAccessKindFlags.IsRegCallArg;
                    }

                    break;
                }
            }

#if DEBUG
            if (user is not null)
            {
                if (user.Oper.IsStore && ReferenceEquals(user.Data.EffectiveVal, local))
                {
                    flags |= PhysicalPromotionAccessKindFlags.IsStoreSource;
                }

                if (user.Oper is GT_RETURN or GT_SWIFT_ERROR_RET)
                {
                    flags |= PhysicalPromotionAccessKindFlags.IsReturned;
                }
            }
#endif

            return flags;
        }

        public readonly bool PickPromotions(PhysicalPromotionAggregateInfoMap aggregates)
        {
            JITDUMP("Picking promotions\n");
            var total = 0;
            var limit = JitConfig.JitMaxLocalsToTrack;
            for (var lclNum = 0; lclNum < _uses.Length; lclNum++)
            {
                if (_uses[lclNum] is PhysicalPromotionLocalUses uses)
                {
#if DEBUG
                    if (_compiler.verbose)
                    {
                        uses.DumpAccesses(lclNum);
                    }
#endif
                    total += uses.PickPromotions(_compiler, lclNum, aggregates);
                    if (total >= limit)
                    {
                        JITDUMP($"Promoted {total} fields which is over our limit of {limit}; " +
                            "will not promote more\n");
                        break;
                    }
                }
            }

            if ((_candidateStores.Count != 0) && (total < limit))
            {
                JITDUMP($"Looking for induced accesses with {_candidateStores.Count} stores between candidates\n");
                for (var iteration = 0; iteration < 10; iteration++)
                {
                    foreach (var (store, block) in _candidateStores)
                    {
                        var source = store.Data.EffectiveVal.AsLclVarCommon();
                        ref var destinationDesc = ref _compiler.lvaGetDesc(store.LclNum);
                        ref var sourceDesc = ref _compiler.lvaGetDesc(source.LclNum);

                        if (destinationDesc.lvPromoted)
                        {
                            InduceFromRegularPromotion(aggregates, source, store, block);
                        }
                        else if (sourceDesc.lvPromoted)
                        {
                            InduceFromRegularPromotion(aggregates, store, source, block);
                        }
                        else
                        {
                            if (PhysicalPromotionIsCandidate(destinationDesc))
                            {
                                InduceInCandidate(aggregates, store, source, block);
                            }

                            if (PhysicalPromotionIsCandidate(sourceDesc))
                            {
                                InduceInCandidate(aggregates, source, store, block);
                            }
                        }
                    }

                    var again = false;
                    for (var lclNum = 0; lclNum < _uses.Length; lclNum++)
                    {
                        if (_uses[lclNum] is not PhysicalPromotionLocalUses uses)
                        {
                            continue;
                        }

#if DEBUG
                        if (_compiler.verbose)
                        {
                            uses.DumpInducedAccesses(lclNum);
                        }
#endif
                        var num = uses.PickInducedPromotions(_compiler, lclNum, aggregates);
                        again |= num > 0;
                        total += num;
                        if (total >= limit)
                        {
                            JITDUMP($"Promoted {total} fields and our limit is {limit}; " +
                                "will not promote more\n");
                            again = false;
                            break;
                        }
                    }

                    if (!again)
                    {
                        break;
                    }

                    foreach (var uses in _uses)
                    {
                        uses?.ClearInducedAccesses();
                    }
                }
            }

            _compiler.Metrics.PhysicallyPromotedFields += total;
            if (total == 0)
            {
                return false;
            }

            foreach (var aggregate in aggregates.Aggregates)
            {
                foreach (var replacement in aggregate.Replacements)
                {
                    replacement.Description = $"V{aggregate.LclNum:D2}.[{replacement.Offset:D3}..{replacement.Offset + replacement.AccessType.Size:D3})";
                    replacement.LclNum = _compiler.lvaGrabTemp(false, replacement.Description);
                    ref var descriptor = ref _compiler.lvaGetDesc(replacement.LclNum);
                    descriptor.Type = replacement.AccessType;
                    if ((replacement.Offset == OFFSETOF__CORINFO_Span__length) &&
                        (replacement.AccessType is TYP_INT) && _compiler.lvaGetDesc(aggregate.LclNum).IsSpan)
                    {
                        descriptor.IsNeverNegative = true;
                    }
                }

#if DEBUG
                JITDUMP($"V{aggregate.LclNum:D2} promoted with {aggregate.Replacements.Count} replacements\n");
                foreach (var replacement in aggregate.Replacements)
                {
                    JITDUMP($"  [{replacement.Offset:D3}..{replacement.Offset + replacement.AccessType.Size:D3}) " +
                        $"promoted as {replacement.AccessType.Name} V{replacement.LclNum:D2}\n");
                }
#endif

                // The managed layout caches its SegmentList; native assignment copies the segments.
                aggregate.Unpromoted = PhysicalPromotionCopyNonPadding(
                    _compiler.lvaGetDesc(aggregate.LclNum).Layout!.GetNonPadding(_compiler));
                foreach (var replacement in aggregate.Replacements)
                {
                    aggregate.Unpromoted.Subtract(new SegmentList.Segment((uint)replacement.Offset,
                        (uint)(replacement.Offset + replacement.AccessType.Size)));
                }

                JITDUMP("  Unpromoted remainder: ");
#if DEBUG
                if (_compiler.verbose)
                {
                    aggregate.Unpromoted.Dump();
                }
#endif
                JITDUMP("\n\n");

                if (aggregate.Unpromoted.CoveringSegment(out var segment))
                {
                    aggregate.UnpromotedMin = checked((int)segment.Start);
                    aggregate.UnpromotedMax = checked((int)segment.End);
                }
            }

            return true;
        }

        private readonly void InduceFromRegularPromotion(PhysicalPromotionAggregateInfoMap aggregates,
            GenTreeLclVarCommon candidate, GenTreeLclVarCommon promoted, BasicBlock block)
        {
            var promotedOffset = promoted.LclOffs;
            var size = checked((int)promoted.GetLayout(_compiler)!.Size);
            ref var descriptor = ref _compiler.lvaGetDesc(promoted.LclNum);
            for (var index = 0; index < descriptor.lvFieldCnt; index++)
            {
                ref var field = ref _compiler.lvaGetDesc(descriptor.lvFieldLclStart + index);
                if ((field.lvFldOffset >= promotedOffset) &&
                    (field.lvFldOffset + field.Type.Size <= promotedOffset + size))
                {
                    InduceAccess(aggregates, candidate.LclNum,
                        candidate.LclOffs + field.lvFldOffset - promotedOffset, field.Type, block);
                }
            }
        }

        private readonly void InduceInCandidate(PhysicalPromotionAggregateInfoMap aggregates,
            GenTreeLclVarCommon candidate, GenTreeLclVarCommon inducer, BasicBlock block)
        {
            var inducerOffset = inducer.LclOffs;
            var size = checked((int)candidate.GetLayout(_compiler)!.Size);
            var aggregate = aggregates.Lookup(inducer.LclNum);
            if ((aggregate is null) || !aggregate.OverlappingReplacements(inducerOffset, size,
                    out var first, out var end))
            {
                return;
            }

            for (var index = first; index < end; index++)
            {
                var replacement = aggregate.Replacements[index];
                if ((replacement.Offset >= inducerOffset) &&
                    (replacement.Offset + replacement.AccessType.Size <= inducerOffset + size))
                {
                    InduceAccess(aggregates, candidate.LclNum,
                        candidate.LclOffs + replacement.Offset - inducerOffset, replacement.AccessType, block);
                }
            }
        }

        private readonly void InduceAccess(PhysicalPromotionAggregateInfoMap aggregates,
                                  int lclNum, int offset, var_types type, BasicBlock block)
        {
            var aggregate = aggregates.Lookup(lclNum);
            if ((aggregate is not null) &&
                aggregate.OverlappingReplacements(offset, type.Size, out _, out _))
            {
                return;
            }

            GetOrCreateUses(lclNum).RecordInducedAccess(offset, type, block.getBBWeight(_compiler));
        }
    }
}
