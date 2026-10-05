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
#elif TARGET_ARM64
        if (!varTypeIsFloating(parentNode.Type))
        {
            if (parentNode.Oper.IsCompare && childNode.IsFloatPositiveZero)
            {
                assert(parentNode.Oper is not (GT_TEST_EQ or GT_TEST_NE));
                return true;
            }

            if (!childNode.Oper.IsCnsIntOrI)
            {
                return false;
            }

            var constant = childNode.AsIntCon();
            if (constant.ImmedValNeedsReloc(CompilerInstance))
            {
                // Windows NativeAOT emits the section-relative immediate with its consuming ADD.
                return CompilerInstance.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && TargetOS.IsWindows &&
                    constant.IsIconHandle(GTF_ICON_SECREL_OFFSET);
            }

            var immVal = constant.IconValue;
            var size = EA_SIZE(childNode.Type.EmitActualSize);
            switch (parentNode.Oper)
            {
                case GT_ADD:
                case GT_SUB:
                {
                    return Emitter.emitIns_valid_imm_for_add(immVal, size);
                }

                case GT_CMPXCHG:
                case GT_LOCKADD:
                case GT_XORR:
                case GT_XAND:
                case GT_XADD:
                {
                    return !CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_Atomics) &&
                        Emitter.emitIns_valid_imm_for_add(immVal, size);
                }

                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_LE:
                case GT_GE:
                case GT_GT:
                case GT_CMP:
                case GT_BOUNDS_CHECK:
                {
                    return Emitter.emitIns_valid_imm_for_cmp(immVal, size);
                }

                case GT_AND:
                case GT_OR:
                case GT_XOR:
                case GT_TEST_EQ:
                case GT_TEST_NE:
                {
                    return Emitter.emitIns_valid_imm_for_alu(immVal, size);
                }

                case GT_JCMP:
                {
                    assert(immVal == 0);
                    return true;
                }

                case GT_JTEST:
                {
                    assert(System.Numerics.BitOperations.IsPow2(immVal));
                    return true;
                }

                case GT_STORE_LCL_FLD:
                case GT_STORE_LCL_VAR:
                {
                    if (immVal == 0)
                    {
                        return true;
                    }
                    break;
                }
            }
        }

        return false;
#elif TARGET_RISCV64
        if (!varTypeIsFloating(parentNode.Type))
        {
            if (!childNode.Oper.IsCnsIntOrI)
            {
                return false;
            }

            var constant = childNode.AsIntCon();
            if (constant.ImmedValNeedsReloc(CompilerInstance))
            {
                return false;
            }

            var immVal = constant.IconValue;
            switch (parentNode.Oper)
            {
                case GT_LT:
                case GT_GE:
                case GT_ADD:
                case GT_AND:
                case GT_OR:
                case GT_XOR:
                {
                    return (immVal >= -2048) && (immVal <= 2047);
                }

                case GT_EQ:
                case GT_NE:
                case GT_GT:
                case GT_LE:
                case GT_JCMP:
                case GT_CMPXCHG:
                case GT_XORR:
                case GT_XAND:
                case GT_XADD:
                case GT_XCHG:
                case GT_STORE_LCL_FLD:
                case GT_STORE_LCL_VAR:
                {
                    return immVal == 0;
                }
            }
        }

        return false;
#elif TARGET_LOONGARCH64
        if (!varTypeIsFloating(parentNode.Type))
        {
            if (!childNode.Oper.IsCnsIntOrI)
            {
                return false;
            }

            var constant = childNode.AsIntCon();
            if (constant.ImmedValNeedsReloc(CompilerInstance))
            {
                return false;
            }

            var immVal = constant.IconValue;
            switch (parentNode.Oper)
            {
                case GT_CMPXCHG:
                case GT_LOCKADD:
                case GT_XADD:
                {
                    NYI_LOONGARCH64("GT_CMPXCHG,GT_LOCKADD,GT_XADD");
                    break;
                }

                case GT_ADD:
                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_LE:
                case GT_GE:
                case GT_GT:
                case GT_BOUNDS_CHECK:
                {
                    return Emitter.isValidSimm12(immVal);
                }

                case GT_AND:
                case GT_OR:
                case GT_XOR:
                {
                    return Emitter.isValidUimm12(immVal);
                }

                case GT_JCMP:
                {
                    assert(immVal == 0);
                    return true;
                }

                case GT_STORE_LCL_FLD:
                case GT_STORE_LCL_VAR:
                {
                    if (immVal == 0)
                    {
                        return true;
                    }
                    break;
                }
            }
        }

        return false;
#elif TARGET_WASM
        return false;
#else
        throw new System.NotImplementedException("Immediate containment is not ported for this target.");
#endif
    }
}
