// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotiondecomposition.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed class PhysicalPromotionDecompositionPlan(
        Compiler compiler, PhysicalPromotionReplaceVisitor replacer,
        PhysicalPromotionAggregateInfoMap aggregates, PhysicalPromotionLiveness liveness,
        GenTree store, GenTree source, bool dstInvolvesReplacements, bool srcInvolvesReplacements)
    {
        private readonly record struct Entry(PhysicalPromotionReplacement? ToReplacement,
            PhysicalPromotionReplacement? FromReplacement, int Offset, var_types Type);

        internal enum RemainderKind { None, Primitive, FullBlock }

        internal readonly record struct RemainderStrategy(RemainderKind Kind, int Offset = 0,
            var_types Type = TYP_UNDEF);

        private readonly List<Entry> _entries = [];
        private bool _hasNonRemainderUseOfStructLocal;

        public void CopyBetweenReplacements(PhysicalPromotionReplacement dst, PhysicalPromotionReplacement src, int offset)
            => _entries.Add(new Entry(dst, src, offset, dst.AccessType));

        public void CopyToReplacement(PhysicalPromotionReplacement dst, int offset)
            => _entries.Add(new Entry(dst, null, offset, dst.AccessType));

        public void CopyFromReplacement(PhysicalPromotionReplacement src, int offset)
            => _entries.Add(new Entry(null, src, offset, src.AccessType));

        public void InitReplacement(PhysicalPromotionReplacement dst, int offset)
            => _entries.Add(new Entry(dst, null, offset, dst.AccessType));

        public void MarkNonRemainderUseOfStructLocal() => _hasNonRemainderUseOfStructLocal = true;

        public void Reverse() => _entries.Reverse();

        public bool CanInitPrimitive(var_types type)
        {
            assert(IsInit);
            return (!varTypeIsGC(type) && !varTypeIsSimd(type)) || (GetInitPattern() == 0);
        }

        private bool IsInit => source.IsCnsInitVal;

        private byte GetInitPattern()
        {
            assert(IsInit);
            var constant = source.Oper is GT_INIT_VAL ? source.AsUnOp().Op1 : source;
            return unchecked((byte)constant.AsIntCon().IconVal);
        }

        private SegmentList ComputeRemainder()
        {
            var remainder = PhysicalPromotionCopyNonPadding(store.GetLayout(compiler).GetNonPadding(compiler));
            foreach (var entry in _entries)
            {
                remainder.Subtract(new SegmentList.Segment(entry.Offset, entry.Offset + entry.Type.Size));
            }

#if DEBUG
            if (compiler.verbose)
            {
                jitprintf("  Block op remainder: ");
                remainder.Dump();
                jitprintf("\n");
            }
#endif
            return remainder;
        }

        internal RemainderStrategy DetermineRemainderStrategy(PhysicalPromotionStructDeaths deaths)
        {
            if (dstInvolvesReplacements && !_hasNonRemainderUseOfStructLocal && deaths.IsRemainderDying())
            {
                JITDUMP("  => Remainder strategy: do nothing (remainder dying)\n");
                return new RemainderStrategy(RemainderKind.None);
            }

            var remainder = ComputeRemainder();
            if (remainder.IsEmpty)
            {
                JITDUMP("  => Remainder strategy: do nothing (no remainder)\n");
                return new RemainderStrategy(RemainderKind.None);
            }

            if (remainder.CoveringSegment(out var segment))
            {
                var layout = store.GetLayout(compiler);
                var size = checked((int)(segment.End - segment.Start));
                var type = TYP_UNDEF;
                if ((size == TARGET_POINTER_SIZE) && ((segment.Start % TARGET_POINTER_SIZE) == 0))
                {
                    type = layout.GetGCPtrType(checked((int)(segment.Start / TARGET_POINTER_SIZE)));
                }
                else if (!layout.IntersectsGCPtr(checked((int)segment.Start), size))
                {
                    type = size switch
                    {
                        1 => TYP_UBYTE,
                        2 => TYP_USHORT,
                        4 => TYP_INT,
                        8 => TYP_LONG,
                        16 when compiler.GetPreferredVectorByteLength() >= 16 => TYP_SIMD16,
#if TARGET_XARCH
                        32 when compiler.GetPreferredVectorByteLength() >= 32 => TYP_SIMD32,
                        64 when compiler.GetPreferredVectorByteLength() >= 64 => TYP_SIMD64,
#endif
                        _ => TYP_UNDEF,
                    };
                }

                if (type != TYP_UNDEF)
                {
                    if (!IsInit || CanInitPrimitive(type))
                    {
                        JITDUMP($"  => Remainder strategy: {type.Name} at +{segment.Start:D3}\n");
                        return new RemainderStrategy(RemainderKind.Primitive, checked((int)segment.Start), type);
                    }

                    JITDUMP($"  Cannot handle initing remainder as primitive of type {type.Name}\n");
                }
            }

            JITDUMP("  => Remainder strategy: retain a full block op\n");
            return new RemainderStrategy(RemainderKind.FullBlock);
        }

        public void Finalize(PhysicalPromotionDecompositionStatementList statements)
        {
            if (IsInit)
            {
                FinalizeInit(statements);
            }
            else
            {
                FinalizeCopy(statements);
            }
        }

        private void FinalizeInit(PhysicalPromotionDecompositionStatementList statements)
        {
            var pattern = GetInitPattern();
            var local = store.AsLclVarCommon();
            var deaths = liveness.GetDeathsForStructLocal(local);
            var aggregate = aggregates.Lookup(local.LclNum)!;

            foreach (var entry in _entries)
            {
                var replacement = entry.ToReplacement!;
                var index = aggregate.Replacements.IndexOf(replacement);
                assert(index >= 0);
                if (!deaths.IsReplacementDying(index))
                {
                    var value = compiler.gtNewConWithPattern(entry.Type, pattern);
                    statements.AddStatement(compiler.gtNewStoreLclVarNode(replacement.LclNum, value));
                }

                replacer.ClearNeedsReadBack(replacement);
                replacer.SetNeedsWriteBack(replacement);
            }

            var remainder = DetermineRemainderStrategy(deaths);
            if (remainder.Kind is RemainderKind.FullBlock)
            {
                statements.AddStatement(store);
            }
            else if (remainder.Kind is RemainderKind.Primitive)
            {
                var value = compiler.gtNewConWithPattern(remainder.Type, pattern);
                var access = new PhysicalPromotionLocationAccess();
                access.InitializeLocal(local);
                statements.AddStatement(access.CreateStore(remainder.Offset, remainder.Type, value, compiler));
            }
        }

        private bool CanSkipEntry(Entry entry, PhysicalPromotionStructDeaths deaths,
            RemainderStrategy remainder, bool dump = false)
        {
            if (entry.ToReplacement is not null)
            {
                var aggregate = aggregates.Lookup(store.AsLclVarCommon().LclNum)!;
                var index = aggregate.Replacements.IndexOf(entry.ToReplacement);
                assert(index >= 0);
                if (deaths.IsReplacementDying(index))
                {
#if DEBUG
                    if (dump)
                    {
                        JITDUMP($"  Skipping def of V{entry.ToReplacement.LclNum:D2} " +
                            $"({entry.ToReplacement.Description}); it is dying\n");
                    }
#endif
                    return true;
                }
            }
            else if (dstInvolvesReplacements && !_hasNonRemainderUseOfStructLocal && deaths.IsRemainderDying())
            {
#if DEBUG
                if (dump)
                {
                    JITDUMP($"  Skipping write to dst+{entry.Offset:D3}; " +
                        "it is the remainder and the remainder is dying\n");
                }
#endif
                return true;
            }

            if ((entry.FromReplacement is not null) && (entry.ToReplacement is null) &&
                (remainder.Kind is RemainderKind.FullBlock) && !entry.FromReplacement.NeedsWriteBack)
            {
#if DEBUG
                if (dump)
                {
                    JITDUMP($"  Skipping dst+{entry.Offset:D3} <- " +
                        $"V{entry.FromReplacement.LclNum:D2} ({entry.FromReplacement.Description}); " +
                        "it is up-to-date in its struct local and will be handled as part of the remainder\n");
                }
#endif
                return true;
            }

            return false;
        }

        private bool RemainderOverwritesDestinationWithStaleBits(
            RemainderStrategy remainder, PhysicalPromotionStructDeaths deaths)
        {
            if (!srcInvolvesReplacements)
            {
                return false;
            }

            if (remainder.Kind is RemainderKind.FullBlock)
            {
                return true;
            }

            if (remainder.Kind is RemainderKind.Primitive)
            {
                foreach (var entry in _entries)
                {
                    if ((entry.Offset < remainder.Offset + remainder.Type.Size) &&
                        (remainder.Offset < entry.Offset + entry.Type.Size) &&
                        !CanSkipEntry(entry, deaths, remainder))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanReuseAddressForDecomposedStore(GenTree address)
        {
            if (address.Oper.IsLocalRead)
            {
                var local = address.AsLclVarCommon();
                ref var descriptor = ref compiler.lvaGetDesc(local.LclNum);
                if (descriptor.IsAddressExposed)
                {
                    return false;
                }

                if (!store.Oper.IsLocalStore)
                {
                    return true;
                }

                var dst = store.AsLclVarCommon().LclNum;
                if ((local.LclNum == dst) ||
                    (descriptor.lvIsStructField && (descriptor.lvParentLcl == dst)))
                {
                    return false;
                }

                foreach (var entry in _entries)
                {
                    if (entry.ToReplacement?.LclNum == local.LclNum)
                    {
                        return false;
                    }
                }

                return true;
            }

            return address.IsInvariant;
        }

        private void CopyRemainder(PhysicalPromotionLocationAccess dst, PhysicalPromotionLocationAccess src,
            RemainderStrategy remainder, PhysicalPromotionDecompositionStatementList statements)
        {
            if (remainder.Kind is RemainderKind.FullBlock)
            {
                if (source.Oper is GT_BLK)
                {
                    source.AsIndir().Addr = src.GrabAddress(0, compiler);
                }
                else if (store.Oper is GT_STORE_BLK)
                {
                    store.AsIndir().Addr = dst.GrabAddress(0, compiler);
                }

                statements.AddStatement(store);
            }
            else if (remainder.Kind is RemainderKind.Primitive)
            {
                var type = remainder.Type;
                var srcField = src.FindRegularlyPromotedField(remainder.Offset, compiler);
                var dstField = dst.FindRegularlyPromotedField(remainder.Offset, compiler);
                if ((srcField != BAD_VAR_NUM) || (dstField != BAD_VAR_NUM))
                {
                    var promotedType = compiler.lvaGetDesc(
                        srcField != BAD_VAR_NUM ? srcField : dstField).Type;
                    if (promotedType.Size == type.Size)
                    {
                        type = promotedType;
                    }
                }

                var value = src.CreateRead(remainder.Offset, type, compiler);
                statements.AddStatement(dst.CreateStore(remainder.Offset, type, value, compiler));
            }
        }

        private void FinalizeCopy(PhysicalPromotionDecompositionStatementList statements)
        {
            assert(store.Oper is GT_STORE_LCL_VAR or GT_STORE_LCL_FLD or GT_STORE_BLK);
            assert(source.Oper is GT_LCL_VAR or GT_LCL_FLD or GT_BLK);
            var dstDeaths = dstInvolvesReplacements
                ? liveness.GetDeathsForStructLocal(store.AsLclVarCommon()) : default;
            var remainder = DetermineRemainderStrategy(dstDeaths);

            if ((remainder.Kind is RemainderKind.FullBlock) && (store.Oper is GT_STORE_BLK) &&
                store.GetLayout(compiler).HasGCPtr)
            {
                foreach (var entry in _entries)
                {
                    if ((entry.FromReplacement is { AccessType: TYP_REF } replacement) &&
                        replacement.NeedsWriteBack)
                    {
                        statements.AddStatement(compiler.PhysicalPromotionCreateWriteBack(
                            source.AsLclVarCommon().LclNum, replacement));
                        JITDUMP($"  Will write back V{replacement.LclNum:D2} ({replacement.Description}) " +
                            "to avoid an additional write barrier\n");
                        replacer.ClearNeedsWriteBack(replacement);
                    }
                }
            }

            var remainderFirst = RemainderOverwritesDestinationWithStaleBits(remainder, dstDeaths);
            GenTree? address = null;
            target_ssize_t baseOffset = 0;
            FieldSeq? baseFields = null;
            var flags = GTF_EMPTY;
            var copyableFlags = GTF_IND_COPYABLE_FLAGS | GTF_IND_TGT_NOT_HEAP | GTF_IND_TGT_HEAP;
            if (store.Oper is GT_STORE_BLK)
            {
                address = store.AsIndir().Addr;
                flags = store.Flags & copyableFlags;
                if (store.GetLayout(compiler).IsStackOnly(compiler))
                {
                    flags |= GTF_IND_TGT_NOT_HEAP;
                }
            }
            else if (source.Oper is GT_BLK)
            {
                address = source.AsIndir().Addr;
                flags = source.Flags & GTF_IND_COPYABLE_FLAGS;
            }

            var addressUses = 0;
            var needsNullCheck = false;
            if (address is not null)
            {
                foreach (var entry in _entries)
                {
                    if (!CanSkipEntry(entry, dstDeaths, remainder))
                    {
                        addressUses++;
                    }
                }

                if (remainder.Kind is not RemainderKind.None)
                {
                    addressUses++;
                }

                if (compiler.fgAddrCouldBeNull(address))
                {
                    if (remainderFirst)
                    {
                        needsNullCheck = (remainder.Kind is RemainderKind.Primitive) &&
                            compiler.fgIsBigOffset(unchecked((nint)remainder.Offset));
                    }
                    else
                    {
                        needsNullCheck = true;
                        foreach (var entry in _entries)
                        {
                            if (!CanSkipEntry(entry, dstDeaths, remainder))
                            {
                                needsNullCheck = compiler.fgIsBigOffset(unchecked((nint)entry.Offset));
                                break;
                            }
                        }
                    }
                }

                if (needsNullCheck)
                {
                    addressUses++;
                }

                if (addressUses == 0)
                {
                    GenTree? effects = null;
                    compiler.gtExtractSideEffList(address, ref effects);
                    if (effects is not null)
                    {
                        statements.AddStatement(effects);
                    }
                }
                else if (addressUses > 1)
                {
                    compiler.gtPeelOffsets(ref address, out baseOffset, out baseFields);
                    if (CanReuseAddressForDecomposedStore(address))
                    {
                        if (address.Oper.IsLocalRead)
                        {
                            address.Flags &= ~GTF_VAR_DEATH;
                        }
                    }
                    else
                    {
                        var temp = compiler.lvaGrabTemp(true, "Spilling address for field-by-field copy");
                        statements.AddStatement(compiler.gtNewTempStore(temp, address));
                        address = compiler.gtNewLclvNode(address.Type, temp);
                    }
                }
            }

            var dstAccess = new PhysicalPromotionLocationAccess();
            var srcAccess = new PhysicalPromotionLocationAccess();
            var indirAccess = store.Oper is GT_STORE_BLK ? dstAccess : srcAccess;
            if (store.Oper is GT_STORE_BLK)
            {
                dstAccess.InitializeIndir(address ??
                    throw new InvalidOperationException("A block store requires an address."),
                    baseOffset, baseFields, flags, addressUses);
            }
            else
            {
                dstAccess.InitializeLocal(store.AsLclVarCommon());
            }

            if (source.Oper is GT_BLK)
            {
                srcAccess.InitializeIndir(address ??
                    throw new InvalidOperationException("A block read requires an address."),
                    baseOffset, baseFields, flags, addressUses);
            }
            else
            {
                srcAccess.InitializeLocal(source.AsLclVarCommon());
            }

            if (needsNullCheck)
            {
                statements.AddStatement(indirAccess.CreateRead(0, TYP_BYTE, compiler));
            }

            if (remainderFirst)
            {
                CopyRemainder(dstAccess, srcAccess, remainder, statements);
                if (source.Oper is GT_LCL_VAR or GT_LCL_FLD)
                {
                    source.Flags &= ~GTF_VAR_DEATH;
                }
            }

            var srcDeaths = srcInvolvesReplacements
                ? liveness.GetDeathsForStructLocal(source.AsLclVarCommon()) : default;
            foreach (var entry in _entries)
            {
                if (entry.ToReplacement is not null)
                {
                    replacer.ClearNeedsReadBack(entry.ToReplacement);
                    replacer.SetNeedsWriteBack(entry.ToReplacement);
                }

                if (CanSkipEntry(entry, dstDeaths, remainder, dump: true))
                {
                    continue;
                }

                GenTree value;
                if (entry.FromReplacement is not null)
                {
                    value = compiler.gtNewLclvNode(entry.Type, entry.FromReplacement.LclNum);
                    var aggregate = aggregates.Lookup(source.AsLclVarCommon().LclNum)!;
                    var index = aggregate.Replacements.IndexOf(entry.FromReplacement);
                    assert(index >= 0);
                    if (srcDeaths.IsReplacementDying(index))
                    {
                        value.Flags |= GTF_VAR_DEATH;
                        replacer.CheckForwardSubForLastUse(entry.FromReplacement.LclNum);
                    }
                }
                else
                {
                    value = srcAccess.CreateRead(entry.Offset, entry.Type, compiler);
                }

                var operation = entry.ToReplacement is not null
                    ? compiler.gtNewStoreLclVarNode(entry.ToReplacement.LclNum, value)
                    : dstAccess.CreateStore(entry.Offset, entry.Type, value, compiler);
                statements.AddStatement(operation);
            }

            if (!remainderFirst)
            {
                CopyRemainder(dstAccess, srcAccess, remainder, statements);
            }

            dstAccess.CheckFullyUsed();
            srcAccess.CheckFullyUsed();
        }
    }
}
