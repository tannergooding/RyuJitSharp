// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Check that flow graph updating removed unreachable, unimported, and compactable blocks.</summary>
    public void fgDebugCheckUpdate()
    {
        for (var block = fgFirstBB; block is not null; block = block.Next)
        {
            if ((block.CountOfInEdges == 0) && !block.HasFlag(BBF_DONT_REMOVE))
            {
                noway_assert(false, "Unreachable block not removed!");
            }

            if (block.isEmpty() && !block.HasFlag(BBF_DONT_REMOVE))
            {
                switch (block.Kind)
                {
                    case BBJ_CALLFINALLY:
                    case BBJ_EHFINALLYRET:
                    case BBJ_EHFAULTRET:
                    case BBJ_EHFILTERRET:
                    case BBJ_RETURN:
                    case BBJ_ALWAYS:
                    case BBJ_EHCATCHRET:
                    {
                        // These jump kinds are allowed to have empty tree lists.
                        break;
                    }

                    default:
                    {
                        // Multiple references may have prevented removal.
                        if (block.CountOfInEdges == 0)
                        {
                            noway_assert(false, "Empty block not removed!");
                        }

                        break;
                    }
                }
            }

            if (!block.HasFlag(BBF_IMPORTED))
            {
                if (!block.HasFlag(BBF_INTERNAL))
                {
                    noway_assert(false, "Non IMPORTED block not removed!");
                }
            }

            // A conditional with the same true and false edge should have become BBJ_ALWAYS.
            if ((block.Kind is BBJ_COND) && (block.TrueEdge == block.FalseEdge))
            {
                noway_assert(false, "Unnecessary jump to the next block!");
            }

            if (block.Kind is BBJ_CALLFINALLY)
            {
                assert(block.HasFlag(BBF_RETLESS_CALL) || block.isBBCallFinallyPair);
            }

            if (fgCanCompactBlock(block))
            {
                noway_assert(false, "Found un-compacted blocks!");
            }
        }
    }
}
#endif
