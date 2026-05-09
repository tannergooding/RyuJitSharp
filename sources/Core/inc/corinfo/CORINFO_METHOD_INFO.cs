// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct CORINFO_METHOD_INFO
{
    public unsafe CORINFO_METHOD_HANDLE ftn;

    public unsafe CORINFO_MODULE_HANDLE scope;

    public unsafe byte* ILCode;

    public int ILCodeSize;

    public int maxStack;

    public int EHcount;

    public CorInfoOptions options;

    public CorInfoRegionKind regionKind;

    public CORINFO_SIG_INFO args;

    public CORINFO_SIG_INFO locals;
}
