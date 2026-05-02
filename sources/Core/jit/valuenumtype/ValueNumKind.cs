// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.ValueNumKind;

namespace RyuJitSharp;

// There are two "kinds" of value numbers, which differ in their modeling of the actions of other threads.
// "Liberal" value numbers assume that the other threads change contents of memory locations only at
// synchronization points.  Liberal VNs are appropriate, for example, in identifying CSE opportunities.
// "Conservative" value numbers assume that the contents of memory locations change arbitrarily between
// every two accesses.  Conservative VNs are appropriate, for example, in assertion prop, where an observation
// of a property of the value in some storage location is used to perform an optimization downstream on
// an operation involving the contents of that storage location.  If other threads may modify the storage
// location between the two accesses, the observed property may no longer hold -- and conservative VNs make
// it clear that the values need not be the same.
public enum ValueNumKind
{
    VNK_Liberal,

    VNK_Conservative,
}
