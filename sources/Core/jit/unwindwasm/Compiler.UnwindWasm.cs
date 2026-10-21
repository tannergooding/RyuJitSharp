// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    // Frame size and virtual IP length are uint32 values, each requiring at most five ULEB128 bytes.
    private const int WasmUnwindInfoBufferSize = 10;

    public void unwindBegPrologWasm()
    {
    }

    public void unwindEndPrologWasm()
    {
    }

    public void unwindAllocStackWasm(uint size)
    {
        ref var func = ref funCurrentFunc();
        func.funWasmFrameSize = unchecked(func.funWasmFrameSize + size);
    }

    public void unwindReserveWasm()
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindReserveFuncWasm(in func);
        }
    }

    public unsafe void unwindEmitWasm(void* pHotCode, void* pColdCode)
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindEmitFuncWasm(in func, pHotCode, pColdCode);
        }
    }

    private void unwindReserveFuncWasm(in FuncInfoDsc func)
    {
        var isFunclet = func.IsFunclet();
        assert(func.endVirtualIP > func.startVirtualIP);
        var virtualIPLength = unchecked(func.endVirtualIP - func.startVirtualIP);
        var encodedSize = WasmUnwindInfoSizeOfULEB128(func.funWasmFrameSize)
            + WasmUnwindInfoSizeOfULEB128(virtualIPLength);

        eeReserveUnwindInfoWasm(isFunclet, false, encodedSize);
    }

    private unsafe void unwindEmitFuncWasm(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
        var emitter = UnwindEmitter();
        var startLocation = func.startLoc;
        if (startLocation is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "Wasm unwind start location is missing.");
            return;
        }
        var endLocation = func.endLoc;
        if (endLocation is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "Wasm unwind end location is missing.");
            return;
        }
        var startOffset = startLocation.Value.CodeOffset(emitter);
        var endOffset = endLocation.Value.CodeOffset(emitter);

        // Wasm does not have cold code.
        pColdCode = null;

        var buffer = stackalloc byte[WasmUnwindInfoBufferSize];
        var index = 0;
        assert(func.endVirtualIP > func.startVirtualIP);
        index += WriteWasmUnwindInfoULEB128(buffer + index, func.funWasmFrameSize);
        index += WriteWasmUnwindInfoULEB128(
            buffer + index, unchecked(func.endVirtualIP - func.startVirtualIP));
        assert(index <= WasmUnwindInfoBufferSize);

        JITDUMP(
            $"Unwind info for {(func.IsFunclet() ? "funclet" : "main")} {func.GetFuncletIdx(this)}: " +
            $"VIP range [{func.startVirtualIP}, {func.endVirtualIP}); frame size {func.funWasmFrameSize}\n");

        eeAllocUnwindInfo(
            (byte*)pHotCode,
            (byte*)pColdCode,
            unchecked((int)startOffset),
            unchecked((int)endOffset),
            index,
            buffer,
            (CorJitFuncKind)func.funKind);
    }

    private unsafe void eeReserveUnwindInfoWasm(bool isFunclet, bool isColdCode, int unwindSize)
    {
        if (Configuration.DEBUG && verbose)
        {
            jitprintf(
                $"reserveUnwindInfo(isFunclet={(isFunclet ? "true" : "false")}, " +
                $"isColdCode={(isColdCode ? "true" : "false")}, unwindSize=0x{unwindSize:x})\n");
        }
        if (info.compMatchedVM)
        {
            info.compCompHnd->reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        }
    }

    private static int WasmUnwindInfoSizeOfULEB128(uint value)
    {
        var size = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            size++;
        }

        return size;
    }

    private static unsafe int WriteWasmUnwindInfoULEB128(byte* destination, uint value)
    {
        var size = 0;
        while (value >= 0x80)
        {
            destination[size++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }

        destination[size++] = (byte)value;
        return size;
    }
}
