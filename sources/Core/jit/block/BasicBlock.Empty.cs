// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    public bool isEmpty()
    {
        if (!IsLIR)
        {
            for (var stmt = GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
            {
                if (stmt.RootNode.Oper != GT_NOP)
                {
                    return false;
                }
            }
        }
        else
        {
            foreach (var node in this)
            {
                if (node.Oper != GT_IL_OFFSET)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
