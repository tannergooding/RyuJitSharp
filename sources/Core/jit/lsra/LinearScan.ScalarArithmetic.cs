// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildShiftRotate(GenTree tree)
    {
#if TARGET_XARCH
        var shiftBy = tree.AsOp().Op2;
        var source = tree.AsOp().Op1;
        var srcCount = 0;
        var srcCandidates = SRBM_NONE;
        var dstCandidates = SRBM_NONE;

        // An immediate shift encodes eight bits; the instruction masks the unused bits.
        if (shiftBy.IsContained)
        {
            assert(shiftBy.Oper.IsConst);

            var shiftByValue = unchecked((int)shiftBy.AsIntConCommon().IconValue);
            if ((tree.Type.ActualType is TYP_LONG) &&
                _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2) &&
                (tree.Oper is GT_ROL or GT_ROR) && (shiftByValue > 0) && (shiftByValue < 64))
            {
                srcCandidates = forceLowGprForApxIfNeeded(source, srcCandidates, _evexIsSupported);
                dstCandidates = forceLowGprForApxIfNeeded(tree, dstCandidates, _evexIsSupported);
            }
        }
        else if (!tree.IsContained && (tree.Oper.IsShift || source.IsContained) &&
                 _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2) &&
                 ((tree.Flags & GTF_SET_FLAGS) == 0))
        {
            srcCount += buildOperandUses(source,
                forceLowGprForApxIfNeeded(source, srcCandidates, _evexIsSupported));
            srcCount += buildOperandUses(shiftBy,
                forceLowGprForApxIfNeeded(shiftBy, dstCandidates, _evexIsSupported));
            _ = buildDef(tree, forceLowGprForApxIfNeeded(tree, dstCandidates, _evexIsSupported));
            return srcCount;
        }
        else
        {
            // The legacy variable-count form reserves RCX for the count operand.
            srcCandidates = _availableIntRegs & ~SRBM_RCX;
            dstCandidates = _availableIntRegs & ~SRBM_RCX;
        }

#if TARGET_X86
        if (tree.Oper is GT_LSH_HI or GT_RSH_LO)
        {
            assert(source.Oper is GT_LONG && source.IsContained);

            var sourceLo = source.AsOp().Op1;
            var sourceHi = source.AsOp().Op2;
            assert(!sourceLo.IsContained && !sourceHi.IsContained);
            var sourceLoUse = buildUse(sourceLo, srcCandidates);
            var sourceHiUse = buildUse(sourceHi, srcCandidates);

            if (!tree.IsContained)
            {
                if (tree.Oper is GT_LSH_HI)
                {
                    setDelayFree(sourceLoUse);
                }
                else
                {
                    setDelayFree(sourceHiUse);
                }
            }
        }
        else
#endif
        if (!source.IsContained)
        {
            _targetPreferredUse = buildUse(source,
                forceLowGprForApxIfNeeded(source, srcCandidates, _evexIsSupported));
            srcCount++;
        }
        else
        {
            srcCount += buildOperandUses(source,
                forceLowGprForApxIfNeeded(source, srcCandidates, _evexIsSupported));
        }

        if (!tree.IsContained)
        {
            if (!shiftBy.IsContained)
            {
                RefPosition? shiftUse = null;
                srcCount += buildDelayFreeUses(shiftBy, source, SRBM_RCX, ref shiftUse);
                _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, new regMaskTP(SRBM_RCX));
            }

#if DEBUG
            var assignedRegister = tree.Reg;
#else
            var assignedRegister = tree.RegNum;
#endif
            if (assignedRegister is REG_NA)
            {
                dstCandidates = forceLowGprForApxIfNeeded(tree, dstCandidates, _evexIsSupported);
            }

            _ = buildDef(tree, dstCandidates);
        }
        else if (!shiftBy.IsContained)
        {
            srcCount += buildOperandUses(shiftBy, SRBM_RCX);
            _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, new regMaskTP(SRBM_RCX));
        }

        return srcCount;
#else
        throw new FatalJitException("LSRA shift/rotate reference building is not implemented outside xarch.");
#endif
    }

    private int buildModDiv(GenTree tree)
    {
#if TARGET_XARCH
        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;

        if (varTypeIsFloating(tree.Type))
        {
            return buildSimple(tree);
        }

        // DIV/IDIV takes the dividend in RAX:RDX and produces the quotient in RAX,
        // the remainder in RDX.
        var dstCandidates = (tree.Oper is GT_MOD or GT_UMOD) ? SRBM_RDX : SRBM_RAX;
        var srcCount = 0;
#if TARGET_X86
        if (op1.Oper is GT_LONG)
        {
            assert(op1.IsContained);

            var loVal = op1.AsOp().Op1;
            var hiVal = op1.AsOp().Op2;
            assert(!loVal.IsContained && !hiVal.IsContained);
            assert(op2.Oper.IsCnsIntOrI);
            assert(tree.Oper is GT_UMOD);

            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            _ = buildUse(loVal, SRBM_EAX);
            _ = buildUse(hiVal, SRBM_EDX);
            srcCount = 2;
        }
        else
#endif
        {
            var op1Use = buildUse(op1, SRBM_EAX);
            _targetPreferredUse = op1Use;
            srcCount = 1;
        }

        RefPosition? op2Use = null;
        srcCount += buildDelayFreeUses(op2, op1, _availableIntRegs & ~(SRBM_RAX | SRBM_RDX), ref op2Use);
        buildInternalRegisterUses();

        var killMask = getKillSetForModDiv(tree.AsOp());
        buildDefWithKills(tree, 1, dstCandidates, killMask);
        return srcCount;
#else
        throw new FatalJitException("LSRA division reference building is not implemented outside xarch.");
#endif
    }

    private int buildMul(GenTree tree)
    {
#if TARGET_XARCH
        assert(tree.Oper.IsMul);
        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;

        if (varTypeIsFloating(tree.Type))
        {
            return buildSimple(tree);
        }

        var isUnsignedMultiply = tree.AsOp().IsUnsigned;
        var requiresOverflowCheck = tree.HasOverflowCheckEx;
        var useMulx = (tree.Oper is not GT_MUL) && isUnsignedMultiply &&
            _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2);
        var srcCount = 0;
        var dstCount = 1;
        var dstCandidates = SRBM_NONE;

#if TARGET_X86
        if (tree.Oper is not GT_MUL_LONG)
        {
            assert((tree.Flags & GTF_MUL_64RSLT) == 0);
        }
#else
        assert((tree.Flags & GTF_MUL_64RSLT) == 0);
#endif

        if (useMulx)
        {
            var srcCandidates1 = SRBM_NONE;
            var srcCandidates2 = SRBM_NONE;
            if (op1.IsContained)
            {
                assert(!op2.IsContained);
                srcCandidates2 = SRBM_RDX;
            }
            else if (op2.IsContained)
            {
                srcCandidates1 = SRBM_RDX;
            }

            srcCount = buildOperandUses(op1,
                forceLowGprForApxIfNeeded(op1, srcCandidates1, _evexIsSupported));
            srcCount += buildOperandUses(op2,
                forceLowGprForApxIfNeeded(op2, srcCandidates2, _evexIsSupported));
#if TARGET_X86
            if (tree.Oper is GT_MUL_LONG)
            {
                dstCount = 2;
            }
#endif
        }
        else
        {
            assert(!(op1.IsContained && !op1.Oper.IsCnsIntOrI) ||
                   !(op2.IsContained && !op2.Oper.IsCnsIntOrI));
            srcCount = buildBinaryUses(tree.AsOp());

            if (isUnsignedMultiply && requiresOverflowCheck)
            {
                dstCandidates = SRBM_RAX;
            }
            else if (tree.Oper is GT_MULHI)
            {
                dstCandidates = SRBM_RDX;
            }
#if TARGET_X86
            else if (tree.Oper is GT_MUL_LONG)
            {
                dstCandidates = SRBM_RAX | SRBM_RDX;
                dstCount = 2;
            }
#endif
        }

        var killMask = getKillSetForMul(tree.AsOp());
        buildDefWithKills(tree, dstCount, dstCandidates, killMask);
        return srcCount;
#else
        throw new FatalJitException("LSRA multiplication reference building is not implemented outside xarch.");
#endif
    }
}
