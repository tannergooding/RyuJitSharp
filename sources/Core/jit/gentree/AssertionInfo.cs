// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct AssertionInfo
{
    private ushort _bitfield;

    public readonly bool AssertionHoldsOnFalseEdge => (_bitfield & 1) is not 0;

    public AssertionInfo(AssertionIndex assertionIndex)
    {
        _bitfield = unchecked((ushort)(assertionIndex << 1));
    }

    private AssertionInfo(bool assertionHoldsOnFalseEdge, AssertionIndex assertionIndex)
    {
        _bitfield = unchecked((ushort)(assertionHoldsOnFalseEdge ? 1 : 0));
        _bitfield |= unchecked((ushort)(assertionIndex << 1));
        assert(AssertionIndex == assertionIndex);
    }

    public readonly AssertionIndex AssertionIndex => unchecked((AssertionIndex)(_bitfield >>> 1));

    public readonly bool HasAssertion => (AssertionIndex != NO_ASSERTION_INDEX);

    public static AssertionInfo ForNextEdge(AssertionIndex assertionIndex)
    {
        // Ignore the edge information if there's no assertion
        var isNextEdge = (assertionIndex != NO_ASSERTION_INDEX);
        return new AssertionInfo(isNextEdge, assertionIndex);
    }

    public void Clear()
    {
        _bitfield = 0;
    }
}
