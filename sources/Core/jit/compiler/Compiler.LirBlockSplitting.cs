// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public BasicBlock fgSplitBlockAfterNode(BasicBlock current, GenTree? node)
    {
        assert(current.IsLIR);
        var newBlock = fgSplitBlockAtEnd(current);
        if (node is not null)
        {
            if (node != current.LastNode)
            {
                var first = node.Next;
                var last = current.LastNode;
                assert(first is not null && last is not null);
                var moved = current.RemoveAndGetRange(first, last);
                newBlock.InsertAtBeginning(moved);
            }

            assert(newBlock.bbCodeOffs == BAD_IL_OFFSET);
            assert(newBlock.bbCodeOffsEnd == BAD_IL_OFFSET);
            newBlock.bbCodeOffsEnd = current.bbCodeOffsEnd;

            var splitOffset = BAD_IL_OFFSET;
            for (var cursor = current.LastNode; cursor is not null; cursor = cursor.Prev)
            {
                if (cursor.Oper is GT_IL_OFFSET)
                {
                    var debugInfo = cursor.AsILOffset().StmtDebugInfo.GetRoot();
                    if (debugInfo.IsValid)
                    {
                        splitOffset = debugInfo.Location.Offset;
                        break;
                    }
                }
            }

            if (splitOffset == BAD_IL_OFFSET)
            {
                for (var cursor = newBlock.FirstNode; cursor is not null; cursor = cursor.Next)
                {
                    if (cursor.Oper is GT_IL_OFFSET)
                    {
                        var debugInfo = cursor.AsILOffset().StmtDebugInfo.GetRoot();
                        if (debugInfo.IsValid)
                        {
                            splitOffset = debugInfo.Location.Offset;
                            break;
                        }
                    }
                }
            }

            if (splitOffset == BAD_IL_OFFSET)
            {
                splitOffset = current.bbCodeOffsEnd == BAD_IL_OFFSET ? current.bbCodeOffs : current.bbCodeOffsEnd;
            }
            current.bbCodeOffsEnd = splitOffset;
            newBlock.bbCodeOffs = splitOffset;
        }
        else
        {
            assert(current.IsEmpty);
        }

        return newBlock;
    }
}
