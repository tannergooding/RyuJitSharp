// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Result of calling <c>embedGenericHandle</c>.</summary>
public unsafe struct CORINFO_GENERICHANDLE_RESULT
{
    public CORINFO_LOOKUP lookup;

    /// <summary>Guaranteed to be either NULL or a handle that is usable during compile time.</summary>
    /// <remarks>This must not be embedded in the code because it might not be valid at run-time.</remarks>
    public CORINFO_GENERIC_HANDLE compileTimeHandle;

    /// <summary>Type of the result.</summary>
    public CorInfoGenericHandleType handleType;
}
