// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>DebugInfo types shared by JIT-EE interface and EE-Debugger interface.</summary>
public partial struct ICorDebugInfo
{
    /// <summary>Value for the <see cref="CORINFO_VARARGS_HANDLE" /> varNumber.</summary>
    public const int VARARGS_HND_ILNUM = -1;

    /// <summary>Pointer to the return-buffer.</summary>
    public const int RETBUF_ILNUM = -2;

    /// <summary>ParamTypeArg for <c>CORINFO_GENERICS_CTXT_FROM_PARAMTYPEARG</c>.</summary>
    public const int TYPECTXT_ILNUM = -3;

    /// <summary>Unknown variable.</summary>
    public const int UNKNOWN_ILNUM = -4;

    // Sentinel value.
    // This should be set to the largest magnitude value in the enum so that the compression routines know the enum's range.
    public const int MAX_ILNUM = -4;
}
