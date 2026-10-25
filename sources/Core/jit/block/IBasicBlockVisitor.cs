// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public interface IBasicBlockVisitor<TSelf>
    where TSelf : struct, IBasicBlockVisitor<TSelf>, allows ref struct
{
    BasicBlockVisit Visit(BasicBlock block);
}

internal readonly struct BasicBlockDelegateVisitor : IBasicBlockVisitor<BasicBlockDelegateVisitor>
{
    private readonly Func<BasicBlock, BasicBlockVisit> _visitor;

    public BasicBlockDelegateVisitor(Func<BasicBlock, BasicBlockVisit> visitor)
    {
        _visitor = visitor;
    }

    public BasicBlockVisit Visit(BasicBlock block) => _visitor(block);
}
