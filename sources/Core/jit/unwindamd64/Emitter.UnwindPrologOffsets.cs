// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public uint emitGetCurrentCodeOffsetFrom(insGroup? group)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog unwind offsets require Windows AMD64.");
#else
        group ??= emitGetFirstPrologIG();
        noway_assert((group.igFlags & InsGroupFlags.OutOfOrderHead) != 0);

        var kind = group.igFlags & (InsGroupFlags.Prolog | InsGroupFlags.FuncletProlog);
        var offset = 0u;
        var current = (insGroup?)group;

        while (current != emitCurIG)
        {
            noway_assert(current is not null);
            offset = checked(offset + current.igSize);
            current = current.igNext;
        }

        noway_assert(current is not null && (current.igFlags & kind) == kind);
        noway_assert(emitCurIGsize >= 0);

        return checked(offset + (uint)emitCurIGsize);
#endif
    }
}
