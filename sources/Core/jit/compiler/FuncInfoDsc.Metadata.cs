// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if TARGET_WASM || FEATURE_CFI_SUPPORT
using System.Collections.Generic;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct FuncInfoDsc
{
    public readonly bool IsFunclet()
    {
        return funKind != FuncKind.FUNC_ROOT;
    }

    public readonly bool IsMethod()
    {
        return funKind == FuncKind.FUNC_ROOT;
    }

    public readonly uint GetFuncletIdx(Compiler compiler)
    {
        assert(Unsafe.IsAddressLessThanOrEqualTo(in MemoryMarshal.GetArrayDataReference(compiler.compFuncInfos), in this)
            && Unsafe.IsAddressLessThan(in this,
                in Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(compiler.compFuncInfos), compiler.compFuncInfoCount)));

        // Use the owned CLR table's stride, not an estimate of the native descriptor layout.
        var index = unchecked((uint)(Unsafe.ByteOffset(
            in MemoryMarshal.GetArrayDataReference(compiler.compFuncInfos), in this) / Unsafe.SizeOf<FuncInfoDsc>()));

        assert(Unsafe.AreSame(in this, in compiler.compFuncInfos[index]));
        return index;
    }

#if TARGET_WASM
    public struct WasmLocalsDecl
    {
        public WasmValueType Type;
        public uint Count;
    }

    public List<WasmLocalsDecl>? funWasmLocalDecls;
    public uint funWasmFrameSize;

    // CLR arrays bypass struct constructors; zero backing preserves the native UINT_MAX default.
    private uint _funWasmExnRefLocalIndex;

    public uint funWasmExnRefLocalIndex
    {
        readonly get
        {
            return unchecked(_funWasmExnRefLocalIndex - 1);
        }
        set
        {
            _funWasmExnRefLocalIndex = unchecked(value + 1);
        }
    }

    public bool needsUnwindableFrame;
    public uint startVirtualIP;
    public uint endVirtualIP;

    public void ensureUnwindableFrame(Compiler compiler)
    {
        if (!needsUnwindableFrame)
        {
#if DEBUG
            var currentCompiler = JitTls.Compiler;
            assert(currentCompiler is not null);
            if (currentCompiler.verbose)
            {
                logf($"{(IsFunclet() ? "Funclet" : "Main method")} (index {GetFuncletIdx(compiler)}) needs to be unwindable\n");
            }
#endif
            needsUnwindableFrame = true;
        }
    }
#endif

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
    public UnwindInfo? uwi;

    public readonly UnwindInfo GetUnwindInfo()
    {
        return uwi ?? throw new InvalidOperationException("Unwind information has not been initialized.");
    }

    public UnwindInfo? uwiCold;
#endif

#if FEATURE_CFI_SUPPORT
    public List<CFI_CODE>? cfiCodes;
#endif
}
