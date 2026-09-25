// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public insGroup ehEmitCookie(BasicBlock block)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "EH code cookies require Windows AMD64.");
#else
        noway_assert(block is not null);
        return block.bbEmitCookie
            ?? throw new FatalJitException(CORJIT_SKIPPED, "EH block has no emitter code cookie.");
#endif
    }

    public uint ehCodeOffset(BasicBlock block)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "EH code offsets require Windows AMD64.");
#else
        var emitter = codeGen?.Emitter
            ?? throw new FatalJitException(CORJIT_SKIPPED, "EH code offset requires an emitter.");
        return emitter.emitCodeOffset(ehEmitCookie(block), 0);
#endif
    }
}
