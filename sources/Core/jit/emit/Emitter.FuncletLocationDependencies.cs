// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitUpdateFuncletLocations()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm funclet location update is not ported.");
    }
}
#endif
