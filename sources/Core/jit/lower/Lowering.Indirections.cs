// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerIndir(GenTreeIndir ind)
    {
        var next = ind.Next;
        assert(ind.Oper is GT_IND or GT_NULLCHECK);

        if ((ind.Type is not TYP_STRUCT) || ind.IsUnusedValue)
        {
#if !TARGET_XARCH
            // The transformed access width controls non-xarch containment. Xarch instead chooses
            // IND or NULLCHECK based on whether address containment succeeded, so transforms last.
            if ((ind.Oper is GT_NULLCHECK) || ind.IsUnusedValue)
            {
                ind = TransformUnusedIndirection(ind, CompilerInstance, BlockRange());
            }
#endif
#if TARGET_ARM64
            if ((ind.Oper is GT_IND) && ind.IsVolatile && varTypeIsFloating(ind.Type) &&
                BlockRange().TryGetUse(ind, out var use))
            {
                var targetType = ind.Type;
                ind.Type = ind.Type is TYP_DOUBLE ? TYP_LONG : TYP_INT;
                var isContainable = IsInvariantInRange(ind.Addr, ind, GTF_ORDER_SIDEEFF);
                _ = TryCreateAddrMode(ref ind.AddrRef, isContainable, ind);

                var bitCast = CompilerInstance.gtNewBitCastNode(targetType, ind);
                BlockRange().InsertAfter(ind, bitCast);
                use.ReplaceWith(bitCast);
                return bitCast;
            }

            var addressIsContainable = IsInvariantInRange(ind.Addr, ind, GTF_ORDER_SIDEEFF);
            _ = TryCreateAddrMode(ref ind.AddrRef, addressIsContainable, ind);
#else
            _ = TryCreateAddrMode(ref ind.AddrRef, true, ind);
#endif
            ContainCheckIndir(ind);
#if TARGET_XARCH
            if ((ind.Oper is GT_NULLCHECK) || ind.IsUnusedValue)
            {
                ind = TransformUnusedIndirection(ind, CompilerInstance, BlockRange());
            }
#endif
        }
        else
        {
            _ = TryCreateAddrMode(ref ind.AddrRef, false, ind);
        }

#if TARGET_ARM64
        if (CompilerInstance.opts.OptimizationEnabled && (ind.Oper is GT_IND))
        {
            _ = OptimizeForLdpStp(ind);
        }
#endif
        return next;
    }

    private void ContainCheckIndir(GenTreeIndir node)
    {
#if TARGET_XARCH
        var addr = node.Addr;
        if (node.Type is TYP_STRUCT)
        {
            return;
        }
        if ((node.Flags & GTF_IND_REQ_ADDR_IN_REG) != 0)
        {
            return;
        }

        if ((addr.Oper is GT_LCL_ADDR) && IsContainableLclAddr(addr.AsLclFld(), (uint)node.Size))
        {
            MakeSrcContained(node, addr);
        }
        else if (addr.Oper.IsCnsIntOrI)
        {
            var icon = addr.AsIntConCommon();
#if FEATURE_SIMD
            if (((node.Type is not TYP_SIMD12) || !icon.ImmedValNeedsReloc(CompilerInstance)) &&
                icon.FitsInAddrBase(CompilerInstance))
#else
            if (icon.FitsInAddrBase(CompilerInstance))
#endif
            {
                MakeSrcContained(node, addr);
            }
        }
        else if ((addr.Oper is GT_LEA) && IsInvariantInRange(addr, node))
        {
            MakeSrcContained(node, addr);
        }
#elif TARGET_ARM
        if (node.Type is TYP_STRUCT)
        {
            return;
        }
#if FEATURE_SIMD
        if (node.Type is TYP_SIMD12)
        {
            return;
        }
#endif

        var addr = node.Addr;
        if ((addr.Oper is GT_LEA) && IsInvariantInRange(addr, node))
        {
            var makeContained = true;
            var addrMode = addr.AsAddrMode();
            if (addrMode.HasIndex || !Emitter.emitIns_valid_imm_for_vldst_offset(addrMode.Offset))
            {
                if ((node.Oper is GT_STOREIND) && varTypeIsFloating(node.AsStoreInd().Data.Type))
                {
                    makeContained = false;
                }
                else if ((node.Oper is GT_IND) && varTypeIsFloating(node.Type))
                {
                    makeContained = false;
                }
            }

            if (makeContained)
            {
                MakeSrcContained(node, addr);
            }
        }
        else if ((addr.Oper is GT_LCL_ADDR) && (node.Oper is not GT_NULLCHECK) &&
            IsContainableLclAddr(addr.AsLclFld(), (uint)node.Size))
        {
            MakeSrcContained(node, addr);
        }
#elif TARGET_ARM64
        if (node.Type is TYP_STRUCT or TYP_SIMD12)
        {
            return;
        }

        var addr = node.Addr;
        if ((addr.Oper is GT_LEA) && IsInvariantInRange(addr, node))
        {
            MakeSrcContained(node, addr);
        }
        else if ((addr.Oper is GT_LCL_ADDR) && (node.Oper is not GT_NULLCHECK) &&
            IsContainableLclAddr(addr.AsLclFld(), (uint)node.Size))
        {
            MakeSrcContained(node, addr);
        }
        else if (addr.Oper.IsCnsIntOrI && addr.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL))
        {
            MakeSrcContained(node, addr);
        }
#elif TARGET_RISCV64
        if (node.Type is TYP_STRUCT)
        {
            return;
        }
#if FEATURE_SIMD
        NYI_RISCV64("ContainCheckIndir-SIMD");
#endif

        var addr = node.Addr;
        if ((addr.Oper is GT_LEA) && IsSafeToContainMem(node, addr))
        {
            MakeSrcContained(node, addr);
        }
        else if ((addr.Oper is GT_LCL_ADDR) &&
            IsContainableLclAddr(addr.AsLclFld(), (uint)node.Size))
        {
            MakeSrcContained(node, addr);
        }
        else if (addr.Oper.IsCnsIntOrI && !addr.AsIntCon().ImmedValNeedsReloc(CompilerInstance))
        {
            MakeSrcContained(node, addr);
        }
#elif TARGET_LOONGARCH64
        if (node.Type is TYP_STRUCT)
        {
            return;
        }

#if FEATURE_SIMD
        NYI_LOONGARCH64("ContainCheckIndir-SIMD");
#endif

        var addr = node.Addr;
        if ((addr.Oper is GT_LEA) && IsInvariantInRange(addr, node))
        {
            MakeSrcContained(node, addr);
        }
        else if ((addr.Oper is GT_LCL_ADDR) && IsContainableLclAddr(addr.AsLclFld(), (uint)node.Size))
        {
            MakeSrcContained(node, addr);
        }
#elif TARGET_WASM
        if (node.Type is TYP_STRUCT)
        {
            return;
        }

        var foldable = GetFoldableAddrMode(node);
        if ((node.Oper is GT_IND) &&
            (((node.Flags & GTF_IND_NONFAULTING) == 0) || (node.Type is TYP_SIMD12)))
        {
            var addressToReuse = node.Addr;
            if (foldable is not null)
            {
                addressToReuse = foldable.BaseAddress ??
                    throw new InvalidOperationException("Foldable Wasm address is missing its base.");
            }

            SetMultiplyUsed(addressToReuse
#if DEBUG
                , "ContainCheckIndir load Addr (null check or simd12 lane load)"
#endif
            );
        }

        if (foldable is not null)
        {
            MakeSrcContained(node, foldable);
        }
        else
        {
            TryFoldLclAddrOffset(node);
        }

        var address = node.Addr;
        if ((node.Oper is GT_IND) && (node.Type is not TYP_SIMD12) && address.IsIconHandle() &&
            address.AsIntConCommon().ImmedValNeedsReloc(CompilerInstance) &&
            ((address._lirFlags & LIR.Flags.MultiplyUsed) == LIR.Flags.None))
        {
            // Only loads can replace the address computation with a relocated memarg while preserving operand order.
            MakeSrcContained(node, address);
        }
#else
        throw new NotImplementedException("Indirection containment outside xarch and ARM64 is not ported.");
#endif
    }

#if TARGET_WASM
    // Wasm memarg offsets use infinite-precision addition, unlike wrapping i32.add. Requiring a GC base
    // and a non-negative offset ensures an out-of-range address already traps as an invalid object access.
    // SIMD12 reuses the full address for its trailing lane, and write barriers pass that address to a helper.
    private GenTreeAddrMode? GetFoldableAddrMode(GenTreeIndir indirNode)
    {
        var address = indirNode.Addr;
        if ((address.Oper is not GT_LEA) || (indirNode.Type is TYP_SIMD12) ||
            ((address._lirFlags & LIR.Flags.MultiplyUsed) != LIR.Flags.None))
        {
            return null;
        }

        var lea = address.AsAddrMode();
        if (!lea.HasBaseAddress || lea.HasIndex || !varTypeIsGC(lea.BaseAddress.Type) || (lea.Offset < 0))
        {
            return null;
        }

        if (!IsInvariantInRange(lea.BaseAddress, indirNode))
        {
            return null;
        }

        if (indirNode.Oper is GT_STOREIND)
        {
            assert(CompilerInstance.codeGen is not null);
            if (CompilerInstance.codeGen.GCInfo.gcIsWriteBarrierStoreIndNode(indirNode.AsStoreInd()))
            {
                return null;
            }
        }

        return lea;
    }

    // A local-frame address plus a non-negative offset stays within the shadow stack. Keep its single-use
    // address intact: stackification and reference spilling rely on its invariant value.
    private void TryFoldLclAddrOffset(GenTreeIndir indirNode)
    {
        var address = indirNode.Addr;
        if ((indirNode.Oper is not (GT_IND or GT_STOREIND)) || (address.Oper is not GT_LCL_ADDR) ||
            (indirNode.Type is TYP_SIMD12) ||
            ((address._lirFlags & LIR.Flags.MultiplyUsed) != LIR.Flags.None))
        {
            return;
        }

        assert(address.IsInvariant);
        address._lirFlags |= LIR.Flags.FoldedAddr;
    }
#endif

    private bool IsContainableLclAddr(GenTreeLclFld lclAddr, uint accessSize)
    {
        if (((ulong)lclAddr.LclOffs + accessSize > uint.MaxValue) ||
            !CompilerInstance.IsValidLclAddr(lclAddr.LclNum, unchecked((int)(lclAddr.LclOffs + accessSize - 1))))
        {
            // Local morph requires address exposure when containment cannot preserve local liveness.
            assert(CompilerInstance.lvaGetDesc(lclAddr.LclNum).IsAddressExposed);
            return false;
        }

        return true;
    }

    internal static GenTreeIndir TransformUnusedIndirection(GenTreeIndir ind, Compiler compiler, BasicBlock block)
    {
        assert(ind.Oper is GT_NULLCHECK or GT_IND or GT_BLK);
        ind.Type = compiler.gtTypeForNullCheck(ind);
#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
        var useNullCheck = true;
#elif TARGET_ARM
        var useNullCheck = false;
#else
        // A contained address needs a target register; otherwise a compare can perform the probe.
        var useNullCheck = !ind.Addr.IsContained;
        ind.Flags &= ~GTF_DONT_EXTEND;
#endif
        if (useNullCheck && (ind.Oper is not GT_NULLCHECK))
        {
            var replacement = compiler.gtChangeOperToNullCheck(ind, NodeThreading.LIR);
            replacement.IsUnusedValue = false;
            block.ReplaceNode(ind, replacement);
            ind = replacement;
        }
        else if (!useNullCheck && (ind.Oper is not GT_IND))
        {
            var replacement = new GenTreeIndir(GT_IND, ind.Type, ind.Addr, null, ind, NodeThreading.LIR) {
                IsUnusedValue = true,
            };
            block.ReplaceNode(ind, replacement);
            ind = replacement;
        }

        return ind;
    }
}
