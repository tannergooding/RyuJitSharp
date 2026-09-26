// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    internal unsafe void genCreateAndStoreGCInfo(uint codeSize, uint prologSize, uint epilogSize
#if DEBUG
        , void* codePtr
#endif
        )
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "GC-info publication requires Windows AMD64.");
#else
        genCreateAndStoreGCInfoX64(codeSize, prologSize
#if DEBUG
            , codePtr
#endif
            );
#endif
    }

    internal unsafe void genCreateAndStoreGCInfoX64(uint codeSize, uint prologSize
#if DEBUG
        , void* codePtr
#endif
        )
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "GC-info publication requires Windows AMD64.");
#else
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
            var preservedAreaSize = (2 + BitOperations.PopCount((ulong)SRBM_ENC_CALLEE_SAVED)) * REGSIZE_BYTES;
            if ((_compiler.info.compFlags & CORINFO_FLG_SYNCH) != 0)
            {
                preservedAreaSize += TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaMonAcquired) == -preservedAreaSize);
            }
            if (_compiler.lvaResumedIndicator != BAD_VAR_NUM)
            {
                preservedAreaSize += TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaResumedIndicator) == -preservedAreaSize);
            }
            if (_compiler.lvaAsyncThreadObjectVar != BAD_VAR_NUM)
            {
                preservedAreaSize += TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncThreadObjectVar) == -preservedAreaSize);
            }
            if (_compiler.lvaAsyncExecutionContextVar != BAD_VAR_NUM)
            {
                preservedAreaSize += TARGET_POINTER_SIZE;
                assert(_compiler.lvaGetCallerSPRelativeOffset(_compiler.lvaAsyncExecutionContextVar) == -preservedAreaSize);
            }
            if (_compiler.lvaAsyncSynchronizationContextVar != BAD_VAR_NUM)
            {
                preservedAreaSize += TARGET_POINTER_SIZE;
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
#endif
    }
}
