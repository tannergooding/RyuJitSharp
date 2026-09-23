// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe struct CORINFO_WASM_WELLKNOWN_GLOBALS
{
    public CORINFO_WASM_GLOBAL_SYMBOL_HANDLE stackPointer;
    public CORINFO_WASM_GLOBAL_SYMBOL_HANDLE imageBase;
    public CORINFO_WASM_GLOBAL_SYMBOL_HANDLE tableBase;
    public CORINFO_WASM_GLOBAL_SYMBOL_HANDLE asyncContinuation;
}
