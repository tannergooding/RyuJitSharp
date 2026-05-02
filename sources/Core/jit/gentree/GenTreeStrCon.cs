// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeStrCon : GenTree
{
    private readonly uint _sconCpx;
    private readonly unsafe CORINFO_MODULE_HANDLE _scpHnd;

    // Because this node can come from an inlined method we need to have the scope handle, since it will become a helper call.
    public unsafe GenTreeStrCon(uint sconCpx, CORINFO_MODULE_HANDLE scpHnd)
        : base(GT_CNS_STR, TYP_REF)
    {
        _sconCpx = sconCpx;
        _scpHnd = scpHnd;
    }

    // Returns true if this GT_CNS_STR was imported for String.Empty field
    public unsafe bool IsStringEmptyField => (_sconCpx is EMPTY_STRING_SCON)
                                          && (_scpHnd is null);

    public uint SconCpx => _sconCpx;

    public unsafe CORINFO_MODULE_HANDLE ScpHnd => _scpHnd;
}
