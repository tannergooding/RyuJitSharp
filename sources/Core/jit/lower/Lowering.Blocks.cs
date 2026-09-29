// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainBlockStoreAddress(GenTreeBlk block, uint size, GenTree address, GenTree? addressParent)
    {
#if TARGET_XARCH || TARGET_ARM64
        assert((block.Oper is GT_STORE_BLK) && (block._kind is GenTreeBlk.BlkOpKindUnroll));
        assert(size < int.MaxValue);

        if ((address.Oper is GT_LCL_ADDR) && IsContainableLclAddr(address.AsLclFld(), (uint)size))
        {
            address.IsContained = true;
            return;
        }
#if TARGET_XARCH
        if ((address.Oper is not GT_LEA) && !TryCreateAddrMode(ref address, true, block))
        {
            return;
        }

        var mode = address.AsAddrMode();
        // Unrolling adds displacements; keep even the conservative offset + size bound below INT32_MAX.
        if (mode.Offset >= (uint)int.MaxValue - size)
        {
            return;
        }
        var invariant = addressParent is null
            ? IsInvariantInRange(mode, block)
            : IsInvariantInRange(mode, block, addressParent);
        if (invariant)
        {
            mode.IsContained = true;
        }
#else
        if ((address.Oper is not GT_ADD) || address.HasOverflowCheck ||
            (address.AsOp().Op2.Oper is not GT_CNS_INT))
        {
            return;
        }

        var offsetNode = address.AsOp().Op2.AsIntCon();
        var offset = offsetNode.IconValue;
        // Native ClrSafeInt checks the conversion as well as offset + size; INT32_MAX is excluded.
        if ((offset < int.MinValue) || (offset > int.MaxValue) || ((long)offset + size >= int.MaxValue))
        {
            return;
        }

        var invariant = addressParent is null
            ? IsInvariantInRange(address, block)
            : IsInvariantInRange(address, block, addressParent);
        if (!invariant)
        {
            return;
        }

        BlockRange().Remove(offsetNode);
        var mode = new GenTreeAddrMode(address.Type, address.AsOp().Op1, null, 0, (int)offset,
            address, NodeThreading.LIR) {
            Flags = address.Flags & GTF_COMMON_MASK,
            IsContained = true,
        };
        BlockRange().ReplaceNode(address, mode);
#endif
#else
        throw new NotImplementedException("Block-store address containment is not ported for this target.");
#endif
    }

    private GenTree? LowerStoreBlock(GenTreeBlk block)
    {
        var next = block.Next;
        if (block.Data.Oper is GT_CALL)
        {
            LowerStoreSingleRegCallStruct(block);
        }
        else
        {
            LowerBlockStoreCommon(block);
        }

        return next;
    }

    private void LowerBlockStoreCommon(GenTreeBlk block)
    {
        assert(block.Oper is GT_STORE_BLK);
        assert(!block.ContainsReferences || block.IsCopyBlkOp || block.Data.IsIntegralConst(0));

        if (block.Data.Oper is GT_BLK)
        {
            var source = block.Data.AsBlk();
            var indir = new GenTreeIndir(GT_IND, source.Type, source.Addr, null, source, NodeThreading.LIR) {
                Flags = source.Flags,
            };
            BlockRange().ReplaceNode(source, indir);
            _ = LowerIndir(indir);
        }

        if (TryTransformStoreObjAsStoreInd(block))
        {
            return;
        }

        if (block.IsInitBlkOp)
        {
            LowerInitBlockStore(block);
        }
        else
        {
            LowerCopyBlockStore(block);
        }

        LowerIndirectStoreCoalescing(block);
    }

    private bool TryTransformStoreObjAsStoreInd(GenTreeBlk block)
    {
        assert(block.Oper is GT_STORE_BLK);
        if (!CompilerInstance.opts.OptimizationEnabled)
        {
            return false;
        }

        var registerType = block.Layout.RegisterType;
        if (registerType is TYP_UNDEF)
        {
            return false;
        }

        var source = block.Data;
        if (varTypeIsGC(registerType))
        {
            // STOREIND does not contain a source that needs a barrier; STORE_BLK can do better.
            return false;
        }
        if (source.Oper.IsInitVal && !source.IsCnsInitVal)
        {
            return false;
        }

        if (source.IsCnsInitVal)
        {
#if !TARGET_XARCH
            if (varTypeIsSimd(registerType))
            {
                return false;
            }
#endif
            assert(!block.ContainsReferences);
            if (source.Oper.IsInitVal)
            {
                BlockRange().Remove(source);
                source = source.AsUnOp().Op1;
            }

            var fill = unchecked((byte)source.AsIntCon().IconValue);
            var constant = CompilerInstance.gtNewConWithPattern(registerType, fill);
            BlockRange().InsertAfter(source, constant);
            BlockRange().Remove(source);
            block.Data = constant;
        }
        else if (varTypeIsStruct(source.Type))
        {
            source.ChangeType(registerType);
#if TARGET_ARM64
            switch (source.Oper)
            {
                case GT_IND:
                {
                    _ = LowerIndir(source.AsIndir());
                    break;
                }

                case GT_LCL_VAR:
                {
                    LowerLclVar(source.AsLclVar());
                    break;
                }

                case GT_LCL_FLD:
                {
                    VerifyLclFldDoNotEnregister(source.AsLclVarCommon().LclNum);
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
#else
            _ = LowerNode(block.Data);
#endif
        }
        else
        {
            unreached();
        }

#if DEBUG
        JITDUMP($"Replacing STORE_BLK with STOREIND for [{block.TreeId:D6}]\n");
#endif
        var store = new GenTreeStoreInd(registerType, block.Addr, block.Data, block, NodeThreading.LIR);
        BlockRange().ReplaceNode(block, store);

#if TARGET_XARCH
        if (varTypeIsSmall(registerType) && (source.Oper is GT_IND or GT_LCL_FLD))
        {
            source.Flags |= GTF_DONT_EXTEND;
        }
#endif
        _ = LowerStoreIndirCommon(store);
        return true;
    }

    private unsafe void LowerStoreSingleRegCallStruct(GenTreeBlk block)
    {
        assert(block.Data.Oper is GT_CALL);
        var call = block.Data.AsCall();
        assert(!call.HasMultiRegRetVal);

        var registerType = block.Layout.RegisterType;
        if (registerType is not TYP_UNDEF)
        {
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            if (varTypeIsFloating(call.Type))
            {
                registerType = call.Type;
            }
#endif
#if TARGET_WASM
            var wasmAbiType = CompilerInstance.info.compCompHnd->getWasmLowering(call.RetClsHnd);
            assert(wasmAbiType is not CORINFO_WASM_TYPE_VOID);
            registerType = WasmClassifier.ToJitType(wasmAbiType);
#endif
            var store = new GenTreeStoreInd(registerType, block.Addr, block.Data, block, NodeThreading.LIR) {
                Flags = block.Flags,
            };
            BlockRange().ReplaceNode(block, store);
            _ = LowerStoreIndirCommon(store);
            return;
        }

#if WINDOWS_AMD64_ABI
        unreached();
#else
        block._kind = GenTreeBlk.BlkOpKindUnroll;
        block.Data = SpillStructCallResult(call);
        LowerBlockStoreCommon(block);
#endif
    }

    private void LowerInitBlockStore(GenTreeBlk block)
    {
#if TARGET_XARCH || TARGET_ARM64
        assert(block.IsInitBlkOp);
#if TARGET_XARCH
        _ = TryCreateAddrMode(ref block.AddrRef, false, block);
#endif
        var address = block.Addr;
        var source = block.Data;
        var size = block.Size;
        if (source.Oper is GT_INIT_VAL)
        {
            source.IsContained = true;
            source = source.AsUnOp().Op1;
        }

#if TARGET_ARM64
        // Aligned ARM64 SIMD stores preserve 8-byte atomicity for GC pointers.
        const bool canUseSimd = true;
#else
        var canUseSimd = !block.IsOnHeapAndContainsReferences;
#endif
        if ((source.Oper is GT_CNS_INT) &&
            (size <= (uint)CompilerInstance.GetUnrollThreshold(Compiler.UnrollKind.Memset, canUseSimd)))
        {
            var fill = source.AsIntCon().IconValue & 0xFF;
#if TARGET_XARCH
            if (canUseSimd && (size >= XMM_REGSIZE_BYTES))
            {
                // Overlapping SIMD stores handle the remainder without another fill register.
                source.IsContained = true;
            }
            else if (fill != 0)
#else
            if (fill == 0)
            {
                source.IsContained = true;
            }
            else
#endif
            {
#if TARGET_64BIT
                if (size >= REGSIZE_BYTES)
                {
                    fill = unchecked(fill * (nint)0x0101010101010101L);
                    source.Type = TYP_LONG;
                }
                else
#endif
                {
                    fill = unchecked(fill * 0x01010101);
                }
            }

            block._kind = GenTreeBlk.BlkOpKindUnroll;
            source.AsIntCon().IconValue = fill;
            ContainBlockStoreAddress(block, size, address, null);
            return;
        }

        if (source.IsIntegralConst(0) && block.ContainsReferences)
        {
            // A GC-safe helper could observe a torn GC pointer, including in a stack destination.
            block._kind = GenTreeBlk.BlkOpKindLoop;
#if TARGET_ARM64
            source.IsContained = true;
#endif
        }
        else
        {
            LowerBlockStoreAsHelperCall(block);
        }
#else
        throw new NotImplementedException("Block initialization lowering is not ported for this target.");
#endif
    }

    private void LowerCopyBlockStore(GenTreeBlk block)
    {
#if TARGET_XARCH || TARGET_ARM64
        assert((block.Oper is GT_STORE_BLK) && !block.IsInitBlkOp);
        var source = block.Data;
        var address = block.Addr;
        var size = block.Size;
        assert(source.Oper is GT_IND or GT_LCL_VAR or GT_LCL_FLD);
        source.IsContained = true;

        if (source.Oper is GT_LCL_VAR)
        {
            CompilerInstance.lvaSetVarDoNotEnregister(source.AsLclVar().LclNum, DoNotEnregisterReason.StoreBlkSrc);
        }

        var copyGcPointers = block.Layout.HasGCPtr;
        var isNotHeap = block.IsAddressNotOnHeap(CompilerInstance);
        var unrollLimit = CompilerInstance.GetUnrollThreshold(Compiler.UnrollKind.Memcpy, !copyGcPointers || isNotHeap);
#if !JIT32_GCENCODER
        if (copyGcPointers && isNotHeap && (size <= unrollLimit))
        {
            copyGcPointers = false;
            // The temporary registers do not report GC references, so the entire copy must be non-interruptible.
            block._gcUnsafe = true;
        }
#endif
        if (copyGcPointers)
        {
            if (!TryDecomposeBlockStoreAsIndirs(block))
            {
                LowerBlockStoreAsGcBulkCopyCall(block);
            }
            return;
        }

        if (size <= (uint)unrollLimit)
        {
            block._kind = GenTreeBlk.BlkOpKindUnroll;
            if (source.Oper is GT_IND)
            {
                ContainBlockStoreAddress(block, size, source.AsIndir().Addr, source);
            }
            ContainBlockStoreAddress(block, size, address, null);
            return;
        }

        LowerBlockStoreAsHelperCall(block);
#else
        throw new NotImplementedException("Block-copy lowering is not ported for this target.");
#endif
    }
}
