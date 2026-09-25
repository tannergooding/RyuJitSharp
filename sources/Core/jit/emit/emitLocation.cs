// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct emitLocation
{
    private insGroup? ig;
    private uint codePos;

    public emitLocation(insGroup? group)
    {
        ig = group;
        codePos = 0;
    }

    public void Init()
    {
        this = default;
    }

    public readonly insGroup? GetIG()
    {
        return ig;
    }

    public readonly bool IsOffsetZero()
    {
        return codePos == 0;
    }

    public readonly bool Valid()
    {
        return ig is not null;
    }
}
