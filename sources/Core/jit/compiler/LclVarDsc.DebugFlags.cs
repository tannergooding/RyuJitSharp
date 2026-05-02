// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public partial struct LclVarDsc
{
    private enum DebugFlags : ushort
    {
        None = 0,
        TrackedWithoutIndex = 1 << 0,
        ClassInfoUpdated = 1 << 1,
        IsHoist = 1 << 2,
        IsMultiDefCse = 1 << 3,
        KeepType = 1 << 4,
        NoLclFldStress = 1 << 5,
        DefinedViaAddress = 1 << 6,
        UnusedStruct = 1 << 7,
        UndoneStructPromotion = 1 << 8,
    }
}
#endif
