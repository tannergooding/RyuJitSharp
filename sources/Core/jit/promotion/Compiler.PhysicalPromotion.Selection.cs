// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.h and promotion.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RyuJitSharp;

public partial class Compiler
{
    internal const int PHYSICAL_PROMOTION_MAX_PROMOTIONS_PER_STRUCT = 64;

    internal sealed class PhysicalPromotionReplacement(int offset, var_types accessType)
    {
        public BasicBlock? ReadBackPlacement;
        public int Offset = offset;
        public var_types AccessType = accessType;
        public int LclNum = BAD_VAR_NUM;
        // Dense index into the inter-block pending-readback sets.
        public int ReadBackIndex = BAD_VAR_NUM;
        public string Description = "";
        public bool NeedsWriteBack = true;
        // May remain true across blocks when all incoming paths have a current struct field.
        public bool NeedsReadBack;

        public bool Overlaps(int otherStart, int otherSize)
        {
            return (Offset + AccessType.Size > otherStart) &&
                   (otherStart + otherSize > Offset);
        }
    }

    internal sealed class PhysicalPromotionAggregateInfo(int lclNum)
    {
        public readonly List<PhysicalPromotionReplacement> Replacements = [];
        public readonly int LclNum = lclNum;
        public SegmentList Unpromoted = [];
        public int UnpromotedMin;
        public int UnpromotedMax;

        public bool OverlappingReplacements(int offset, int size, out int first, out int end)
        {
            first = 0;
            end = 0;

            if (Replacements.Count == 0)
            {
                return false;
            }

            first = LowerBound(Replacements, offset, static rep => rep.Offset);
            if ((first > 0) && Replacements[first - 1].Overlaps(offset, size))
            {
                first--;
            }

            if ((first == Replacements.Count) || !Replacements[first].Overlaps(offset, size))
            {
                return false;
            }

            end = LowerBound(Replacements, offset + size, static rep => rep.Offset);
            return true;
        }
    }

    internal sealed class PhysicalPromotionAggregateInfoMap(int numLocals)
    {
        private readonly int[] _indices = CreateIndices(numLocals);
        public readonly List<PhysicalPromotionAggregateInfo> Aggregates = [];

        private static int[] CreateIndices(int count)
        {
            var indices = new int[count];
            Array.Fill(indices, -1);
            return indices;
        }

        public void Add(PhysicalPromotionAggregateInfo aggregate)
        {
            assert(_indices[aggregate.LclNum] == -1);
            _indices[aggregate.LclNum] = Aggregates.Count;
            Aggregates.Add(aggregate);
        }

        public PhysicalPromotionAggregateInfo? Lookup(int lclNum)
        {
            var index = _indices[lclNum];
            return index == -1 ? null : Aggregates[index];
        }
    }

    internal static int LowerBound<T>(List<T> entries, int offset, Func<T, int> getOffset)
    {
        var min = 0;
        var max = entries.Count;
        while (min < max)
        {
            var mid = min + ((max - min) / 2);
            if (getOffset(entries[mid]) < offset)
            {
                min = mid + 1;
            }
            else
            {
                max = mid;
            }
        }

        return min;
    }

    [Flags]
    internal enum PhysicalPromotionAccessKindFlags
    {
        None = 0,
        IsCallArg = 1,
        IsRegCallArg = 2,
        IsStoredFromCall = 4,
        IsCallRetBuf = 8,
        IsStoreSource = 16,
        IsStoreDestination = 32,
        IsReturned = 64,
    }

    internal sealed class PhysicalPromotionAccess(int offset, var_types accessType, ClassLayout? layout)
    {
        public readonly int Offset = offset;
        public readonly var_types AccessType = accessType;
        public readonly ClassLayout? Layout = layout;
        public int Count;
        public int CountStoredFromCall;
        public int CountCallArgs;
        public int CountRegCallArgs;
        public weight_t CountWtd;
        public weight_t CountStoredFromCallWtd;
        public weight_t CountCallArgsWtd;
        public weight_t CountRegCallArgsWtd;
        public int CountStoreSource;
        public int CountStoreDestination;
        public int CountReturns;
        public int CountPassedAsRetbuf;
        public weight_t CountStoreSourceWtd;
        public weight_t CountStoreDestinationWtd;
        public weight_t CountReturnsWtd;
        public weight_t CountPassedAsRetbufWtd;

        public int GetAccessSize() {
    bool __jitExceptionCondition623;
    __jitExceptionCondition623 = AccessType is TYP_STRUCT;
    if (__jitExceptionCondition623)
    {
        int __jitExceptionReturnResult624;
        int __jitExceptionOperand625;
        uint __jitExceptionOperand626;
        global::RyuJitSharp.ClassLayout __jitExceptionOperand627;
        var __jitExceptionCoalesceLeft628 = Layout;
        if (__jitExceptionCoalesceLeft628 != null)
        {
            __jitExceptionOperand627 = __jitExceptionCoalesceLeft628;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.StructAccessRequiresLayout);
            __jitExceptionOperand627 = default;
        }

        __jitExceptionOperand626 = __jitExceptionOperand627.Size;
        __jitExceptionOperand625 = (int)__jitExceptionOperand626;
        __jitExceptionReturnResult624 = checked(__jitExceptionOperand625);
        return __jitExceptionReturnResult624;
    }
    else
    {
        return AccessType.Size;
    }
}
        public bool Overlaps(int otherStart, int otherSize)
        {
            return (Offset + GetAccessSize() > otherStart) &&
                   (otherStart + otherSize > Offset);
        }
    }

    internal sealed class PhysicalPromotionPrimitiveAccess(int offset, var_types accessType)
    {
        public int Count;
        public weight_t CountWtd;
        public readonly int Offset = offset;
        public readonly var_types AccessType = accessType;
    }

    internal sealed class PhysicalPromotionLocalUses
    {
        private readonly List<PhysicalPromotionAccess> _accesses = [];
        private readonly List<PhysicalPromotionPrimitiveAccess> _inducedAccesses = [];

        public IReadOnlyList<PhysicalPromotionAccess> Accesses => _accesses;

        public void RecordAccess(int offset, var_types type, ClassLayout? layout,
                                 PhysicalPromotionAccessKindFlags flags, weight_t weight)
        {
            var index = LowerBound(_accesses, offset, static access => access.Offset);
            PhysicalPromotionAccess? access = null;

            while ((index < _accesses.Count) && (_accesses[index].Offset == offset))
            {
                var candidate = _accesses[index];
                if ((candidate.AccessType == type) && ReferenceEquals(candidate.Layout, layout))
                {
                    access = candidate;
                    break;
                }

                index++;
            }

            if (access is null)
            {
                access = new PhysicalPromotionAccess(offset, type, layout);
                _accesses.Insert(index, access);
            }

            access.Count++;
            access.CountWtd += weight;

            if ((flags & PhysicalPromotionAccessKindFlags.IsCallArg) != 0)
            {
                access.CountCallArgs++;
                access.CountCallArgsWtd += weight;

                if ((flags & PhysicalPromotionAccessKindFlags.IsRegCallArg) != 0)
                {
                    access.CountRegCallArgs++;
                    access.CountRegCallArgsWtd += weight;
                }
            }

            if ((flags & (PhysicalPromotionAccessKindFlags.IsStoredFromCall |
                          PhysicalPromotionAccessKindFlags.IsCallRetBuf)) != 0)
            {
                access.CountStoredFromCall++;
                access.CountStoredFromCallWtd += weight;
            }

            if (Configuration.DEBUG)
            {
                if ((flags & PhysicalPromotionAccessKindFlags.IsCallRetBuf) != 0)
                {
                    access.CountPassedAsRetbuf++;
                    access.CountPassedAsRetbufWtd += weight;
                }

                if ((flags & PhysicalPromotionAccessKindFlags.IsStoreSource) != 0)
                {
                    access.CountStoreSource++;
                    access.CountStoreSourceWtd += weight;
                }

                if ((flags & PhysicalPromotionAccessKindFlags.IsStoreDestination) != 0)
                {
                    access.CountStoreDestination++;
                    access.CountStoreDestinationWtd += weight;
                }

                if ((flags & PhysicalPromotionAccessKindFlags.IsReturned) != 0)
                {
                    access.CountReturns++;
                    access.CountReturnsWtd += weight;
                }
            }
        }

        [Conditional("DEBUG")]
        public void DumpAccesses(int lclNum)
        {
            if (!Configuration.DEBUG)
            {
                return;
            }

            if (_accesses.Count == 0)
            {
                return;
            }

            jitprintf($"Accesses for V{lclNum:D2}\n");
            foreach (var access in _accesses)
            {
                if (access.AccessType is TYP_STRUCT)
                {
                    global::RyuJitSharp.ClassLayout __jitExceptionResult139;
                    var __jitExceptionCoalesceLeft140 = access.Layout;
                    if (__jitExceptionCoalesceLeft140 != null)
                    {
                        __jitExceptionResult139 = __jitExceptionCoalesceLeft140;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.StructAccessRequiresLayout);
                        __jitExceptionResult139 = default;
                    }
                    var layout = __jitExceptionResult139;
                    jitprintf($"  [{access.Offset:D3}..{access.Offset + layout.Size:D3}) as " +
                        $"{layout.ClassName}\n");
                }
                else
                {
                    jitprintf($"  {access.AccessType.Name} @ {access.Offset:D3}\n");
                }

                jitprintf($"    #:                             ({access.Count}, {FMT_WT(access.CountWtd)})\n");
                jitprintf($"    # store source:                ({access.CountStoreSource}, " +
                    $"{FMT_WT(access.CountStoreSourceWtd)})\n");
                jitprintf($"    # store destination:           ({access.CountStoreDestination}, " +
                    $"{FMT_WT(access.CountStoreDestinationWtd)})\n");
                jitprintf($"    # as call arg:                 ({access.CountCallArgs}, " +
                    $"{FMT_WT(access.CountCallArgsWtd)})\n");
                jitprintf($"    # as reg call arg:             ({access.CountRegCallArgs}, " +
                    $"{FMT_WT(access.CountRegCallArgsWtd)})\n");
                jitprintf($"    # as retbuf:                   ({access.CountPassedAsRetbuf}, " +
                    $"{FMT_WT(access.CountPassedAsRetbufWtd)})\n");
                jitprintf($"    # as returned value:           ({access.CountReturns}, " +
                    $"{FMT_WT(access.CountReturnsWtd)})\n\n");
            }
        }

        [Conditional("DEBUG")]
        public void DumpInducedAccesses(int lclNum)
        {
            if (!Configuration.DEBUG)
            {
                return;
            }

            if (_inducedAccesses.Count == 0)
            {
                return;
            }

            jitprintf($"Induced accesses for V{lclNum:D2}\n");
            foreach (var access in _inducedAccesses)
            {
                jitprintf($"  {access.AccessType.Name} @ {access.Offset:D3}\n");
                jitprintf($"    #: ({access.Count}, {FMT_WT(access.CountWtd)})\n");
            }
        }
        public void RecordInducedAccess(int offset, var_types type, weight_t weight)
        {
            var index = LowerBound(_inducedAccesses, offset, static access => access.Offset);
            PhysicalPromotionPrimitiveAccess? access = null;

            while ((index < _inducedAccesses.Count) && (_inducedAccesses[index].Offset == offset))
            {
                if (_inducedAccesses[index].AccessType == type)
                {
                    access = _inducedAccesses[index];
                    break;
                }

                index++;
            }

            if (access is null)
            {
                access = new PhysicalPromotionPrimitiveAccess(offset, type);
                _inducedAccesses.Insert(index, access);
            }

            access.Count++;
            access.CountWtd += weight;
        }

        public void ClearInducedAccesses() => _inducedAccesses.Clear();

        public int PickPromotions(Compiler compiler, int lclNum, PhysicalPromotionAggregateInfoMap aggregates)
        {
            if (_accesses.Count == 0)
            {
                return 0;
            }

            JITDUMP($"Picking promotions for V{lclNum:D2}\n");
            PhysicalPromotionAggregateInfo? aggregate = null;
            var numReplacements = 0;
            foreach (var access in _accesses)
            {
                if ((access.AccessType is TYP_STRUCT) ||
                    !EvaluateReplacement(compiler, lclNum, access, 0, 0))
                {
                    continue;
                }

                aggregate ??= AddAggregate(aggregates, lclNum);
                aggregate.Replacements.Add(new PhysicalPromotionReplacement(access.Offset, access.AccessType));
                numReplacements++;

                if (aggregate.Replacements.Count >= PHYSICAL_PROMOTION_MAX_PROMOTIONS_PER_STRUCT)
                {
                    JITDUMP($"  Promoted {aggregate.Replacements.Count} fields in V{aggregate.LclNum:D2}; " +
                        "will not promote more\n");
                    break;
                }
            }

            JITDUMP("\n");
            return numReplacements;
        }

        public int PickInducedPromotions(Compiler compiler, int lclNum, PhysicalPromotionAggregateInfoMap aggregates)
        {
            if (_inducedAccesses.Count == 0)
            {
                return 0;
            }

            var aggregate = aggregates.Lookup(lclNum);
            if ((aggregate is not null) &&
                (aggregate.Replacements.Count >= PHYSICAL_PROMOTION_MAX_PROMOTIONS_PER_STRUCT))
            {
                return 0;
            }

            var numReplacements = 0;
            JITDUMP($"Picking induced promotions for V{lclNum:D2}\n");
            foreach (var induced in _inducedAccesses)
            {
                var overlaps = false;
                foreach (var other in _inducedAccesses)
                {
                    if (ReferenceEquals(other, induced))
                    {
                        continue;
                    }

                    if (induced.Offset + induced.AccessType.Size <= other.Offset)
                    {
                        break;
                    }

                    if (other.Offset + other.AccessType.Size > induced.Offset)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (overlaps)
                {
                    continue;
                }

                var access = FindAccess(induced.Offset, induced.AccessType) ??
                    new PhysicalPromotionAccess(induced.Offset, induced.AccessType, null);
                if (!EvaluateReplacement(compiler, lclNum, access, induced.Count, induced.CountWtd))
                {
                    continue;
                }

                aggregate ??= AddAggregate(aggregates, lclNum);
                var index = LowerBound(aggregate.Replacements, induced.Offset, static rep => rep.Offset);
                assert(!aggregate.OverlappingReplacements(induced.Offset,
                    induced.AccessType.Size, out _, out _));
                aggregate.Replacements.Insert(index, new PhysicalPromotionReplacement(induced.Offset, induced.AccessType));
                numReplacements++;

                if (aggregate.Replacements.Count >= PHYSICAL_PROMOTION_MAX_PROMOTIONS_PER_STRUCT)
                {
                    JITDUMP($"  Promoted {aggregate.Replacements.Count} fields in V{lclNum:D2}; " +
                        "will not promote more\n");
                    break;
                }
            }

            return numReplacements;
        }

        private static PhysicalPromotionAggregateInfo AddAggregate(PhysicalPromotionAggregateInfoMap aggregates, int lclNum)
        {
            var aggregate = new PhysicalPromotionAggregateInfo(lclNum);
            aggregates.Add(aggregate);
            return aggregate;
        }

        private PhysicalPromotionAccess? FindAccess(int offset, var_types type)
        {
            var index = LowerBound(_accesses, offset, static access => access.Offset);
            while ((index < _accesses.Count) && (_accesses[index].Offset == offset))
            {
                if (_accesses[index].AccessType == type)
                {
                    return _accesses[index];
                }

                index++;
            }

            return null;
        }

        private bool EvaluateReplacement(Compiler compiler, int lclNum, PhysicalPromotionAccess access,
                                         int inducedCount, weight_t inducedCountWtd)
        {
            ref var local = ref compiler.lvaGetDesc(lclNum);
            global::RyuJitSharp.ClassLayout __jitExceptionResult629;
            var __jitExceptionCoalesceLeft630 = local.Layout;
            if (__jitExceptionCoalesceLeft630 != null)
            {
                __jitExceptionResult629 = __jitExceptionCoalesceLeft630;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CandidateRequiresStructLayout);
                __jitExceptionResult629 = default;
            }
            var layout = __jitExceptionResult629;
            var accessSize = access.AccessType.Size;

            if (layout.IntersectsGCPtr(access.Offset, accessSize))
            {
                var pointerSize = Configuration.TARGET_POINTER_SIZE;
                if (((access.Offset % pointerSize) != 0) ||
                    (layout.GetGCPtrType(access.Offset / pointerSize) != access.AccessType))
                {
                    return false;
                }
            }
            else if (varTypeIsGC(access.AccessType))
            {
                return false;
            }

            var overlappedCallArgs = 0;
            var overlappedStoredFromCall = 0;
            weight_t overlappedCallArgsWtd = 0;
            weight_t overlappedStoredFromCallWtd = 0;

            foreach (var other in _accesses)
            {
                if (ReferenceEquals(other, access) || !other.Overlaps(access.Offset, accessSize))
                {
                    continue;
                }

                if (other.AccessType is not TYP_STRUCT)
                {
                    return false;
                }

                overlappedCallArgs += other.CountCallArgs;
                overlappedStoredFromCall += other.CountStoredFromCall;
                overlappedCallArgsWtd += other.CountCallArgsWtd;
                overlappedStoredFromCallWtd += other.CountStoredFromCallWtd;
                if (other.CountRegCallArgs > 0)
                {
                    overlappedCallArgs -= other.CountRegCallArgs;
                    overlappedCallArgsWtd -= other.CountRegCallArgsWtd;
                }
            }

            const weight_t structCycles = 3;
            const weight_t structSize = 4;
            const weight_t regCycles = 0.5;
            const weight_t regSize = 2;

            var costWithout = (access.CountWtd + inducedCountWtd) * structCycles;
            var sizeWithout = (access.Count + inducedCount) * structSize;
            var costWith = (access.CountWtd + inducedCountWtd) * regCycles;
            var sizeWith = (access.Count + inducedCount) * regSize;

            var readBacks = 0;
            weight_t readBacksWtd = 0;
            var entryWeight = compiler.fgFirstBB!.getBBWeight(compiler);
            if (local.lvIsOSRLocal)
            {
                readBacks++;
                readBacksWtd += entryWeight;
            }
            else if (local.lvIsParam)
            {
                if (compiler.PhysicalPromotionMapsToParameterRegister(lclNum, access.Offset,
                        access.AccessType))
                {
                    costWithout += structCycles * entryWeight;
                    sizeWithout += structSize;
                    costWith += regCycles * entryWeight;
                    sizeWith += regSize;
                }
                else
                {
                    readBacks++;
                    readBacksWtd += entryWeight;
                }
            }

            readBacksWtd += overlappedStoredFromCallWtd;
            readBacks += overlappedStoredFromCall;
            costWith += readBacksWtd * structCycles;
            sizeWith += readBacks * structSize;

            var writeBacksWtd = overlappedCallArgsWtd;
            var writeBacks = overlappedCallArgs;
            costWith += writeBacksWtd * structCycles;
            sizeWith += writeBacks * structSize;

            var cycleImprovement = (costWithout - costWith) / entryWeight;
            var sizeImprovement = sizeWithout - sizeWith;

            JITDUMP($"  Evaluating access {access.AccessType.Name} @ {access.Offset:D3}\n");
            JITDUMP($"    Single write-back cost: {FMT_WT(structCycles)}\n");
            JITDUMP($"    Write backs: {FMT_WT(writeBacksWtd)}\n");
            JITDUMP($"    Read backs: {FMT_WT(readBacksWtd)}\n");
            JITDUMP($"    Estimated cycle improvement: {FMT_WT(cycleImprovement)} cycles per invocation\n");
            JITDUMP($"    Estimated size improvement: {FMT_WT(sizeImprovement)} bytes\n");

            if ((cycleImprovement > 0) && (cycleImprovement * 2 >= -sizeImprovement))
            {
                JITDUMP("  Promoting replacement (cycle improvement)\n\n");
                return true;
            }

            if ((sizeImprovement > 0) && (sizeImprovement * 0.01 >= -cycleImprovement))
            {
                JITDUMP("  Promoting replacement (size improvement)\n\n");
                return true;
            }

            if (Configuration.DEBUG && compiler.compStressCompile(STRESS_PHYSICAL_PROMOTION_COST, 25))
            {
                JITDUMP("  Promoting replacement (stress)\n\n");
                return true;
            }

            JITDUMP("  Disqualifying replacement\n\n");
            return false;
        }
    }
}
