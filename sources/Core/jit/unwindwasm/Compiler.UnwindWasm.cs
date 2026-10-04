// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    // Frame size and virtual IP length are uint32 values, each requiring at most five ULEB128 bytes.
    private const int WasmUnwindInfoBufferSize = 10;

    public void unwindBegProlog()
    {
    }

    public void unwindEndProlog()
    {
    }

    public void unwindAllocStack(uint size)
    {
        ref var func = ref funCurrentFunc();
        func.funWasmFrameSize = unchecked(func.funWasmFrameSize + size);
    }

    public void unwindReserve()
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindReserveFunc(in func);
        }
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindEmitFunc(in func, pHotCode, pColdCode);
        }
    }

    private void unwindReserveFunc(in FuncInfoDsc func)
    {
        var isFunclet = func.IsFunclet();
        assert(func.endVirtualIP > func.startVirtualIP);
        var virtualIPLength = unchecked(func.endVirtualIP - func.startVirtualIP);
        var encodedSize = WasmUnwindInfoSizeOfULEB128(func.funWasmFrameSize)
            + WasmUnwindInfoSizeOfULEB128(virtualIPLength);

        eeReserveUnwindInfo(isFunclet, false, encodedSize);
    }

    private unsafe void unwindEmitFunc(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
        var emitter = UnwindEmitter();
        var startLocation = func.startLoc
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Wasm unwind start location is missing.");
        var endLocation = func.endLoc
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Wasm unwind end location is missing.");
        var startOffset = startLocation.CodeOffset(emitter);
        var endOffset = endLocation.CodeOffset(emitter);

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

    private unsafe void eeReserveUnwindInfo(bool isFunclet, bool isColdCode, int unwindSize)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf(
                $"reserveUnwindInfo(isFunclet={(isFunclet ? "true" : "false")}, " +
                $"isColdCode={(isColdCode ? "true" : "false")}, unwindSize=0x{unwindSize:x})\n");
        }
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        }
    }

    private unsafe void eeAllocUnwindInfo(
        byte* pHotCode, byte* pColdCode, int startOffset, int endOffset, int unwindSize, byte* pUnwindBlock,
        CorJitFuncKind funcKind)
    {
#if DEBUG
        if (verbose)
        {
            var functionDescription = funcKind switch
            {
                CorJitFuncKind.CORJIT_FUNC_ROOT => "main function",
                CorJitFuncKind.CORJIT_FUNC_HANDLER => "handler",
                CorJitFuncKind.CORJIT_FUNC_FILTER => "filter",
                _ => "ILLEGAL",
            };
            jitprintf(
                $"allocUnwindInfo(pHotCode=0x{(nuint)dspPtr(pHotCode):X16}, " +
                $"pColdCode=0x{(nuint)dspPtr(pColdCode):X16}, startOffset=0x{startOffset:x}, " +
                $"endOffset=0x{endOffset:x}, unwindSize=0x{unwindSize:x}, " +
                $"pUnwindBlock=0x{(nuint)dspPtr(pUnwindBlock):X16}, funKind={(int)funcKind} " +
                $"({functionDescription}))\n");
        }
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->allocUnwindInfo(
                pHotCode, pColdCode, startOffset, endOffset, unwindSize, pUnwindBlock, funcKind);
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
#endif
