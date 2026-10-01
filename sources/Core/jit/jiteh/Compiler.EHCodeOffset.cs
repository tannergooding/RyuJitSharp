// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public insGroup ehEmitCookie(BasicBlock block)
    {
        noway_assert(block is not null);
        var cookie = block.bbEmitCookie;
        noway_assert(cookie is not null);

        return cookie;
    }

    public uint ehCodeOffset(BasicBlock block)
    {
        var emitter = codeGen?.Emitter
            ?? throw new FatalJitException(CORJIT_SKIPPED, "EH code offset requires an emitter.");
        return emitter.emitCodeOffset(ehEmitCookie(block), 0);
    }
}
