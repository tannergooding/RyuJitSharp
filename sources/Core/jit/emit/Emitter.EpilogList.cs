// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if JIT32_GCENCODER
namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe delegate nuint emitEpilogCallbackType(void* context, uint codeOffset);

    protected sealed class EpilogList
    {
        public EpilogList? elNext;
        public emitLocation elLoc;
    }

    public unsafe nuint emitGenEpilogLst(emitEpilogCallbackType callback, void* context)
    {
        nuint size = 0;

        for (var epilog = emitEpilogList; epilog is not null; epilog = epilog.elNext)
        {
            var group = epilog.elLoc.GetIG();
            assert(group is not null);
            assert((group.igFlags & InsGroupFlags.Epilog) != 0);

            // The recorded location, not necessarily the group's start, identifies the epilog.
            size = unchecked(size + callback(context, epilog.elLoc.CodeOffset(this)));
        }

        return size;
    }
}
#endif
