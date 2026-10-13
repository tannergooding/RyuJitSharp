// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // Keep the ARM64-only TLS emitter API behind a target-selected CodeGen partial.
    private unsafe void genEmitTlsDescriptorAddressArm64(emitAttr attr, regNumber reg1, regNumber reg2, nint address
#if DEBUG
        , nuint targetHandle, GenTreeFlags flags
#endif
        )
    {
#if DEBUG
        Emitter.emitIns_Adrp_Ldr_Add(attr, reg1, reg2, address, targetHandle, flags);
#else
        Emitter.emitIns_Adrp_Ldr_Add(attr, reg1, reg2, address);
#endif
    }
}
