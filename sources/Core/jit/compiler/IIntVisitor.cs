// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

internal interface IIntActionVisitor<TSelf>
    where TSelf : struct, IIntActionVisitor<TSelf>
{
    void Visit(int value);
}

internal interface IIntPredicateVisitor<TSelf>
    where TSelf : struct, IIntPredicateVisitor<TSelf>
{
    bool Visit(int value);
}

internal readonly struct IntActionDelegateVisitor(Action<int> action) : IIntActionVisitor<IntActionDelegateVisitor>
{
    public void Visit(int value) => action(value);
}

internal readonly struct IntPredicateDelegateVisitor(Func<int, bool> predicate)
    : IIntPredicateVisitor<IntPredicateDelegateVisitor>
{
    public bool Visit(int value) => predicate(value);
}
