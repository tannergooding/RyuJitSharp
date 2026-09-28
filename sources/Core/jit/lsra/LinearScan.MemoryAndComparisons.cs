// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildIndir(GenTreeIndir indirection)
    {
#if TARGET_AMD64
        assert(indirection.Type is not TYP_STRUCT);
        var useCandidates = SRBM_NONE;
        if (varTypeUsesIntReg(indirection.Addr.Type))
        {
            useCandidates = forceLowGprForApxIfNeeded(indirection.Addr, useCandidates, _evexIsSupported);
        }

        var srcCount = buildIndirUses(indirection, useCandidates);
        if (indirection.Oper is GT_STOREIND)
        {
            var source = indirection.Data;
            if (indirection.AsStoreInd().RmwStatus is STOREIND_RMW_DST_IS_OP1 or STOREIND_RMW_DST_IS_OP2)
            {
                // Contained shifts and rotates have not yet had their fixed-register requirements built.
                assert(source.IsContained && source.Oper.IsRmwMemOp);
                srcCount += source.Oper.IsShiftOrRotate
                    ? buildShiftRotate(source)
                    : buildBinaryUses(source.AsUnOp());
            }
            else
            {
                srcCount += buildOperandUses(source);
            }
        }

#if FEATURE_SIMD
        buildInternalRegisterUses();
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
#else
        NYI("LinearScan.buildIndir outside AMD64");
        throw new FatalJitException("LinearScan.buildIndir outside AMD64.");
#endif
    }

    private int buildCmp(GenTree tree)
    {
#if TARGET_AMD64
        assert(tree.Oper.IsCompare || tree.Oper is GT_CMP or GT_TEST or GT_BT or GT_CCMP);
        var srcCount = buildCmpOperands(tree);
        if (tree.Type is not TYP_VOID)
        {
            _ = buildDef(tree, SRBM_NONE);
        }
        return srcCount;
#elif TARGET_ARM64
        assert(tree.Oper.IsCompare || tree.Oper is GT_CMP or GT_TEST or GT_CCMP or GT_JCMP or GT_JTEST);
        var sourceCount = buildCmpOperands(tree);
        if (tree.Type is not TYP_VOID)
        {
            _ = buildDef(tree, SRBM_NONE);
        }

        return sourceCount;
#else
        NYI("LinearScan.buildCmp outside AMD64");
        throw new FatalJitException("LinearScan.buildCmp outside AMD64.");
#endif
    }

    private int buildCmpOperands(GenTree tree)
    {
#if TARGET_AMD64
        var op1Candidates = SRBM_NONE;
        var op2Candidates = SRBM_NONE;
        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;

        if (op2.IsContainedIndir && varTypeUsesFloatReg(op1.Type))
        {
            op2Candidates = _lowGprRegs;
        }
        if (op1.IsContainedIndir && varTypeUsesFloatReg(op2.Type))
        {
            op1Candidates = _lowGprRegs;
        }

        var srcCount = buildOperandUses(op1, op1Candidates);
        srcCount += buildOperandUses(op2, op2Candidates);
        return srcCount;
#elif TARGET_ARM64
        return buildBinaryUses(tree.AsOp());
#else
        NYI("LinearScan.buildCmpOperands outside AMD64");
        throw new FatalJitException("LinearScan.buildCmpOperands outside AMD64.");
#endif
    }

    private int buildLclHeap(GenTree tree)
    {
#if TARGET_AMD64
        var srcCount = 1;
        var size = tree.AsUnOp().Op1;
        if (size.Oper.IsCnsIntOrI && size.IsContained)
        {
            srcCount = 0;
            var alignedSize = unchecked(((nuint)size.AsIntCon().IconValue + (STACK_ALIGN - 1u)) &
                ~(nuint)(STACK_ALIGN - 1u));
            if (alignedSize >= _compiler.eeGetPageSize())
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            }
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
        NYI("LinearScan.buildLclHeap outside AMD64");
        throw new FatalJitException("LinearScan.buildLclHeap outside AMD64.");
#endif
    }
}
