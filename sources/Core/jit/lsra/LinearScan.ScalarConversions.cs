// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildCast(GenTreeCast cast)
    {
#if TARGET_XARCH
        var source = cast.CastOp;
        var sourceType = source.Type;
        var castType = cast.CastType;

        if (cast.IsUnsigned && varTypeIsLong(sourceType) && varTypeIsFloating(castType) && !_evexIsSupported)
        {
            _ = buildInternalIntRegisterDefForNode(cast, forceLowGprForApx(cast, _availableIntRegs, true));
            _ = buildInternalIntRegisterDefForNode(cast, _availableIntRegs);
        }

        var candidates = SRBM_NONE;
#if TARGET_X86
        if (varTypeIsByte(castType))
        {
            candidates = _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
        }

        assert(!varTypeIsLong(sourceType) || (source.Oper is GT_LONG && source.IsContained));
#else
        if (cast.HasOverflowCheckEx && varTypeIsLong(sourceType) && varTypeIsInt(castType))
        {
            _ = buildInternalIntRegisterDefForNode(cast, _availableIntRegs);
        }

        if ((varTypeUsesIntReg(sourceType) || source.IsContainedIndir) &&
            varTypeUsesFloatReg(cast.Type) && !_evexIsSupported)
        {
            candidates = forceLowGprForApx(cast, candidates, true);
        }
#endif

        var srcCount = buildCastUses(cast, candidates);
        buildInternalRegisterUses();
#if TARGET_AMD64
        candidates = SRBM_NONE;
#endif
        _ = buildDef(cast, candidates);
        return srcCount;
#elif TARGET_ARM64
        var sourceCount = buildCastUses(cast, SRBM_NONE);
        buildInternalRegisterUses();
        _ = buildDef(cast, SRBM_NONE);
        return sourceCount;
#elif TARGET_LOONGARCH64
        var sourceCount = buildOperandUses(cast.CastOp);
        _ = buildDef(cast, SRBM_NONE);
        return sourceCount;
#elif TARGET_RISCV64
        var castDescriptor = new CodeGen.GenIntCastDesc(cast);
        if (castDescriptor.Check is not CodeGen.GenIntCastDesc.CheckKind.CHECK_NONE)
        {
            _ = buildInternalIntRegisterDefForNode(cast);
        }

        buildInternalRegisterUses();
        var sourceCount = buildOperandUses(cast.CastOp);
        _ = buildDef(cast, SRBM_NONE);

        if (varTypeIsFloating(cast.CastOp.Type) && !varTypeIsFloating(cast.Type))
        {
            _ = buildInternalIntRegisterDefForNode(cast);
            buildInternalRegisterUses();
        }

        return sourceCount;
#else
        NYI("LinearScan.buildCast outside AMD64");
        throw new FatalJitException("LinearScan.buildCast outside AMD64.");
#endif
    }

    private int buildCastUses(GenTreeCast cast, SingleTypeRegSet candidates)
    {
        var source = cast.CastOp;
        if (source.IsContained)
        {
            return buildOperandUses(source, candidates);
        }

        var sourceUse = buildUse(source, candidates);
#if TARGET_64BIT
        if ((source.Type is TYP_LONG) && (cast.Type is TYP_INT))
        {
            _targetPreferredUse = sourceUse;
        }
#endif

        return 1;
    }

    private int buildIntrinsic(GenTree tree)
    {
#if TARGET_XARCH
        var intrinsic = tree.AsIntrinsic();
        var operand = intrinsic.Op1;
        assert(varTypeIsFloating(operand.Type));
        assert(operand.Type == tree.Type);
        RefPosition? internalFloatDef = null;

        switch (intrinsic.IntrinsicName)
        {
            case NI_System_Math_Abs:
            {
                internalFloatDef = buildInternalFloatRegisterDefForNode(tree, internalFloatRegCandidates());
                break;
            }

            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_Truncate:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            {
                break;
            }

            default:
            {
                throw new FatalJitException("Unsupported scalar math intrinsic.");
            }
        }

        assert(intrinsic.Op2 is null);

        int srcCount;
        if (operand.IsContained)
        {
            SingleTypeRegSet operandCandidates;
            switch (intrinsic.IntrinsicName)
            {
                case NI_System_Math_Ceiling:
                case NI_System_Math_Floor:
                case NI_System_Math_Truncate:
                case NI_System_Math_Round:
                case NI_System_Math_Sqrt:
                {
                    operandCandidates = forceLowGprForApx(operand);
                    break;
                }

                case NI_System_Math_Abs:
                {
                    operandCandidates = forceLowGprForApxIfNeeded(operand, SRBM_NONE, _evexIsSupported);
                    break;
                }

                default:
                {
                    throw new FatalJitException("Unsupported scalar math intrinsic.");
                }
            }

            srcCount = buildOperandUses(operand, operandCandidates);
        }
        else
        {
            _targetPreferredUse = buildUse(operand);
            srcCount = 1;
        }

        if (internalFloatDef is not null)
        {
            buildInternalRegisterUses();
        }

        _ = buildDef(tree, SRBM_NONE);
        return srcCount;
#elif TARGET_ARM64
        var intrinsic = tree.AsIntrinsic();
        var operand = intrinsic.Op1;
        int sourceCount;
        switch (intrinsic.IntrinsicName)
        {
            case NI_System_Math_Abs:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_Truncate:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            {
                assert(varTypeIsFloating(operand.Type) && (operand.Type == tree.Type));
                _ = buildUse(operand);
                sourceCount = 1;
                break;
            }

            case NI_PRIMITIVE_PopCount:
            {
                assert(varTypeIsIntegral(operand.Type));
                if (!_compiler.compOpportunisticallyDependsOn(InstructionSet_Cssc))
                {
                    _ = buildInternalFloatRegisterDefForNode(tree, _availableFloatRegs);
                }

                _ = buildUse(operand);
                buildInternalRegisterUses();
                sourceCount = 1;
                break;
            }

            case NI_PRIMITIVE_TrailingZeroCount:
            {
                assert(varTypeIsIntegral(operand.Type));
                _ = buildUse(operand);
                sourceCount = 1;
                break;
            }

            default:
            {
                throw new FatalJitException($"Unsupported ARM64 scalar intrinsic {intrinsic.IntrinsicName}.");
            }
        }

        _ = buildDef(tree, SRBM_NONE);
        return sourceCount;
#else
        NYI("LinearScan.buildIntrinsic outside AMD64");
        throw new FatalJitException("LinearScan.buildIntrinsic outside AMD64.");
#endif
    }

    private int buildSelect(GenTreeOp select)
    {
#if TARGET_XARCH
        var srcCount = 0;
        if (select.Oper is GT_SELECT)
        {
            _ = buildUse(select.AsConditional().Cond);
            srcCount++;
        }

        var trueValue = select.Op1;
        var falseValue = select.Op2;
        assert(refPositions.Count != 0);
        var firstUsesStart = refPositions.Count - 1;

        RefPosition? uncontainedTrueUse = null;
        if (trueValue.IsContained)
        {
            srcCount += buildOperandUses(trueValue);
        }
        else
        {
            _targetPreferredUse = uncontainedTrueUse = buildUse(trueValue);
            srcCount++;
        }

        var secondUsesStart = refPositions.Count - 1;

        RefPosition? uncontainedFalseUse = null;
        if (falseValue.IsContained)
        {
            srcCount += buildOperandUses(falseValue);
        }
        else
        {
            _targetPreferredUse2 = uncontainedFalseUse = buildUse(falseValue);
            srcCount++;
        }

        if ((_targetPreferredUse is not null) && (_targetPreferredUse2 is not null))
        {
            _targetPreferredUse2 = null;
        }

        // If both values use the same interval, the first value must survive the second operand's use.
        for (var first = firstUsesStart + 1; first <= secondUsesStart; first++)
        {
            var firstUse = refPositions[first];
            if (firstUse.refType is not RefType.RefTypeUse)
            {
                continue;
            }

            for (var second = secondUsesStart + 1; second < refPositions.Count; second++)
            {
                var secondUse = refPositions[second];
                if ((secondUse.refType is RefType.RefTypeUse) &&
                    ReferenceEquals(firstUse.getInterval(), secondUse.getInterval()))
                {
                    setDelayFree(firstUse);
                    break;
                }
            }
        }

        if (select.Oper is GT_SELECTCC)
        {
            switch (select.AsOpCC().Condition.Code)
            {
                case GenCondition.FEQ:
                case GenCondition.FLT:
                case GenCondition.FLE:
                {
                    assert(uncontainedFalseUse is not null);
                    setDelayFree(uncontainedFalseUse);
                    break;
                }

                case GenCondition.FNEU:
                case GenCondition.FGEU:
                case GenCondition.FGTU:
                {
                    assert(uncontainedTrueUse is not null);
                    setDelayFree(uncontainedTrueUse);
                    break;
                }
            }
        }

        _ = buildDef(select, SRBM_NONE);
        return srcCount;
#elif TARGET_ARM || TARGET_ARM64
        assert(select.Oper is GT_SELECT or GT_SELECTCC);
        var sourceCount = select.Oper is GT_SELECT
            ? buildOperandUses(select.AsConditional().Cond)
            : 0;
        sourceCount += buildOperandUses(select.Op1);
        sourceCount += buildOperandUses(select.Op2);
        _ = buildDef(select, SRBM_NONE);
        return sourceCount;
#else
        NYI("LinearScan.buildSelect outside xarch/ARM/ARM64");
        throw new FatalJitException("LinearScan.buildSelect outside xarch/ARM/ARM64.");
#endif
    }
}
