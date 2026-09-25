// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool IsCFGCallArgInvariantInRange(GenTree node, GenTree endExclusive)
    {
        assert(node.Precedes(endExclusive));
        if (node.IsInvariant)
        {
            return true;
        }
        if (!node.IsValue)
        {
            return false;
        }
        if (node.Oper.IsLocal)
        {
            ref var descriptor = ref CompilerInstance.lvaGetDesc(node.AsLclVarCommon().LclNum);
            // Non-address-exposed locals are consumed at their users, so they need no range scan.
            return !descriptor.IsAddressExposed;
        }

        return false;
    }

    private void MovePutArgUpToCall(GenTreeCall call, GenTree node)
    {
#if HAS_FIXED_REGISTER_SET
        assert(node.Oper.IsPutArg || (node.Oper is GT_FIELD_LIST));
#endif
        if (node.Oper is GT_FIELD_LIST)
        {
            JITDUMP("Node is a GT_FIELD_LIST; moving all operands\n");
            foreach (var operand in node.AsFieldList().Uses)
            {
                assert(operand.Node.Oper.IsPutArg);
                MovePutArgUpToCall(call, operand.Node);
            }
        }
        else if (node.Oper.IsPutArg)
        {
            var operand = node.AsUnOp().Op1;
            JITDUMP("Checking if we can move operand of GT_PUTARG_* node:\n");
            DISPTREE(operand);
            if (((operand.Flags & GTF_ALL_EFFECT) == 0) && IsCFGCallArgInvariantInRange(operand, call))
            {
                JITDUMP("...yes, moving to after validator call\n");
                BlockRange().Remove(operand);
                BlockRange().InsertBefore(call, operand);
            }
            else
            {
                JITDUMP("...no, operand has side effects or is not invariant\n");
            }
        }
        else
        {
#if HAS_FIXED_REGISTER_SET
            unreached();
#endif
            return;
        }

        JITDUMP("Moving\n");
        DISPTREE(node);
        JITDUMP("\n");
        BlockRange().Remove(node);
        BlockRange().InsertBefore(call, node);
    }

    private void MovePutArgNodesUpToCall(GenTreeCall call)
    {
        foreach (var arg in call.Args.EarlyArgs)
        {
            var node = arg.EarlyNode;
            assert(node is not null);
            MovePutArgUpToCall(call, node);
        }
        foreach (var arg in call.Args.LateArgs)
        {
            var node = arg.LateNode;
            assert(node is not null);
            MovePutArgUpToCall(call, node);
        }
    }
}
