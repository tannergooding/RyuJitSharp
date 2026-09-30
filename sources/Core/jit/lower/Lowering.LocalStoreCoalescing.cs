// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private readonly struct LocalStoreCoalescingData
    {
        public readonly GenTreeLclVarCommon Store;
        public readonly GenTree Value;
        public readonly GenTree RangeStart;
        public readonly GenTree RangeEnd;
        public readonly int Offset;
        public readonly uint AccessSize;
        public readonly bool IsAddressExposed;

        public LocalStoreCoalescingData(GenTreeLclVarCommon store, GenTree rangeStart, GenTree rangeEnd,
            uint accessSize, bool isAddressExposed)
        {
            Store = store;
            Value = store.Data;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Offset = store.LclOffs;
            AccessSize = accessSize;
            IsAddressExposed = isAddressExposed;
        }
    }

    private bool IsLocalStoreCoalescingInvariant(GenTree? node, bool allowNull = false)
    {
        if (node is null)
        {
            return allowNull;
        }

        return node.Oper.IsConst ||
            ((node.Oper is GT_LCL_VAR) && !CompilerInstance.lvaGetDesc(node.AsLclVar().LclNum).IsAddressExposed);
    }

    private bool TryGetLocalStoreCoalescingData(GenTreeLclVarCommon store, out LocalStoreCoalescingData data)
    {
        data = default;
        if (!IsLocalStoreCoalescingInvariant(store.Data))
        {
            return false;
        }

        var range = BlockRange().GetTreeRange(store, out var isClosed);
        if (!isClosed)
        {
            return false;
        }
        if ((range.FirstNode is not GenTree first) || (range.LastNode is not GenTree last))
        {
            throw new InvalidOperationException("A closed store tree range must include its root.");
        }

        ref var descriptor = ref CompilerInstance.lvaGetDesc(store.LclNum);
        var accessSize = store.Oper is GT_STORE_LCL_FLD
            ? store.AsLclFld().Size
            : checked((uint)descriptor.lvExactSize);
        data = new LocalStoreCoalescingData(store, first, last, accessSize,
            descriptor.IsAddressExposed);
        return true;
    }

    private static bool TryGetLocalStoreConstantBits(GenTree node, out ulong bits)
    {
        bits = 0;
        if (node.Oper.IsCnsIntOrI)
        {
            bits = unchecked((ulong)node.AsIntCon().IconValue);
            return true;
        }
        if (node.Oper.IsCnsFltOrDbl)
        {
            if (node.Type is TYP_FLOAT)
            {
                bits = unchecked((uint)BitConverter.SingleToInt32Bits((float)node.AsDblCon().DconVal));
                return true;
            }
#if TARGET_64BIT
            assert(node.Type is TYP_DOUBLE);
            bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(node.AsDblCon().DconVal));
            return true;
#endif
        }
        return false;
    }

    private bool CanCoalesceLocalStoreAtomically(in LocalStoreCoalescingData current,
        in LocalStoreCoalescingData previous, int minOffset, int combinedSize)
    {
        var compiler = CompilerInstance;
        if ((compiler.info.compRetBuffArg == current.Store.LclNum) ||
            (!current.IsAddressExposed && !previous.IsAddressExposed))
        {
            return true;
        }

        var alignment = Math.Min(compiler.lvaLclStackHomeSize(current.Store.LclNum), TARGET_POINTER_SIZE);
#if TARGET_ARM64
        if (current.AccessSize == TARGET_POINTER_SIZE)
        {
            // A pointer-aligned 128-bit SIMD write preserves ARM64's pair of atomic 64-bit writes.
            return (alignment >= TARGET_POINTER_SIZE) && ((minOffset % TARGET_POINTER_SIZE) == 0);
        }
#endif
        return (combinedSize <= alignment) && ((minOffset % combinedSize) == 0);
    }

    private void LowerLocalStoreCoalescing(GenTreeLclVarCommon store)
    {
#if TARGET_XARCH || TARGET_ARM64
        var compiler = CompilerInstance;
        if (!compiler.opts.OptimizationEnabled)
        {
            return;
        }

        while (true)
        {
            if (!TryGetLocalStoreCoalescingData(store, out var current))
            {
                return;
            }

            var previousNode = current.RangeStart.Prev;
            while ((previousNode is not null) && (previousNode.Oper is GT_NOP or GT_IL_OFFSET))
            {
                previousNode = previousNode.Prev;
            }

            if ((previousNode is null) || !previousNode.Oper.IsLocalStore ||
                !TryGetLocalStoreCoalescingData(previousNode.AsLclVarCommon(), out var previous))
            {
                return;
            }

            var previousEnd = (long)previous.Offset + previous.AccessSize;
            var currentEnd = (long)current.Offset + current.AccessSize;
            var overlaps = ((long)current.Offset < previousEnd) && ((long)previous.Offset < currentEnd);
            if ((current.Store.LclNum != previous.Store.LclNum) ||
                (!overlaps && (current.Store.Type != previous.Store.Type)))
            {
                return;
            }

            if ((current.Offset == previous.Offset) && (current.AccessSize >= previous.AccessSize) &&
                (current.Store.Type == previous.Store.Type))
            {
                if (compiler.gtTreeHasSideEffects(previous.Value, GTF_GLOB_EFFECT) ||
                    compiler.gtTreeHasSideEffects(current.Value, GTF_GLOB_EFFECT))
                {
                    return;
                }
#if DEBUG
                JITDUMP($"Store coalescing: removing previous store [{previousNode.TreeId:D6}] because store [{store.TreeId:D6}] rewrites the same location\n");
#endif
                BlockRange().Remove(previous.RangeStart, previous.RangeEnd);
                continue;
            }

            var oldType = store.Type;
            if (!varTypeIsIntegral(oldType) && !varTypeIsSimd(oldType))
            {
                return;
            }
            if (!previous.Value.Oper.IsConst || !current.Value.Oper.IsConst ||
                (previous.Value.Oper.IsCnsIntOrI && previous.Value.AsIntCon().ImmedValNeedsReloc(compiler)) ||
                (current.Value.Oper.IsCnsIntOrI && current.Value.AsIntCon().ImmedValNeedsReloc(compiler)))
            {
                return;
            }

            var minOffset = Math.Min(previous.Offset, current.Offset);
            var combinedSpan = Math.Max(previousEnd, currentEnd) - minOffset;
            if (combinedSpan > int.MaxValue)
            {
                return;
            }
            var combinedSize = (int)combinedSpan;
            var previousContainsCurrent = (previous.Offset <= current.Offset) && (previousEnd >= currentEnd);
            var currentContainsPrevious = (current.Offset <= previous.Offset) && (currentEnd >= previousEnd);
            var adjacent = (previousEnd == current.Offset) || (currentEnd == previous.Offset);
            var sameSize = previous.AccessSize == current.AccessSize;
            var newType = TYP_UNDEF;
            var reusePreviousValue = false;

            if (currentContainsPrevious)
            {
#if DEBUG
                JITDUMP($"Store coalescing: removing previous store [{previousNode.TreeId:D6}] because store [{store.TreeId:D6}] fully overwrites it\n");
#endif
                BlockRange().Remove(previous.RangeStart, previous.RangeEnd);
                continue;
            }
            if (previousContainsCurrent)
            {
                if (!varTypeIsIntegral(oldType))
                {
                    return;
                }
                newType = combinedSize switch {
                    1 => TYP_UBYTE,
                    2 => TYP_USHORT,
                    4 => TYP_INT,
#if TARGET_64BIT
                    8 => TYP_LONG,
#endif
                    _ => TYP_UNDEF,
                };
            }
            else
            {
                if (!adjacent || !sameSize ||
                    (Math.Abs((long)previous.Offset - current.Offset) != previous.AccessSize))
                {
                    return;
                }
                newType = oldType switch {
                    TYP_BYTE or TYP_UBYTE => TYP_USHORT,
                    TYP_SHORT or TYP_USHORT => TYP_INT,
#if TARGET_64BIT
                    TYP_INT => TYP_LONG,
#if FEATURE_HW_INTRINSICS
                    TYP_LONG or TYP_REF => TYP_SIMD16,
#if TARGET_AMD64
                    TYP_SIMD16 when compiler.GetPreferredVectorByteLength() >= 32 => TYP_SIMD32,
                    TYP_SIMD32 when compiler.GetPreferredVectorByteLength() >= 64 => TYP_SIMD64,
#endif
#endif
#endif
                    _ => TYP_UNDEF,
                };
#if TARGET_64BIT && FEATURE_HW_INTRINSICS
                reusePreviousValue = (newType is TYP_UNDEF) && (oldType is TYP_SIMD16
#if TARGET_AMD64
                    or TYP_SIMD32
#endif
                    );
                if ((oldType is TYP_REF) &&
                    (!current.Value.IsIntegralConst(0) || !previous.Value.IsIntegralConst(0)))
                {
                    return;
                }
#endif
            }
            if ((newType is TYP_UNDEF) && !reusePreviousValue)
            {
                return;
            }
            if (!CanCoalesceLocalStoreAtomically(in current, in previous, minOffset, combinedSize))
            {
                return;
            }

            if (reusePreviousValue)
            {
#if FEATURE_HW_INTRINSICS
                if ((current.Value.Oper is GT_CNS_VEC) && GenTree.Compare(previous.Value, current.Value) &&
                    BlockRange().TryGetUse(previous.Value, out var use))
                {
                    var temp = use.ReplaceWithLclVar(compiler);
                    var read = compiler.gtNewLclvNode(current.Value.Type, temp);
                    BlockRange().InsertBefore(current.Value, read);
                    BlockRange().Remove(current.Value);
                    store.DataRef = read;
                }
#endif
                return;
            }

#if FEATURE_HW_INTRINSICS && TARGET_AMD64
            ulong lowerBits = 0;
            ulong upperBits = 0;
            if (varTypeIsSimd(oldType))
            {
                if ((previous.Value.Oper is not GT_CNS_VEC) || (current.Value.Oper is not GT_CNS_VEC))
                {
                    return;
                }
            }
            else
            if (!TryGetLocalStoreConstantBits(previous.Value, out lowerBits) ||
                !TryGetLocalStoreConstantBits(current.Value, out upperBits))
#else
            if (!TryGetLocalStoreConstantBits(previous.Value, out var lowerBits) ||
                !TryGetLocalStoreConstantBits(current.Value, out var upperBits))
#endif
            {
                return;
            }

            BlockRange().Remove(previous.RangeStart, previous.RangeEnd);
            current.Value.IsContained = false;
            store.Type = newType;
            current.Value.Type = newType;
            if (store.Oper is GT_STORE_LCL_FLD)
            {
                store.AsLclFld().LclOffs = checked((ushort)minOffset);
            }

#if FEATURE_HW_INTRINSICS && TARGET_AMD64
            if (varTypeIsSimd(oldType))
            {
                var left = previous.Value.AsVecCon().SimdVal.AsSpan<byte>();
                var right = current.Value.AsVecCon().SimdVal.AsSpan<byte>();
                var width = oldType.Size;
                var lower = previous.Offset < current.Offset ? left[..width].ToArray() : right[..width].ToArray();
                var upper = previous.Offset < current.Offset ? right[..width].ToArray() : left[..width].ToArray();
                right.Clear();
                lower.CopyTo(right[..width]);
                upper.CopyTo(right.Slice(width, width));
                continue;
            }
#endif
#if TARGET_64BIT && FEATURE_HW_INTRINSICS
            if (varTypeIsSimd(newType))
            {
                if (previous.Offset > current.Offset)
                {
                    (lowerBits, upperBits) = (upperBits, lowerBits);
                }
                var vector = compiler.gtNewVconNode(newType);
                vector.SimdVal.u64[0] = lowerBits;
                vector.SimdVal.u64[1] = upperBits;
                BlockRange().InsertAfter(current.Value, vector);
                BlockRange().Remove(current.Value);
                store.DataRef = vector;
                continue;
            }
#endif

            static ulong Mask(uint size)
            {
                return size >= sizeof(ulong) ? ulong.MaxValue : (1UL << ((int)size * 8)) - 1;
            }
            var previousMask = Mask(previous.AccessSize);
            var currentMask = Mask(current.AccessSize);
            var newMask = Mask((uint)newType.Size);
            var previousShift = (previous.Offset - minOffset) * 8;
            var currentShift = (current.Offset - minOffset) * 8;
            var currentBitsMask = (currentMask << currentShift) & newMask;
            var result = (((lowerBits & previousMask) << previousShift) & newMask & ~currentBitsMask) |
                (((upperBits & currentMask) << currentShift) & newMask);
#if DEBUG
            JITDUMP($"Coalesced two stores into a single store with value {unchecked((long)result)}\n");
#endif
            current.Value.AsIntCon().IconVal = unchecked((nint)result);
        }
#endif
    }
}
