// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly struct ILLocation : IEquatable<ILLocation>
{
    // Complementing the offset makes default(ILLocation) invalid, like the native constructor.
    private readonly IL_OFFSET _encodedOffset;
    private readonly ICorDebugInfo.SourceTypes _sourceTypes;

    public ILLocation()
    {
    }

    public ILLocation(IL_OFFSET offset, ICorDebugInfo.SourceTypes sourceTypes)
    {
        _encodedOffset = ~offset;
        _sourceTypes = sourceTypes;
    }

    public bool IsAsync
        => (_sourceTypes & ICorDebugInfo.ASYNC) != 0;

    public bool IsCallInstruction
        => (_sourceTypes & ICorDebugInfo.CALL_INSTRUCTION) != 0;

    public bool IsValid => _encodedOffset != 0;

    public IL_OFFSET Offset => ~_encodedOffset;

    public ICorDebugInfo.SourceTypes SourceTypes => _sourceTypes;

    public bool Equals(ILLocation other)
    {
        return (_encodedOffset == other._encodedOffset) && (_sourceTypes == other._sourceTypes);
    }

    public override bool Equals(object? obj)
    {
        return obj is ILLocation other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_encodedOffset, _sourceTypes);
    }

    public static bool operator ==(ILLocation left, ILLocation right) => left.Equals(right);

    public static bool operator !=(ILLocation left, ILLocation right) => !left.Equals(right);

#if DEBUG
    public void Dump()
    {
        if (!IsValid)
        {
            jitprintf("???");
        }
        else
        {
            jitprintf($"0x{Offset:X3}[");
            jitprintf(((_sourceTypes & ICorDebugInfo.STACK_EMPTY) != 0) ? "E" : "-");
            jitprintf(((_sourceTypes & ICorDebugInfo.CALL_INSTRUCTION) != 0) ? "C" : "-");
            jitprintf(((_sourceTypes & ICorDebugInfo.ASYNC) != 0) ? "A" : "-");
            jitprintf("]");
        }
    }
#endif
}
