// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if CPU_LOAD_STORE_ARCH || TARGET_WASM
namespace RyuJitSharp;

public partial class Emitter
{
#if CPU_LOAD_STORE_ARCH
    public bool emitInsIsStore(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target instruction store classification is not implemented.");
    }
#endif

#if TARGET_WASM
    public void emitIns_S_R(instruction ins, emitAttr attr, regNumber reg, int varNum, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "WebAssembly local-stack store recording is not implemented.");
    }
#endif
}
#endif
