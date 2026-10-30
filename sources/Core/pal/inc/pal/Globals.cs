// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    // ******************* HRESULT types ****************************************

    public const int FACILITY_NULL = 0;

    public const int NO_ERROR = 0;

    public const int SEVERITY_ERROR = 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SUCCEEDED(int Status) => (Status >= 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FAILED(int Status) => (Status < 0);

}
