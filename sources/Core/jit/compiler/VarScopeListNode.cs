// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class VarScopeListNode
{
    public int DataIndex;
    public VarScopeListNode? Next;

    public VarScopeListNode(int dataIndex)
    {
        DataIndex = dataIndex;
    }
}
