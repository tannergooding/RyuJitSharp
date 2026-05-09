// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public interface IBitSetTraits<TEnv>
    where TEnv : class
{
    static abstract int GetArrSize(TEnv env);

    static abstract int GetEpoch(TEnv env);

    static abstract int GetSize(TEnv env);
}
