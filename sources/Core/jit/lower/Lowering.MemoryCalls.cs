// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool LowerCallMemset(GenTreeCall call, out GenTreeBlk? next)
    {
        var compiler = CompilerInstance;
        assert(call.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_Fill) ||
            call.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_ClearWithoutReferences) ||
            call.IsHelperCall(CORINFO_HELP_MEMSET));
        next = null;

#if DEBUG
        JITDUMP($"Considering Memset-like call [{call.TreeId:D6}] for unrolling.. ");
#endif
        if (compiler.info.compHasNextCallRetAddr)
        {
            JITDUMP("compHasNextCallRetAddr=true so we won't be able to remove the call - bail out.\n");
            return false;
        }

        var destination = call.Args.GetUserArgByIndex(0);
        assert(destination is not null);
        var destinationNode = destination.Node;
        GenTree length;
        GenTree value;
        int lengthScale;

        if (call.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_Fill))
        {
            assert(call.Args.CountUserArgs() == 3);
            var lengthArg = call.Args.GetUserArgByIndex(1);
            var valueArg = call.Args.GetUserArgByIndex(2);
            assert((lengthArg is not null) && (valueArg is not null));
            length = lengthArg.Node;
            value = valueArg.Node;
            lengthScale = valueArg.SignatureType.Size;
        }
        else if (call.IsHelperCall(CORINFO_HELP_MEMSET))
        {
            assert(call.Args.CountUserArgs() == 3);
            var lengthArg = call.Args.GetUserArgByIndex(2);
            var valueArg = call.Args.GetUserArgByIndex(1);
            assert((lengthArg is not null) && (valueArg is not null));
            length = lengthArg.Node;
            value = valueArg.Node;
            lengthScale = 1;
        }
        else
        {
            assert(call.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_ClearWithoutReferences));
            assert(call.Args.CountUserArgs() == 2);
            var lengthArg = call.Args.GetUserArgByIndex(1);
            assert(lengthArg is not null);
            length = lengthArg.Node;
            value = compiler.gtNewZeroConNode(TYP_INT);
            lengthScale = 1;
        }

        if (!length.Oper.IsIntegralConst)
        {
            JITDUMP("Length is not a constant - bail out.\n");
            return false;
        }
        if (!value.Oper.IsCnsIntOrI || (value.Type is not TYP_INT))
        {
            JITDUMP("Value is not a constant - bail out.\n");
            return false;
        }
        if (!value.IsIntegralConst(0) && (lengthScale != 1))
        {
            JITDUMP("Value is not unroll-friendly - bail out.\n");
            return false;
        }

        var lengthElements = (long)length.AsIntConCommon().IconValue;
        if (!CheckedOps.TryMul(lengthElements, (long)lengthScale, out var byteLength))
        {
            JITDUMP("lenCns * lengthScale overflows - bail out.\n");
            return false;
        }
        if ((byteLength <= 0) || (byteLength > compiler.GetUnrollThreshold(Compiler.UnrollKind.Memset)))
        {
            JITDUMP("Size is either 0 or too big to unroll - bail out.\n");
            return false;
        }

        JITDUMP("Accepted for unrolling!\nOld tree:\n");
        DISPTREERANGE(BlockRange(), call);
        if (!value.IsIntegralConst(0))
        {
            var initializedValue = value;
            value = compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT, initializedValue);
            BlockRange().InsertAfter(initializedValue, value);
        }

        var store = compiler.gtNewStoreBlkNode(destinationNode, value,
            compiler.typGetBlkLayout((int)byteLength), GTF_IND_UNALIGNED);
        store._kind = GenTreeBlk.BlkOpKind.BlkOpKindUnroll;

        BlockRange().InsertBefore(call, store);
        if (call.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_ClearWithoutReferences))
        {
            BlockRange().InsertBefore(store, value);
        }

        BlockRange().Remove(call, markOperandsUnused: true);
        destinationNode.IsUnusedValue = false;
        value.IsUnusedValue = false;
        if (value.Oper is GT_INIT_VAL)
        {
            value.AsUnOp().Op1.IsUnusedValue = false;
        }

        JITDUMP("\nNew tree:\n");
        DISPTREERANGE(BlockRange(), store);
        next = store;
        return true;
    }

    private unsafe bool LowerCallMemmove(GenTreeCall call, out GenTree? next)
    {
        var compiler = CompilerInstance;
        next = null;
#if DEBUG
        JITDUMP($"Considering Memmove [{call.TreeId:D6}] for unrolling.. ");
#endif
        assert(call.IsHelperCall(CORINFO_HELP_MEMCPY) ||
            (compiler.lookupNamedIntrinsic(call._callMethHnd) is NI_System_SpanHelpers_Memmove));
        assert(call.Args.CountUserArgs() == 3);

        if (compiler.info.compHasNextCallRetAddr)
        {
            JITDUMP("compHasNextCallRetAddr=true so we won't be able to remove the call - bail out.\n");
            return false;
        }

        var lengthArg = call.Args.GetUserArgByIndex(2);
        assert(lengthArg is not null);
        var length = lengthArg.Node;
        if (length.Oper.IsIntegralConst)
        {
            var size = (long)length.AsIntConCommon().IconValue;
            JITDUMP($"Size={size}.. ");
            if ((size > 0) && (size <= compiler.GetUnrollThreshold(Compiler.UnrollKind.Memmove)))
            {
                JITDUMP("Accepted for unrolling!\nOld tree:\n");
                DISPTREE(call);

                var destinationArg = call.Args.GetUserArgByIndex(0);
                var sourceArg = call.Args.GetUserArgByIndex(1);
                assert((destinationArg is not null) && (sourceArg is not null));
                var destination = destinationArg.Node;
                var source = sourceArg.Node;
                assert(!destination.IsContained && !source.IsContained);

                var sourceBlock = compiler.gtNewIndir(TYP_STRUCT, source);
                sourceBlock.IsContained = true;
                var store = new GenTreeBlk(TYP_STRUCT, destination, sourceBlock,
                    compiler.typGetBlkLayout((int)size)) {
                    _kind = GenTreeBlk.BlkOpKind.BlkOpKindUnrollMemmove,
                };
                store.Flags |= GTF_IND_UNALIGNED | GTF_ASG | GTF_EXCEPT | GTF_GLOB_REF;

                BlockRange().InsertBefore(call, sourceBlock);
                BlockRange().InsertBefore(call, store);
                BlockRange().Remove(length);
                BlockRange().Remove(call);

                foreach (var arg in call.Args.Args)
                {
                    if (arg.IsArgAddedLate)
                    {
                        arg.Node.IsUnusedValue = true;
                    }
                }

                JITDUMP("\nNew tree:\n");
                DISPTREE(store);
                next = store.Next;
                return true;
            }
            JITDUMP("Size is either 0 or too big to unroll.\n");
        }
        else
        {
            JITDUMP("size is not a constant.\n");
        }
        return false;
    }
}
