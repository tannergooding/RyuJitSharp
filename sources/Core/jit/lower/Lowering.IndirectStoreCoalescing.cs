// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private readonly struct IndirectStoreCoalescingData
    {
        public readonly GenTreeIndir Store;
        public readonly GenTree Value;
        public readonly GenTree? BaseAddress;
        public readonly GenTree? Index;
        public readonly GenTree RangeStart;
        public readonly GenTree RangeEnd;
        public readonly long Offset;
        public readonly int Scale;
        public readonly int AccessSize;
        public readonly int LocalNumber;

        public IndirectStoreCoalescingData(GenTreeIndir store, GenTree? baseAddress, GenTree? index,
            GenTree rangeStart, GenTree rangeEnd, long offset, int scale, int localNumber)
        {
            Store = store;
            Value = store.Data;
            BaseAddress = baseAddress;
            Index = index;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Offset = offset;
            Scale = scale;
            AccessSize = checked((int)store.Size);
            LocalNumber = localNumber;
        }

        public bool IsAddressEqual(in IndirectStoreCoalescingData other)
        {
            return (Scale == other.Scale) && (Store.Type == other.Store.Type) &&
                GenTree.Compare(BaseAddress, other.BaseAddress) && GenTree.Compare(Index, other.Index);
        }
    }

    private bool TryGetIndirectStoreCoalescingData(GenTreeIndir store, out IndirectStoreCoalescingData data)
    {
        data = default;
        if (store.IsVolatile || (store.Oper is not (GT_STOREIND or GT_STORE_BLK)) ||
            !IsLocalStoreCoalescingInvariant(store.Data) || (store.Size > int.MaxValue))
        {
            return false;
        }

        GenTree? baseAddress;
        GenTree? index;
        long offset;
        int scale;
        var localNumber = BAD_VAR_NUM;
        var address = store.Addr;
        if (address.Oper is GT_LEA)
        {
            var mode = address.AsAddrMode();
            baseAddress = mode.BaseAddress;
            index = mode.Index;
            if ((baseAddress is not GenTree baseNode) || !IsLocalStoreCoalescingInvariant(baseNode) ||
                ((index is not null) && !IsLocalStoreCoalescingInvariant(index)))
            {
                return false;
            }

            scale = mode.Scale;
            offset = mode.Offset;
            if (baseNode.Oper is GT_LCL_VAR)
            {
                localNumber = baseNode.AsLclVar().LclNum;
            }
        }
        else if (address.Oper.IsCnsIntOrI && !address.AsIntCon().ImmedValNeedsReloc(CompilerInstance))
        {
            baseAddress = null;
            index = null;
            scale = 1;
            offset = (long)address.AsIntCon().IconValue;
        }
        else if (IsLocalStoreCoalescingInvariant(address))
        {
            baseAddress = address;
            index = null;
            scale = 1;
            offset = 0;
            if (address.Oper is GT_LCL_VAR)
            {
                localNumber = address.AsLclVar().LclNum;
            }
        }
        else
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

        data = new IndirectStoreCoalescingData(store, baseAddress, index, first, last, offset, scale, localNumber);
        return true;
    }

    private bool CanCoalesceIndirectStoreAtomically(in IndirectStoreCoalescingData current,
        in IndirectStoreCoalescingData previous, long minOffset, long combinedSize)
    {
        if ((((current.Store.Flags & previous.Store.Flags) & GTF_IND_ALLOW_NON_ATOMIC) != 0) ||
            ((CompilerInstance.info.compRetBuffArg != BAD_VAR_NUM) &&
             (current.LocalNumber == CompilerInstance.info.compRetBuffArg)))
        {
            return true;
        }

        if ((current.Store.Type.Size > 1) && !varTypeIsSimd(current.Store.Type))
        {
            if ((current.Index is not null) ||
                ((current.BaseAddress is not null) && (current.BaseAddress.Type is not TYP_REF)))
            {
                return false;
            }

            return (combinedSize <= TARGET_POINTER_SIZE) && ((minOffset % combinedSize) == 0);
        }

        return true;
    }

    private void LowerIndirectStoreCoalescing(GenTreeIndir store)
    {
#if TARGET_XARCH
        var compiler = CompilerInstance;
        if (!compiler.opts.OptimizationEnabled || (store.Oper is not (GT_STOREIND or GT_STORE_BLK)))
        {
            return;
        }

        while (true)
        {
            if (!TryGetIndirectStoreCoalescingData(store, out var current))
            {
                return;
            }

            var previousNode = current.RangeStart.Prev;
            while ((previousNode is not null) && (previousNode.Oper is GT_NOP or GT_IL_OFFSET))
            {
                previousNode = previousNode.Prev;
            }
            if ((previousNode is not GenTreeIndir previousStore) ||
                (previousStore.Oper is not (GT_STOREIND or GT_STORE_BLK)) ||
                !TryGetIndirectStoreCoalescingData(previousStore, out var previous))
            {
                return;
            }
            if (((current.Store.Flags | previous.Store.Flags) & GTF_IND_VOLATILE) != 0 ||
                !current.IsAddressEqual(in previous))
            {
                return;
            }

            if ((current.Offset == previous.Offset) && (current.AccessSize >= previous.AccessSize))
            {
                if (compiler.gtTreeHasSideEffects(previous.Value, GTF_GLOB_EFFECT) ||
                    compiler.gtTreeHasSideEffects(current.Value, GTF_GLOB_EFFECT))
                {
                    return;
                }
#if DEBUG
                JITDUMP($"Store coalescing: removing previous store [{previousStore.TreeId:D6}] because store [{store.TreeId:D6}] rewrites the same location\n");
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
            if ((store.Addr.Oper is not GT_LEA) &&
                (!store.Addr.Oper.IsCnsIntOrI || store.Addr.AsIntCon().ImmedValNeedsReloc(compiler)))
            {
                return;
            }

            var previousEnd = unchecked(previous.Offset + previous.AccessSize);
            var currentEnd = unchecked(current.Offset + current.AccessSize);
            var minOffset = Math.Min(previous.Offset, current.Offset);
            var combinedSpan = unchecked(Math.Max(previousEnd, currentEnd) - minOffset);
            if ((combinedSpan <= 0) || (combinedSpan > int.MaxValue))
            {
                return;
            }
            var combinedSize = (int)combinedSpan;
            if ((previousEnd != current.Offset && currentEnd != previous.Offset) ||
                (previous.AccessSize != current.AccessSize) ||
                (unchecked(previous.Offset - current.Offset) != previous.AccessSize &&
                 unchecked(current.Offset - previous.Offset) != previous.AccessSize))
            {
                return;
            }

            var reusePreviousValue = false;
            var newType = oldType switch {
                TYP_BYTE or TYP_UBYTE => TYP_USHORT,
                TYP_SHORT or TYP_USHORT => TYP_INT,
                TYP_INT => TYP_LONG,
#if FEATURE_HW_INTRINSICS
                TYP_LONG or TYP_REF => TYP_SIMD16,
                TYP_SIMD16 when compiler.GetPreferredVectorByteLength() >= 32 => TYP_SIMD32,
                TYP_SIMD32 when compiler.GetPreferredVectorByteLength() >= 64 => TYP_SIMD64,
#endif
                _ => TYP_UNDEF,
            };
#if FEATURE_HW_INTRINSICS
            reusePreviousValue = (newType is TYP_UNDEF) && (oldType is TYP_SIMD16 or TYP_SIMD32);
            if ((oldType is TYP_REF) &&
                (!current.Value.IsIntegralConst(0) || !previous.Value.IsIntegralConst(0)))
            {
                return;
            }
#endif
            if ((newType is TYP_UNDEF) && !reusePreviousValue)
            {
                return;
            }
            if (!CanCoalesceIndirectStoreAtomically(in current, in previous, minOffset, combinedSize))
            {
                return;
            }
            if (reusePreviousValue)
            {
#if FEATURE_HW_INTRINSICS
                if ((current.Value.Oper is GT_CNS_VEC) && GenTree.Compare(previous.Value, current.Value) &&
                    BlockRange().TryGetUse(previous.Value, out var use))
                {
                    var temp = use.ReplaceWithLclVar(compiler, out var tempStore);
                    var read = compiler.gtNewLclvNode(current.Value.Type, temp);
                    BlockRange().InsertBefore(current.Value, read);
                    BlockRange().Remove(current.Value);
                    store.Data = read;
                    _ = LowerNode(tempStore);
                }
#endif
                return;
            }

            ulong previousBits = 0;
            ulong currentBits = 0;
#if FEATURE_HW_INTRINSICS
            if (varTypeIsSimd(oldType))
            {
                if ((previous.Value.Oper is not GT_CNS_VEC) || (current.Value.Oper is not GT_CNS_VEC))
                {
                    return;
                }
            }
            else
#endif
            if (!TryGetLocalStoreConstantBits(previous.Value, out previousBits) ||
                !TryGetLocalStoreConstantBits(current.Value, out currentBits))
            {
                return;
            }

            var codeGen = compiler.codeGen ?? throw new InvalidOperationException("Store coalescing requires code generation state.");
            assert(!codeGen.GCInfo.gcIsWriteBarrierStoreIndNode(store.AsStoreInd()));
            BlockRange().Remove(previous.RangeStart, previous.RangeEnd);
            store.Data.IsContained = false;
            if (store.Addr.Oper is GT_LEA)
            {
                store.Addr.AsAddrMode().Offset = checked((int)minOffset);
            }
            else
            {
                store.Addr.AsIntCon().IconVal = unchecked((nint)minOffset);
            }
            store.Type = newType;
            store.Data.Type = newType;

#if FEATURE_HW_INTRINSICS
            if (varTypeIsSimd(oldType))
            {
                var left = previous.Value.AsVecCon().SimdVal.AsSpan<byte>();
                var right = current.Value.AsVecCon().SimdVal.AsSpan<byte>();
                var width = oldType.Size;
                var lower = previous.Offset < current.Offset ? left[..width].ToArray() : right[..width].ToArray();
                var upper = previous.Offset < current.Offset ? right[..width].ToArray() : left[..width].ToArray();
                lower.CopyTo(right[..width]);
                upper.CopyTo(right.Slice(width, width));
                continue;
            }
            if (varTypeIsSimd(newType))
            {
                if (previous.Offset > current.Offset)
                {
                    (previousBits, currentBits) = (currentBits, previousBits);
                }
                var vector = compiler.gtNewVconNode(newType);
                vector.SimdVal.u64[0] = previousBits;
                vector.SimdVal.u64[1] = currentBits;
                BlockRange().InsertAfter(store.Data, vector);
                BlockRange().Remove(store.Data);
                store.Data = vector;
                continue;
            }
#endif
            static ulong Mask(int size)
            {
                return size >= sizeof(ulong) ? ulong.MaxValue : (1UL << (size * 8)) - 1;
            }
            var previousMask = Mask(previous.AccessSize);
            var currentMask = Mask(current.AccessSize);
            var newMask = Mask(newType.Size);
            var previousShift = checked((int)((previous.Offset - minOffset) * 8));
            var currentShift = checked((int)((current.Offset - minOffset) * 8));
            var currentBitsMask = (currentMask << currentShift) & newMask;
            var result = (((previousBits & previousMask) << previousShift) & newMask & ~currentBitsMask) |
                (((currentBits & currentMask) << currentShift) & newMask);
#if DEBUG
            JITDUMP($"Coalesced two stores into a single store with value {unchecked((long)result)}\n");
#endif
            store.Data.AsIntCon().IconVal = unchecked((nint)result);
            if ((oldType.Size == 1) && (store.Oper is GT_STOREIND))
            {
                store.Flags |= GTF_IND_ALLOW_NON_ATOMIC;
            }
        }
#else
        throw new NotImplementedException("Non-xarch indirect-store coalescing is not ported.");
#endif
    }
}
