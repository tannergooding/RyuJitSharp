// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal interface IEpilogListVisitor
{
    nuint Visit(uint codeOffset);
}

public partial class Emitter
{
    protected sealed class EpilogList
    {
        public EpilogList? elNext;
        public emitLocation elLoc;
    }

    internal nuint emitGenEpilogLst<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IEpilogListVisitor
    {
        if (!Configuration.JIT32_GCENCODER)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.EpilogRecorderIsOnlySupportedByTheX86GCEncoder
    );
            return default!;
        }

        nuint size = 0;

        for (var epilog = emitEpilogList; epilog is not null; epilog = epilog.elNext)
        {
            var group = epilog.elLoc.GetIG();
            assert(group is not null);
            assert((group.igFlags & InsGroupFlags.Epilog) != 0);

            // The recorded location, not necessarily the group's start, identifies the epilog.
            size = unchecked(size + visitor.Visit(epilog.elLoc.CodeOffset(this)));
        }

        return size;
    }
}
