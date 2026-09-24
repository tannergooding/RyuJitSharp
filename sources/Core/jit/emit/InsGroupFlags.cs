// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[Flags]
public enum InsGroupFlags : ushort
{
    None = 0,
    GCVars = 1 << 0,
    ByrefRegs = 1 << 1,
    Prolog = 1 << 2,
    Epilog = 1 << 3,
    FuncletProlog = 1 << 4,
    FuncletEpilog = 1 << 5,
    NoGCInterrupt = 1 << 6,
    UpdatedInstructionSize = 1 << 7,
    Placeholder = 1 << 8,
    Extend = 1 << 9,
    HasAlign = 1 << 10,
    RemovedAlign = 1 << 11,
    HasRemovableJump = 1 << 12,
#if TARGET_ARM64
    HasRemovedInstruction = 1 << 13,
#endif
    OutOfOrderHead = 1 << 14,
}
