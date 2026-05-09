// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal partial struct FgStack
{
    public const FgSlot SLOT_INVALID = FgSlot.SLOT_INVALID;
    public const FgSlot SLOT_UNKNOWN = FgSlot.SLOT_UNKNOWN;
    public const FgSlot SLOT_CONSTANT = FgSlot.SLOT_CONSTANT;
    public const FgSlot SLOT_ARRAYLEN = FgSlot.SLOT_ARRAYLEN;
    public const FgSlot SLOT_ARGUMENT = FgSlot.SLOT_ARGUMENT;

    public enum FgSlot
    {
        SLOT_INVALID = -1,
        SLOT_UNKNOWN = 0,
        SLOT_CONSTANT = 1,
        SLOT_ARRAYLEN = 2,
        SLOT_ARGUMENT = 3
    }
}
