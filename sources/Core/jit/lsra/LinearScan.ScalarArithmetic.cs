// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildShiftRotate(GenTree tree)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LSRA shift/rotate reference building is not implemented outside xarch.");
        }

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
            var countRegister = TargetRuntimeMetadata.SelectXarch(SRBM_X86_ECX, SRBM_X64_RCX);
            srcCandidates = _availableIntRegs & ~countRegister;
            dstCandidates = _availableIntRegs & ~countRegister;
        }

        if (Configuration.TARGET_X86 && (tree.Oper is GT_LSH_HI or GT_RSH_LO))
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
        else if (!source.IsContained)
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
                var countRegister = TargetRuntimeMetadata.SelectXarch(SRBM_X86_ECX, SRBM_X64_RCX);
                srcCount += buildDelayFreeUses(shiftBy, source, countRegister, ref shiftUse);
                _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, new regMaskTP(countRegister));
            }

            var assignedRegister = Configuration.DEBUG ? tree.Reg : tree.RegNum;
            if (assignedRegister == REG_NA)
            {
                dstCandidates = forceLowGprForApxIfNeeded(tree, dstCandidates, _evexIsSupported);
            }

            _ = buildDef(tree, dstCandidates);
        }
        else if (!shiftBy.IsContained)
        {
            var countRegister = TargetRuntimeMetadata.SelectXarch(SRBM_X86_ECX, SRBM_X64_RCX);
            srcCount += buildOperandUses(shiftBy, countRegister);
            _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, new regMaskTP(countRegister));
        }

        return srcCount;
    }

    private int buildModDiv(GenTree tree)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LSRA division reference building is not implemented outside xarch.");
        }

        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;

        if (varTypeIsFloating(tree.Type))
        {
            return buildSimple(tree);
        }

        // DIV/IDIV takes the dividend in RAX:RDX and produces the quotient in RAX,
        // the remainder in RDX.
        var dstCandidates = (tree.Oper is GT_MOD or GT_UMOD)
            ? TargetRuntimeMetadata.IntegerReturnRegister1Mask.Lower
            : TargetRuntimeMetadata.IntegerReturnMask;
        int srcCount;
        if (Configuration.TARGET_X86 && op1.Oper is GT_LONG)
        {
            assert(op1.IsContained);

            var loVal = op1.AsOp().Op1;
            var hiVal = op1.AsOp().Op2;
            assert(!loVal.IsContained && !hiVal.IsContained);
            assert(op2.Oper.IsCnsIntOrI);
            assert(tree.Oper is GT_UMOD);

            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            _ = buildUse(loVal, SRBM_X86_EAX);
            _ = buildUse(hiVal, SRBM_X86_EDX);
            srcCount = 2;
        }
        else
        {
            var op1Use = buildUse(op1, TargetRuntimeMetadata.IntegerReturnMask);
            _targetPreferredUse = op1Use;
            srcCount = 1;
        }

        RefPosition? op2Use = null;
        srcCount += buildDelayFreeUses(op2, op1,
            _availableIntRegs & ~(TargetRuntimeMetadata.IntegerReturnMask |
                TargetRuntimeMetadata.IntegerReturnRegister1Mask.Lower), ref op2Use);
        buildInternalRegisterUses();

        var killMask = getKillSetForModDiv(tree.AsOp());
        buildDefWithKills(tree, 1, dstCandidates, killMask);
        return srcCount;
    }

    private int buildMul(GenTree tree)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LSRA multiplication reference building is not implemented outside xarch.");
        }

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
        int srcCount;
        var dstCount = 1;
        var dstCandidates = SRBM_NONE;

        if (Configuration.TARGET_X86)
        {
            if (tree.Oper is not GT_MUL_LONG)
            {
                assert((tree.Flags & GTF_MUL_64RSLT) == 0);
            }
        }
        else
        {
            assert((tree.Flags & GTF_MUL_64RSLT) == 0);
        }

        if (useMulx)
        {
            var srcCandidates1 = SRBM_NONE;
            var srcCandidates2 = SRBM_NONE;
            if (op1.IsContained)
            {
                assert(!op2.IsContained);
                srcCandidates2 = TargetRuntimeMetadata.IntegerReturnRegister1Mask.Lower;
            }
            else if (op2.IsContained)
            {
                srcCandidates1 = TargetRuntimeMetadata.IntegerReturnRegister1Mask.Lower;
            }

            srcCount = buildOperandUses(op1,
                forceLowGprForApxIfNeeded(op1, srcCandidates1, _evexIsSupported));
            srcCount += buildOperandUses(op2,
                forceLowGprForApxIfNeeded(op2, srcCandidates2, _evexIsSupported));
            if (Configuration.TARGET_X86 && tree.Oper is GT_MUL_LONG)
            {
                dstCount = 2;
            }
        }
        else
        {
            assert(!(op1.IsContained && !op1.Oper.IsCnsIntOrI) ||
                   !(op2.IsContained && !op2.Oper.IsCnsIntOrI));
            srcCount = buildBinaryUses(tree.AsOp());

            if (isUnsignedMultiply && requiresOverflowCheck)
            {
                dstCandidates = TargetRuntimeMetadata.IntegerReturnMask;
            }
            else if (tree.Oper is GT_MULHI)
            {
                dstCandidates = TargetRuntimeMetadata.IntegerReturnRegister1Mask.Lower;
            }
            else if (Configuration.TARGET_X86 && tree.Oper is GT_MUL_LONG)
            {
                dstCandidates = TargetRuntimeMetadata.IntegerReturnMask |
                    TargetRuntimeMetadata.IntegerReturnRegister1Mask.Lower;
                dstCount = 2;
            }
        }

        var killMask = getKillSetForMul(tree.AsOp());
        buildDefWithKills(tree, dstCount, dstCandidates, killMask);
        return srcCount;
    }
}
