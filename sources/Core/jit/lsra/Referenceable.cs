// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public class Referenceable
{
    public Referenceable(RegisterType registerType)
    {
        this.registerType = registerType;
    }

    public RefPosition? firstRefPosition;
    public RefPosition? recentRefPosition;
    public RefPosition? lastRefPosition;

    public RegisterType registerType;

    public RefPosition? getNextRefPosition()
    {
        if (recentRefPosition is null)
        {
            return firstRefPosition;
        }

        return recentRefPosition.nextRefPosition;
    }

    public LsraLocation getNextRefLocation()
    {
        var nextRefPosition = getNextRefPosition();
        if (nextRefPosition is null)
        {
            return MaxLocation;
        }

        return nextRefPosition.nodeLocation;
    }
}
