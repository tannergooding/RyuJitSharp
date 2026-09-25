// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public struct emitLocation : IEquatable<emitLocation>
{
    private insGroup? ig;
    private uint codePos;

    public emitLocation(insGroup? group)
    {
        ig = group;
        codePos = 0;
    }

    public emitLocation(insGroup? group, uint position)
    {
        SetLocation(group, position);
    }

    public emitLocation(Emitter emitter)
    {
        CaptureLocation(emitter);
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

    public void CaptureLocation(Emitter emitter)
    {
        ig = emitter.emitCurIG;
        codePos = emitter.emitCurOffset();
        assert(Valid());
    }

    public void SetLocation(insGroup? group, uint position)
    {
        ig = group;
        codePos = position;
        assert(Valid());
    }

    public void SetLocation(emitLocation location)
    {
        ig = location.ig;
        codePos = location.codePos;
        assert(Valid());
    }

    public readonly bool IsCurrentLocation(Emitter emitter)
    {
        assert(Valid());
        return (ig == emitter.emitCurIG) && (codePos == emitter.emitCurOffset());
    }

    public readonly uint CodeOffset(Emitter emitter)
    {
        assert(ig is not null);
        return emitter.emitCodeOffset(ig, codePos);
    }

    public readonly int GetInsNum()
    {
        return (int)emitGetInsNumFromCodePos(codePos);
    }

    public readonly int GetInsOffset()
    {
        return (int)emitGetInsOfsFromCodePos(codePos);
    }

    public readonly bool IsPreviousInsNum(Emitter emitter)
    {
        assert(ig is not null);

        if (ig == emitter.emitCurIG)
        {
            return emitGetInsNumFromCodePos(codePos)
                == unchecked(emitGetInsNumFromCodePos(emitter.emitCurOffset()) - 1);
        }

        if (ig.igNext == emitter.emitCurIG)
        {
            return (emitGetInsNumFromCodePos(codePos) == ig.igInsCnt)
                && (emitGetInsNumFromCodePos(emitter.emitCurOffset()) == 1);
        }

        return false;
    }

#if DEBUG
    public readonly void Print(int compMethodID)
    {
        assert(ig is not null);
        jitprintf($"(G_M{unchecked((uint)compMethodID):D3}_IG{ig.GetDisplayId():D2},ins#{GetInsNum()},ofs#{GetInsOffset()})");
    }
#endif

    public readonly bool Equals(emitLocation other)
    {
        return (ig == other.ig) && (codePos == other.codePos);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is emitLocation other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(ig, codePos);
    }

    public static bool operator ==(emitLocation left, emitLocation right) => left.Equals(right);

    public static bool operator !=(emitLocation left, emitLocation right) => !left.Equals(right);
}
