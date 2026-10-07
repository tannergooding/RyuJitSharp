// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_WASM
    private unsafe void genCreateAndStoreGCInfoWasm()
    {
        var maxVirtualIP = 0u;
        foreach (var func in _compiler.Funcs)
        {
            if (func.endVirtualIP > maxVirtualIP)
            {
                maxVirtualIP = func.endVirtualIP;
            }
        }

        var codeSize = unchecked(2 * maxVirtualIP);
        const uint prologSize = 1;

        using var encoder = new GcInfoEncoder(_compiler.info.compCompHnd, _compiler.info.compMethodInfo);
        GCInfo.gcInfoBlockHdrSave(encoder, codeSize, prologSize);

        var callCount = 0u;
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref callCount);
        encoder.FinalizeSlotIds();
        GCInfo.gcMakeRegPtrTable(encoder, codeSize, prologSize,
            GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref callCount);

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
#endif
}
