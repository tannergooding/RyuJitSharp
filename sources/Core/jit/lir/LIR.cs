// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class LIR
{
    /// <summary>Moves a range before a conditional, switch, or return terminator; otherwise appends it.</summary>
    public static void InsertBeforeTerminator(BasicBlock block, Range range)
    {
        var insertionPoint = null as GenTree;
        if (block.Kind is BBJ_COND or BBJ_SWITCH or BBJ_RETURN)
        {
            insertionPoint = block.LastNode;
            assert(insertionPoint is not null);

#if DEBUG
            switch (block.Kind)
            {
                case BBJ_COND:
                {
                    assert(insertionPoint.Oper.IsConditionalJump);
                    break;
                }

                case BBJ_SWITCH:
                {
                    assert(insertionPoint.Oper is GT_SWITCH or GT_SWITCH_TABLE);
                    break;
                }

                case BBJ_RETURN:
                {
                    assert(insertionPoint.Oper is GT_RETURN or GT_SWIFT_ERROR_RET or GT_JMP or GT_CALL);
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
#endif
        }

        block.InsertBefore(insertionPoint, range);
    }

    public static Range SeqTree(Compiler compiler, GenTree tree)
    {
        _ = compiler.gtSetEvalOrder(tree);
        return new Range(compiler.fgSetTreeSeq(tree, isLIR: true), tree);
    }
}
