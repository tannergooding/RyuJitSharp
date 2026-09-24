// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void MakeSrcContained(GenTree parentNode, GenTree childNode)
    {
        assert(!parentNode.Oper.IsLeaf);
#if DEBUG
        assert(childNode.CanBeContained);
#endif

        childNode.IsContained = true;
        assert(childNode.IsContained);

#if DEBUG
        if (IsContainableMemoryOp(childNode))
        {
            var isSafeToContainMem = IsSafeToContainMem(parentNode, childNode);
            if (!isSafeToContainMem)
            {
                JITDUMP($"** Unsafe mem containment of [{childNode.TreeId:D6}] in [{parentNode.TreeId:D6}]\n");
                assert(isSafeToContainMem);
            }
        }
#endif
    }

    private void MakeSrcRegOptional(GenTree parentNode, GenTree childNode)
    {
        assert(!parentNode.Oper.IsLeaf);

        childNode.IsRegOptional = true;
        assert(childNode.IsRegOptional);

#if DEBUG
        var isSafeToMarkRegOptional = IsSafeToMarkRegOptional(parentNode, childNode);
        if (!isSafeToMarkRegOptional)
        {
            JITDUMP($"** Unsafe regOptional of [{childNode.TreeId:D6}] in [{parentNode.TreeId:D6}]\n");
            assert(isSafeToMarkRegOptional);
        }
#endif
    }

    private void TryMakeSrcContainedOrRegOptional(GenTree parentNode, GenTree childNode)
    {
        assert(!parentNode.Oper.IsHWIntrinsic);
        if (IsContainableMemoryOp(childNode) && IsSafeToContainMem(parentNode, childNode))
        {
            MakeSrcContained(parentNode, childNode);
        }
        else if (IsSafeToMarkRegOptional(parentNode, childNode))
        {
            MakeSrcRegOptional(parentNode, childNode);
        }
    }

    private bool CheckImmedAndMakeContained(GenTree parentNode, GenTree childNode)
    {
        assert(!parentNode.Oper.IsLeaf);
        if (IsContainableImmed(parentNode, childNode))
        {
            MakeSrcContained(parentNode, childNode);
            return true;
        }
        return false;
    }

    private bool IsInvariantInRange(GenTree node, GenTree endExclusive, GenTreeFlags ignoreFlagsOnNode = GTF_EMPTY)
        => _scratchSideEffects.IsLirInvariantInRange(CompilerInstance, node, endExclusive, ignoreFlagsOnNode);

    private bool IsInvariantInRange(GenTree node, GenTree endExclusive, GenTree ignoreNode,
        GenTreeFlags ignoreFlagsOnNode = GTF_EMPTY)
        => _scratchSideEffects.IsLirInvariantInRange(CompilerInstance, node, endExclusive, ignoreNode, ignoreFlagsOnNode);

    private bool IsRangeInvariantInRange(GenTree rangeStart, GenTree rangeEnd, GenTree endExclusive, GenTree ignoreNode)
        => _scratchSideEffects.IsLirRangeInvariantInRange(CompilerInstance, rangeStart, rangeEnd, endExclusive, ignoreNode);

    private bool IsSafeToContainMem(GenTree parentNode, GenTree childNode)
        => IsInvariantInRange(childNode, parentNode);

    private bool IsSafeToContainMem(GenTree grandparentNode, GenTree parentNode, GenTree childNode)
        => IsInvariantInRange(childNode, grandparentNode, parentNode);

    private bool IsSafeToMarkRegOptional(GenTree parentNode, GenTree childNode)
    {
        if (childNode.Oper is not GT_LCL_VAR)
        {
            return true;
        }

        ref var descriptor = ref CompilerInstance.lvaGetDesc(childNode.AsLclVarCommon().LclNum);
        return !descriptor.IsAddressExposed;
    }

    public bool IsContainableMemoryOp(GenTree node) => _regAlloc.IsContainableMemoryOp(node);

    public bool IsContainableMemoryOpSize(GenTree parentNode, GenTree childNode)
    {
        if (parentNode.Oper.IsBinary)
        {
            var operatorSize = parentNode.Type.Size;
#if TARGET_XARCH
            if (parentNode.Oper is GT_AND or GT_OR or GT_XOR)
            {
                return childNode.Type.Size >= operatorSize;
            }
#endif
#if TARGET_X86
            if (parentNode.Oper is GT_MUL_LONG)
            {
                return childNode.Type.Size == operatorSize / 2;
            }
#endif
            return childNode.Type.Size == operatorSize;
        }
        return false;
    }

    public bool IsContainableImmed(GenTree parentNode, GenTree childNode)
    {
#if TARGET_XARCH
        return childNode.IsIntCnsFitsInI32 && !childNode.AsIntConCommon().ImmedValNeedsReloc(CompilerInstance);
#else
        throw new System.NotImplementedException("Non-xarch immediate containment is not ported.");
#endif
    }
}
