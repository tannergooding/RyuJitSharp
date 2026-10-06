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
        var current = emitIGlist;
        insGroup? previous = null;

        while (current is not null)
        {
            assert(_compiler is not null);
            ref var func = ref _compiler.funGetFunc(current.igFuncIdx);

            if ((previous is null) || (previous.igFuncIdx != current.igFuncIdx))
            {
                func.startLoc = new emitLocation(current);
            }

            var next = current.igNext;
            if ((next is null) || (next.igFuncIdx != current.igFuncIdx))
            {
                func.endLoc = new emitLocation(current, current.igInsCnt);
            }

            previous = current;
            current = next;
        }
    }
}
#endif
