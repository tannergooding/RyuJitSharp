// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    internal unsafe void* genCreateAndStoreGCInfoJIT32(uint codeSize, uint prologSize, uint epilogSize)
    {
        if (!Configuration.JIT32_GCENCODER)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "The JIT32 GC encoder requires X86.");
        }

#pragma warning disable IDE0007
        byte* headerBuf = stackalloc byte[64];
#pragma warning restore IDE0007
        var header = default(GCInfo.InfoHdr);
        var cached = 0;

        if (_compiler.compHndBBtabCount != 0)
        {
            _gcInfo.gcMarkFilterVarsPinned();
        }

        var headerSize = _gcInfo.gcInfoBlockHdrSave(headerBuf, 0, codeSize, prologSize,
            epilogSize, ref header, ref cached);
        _compiler.compInfoBlkSize = unchecked((nint)headerSize);

        nuint argTabOffset = 0;
        var ptrMapSize = _gcInfo.gcPtrTableSize(header, codeSize, ref argTabOffset);

        if (Configuration.DISPLAY_SIZES)
        {
            if (Interruptible)
            {
                gcHeaderISize += headerSize;
                gcPtrMapISize += ptrMapSize;
            }
            else
            {
                gcHeaderNSize += headerSize;
                gcPtrMapNSize += ptrMapSize;
            }
        }

        _compiler.compInfoBlkSize = unchecked(_compiler.compInfoBlkSize + (nint)ptrMapSize);
        _compiler.compInfoBlkAddr = (byte*)_compiler.info.compCompHnd->allocGCInfo(_compiler.compInfoBlkSize);
        var infoPtr = _compiler.compInfoBlkAddr;

        var writtenHeaderSize = _gcInfo.gcInfoBlockHdrSave(_compiler.compInfoBlkAddr, -1,
            codeSize, prologSize, epilogSize, ref header, ref cached);
        _compiler.compInfoBlkAddr = (byte*)((nuint)_compiler.compInfoBlkAddr + writtenHeaderSize);

        if (Configuration.DEBUG)
        {
            assert(_compiler.compInfoBlkAddr == (byte*)((nuint)infoPtr + headerSize));
        }
        _compiler.compInfoBlkAddr = _gcInfo.gcPtrTableSave(_compiler.compInfoBlkAddr,
            header, codeSize, ref argTabOffset);

        if (Configuration.DEBUG)
        {
            assert(_compiler.compInfoBlkAddr == (byte*)((nuint)infoPtr + headerSize + ptrMapSize));
        }

        if (Configuration.DUMP_GC_TABLES)
        {
            genDumpGCInfoJIT32(infoPtr);
        }

        noway_assert(_compiler.compInfoBlkAddr == (byte*)((nuint)infoPtr + (nuint)_compiler.compInfoBlkSize));

        return infoPtr;
    }

    private unsafe void genDumpGCInfoJIT32(byte* infoPtr)
    {
        Debug.Assert(Configuration.DUMP_GC_TABLES);

        if (_compiler.opts.dspGCtbls)
        {
            var basePtr = infoPtr;
            GCInfo.InfoHdr dumpHeader = default;
            jitprintf($"GC Info for method {_compiler.info.compFullName}\n");
            jitprintf($"GC info size = {_compiler.compInfoBlkSize,3}\n");

            var size = _gcInfo.gcInfoBlockHdrDump(basePtr, ref dumpHeader, out var methodSize);
            jitprintf("\n");

            if (_compiler.opts.dspGCtbls)
            {
                basePtr = (byte*)((nuint)basePtr + size);
                size = _gcInfo.gcDumpPtrTable(basePtr, dumpHeader, methodSize);
                jitprintf("\n");
                noway_assert(_compiler.compInfoBlkAddr == (byte*)((nuint)basePtr + size));
            }
        }
    }

    internal unsafe void genCreateAndStoreGCInfo(uint codeSize, uint prologSize, uint epilogSize)
    {
        if (Configuration.JIT32_GCENCODER)
        {
            _ = genCreateAndStoreGCInfoJIT32(codeSize, prologSize, epilogSize);
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            genCreateAndStoreGCInfoWasm();
            return;
        }

        if (Configuration.TARGET_ARMARCH)
        {
            genCreateAndStoreGCInfoArmArch(codeSize, prologSize, epilogSize);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            genCreateAndStoreGCInfoRiscV64(codeSize, prologSize, epilogSize);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            genCreateAndStoreGCInfoLoongArch64(codeSize, prologSize, epilogSize);
            return;
        }

        if (!Configuration.TARGET_AMD64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "GC-info publication is unsupported for this target.");
        }

        genCreateAndStoreGCInfoX64(codeSize, prologSize);
    }

    internal unsafe void genCreateAndStoreGCInfoRiscV64(uint codeSize, uint prologSize, uint epilogSize)
    {
        using var encoder = new GcInfoEncoder(_compiler.info.compCompHnd, _compiler.info.compMethodInfo);
        GCInfo.gcInfoBlockHdrSave(encoder, codeSize, prologSize);

        var callCount = 0u;
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref callCount);
        encoder.FinalizeSlotIds();
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref callCount);

        if (Configuration.FEATURE_REMAP_FUNCTION && _compiler.opts.compDbgEnC)
        {
            NYI_RISCV64("compDbgEnc in genCreateAndStoreGCInfo-----unimplemented/unused on RISCV64 yet----");
        }

        if (_compiler.opts.IsReversePInvoke)
        {
            var reversePInvokeFrameVarNumber = _compiler.lvaReversePInvokeFrameVar;
            assert(reversePInvokeFrameVarNumber != BAD_VAR_NUM);
            ref var reversePInvokeFrameVar = ref _compiler.lvaGetDesc(reversePInvokeFrameVarNumber);
            encoder.SetReversePInvokeFrameSlot(reversePInvokeFrameVar.StackOffset);
        }

        encoder.Build();
        _compiler.compInfoBlkAddr = encoder.Emit();
        _compiler.compInfoBlkSize = unchecked((nint)encoder.GetEncodedGCInfoSize());
    }

    internal unsafe void genCreateAndStoreGCInfoArmArch(uint codeSize, uint prologSize, uint epilogSize)
    {
        using var encoder = new GcInfoEncoder(_compiler.info.compCompHnd, _compiler.info.compMethodInfo);
        GCInfo.gcInfoBlockHdrSave(encoder, codeSize, prologSize);

        var callCount = 0u;
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref callCount);
        encoder.FinalizeSlotIds();
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref callCount);

        if (Configuration.TARGET_ARM64 && _compiler.opts.compDbgEnC)
        {
            // Preserve the return address, saved frame pointer, varargs callee-saves,
            // synchronized-method state, and async contexts for EnC remapping.
            var preservedAreaSize = (2 + BitOperations.PopCount((ulong)TargetRuntimeMetadata.EncodedCalleeSavedMask)) * Configuration.REGSIZE_BYTES;
            if (_compiler.info.compIsVarArgs)
            {
                preservedAreaSize += Configuration.MAX_REG_ARG * Configuration.REGSIZE_BYTES;
            }
            if ((_compiler.info.compFlags & CORINFO_FLG_SYNCH) != 0)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaMonAcquired) == -preservedAreaSize);
            }
            if (_compiler.lvaResumedIndicator != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaResumedIndicator) ==
                    -preservedAreaSize);
            }
            if (_compiler.lvaAsyncThreadObjectVar != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncThreadObjectVar) ==
                    -preservedAreaSize);
            }
            if (_compiler.lvaAsyncExecutionContextVar != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncExecutionContextVar) ==
                    -preservedAreaSize);
            }
            if (_compiler.lvaAsyncSynchronizationContextVar != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncSynchronizationContextVar) ==
                    -preservedAreaSize);
            }

            encoder.SetSizeOfEditAndContinuePreservedArea(unchecked((uint)preservedAreaSize));
            encoder.SetSizeOfEditAndContinueFixedStackFrame(unchecked((uint)genTotalFrameSize));
            JITDUMP("EnC info:\n");
            JITDUMP($"  EnC preserved area size = {preservedAreaSize}\n");
            JITDUMP($"  Fixed stack frame size = {genTotalFrameSize}\n");
        }
        if (_compiler.opts.IsReversePInvoke)
        {
            var reversePInvokeFrameVarNumber = _compiler.lvaReversePInvokeFrameVar;
            assert(reversePInvokeFrameVarNumber != BAD_VAR_NUM);
            ref var reversePInvokeFrameVar = ref _compiler.lvaGetDesc(reversePInvokeFrameVarNumber);
            encoder.SetReversePInvokeFrameSlot(reversePInvokeFrameVar.StackOffset);
        }

        encoder.Build();
        _compiler.compInfoBlkAddr = encoder.Emit();
        _compiler.compInfoBlkSize = unchecked((nint)encoder.GetEncodedGCInfoSize());
    }

    internal unsafe void genCreateAndStoreGCInfoX64(uint codeSize, uint prologSize)
    {
        if (!Configuration.TARGET_AMD64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "GC-info publication requires AMD64.");
        }

        using var encoder = new GcInfoEncoder(_compiler.info.compCompHnd, _compiler.info.compMethodInfo);
        GCInfo.gcInfoBlockHdrSave(encoder, codeSize, prologSize);

        // Assign every slot before freezing the table, then describe its live ranges.
        var callCount = 0u;
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref callCount);
        encoder.FinalizeSlotIds();
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref callCount);

        if (_compiler.opts.compDbgEnC)
        {
            // The EnC frame header contains the return address, saved frame pointer
            // and callee-saves, followed by synchronized and async state when present.
            var preservedAreaSize = (2 + BitOperations.PopCount((ulong)TargetRuntimeMetadata.EncodedCalleeSavedMask)) * Configuration.REGSIZE_BYTES;
            if ((_compiler.info.compFlags & CORINFO_FLG_SYNCH) != 0)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaMonAcquired) == -preservedAreaSize);
            }
            if (_compiler.lvaResumedIndicator != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaResumedIndicator) == -preservedAreaSize);
            }
            if (_compiler.lvaAsyncThreadObjectVar != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncThreadObjectVar) == -preservedAreaSize);
            }
            if (_compiler.lvaAsyncExecutionContextVar != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncExecutionContextVar) == -preservedAreaSize);
            }
            if (_compiler.lvaAsyncSynchronizationContextVar != BAD_VAR_NUM)
            {
                preservedAreaSize += Configuration.TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncSynchronizationContextVar) == -preservedAreaSize);
            }

            encoder.SetSizeOfEditAndContinuePreservedArea((uint)preservedAreaSize);
            JITDUMP("EnC info:\n");
            JITDUMP($"  EnC preserved area size = {preservedAreaSize}\n");
        }

        if (_compiler.opts.IsReversePInvoke)
        {
            var reversePInvokeFrameVarNumber = _compiler.lvaReversePInvokeFrameVar;
            assert(reversePInvokeFrameVarNumber != BAD_VAR_NUM);
            ref var reversePInvokeFrameVar = ref _compiler.lvaGetDesc(reversePInvokeFrameVarNumber);
            encoder.SetReversePInvokeFrameSlot(reversePInvokeFrameVar.StackOffset);
        }

        encoder.Build();
        _compiler.compInfoBlkAddr = encoder.Emit();
        _compiler.compInfoBlkSize = unchecked((nint)encoder.GetEncodedGCInfoSize());
    }
}
