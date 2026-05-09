// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct BBJumpTableList
{
    private BBJumpTable m_bbJumpTable;

    public BBJumpTableList(BBJumpTable bbJumpTable)
    {
        m_bbJumpTable = bbJumpTable;
    }

    public readonly BlockEnumerator GetEnumerator() => new BlockEnumerator(m_bbJumpTable.Succs);
}
