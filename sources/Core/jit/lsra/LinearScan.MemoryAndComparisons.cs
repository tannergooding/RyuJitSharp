// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildIndir(GenTreeIndir indirection)
    {
#if TARGET_XARCH
        assert(indirection.Type is not TYP_STRUCT);
        var useCandidates = SRBM_NONE;
#if TARGET_AMD64
        if (varTypeUsesIntReg(indirection.Addr.Type))
        {
            useCandidates = forceLowGprForApxIfNeeded(indirection.Addr, useCandidates, _evexIsSupported);
        }
#endif

        var srcCount = buildIndirUses(indirection, useCandidates);
        if (indirection.Oper is GT_STOREIND)
        {
            var source = indirection.Data;
            var store = indirection.AsStoreInd();
            if (store.IsRMWMemoryOp)
            {
                // Contained shifts and rotates have not yet had their fixed-register requirements built.
                assert(source.IsContained && source.Oper.IsRmwMemOp);
                if (source.Oper.IsShiftOrRotate)
                {
                    srcCount += buildShiftRotate(source);
                }
                else
                {
                    var sourceCandidates = SRBM_NONE;
#if TARGET_X86
                    GenTree? nonMemorySource = null;
                    GenTreeIndir? otherIndirection = null;

                    if (store.IsRMWDstOp1)
                    {
                        otherIndirection = source.AsUnOp().Op1.AsIndir();
                        if (source.Oper.IsBinary)
                        {
                            nonMemorySource = source.AsOp().Op2;
                        }
                    }
                    else if (store.IsRMWDstOp2)
                    {
                        otherIndirection = source.AsOp().Op2.AsIndir();
                        nonMemorySource = source.AsOp().Op1;
                    }

                    if ((nonMemorySource is not null) && !nonMemorySource.IsContained && varTypeIsByte(indirection.Type))
                    {
                        sourceCandidates = _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
                    }

                    if (otherIndirection is not null)
                    {
                        checkAndMoveRMWLastUse(otherIndirection.Base, indirection.Base);
                        checkAndMoveRMWLastUse(otherIndirection.Index, indirection.Index);
                    }
#endif
                    srcCount += buildBinaryUses(source.AsUnOp(), sourceCandidates);
                }
            }
            else
            {
#if TARGET_X86
                if (varTypeIsByte(indirection.Type) && !source.IsContained)
                {
                    _ = buildUse(source, _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet);
                    srcCount++;
                }
                else
#endif
                {
                    srcCount += buildOperandUses(source);
                }
            }
        }

#if FEATURE_SIMD
        buildInternalRegisterUses();
#endif

#if TARGET_X86
        // target.h defines four byte-register candidates on x86: EAX, ECX, EDX, and EBX.
        assert(srcCount <= 4);
#endif

        if (indirection.Oper is not GT_STOREIND)
        {
            _ = buildDef(indirection, SRBM_NONE);
        }
        return srcCount;
#elif TARGET_ARM64
        assert(indirection.Type is not TYP_STRUCT);
        var address = indirection.Addr;
        if (address.IsContained && (address.Oper is GT_LEA))
        {
            var mode = address.AsAddrMode();
            var accessType = indirection.Oper is GT_STOREIND
                ? indirection.AsStoreInd().Data.Type
                : indirection.Type;
            if ((mode.HasIndex && (mode.Offset != 0)) ||
                !Emitter.emitIns_valid_imm_for_ldst_offset(mode.Offset, (emitAttr)accessType.Size))
            {
                _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
            }
        }

#if FEATURE_SIMD
        if (indirection.Type is TYP_SIMD12)
        {
            assert(!address.IsContained);
            _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
        }
#endif

        var sourceCount = buildIndirUses(indirection, SRBM_NONE);
        buildInternalRegisterUses();
        if (indirection.Oper is not (GT_STOREIND or GT_NULLCHECK))
        {
            _ = buildDef(indirection, SRBM_NONE);
        }

        return sourceCount;
#elif TARGET_LOONGARCH64
        assert(indirection.Type is not TYP_STRUCT);
        var address = indirection.Addr;
        if (address.IsContained && (address.Oper is GT_LEA))
        {
            var mode = address.AsAddrMode();
            if ((mode.HasIndex && (mode.Offset != 0)) || !Emitter.isValidSimm12(mode.Offset))
            {
                _ = buildInternalIntRegisterDefForNode(indirection);
            }
        }

#if FEATURE_SIMD
        if (indirection.Type is TYP_SIMD12)
        {
            assert(!address.IsContained);
            _ = buildInternalIntRegisterDefForNode(indirection);
        }
#endif

        var sourceCount = buildIndirUses(indirection, SRBM_NONE);
        buildInternalRegisterUses();
        if (indirection.Oper is not (GT_STOREIND or GT_NULLCHECK))
        {
            _ = buildDef(indirection, SRBM_NONE);
        }

        return sourceCount;
#elif TARGET_RISCV64
        assert(indirection.Type is not TYP_STRUCT);

        var address = indirection.Addr;
        if (address.IsContained)
        {
            if (address.Oper is GT_CNS_INT)
            {
                var constant = address.AsIntConCommon();
                var needsRelocation = constant.FitsInAddrBase(_compiler) && constant.AddrNeedsReloc(_compiler);
                if (needsRelocation || !Emitter.isValidSimm12(indirection.Offset))
                {
                    var needsTemporary = indirection.Oper is GT_STOREIND or GT_NULLCHECK ||
                        varTypeIsFloating(indirection.Type);
                    if (needsTemporary)
                    {
                        _ = buildInternalIntRegisterDefForNode(indirection);
                    }
                }
            }
            else if (!Emitter.isValidSimm12(indirection.Offset))
            {
                _ = buildInternalIntRegisterDefForNode(indirection);
            }
        }

#if FEATURE_SIMD
        if (indirection.Type is TYP_SIMD12)
        {
            assert(!address.IsContained);
            _ = buildInternalIntRegisterDefForNode(indirection);
        }
#endif

        var sourceCount = buildIndirUses(indirection, SRBM_NONE);
        buildInternalRegisterUses();
        if (indirection.Oper is not (GT_STOREIND or GT_NULLCHECK))
        {
            _ = buildDef(indirection, SRBM_NONE);
        }

        return sourceCount;
#else
        NYI("LinearScan.buildIndir outside xarch and ARM64");
        throw new FatalJitException("LinearScan.buildIndir outside xarch and ARM64.");
#endif
    }

#if TARGET_X86
    private void checkAndMoveRMWLastUse(GenTree? fromTree, GenTree? toTree)
    {
        if ((fromTree is null) || (fromTree.Oper is not GT_LCL_VAR) ||
            ((fromTree.Flags & GTF_VAR_DEATH) == 0))
        {
            return;
        }

        if (!fromTree.IsContained || (toTree is null) || (toTree.Oper is not GT_LCL_VAR) ||
            (fromTree.AsLclVarCommon().LclNum != toTree.AsLclVarCommon().LclNum))
        {
            assert(false, "Unmatched RMW indirections");
            return;
        }

        fromTree.Flags &= ~GTF_VAR_DEATH;
        toTree.Flags |= GTF_VAR_DEATH;
    }
#endif

    private int buildCmp(GenTree tree)
    {
#if TARGET_AMD64
        assert(tree.Oper.IsCompare || tree.Oper is GT_CMP or GT_TEST or GT_BT or GT_CCMP);
#elif TARGET_X86
        assert(tree.Oper.IsCompare || tree.Oper is GT_CMP or GT_TEST or GT_BT);
#elif TARGET_ARM64
        assert(tree.Oper.IsCompare || tree.Oper is GT_CMP or GT_TEST or GT_CCMP or GT_JCMP or GT_JTEST);
#elif TARGET_RISCV64
        assert(tree.Oper.IsCmpCompare || tree.Oper is GT_JCMP);
#else
        assert(tree.Oper.IsCompare || tree.Oper is GT_CMP or GT_TEST or GT_JCMP);
#endif

        var sourceCount = buildCmpOperands(tree);
        if (tree.Type is not TYP_VOID)
        {
            var destinationCandidates = SRBM_NONE;
#if TARGET_X86
            // A materialized condition uses SETcc, which needs a byte-addressable destination.
            destinationCandidates = _availableIntRegs & (SRBM_EAX | SRBM_ECX | SRBM_EDX | SRBM_EBX);
#endif
            _ = buildDef(tree, destinationCandidates);
        }

        return sourceCount;
    }

    private int buildCmpOperands(GenTree tree)
    {
        var op1Candidates = SRBM_NONE;
        var op2Candidates = SRBM_NONE;
        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;

#if TARGET_X86
        var needByteRegs = (varTypeIsByte(tree.Type) && varTypeUsesIntReg(op1.Type)) ||
            (tree.AsOp().GetCompareSize() == 1);
        if (needByteRegs)
        {
            var byteCandidates = _availableIntRegs & (SRBM_EAX | SRBM_ECX | SRBM_EDX | SRBM_EBX);
            if (!op1.IsContained)
            {
                op1Candidates = byteCandidates;
            }

            if (!op2.IsContained)
            {
                op2Candidates = byteCandidates;
            }
        }
#endif

#if TARGET_AMD64
        if (op2.IsContainedIndir && varTypeUsesFloatReg(op1.Type) && (op2Candidates == SRBM_NONE))
        {
            op2Candidates = _lowGprRegs;
        }

        if (op1.IsContainedIndir && varTypeUsesFloatReg(op2.Type) && (op1Candidates == SRBM_NONE))
        {
            op1Candidates = _lowGprRegs;
        }
#endif

        var srcCount = buildOperandUses(op1, op1Candidates);
        srcCount += buildOperandUses(op2, op2Candidates);

        return srcCount;
    }

    private int buildLclHeap(GenTree tree)
    {
#if TARGET_XARCH
        var srcCount = 1;
        var size = tree.AsUnOp().Op1;
        if (size.Oper.IsCnsIntOrI && size.IsContained)
        {
            srcCount = 0;
#if TARGET_X86
            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
#else
            var alignedSize = unchecked(((nuint)size.AsIntCon().IconValue + (STACK_ALIGN - 1u)) &
                ~(nuint)(STACK_ALIGN - 1u));
            if (alignedSize >= _compiler.eeGetPageSize())
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            }
#endif
        }
        else
        {
            if (!_compiler.info.compInitMem)
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            }
            _ = buildUse(size);
        }

        buildInternalRegisterUses();
        _ = buildDef(tree, SRBM_NONE);
        return srcCount;
#elif TARGET_ARM64
        var size = tree.AsUnOp().Op1;
        var sourceCount = size.IsContainedIntOrIImmed ? 0 : 1;
        if (sourceCount == 0)
        {
            var alignedSize = unchecked(((nuint)size.AsIntCon().IconValue + (STACK_ALIGN - 1u)) &
                ~(nuint)(STACK_ALIGN - 1u));
            if (alignedSize >= _compiler.eeGetPageSize())
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            }
        }
        else if (!_compiler.info.compInitMem)
        {
            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
        }

        if (!size.IsContained)
        {
            _ = buildUse(size);
        }

        buildInternalRegisterUses();
        _ = buildDef(tree, SRBM_NONE);
        return sourceCount;
#else
        NYI("LinearScan.buildLclHeap outside xarch and ARM64");
        throw new FatalJitException("LinearScan.buildLclHeap outside xarch and ARM64.");
#endif
    }
}
