// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct CORINFO_SIG_INST
{
    public int classInstCount;

    /// <summary>Instantiation for class type variables in signature.</summary>
    /// <remarks>Representative, not exact.</remarks>
    public unsafe CORINFO_CLASS_HANDLE* classInst;

    public int methInstCount;

    /// <summary>Instantiation for method type variables in signature.</summary>
    /// <remarks>Representative, not exact.</remarks>
    public unsafe CORINFO_CLASS_HANDLE* methInst;
}
