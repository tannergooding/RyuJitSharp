// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>The jit assumes the CORINFO_VARARGS_HANDLE is a pointer to a subclass of this.</summary>
public struct CORINFO_VarArgInfo
{
    /// <summary>Number of bytes the arguments take up.</summary>
    /// <remarks>The CORINFO_VARARGS_HANDLE counts as an arg.</remarks>
    public uint argBytes;
}
