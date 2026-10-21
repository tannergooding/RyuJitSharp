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
            // The transformed access width controls non-xarch containment. Xarch instead chooses
            // IND or NULLCHECK based on whether address containment succeeded, so transforms last.
            if (!Configuration.TARGET_XARCH && ((ind.Oper is GT_NULLCHECK) || ind.IsUnusedValue))
            {
                ind = TransformUnusedIndirection(ind, CompilerInstance, BlockRange());
            }
            if (Configuration.TARGET_ARM64)
            {
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
            }
            else
            {
                _ = TryCreateAddrMode(ref ind.AddrRef, true, ind);
            }

            ContainCheckIndir(ind);
            if (Configuration.TARGET_XARCH && ((ind.Oper is GT_NULLCHECK) || ind.IsUnusedValue))
            {
                ind = TransformUnusedIndirection(ind, CompilerInstance, BlockRange());
            }
        }
        else
        {
            _ = TryCreateAddrMode(ref ind.AddrRef, false, ind);
        }

        if (Configuration.TARGET_ARM64 && CompilerInstance.opts.OptimizationEnabled && (ind.Oper is GT_IND))
        {
            _ = OptimizeForLdpStp(ind);
        }
        return next;
    }

    private void ContainCheckIndir(GenTreeIndir node)
    {
        if (Configuration.TARGET_XARCH)
        {
            var addr = node.Addr;
            if (node.Type is TYP_STRUCT)
            {
                return;
            }
            if ((node.Flags & GTF_IND_REQ_ADDR_IN_REG) != 0)
            {
                return;
            }

            if ((addr.Oper is GT_LCL_ADDR) && ((node.Flags & GTF_IND_VOLATILE) == 0) &&
                IsContainableLclAddr(addr.AsLclFld(), (uint)node.Size))
            {
                MakeSrcContained(node, addr);
            }
            else if (addr.Oper.IsCnsIntOrI)
            {
                var icon = addr.AsIntConCommon();
                if ((!Configuration.FEATURE_SIMD || (node.Type is not TYP_SIMD12) ||
                     !icon.ImmedValNeedsReloc(CompilerInstance)) &&
                    icon.FitsInAddrBase(CompilerInstance))
                {
                    MakeSrcContained(node, addr);
                }
            }
            else if ((addr.Oper is GT_LEA) && IsInvariantInRange(addr, node))
            {
                MakeSrcContained(node, addr);
            }

            return;
        }

        if (Configuration.TARGET_ARM)
        {
            if (node.Type is TYP_STRUCT)
            {
                return;
            }
            if (Configuration.FEATURE_SIMD && (node.Type is TYP_SIMD12))
            {
                return;
            }

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

            return;
        }

        if (Configuration.TARGET_ARM64)
        {
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

            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (node.Type is TYP_STRUCT)
            {
                return;
            }
            if (Configuration.FEATURE_SIMD)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V SIMD indirection containment is not ported.");
                return;
            }

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

            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            if (node.Type is TYP_STRUCT)
            {
                return;
            }

            if (Configuration.FEATURE_SIMD)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "LoongArch SIMD indirection containment is not ported.");
                return;
            }

            var addr = node.Addr;
            if ((addr.Oper is GT_LEA) && IsInvariantInRange(addr, node))
            {
                MakeSrcContained(node, addr);
            }
            else if ((addr.Oper is GT_LCL_ADDR) && IsContainableLclAddr(addr.AsLclFld(), (uint)node.Size))
            {
                MakeSrcContained(node, addr);
            }

            return;
        }

        if (Configuration.TARGET_WASM)
        {
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

                SetMultiplyUsed(
                    addressToReuse,
                    Configuration.DEBUG ? "ContainCheckIndir load Addr (null check or simd12 lane load)" : null);
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
            var isNativeAotFunctionAddress =
                CompilerInstance.IsNativeAot && address.IsIconHandle(Globals.GTF_ICON_FTN_ADDR);
            if ((node.Oper is GT_IND) && (node.Type is not TYP_SIMD12) && address.IsIconHandle() &&
                !isNativeAotFunctionAddress &&
                address.AsIntConCommon().ImmedValNeedsReloc(CompilerInstance) &&
                ((address._lirFlags & LIR.Flags.MultiplyUsed) == LIR.Flags.None))
            {
                // Only loads can replace the address computation with a relocated memarg while preserving operand order.
                MakeSrcContained(node, address);
            }

            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Indirection containment is not ported for this target.");
        return;
    }

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
        bool useNullCheck;
        if (Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64 ||
            Configuration.TARGET_WASM)
        {
            useNullCheck = true;
        }
        else if (Configuration.TARGET_ARM)
        {
            useNullCheck = false;
        }
        else if (Configuration.TARGET_XARCH)
        {
            // A contained address needs a target register; otherwise a compare can perform the probe.
            useNullCheck = !ind.Addr.IsContained;
            ind.Flags &= ~GTF_DONT_EXTEND;
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Unused indirection lowering is not ported for this target.");
            return default!;
        }
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
