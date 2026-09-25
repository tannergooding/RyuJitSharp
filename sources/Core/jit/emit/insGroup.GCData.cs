// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class insGroup
{
    internal VARSET_TP SavedGcVars = [];
    internal uint SavedByrefRegs;

    public insPlaceholderGroupData? igPhData;

    public VARSET_TP igGCvars()
    {
        assert((igFlags & InsGroupFlags.GCVars) != 0);
        return SavedGcVars;
    }

    public uint igByrefRegs()
    {
        assert((igFlags & InsGroupFlags.ByrefRegs) != 0);
        return SavedByrefRegs;
    }
}
