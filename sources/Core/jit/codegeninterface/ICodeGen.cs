// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public interface ICodeGen
{
#if LATE_DISASM
    Disassembler Disassembler { get; }
#endif

    Emitter Emitter { get; }

    bool IsFramePointerRequired { get; set; }

    bool IsFrameRequired { get; set; }

    bool ShouldAlignLoops { get; set; }
}
