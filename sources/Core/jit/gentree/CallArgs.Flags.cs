// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct CallArgs
{
    private enum Flags : ushort
    {
        None = 0,
        HasThisPointer = 1 << 0,
        HasRetBuffer = 1 << 1,
        IsVarArgs = 1 << 2,
        AbiInformationDetermined = 1 << 3,
        HasAddedFinalArgs = 1 << 4,
        HasRegArgs = 1 << 5,
        HasStackArgs = 1 << 6,
        ArgsComplete = 1 << 7,
        NeedsTemps = 1 << 8,
#if UNIX_X86_ABI
        AlignmentDone = 1 << 9,
#endif
    }
}
