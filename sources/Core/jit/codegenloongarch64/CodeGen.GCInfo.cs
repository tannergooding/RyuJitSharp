// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genCreateAndStoreGCInfoLoongArch64(uint codeSize, uint prologSize, uint epilogSize)
    {
        using var encoder = new GcInfoEncoder(_compiler.info.compCompHnd, _compiler.info.compMethodInfo);
        GCInfo.gcInfoBlockHdrSave(encoder, codeSize, prologSize);

        var callCount = 0u;
        GCInfo.gcMakeRegPtrTable(
            encoder,
            codeSize,
            prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS,
            ref callCount);

        encoder.FinalizeSlotIds();

        GCInfo.gcMakeRegPtrTable(
            encoder,
            codeSize,
            prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK,
            ref callCount);

#if FEATURE_REMAP_FUNCTION
        if (_compiler.opts.compDbgEnC)
        {
            NYI("compDbgEnc in CodeGen::genCreateAndStoreGCInfo() ---unimplemented/unused on LA64 yet---");
        }
#endif

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
#endif
