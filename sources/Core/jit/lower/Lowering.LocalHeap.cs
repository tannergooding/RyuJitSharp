// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerLclHeap(GenTreeUnOp node)
    {
        assert(node.Oper is GT_LCLHEAP);

#if TARGET_XARCH || TARGET_ARM64
        var sizeNode = node.Op1;
        if (sizeNode.Oper.IsCnsIntOrI)
        {
            var size = sizeNode.AsIntCon().IconValue;
            if (size == 0)
            {
                var zero = new GenTreeIntCon(TYP_I_IMPL, 0, null, node, NodeThreading.LIR);
                zero._vnPair.SetBoth(ValueNumStore.NoVN);
                BlockRange().ReplaceNode(node, zero);
                BlockRange().Remove(sizeNode);
                return zero.Next;
            }

            if (CompilerInstance.info.compInitMem)
            {
                var alignedSize = unchecked((long)(((ulong)size + (STACK_ALIGN - 1)) &
                    ~(ulong)(STACK_ALIGN - 1)));
                if ((size > uint.MaxValue) || (alignedSize > uint.MaxValue))
                {
                    return node.Next;
                }

                if (!BlockRange().TryGetUse(node, out var use))
                {
                    return node.Next;
                }

                sizeNode.AsIntCon().IconValue = (nint)alignedSize;
                var temp = use.ReplaceWithLclVar(CompilerInstance);
                var heapLocal = CompilerInstance.gtNewLclvNode(TYP_I_IMPL, temp);
                var zero = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                var store = new GenTreeBlk(TYP_STRUCT, heapLocal, zero,
                    CompilerInstance.typGetBlkLayout(unchecked((uint)alignedSize)));
                store.Flags |= GTF_IND_UNALIGNED | GTF_ASG | GTF_EXCEPT | GTF_GLOB_REF;
                BlockRange().InsertAfter(use.Def(), heapLocal, zero, store);
            }
        }
#endif

        ContainCheckLclHeap(node);
        return node.Next;
    }

    private void ContainCheckLclHeap(GenTreeUnOp node)
    {
        assert(node.Oper is GT_LCLHEAP);
        var size = node.Op1;
        if (size.Oper.IsCnsIntOrI)
        {
            MakeSrcContained(node, size);
        }
    }

    private void ContainCheckNonLocalJmp(GenTreeUnOp node)
    {
#if TARGET_XARCH
        var address = node.Op1;
        if (IsContainableMemoryOp(address) && IsSafeToContainMem(node, address))
        {
            MakeSrcContained(node, address);
        }
        else if (IsSafeToMarkRegOptional(node, address))
        {
            MakeSrcRegOptional(node, address);
        }
#else
        throw new NotImplementedException("Non-xarch nonlocal jump containment is not ported.");
#endif
    }
}
