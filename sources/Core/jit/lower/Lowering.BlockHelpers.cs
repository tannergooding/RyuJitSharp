// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private static GenTreeIntCon NewBlockHelperSize(Compiler compiler, GenTreeBlk block)
        => compiler.gtNewIconNode(TYP_I_IMPL, (nint)block.Size);

    private GenTree GetBlockCopySourceAddress(GenTree source)
    {
        if (source.Oper is GT_IND)
        {
            BlockRange().Remove(source);
            return source.AsIndir().Addr;
        }

        assert(source.Oper is GT_LCL_VAR or GT_LCL_FLD);
        var local = source.AsLclVarCommon();
        var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_I_IMPL, local.LclNum, local.LclOffs,
            null, null, source, NodeThreading.LIR);
        address.CopySsaIdentityFrom(local);
        address.IsContained = false;
        BlockRange().ReplaceNode(source, address);

        return address;
    }

    private unsafe void LowerBlockStoreAsHelperCall(GenTreeBlk block)
    {
        assert(!block.IsZeroingGCPointersOnHeap);
        assert(!BlockRange().TryGetUse(block, out _));
        var isVolatile = block.IsVolatile;
        var destination = block.Addr;
        var data = block.Data;
        CorInfoHelpFunc helper;

        if (block.IsInitBlkOp)
        {
            helper = CORINFO_HELP_MEMSET;
            if (data.Oper.IsInitVal)
            {
                BlockRange().Remove(data);
                data = data.AsUnOp().Op1;
            }
        }
        else
        {
            helper = CORINFO_HELP_MEMCPY;
            data = GetBlockCopySourceAddress(data);
        }

        var compiler = CompilerInstance;
        var size = NewBlockHelperSize(compiler, block);
        BlockRange().InsertBefore(data, size);
        // Morph detached placeholders, then reconnect the existing LIR values in their original order.
        var destinationPlaceholder = compiler.gtNewZeroConNode(destination.Type);
        var dataPlaceholder = compiler.gtNewZeroConNode(data.Type.ActualType);
        var sizePlaceholder = compiler.gtNewZeroConNode(size.Type.ActualType);
        var isMemzero = (helper is CORINFO_HELP_MEMSET) && data.IsIntegralConst(0);
        GenTreeCall call;
        if (isMemzero)
        {
            BlockRange().Remove(data);
            call = compiler.gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_MEMZERO, destinationPlaceholder, sizePlaceholder);
        }
        else
        {
            call = compiler.gtNewHelperCallNode(TYP_VOID, helper, destinationPlaceholder, dataPlaceholder, sizePlaceholder);
        }
        _ = compiler.fgMorphArgs(call);

        var range = LIR.SeqTree(compiler, call);
        var first = range.FirstNode;
        var last = range.LastNode;
        BlockRange().InsertBefore(block, range);
        block.BashToNOP();

        _ = BlockRange().TryGetUse(destinationPlaceholder, out var destinationUse);
        _ = BlockRange().TryGetUse(sizePlaceholder, out var sizeUse);
        destinationUse.ReplaceWith(destination);
        sizeUse.ReplaceWith(size);
        destinationPlaceholder.IsUnusedValue = true;
        sizePlaceholder.IsUnusedValue = true;
        if (!isMemzero)
        {
            _ = BlockRange().TryGetUse(dataPlaceholder, out var dataUse);
            dataUse.ReplaceWith(data);
            dataPlaceholder.IsUnusedValue = true;
        }

        LowerRange(first, last);
        MovePutArgNodesUpToCall(call);
        BlockRange().Remove(destinationPlaceholder);
        BlockRange().Remove(sizePlaceholder);
        if (!isMemzero)
        {
            BlockRange().Remove(dataPlaceholder);
        }

        if (isVolatile)
        {
            var firstBarrier = compiler.gtNewMemoryBarrierNode(BARRIER_STORE_ONLY);
            var secondBarrier = compiler.gtNewMemoryBarrierNode(BARRIER_LOAD_ONLY);
            BlockRange().InsertBefore(call, firstBarrier);
            BlockRange().InsertAfter(call, secondBarrier);
            _ = LowerNode(firstBarrier);
            _ = LowerNode(secondBarrier);
        }
    }

    private unsafe void LowerBlockStoreAsGcBulkCopyCall(GenTreeBlk block)
    {
        // The managed helper polls between chunks; its raw worker is safe only for bounded small copies.
        const int BULK_WRITEBARRIER_SMALL_SIZE = 128;
        assert((block.Oper is GT_STORE_BLK) && block.Layout.HasGCPtr && !block.IsInitBlkOp);
        assert(!block.IsVolatile);
        assert((block.Data.Oper is not GT_IND) || !block.Data.AsIndir().IsVolatile);

        var compiler = CompilerInstance;
        var destination = block.Addr;
        var data = block.Data;
        var destinationMayFault = block.IndirMayFault(compiler);
        var dataMayFault = (data.Oper is GT_IND) && data.IndirMayFault(compiler);
        data = GetBlockCopySourceAddress(data);

        var size = NewBlockHelperSize(compiler, block);
        BlockRange().InsertBefore(data, size);
        var helper = block.Layout.Size <= BULK_WRITEBARRIER_SMALL_SIZE
            ? CORINFO_HELP_BULK_WRITEBARRIER_SMALL
            : CORINFO_HELP_BULK_WRITEBARRIER;
        var destinationPlaceholder = compiler.gtNewZeroConNode(destination.Type);
        var dataPlaceholder = compiler.gtNewZeroConNode(data.Type.ActualType);
        var sizePlaceholder = compiler.gtNewZeroConNode(size.Type.ActualType);
        var call = compiler.gtNewHelperCallNode(TYP_VOID, helper, destinationPlaceholder, dataPlaceholder, sizePlaceholder);
        _ = compiler.fgMorphArgs(call);

        var range = LIR.SeqTree(compiler, call);
        var first = range.FirstNode;
        var last = range.LastNode;
        BlockRange().InsertBefore(block, range);
        block.BashToNOP();

        _ = BlockRange().TryGetUse(destinationPlaceholder, out var destinationUse);
        _ = BlockRange().TryGetUse(sizePlaceholder, out var sizeUse);
        destinationUse.ReplaceWith(destination);
        sizeUse.ReplaceWith(size);
        destinationPlaceholder.IsUnusedValue = true;
        sizePlaceholder.IsUnusedValue = true;
        _ = BlockRange().TryGetUse(dataPlaceholder, out var dataUse);
        dataUse.ReplaceWith(data);
        dataPlaceholder.IsUnusedValue = true;

        LowerRange(first, last);
        BlockRange().Remove(destinationPlaceholder);
        BlockRange().Remove(sizePlaceholder);
        BlockRange().Remove(dataPlaceholder);

        void WrapWithNullcheck(GenTree node)
        {
            if (compiler.fgAddrCouldBeNull(node))
            {
                _ = BlockRange().TryGetUse(node, out var use);
                var clone = compiler.gtNewLclvNode(node.Type.ActualType, use.ReplaceWithLclVar(compiler));
                var nullcheck = compiler.gtNewNullCheck(clone);
                BlockRange().InsertBefore(call, clone, nullcheck);
                _ = LowerNode(nullcheck);
            }
        }

        // Preserve faults after evaluating both addresses, before entering a helper that assumes non-null inputs.
        if (destinationMayFault)
        {
            WrapWithNullcheck(destination);
        }
        if (dataMayFault)
        {
            WrapWithNullcheck(data);
        }
        MovePutArgNodesUpToCall(call);
    }

    private bool TryDecomposeBlockStoreAsIndirs(GenTreeBlk block)
    {
        assert((block.Oper is GT_STORE_BLK) && !block.IsInitBlkOp && block.Layout.HasGCPtr);
        var compiler = CompilerInstance;
        var layout = block.Layout;
        var source = block.Data;
        assert(layout.Size == layout.SlotCount * TARGET_POINTER_SIZE);
        assert(source.Oper is GT_IND or GT_LCL_VAR or GT_LCL_FLD);

        if (!(block.IsVolatile || ((source.Oper is GT_IND) && source.AsIndir().IsVolatile)))
        {
            if (layout.GCPtrCount >= 4)
            {
                return false;
            }
            if ((layout.GCPtrCount > 1) &&
                (!compiler.opts.OptimizationEnabled || ((_block is not null) && _block.isRunRarely)))
            {
                return false;
            }
        }
#if DEBUG
        JITDUMP($"Decomposing STORE_BLK [{block.TreeId:D6}] as individual indirections\n");
#endif
        var destinationUse = new LIR.Use(BlockRange(), ref block.AddrRef, block);
        var destinationType = block.Addr.Type;
        var destinationLocal = destinationUse.ReplaceWithLclVar(compiler);
        var sourceAddressLocal = BAD_VAR_NUM;
        var sourceAddressType = TYP_UNDEF;
        var sourceLocal = BAD_VAR_NUM;
        ushort sourceOffset = 0;
        if (source.Oper is GT_IND)
        {
            var sourceUse = new LIR.Use(BlockRange(), ref source.AsIndir().AddrRef, source);
            sourceAddressType = source.AsIndir().Addr.Type;
            sourceAddressLocal = sourceUse.ReplaceWithLclVar(compiler);
        }
        else
        {
            sourceLocal = source.AsLclVarCommon().LclNum;
            sourceOffset = source.AsLclVarCommon().LclOffs;
            compiler.lvaSetVarDoNotEnregister(sourceLocal, DoNotEnregisterReason.BlockOp);
        }

        GenTree OffsetAddress(int local, var_types type, int offset)
        {
            GenTree address = compiler.gtNewLclvNode(type, local);
            if (offset != 0)
            {
                address = new GenTreeOp(GT_ADD, varTypeIsGC(type) ? TYP_BYREF : TYP_I_IMPL,
                    address, compiler.gtNewIconNode(TYP_I_IMPL, offset));
            }

            return address;
        }

        var destinationFlags = block.Flags & GTF_IND_FLAGS;
        var sourceFlags = source.Oper is GT_IND ? source.Flags & GTF_IND_FLAGS : GTF_EMPTY;
        void EmitStore(int offset, var_types scalarType, ClassLayout? runLayout)
        {
            var type = runLayout is not null ? TYP_STRUCT : scalarType;
            GenTree value = source.Oper is GT_IND
                ? compiler.gtNewLoadValueNode(type, OffsetAddress(sourceAddressLocal, sourceAddressType, offset),
                    runLayout, sourceFlags)
                : compiler.gtNewLclFldNode(type, sourceLocal, checked((ushort)(sourceOffset + offset)), runLayout);
            var store = compiler.gtNewStoreValueNode(type, OffsetAddress(destinationLocal, destinationType, offset),
                value, runLayout, destinationFlags);
            var range = LIR.SeqTree(compiler, store);
            var first = range.FirstNode;
            var last = range.LastNode;
            BlockRange().InsertBefore(block, range);
            LowerRange(first, last);
        }

        for (var i = 0; i < layout.SlotCount;)
        {
            if (layout.IsGCPtr(i))
            {
                EmitStore(i * TARGET_POINTER_SIZE, layout.GetGCPtrType(i), null);
                i++;
            }
            else
            {
                var start = i;
                do
                {
                    i++;
                }
                while ((i < layout.SlotCount) && !layout.IsGCPtr(i));
                var runSlots = i - start;
                var runLayout = runSlots > 1 ? compiler.typGetBlkLayout(runSlots * TARGET_POINTER_SIZE) : null;
                EmitStore(start * TARGET_POINTER_SIZE, TYP_I_IMPL, runLayout);
            }
        }

        BlockRange().Remove(block.Addr);
        if (source.Oper is GT_IND)
        {
            BlockRange().Remove(source.AsIndir().Addr);
        }
        BlockRange().Remove(source);
        block.BashToNOP();

        return true;
    }
}
