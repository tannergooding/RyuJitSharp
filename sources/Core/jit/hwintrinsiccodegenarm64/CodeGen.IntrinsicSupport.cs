// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insSveMovOpts;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private readonly struct Arm64HWIntrinsic
    {
        private readonly GenTree? _op1;
        private readonly GenTree? _op2;
        private readonly GenTree? _op3;
        private readonly GenTree? _op4;
        private readonly GenTree? _op5;

        public readonly NamedIntrinsic Id;
        public readonly HWIntrinsicCategory Category;
        public readonly int NumOperands;
        public readonly var_types BaseType;

        public Arm64HWIntrinsic(GenTreeHWIntrinsic node)
        {
            assert(node is not null);
            Id = node.HWIntrinsicId;
            Category = HWIntrinsicInfo.lookupCategory(Id);
            assert(HWIntrinsicInfo.RequiresCodegen(Id));
            NumOperands = node.Operands.Length;
            _op1 = null;
            _op2 = null;
            _op3 = null;
            _op4 = null;
            _op5 = null;

            switch (NumOperands)
            {
                case 5:
                {
                    _op5 = node.GetOp(5);
                    goto case 4;
                }
                case 4:
                {
                    _op4 = node.GetOp(4);
                    goto case 3;
                }
                case 3:
                {
                    _op3 = node.GetOp(3);
                    goto case 2;
                }
                case 2:
                {
                    _op2 = node.GetOp(2);
                    goto case 1;
                }
                case 1:
                {
                    _op1 = node.GetOp(1);
                    goto case 0;
                }
                case 0:
                {
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }

            BaseType = node.SimdBaseType;
            if (BaseType == TYP_UNKNOWN)
            {
                assert(Category is HW_Category_Scalar or HW_Category_Special);
                if (HWIntrinsicInfo.BaseTypeFromFirstArg(Id))
                {
                    assert(_op1 is not null);
                    BaseType = _op1.Type;
                }
                else if (HWIntrinsicInfo.BaseTypeFromSecondArg(Id))
                {
                    assert(_op2 is not null);
                    BaseType = _op2.Type;
                }
                else
                {
                    BaseType = node.Type;
                }
                if (Category == HW_Category_Scalar)
                {
                    BaseType = BaseType.ActualType;
                }
            }
        }

        public GenTree Op1 => Operand(_op1);
        public GenTree Op2 => Operand(_op2);
        public GenTree Op3 => Operand(_op3);
        public GenTree Op4 => Operand(_op4);
        public GenTree Op5 => Operand(_op5);

        public bool CodeGenIsTableDriven =>
            Category != HW_Category_Helper && !HWIntrinsicInfo.HasSpecialCodegen(Id);

        private static GenTree Operand(GenTree? operand)
        {
            assert(operand is not null);

            return operand;
        }
    }

    private static insOpts genGetSimdInsOpt(emitAttr size, var_types elementType)
    {
        assert(size is EA_16BYTE or EA_8BYTE);
        var result = INS_OPTS_NONE;
        switch (elementType)
        {
            case TYP_DOUBLE:
            case TYP_ULONG:
            case TYP_LONG:
            {
                result = size == EA_16BYTE ? INS_OPTS_2D : INS_OPTS_1D;
                break;
            }
            case TYP_FLOAT:
            case TYP_UINT:
            case TYP_INT:
            {
                result = size == EA_16BYTE ? INS_OPTS_4S : INS_OPTS_2S;
                break;
            }
            case TYP_USHORT:
            case TYP_SHORT:
            {
                result = size == EA_16BYTE ? INS_OPTS_8H : INS_OPTS_4H;
                break;
            }
            case TYP_UBYTE:
            case TYP_BYTE:
            {
                result = size == EA_16BYTE ? INS_OPTS_16B : INS_OPTS_8B;
                break;
            }
            default:
            {
                assert(false, "Unsupported element type");
                unreached();
                break;
            }
        }

        return result;
    }

    private static emitAttr emitTypeSize(var_types type) => type.EmitSize;
    private static emitAttr emitTypeSize(GenTree tree) => tree.Type.EmitSize;
    private static emitAttr emitActualTypeSize(var_types type) => type.EmitActualSize;
    private static emitAttr emitActualTypeSize(GenTree tree) => tree.Type.EmitActualSize;

    private static void genEmitCreateWhileMask(Emitter emit, GenTreeHWIntrinsic node,
        instruction ins, instruction unsignedIns, regNumber targetReg, regNumber op1Reg,
        regNumber op2Reg, insOpts opt)
    {
        var auxType = node.AuxiliaryType;
        var emitSize = emitActualTypeSize(auxType);
        if (varTypeIsUnsigned(auxType))
        {
            ins = unsignedIns;
        }
        emit.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
    }

    private static regNumber getNextSIMDRegWithWraparound(regNumber reg)
    {
        var nextReg = unchecked((regNumber)((int)reg + 1));

        return nextReg > REG_V31 ? REG_V0 : nextReg;
    }

    private static uint GetArm64IntrinsicRegisterList(GenTree operand, out regNumber firstReg,
        regNumber excludedReg = REG_NA)
    {
        var fieldList = operand.AsFieldList();
        var firstField = fieldList.Uses.Head;
        assert(firstField is not null);
        firstReg = firstField.Node.RegNum;
        uint count = 0;
#if DEBUG
        var argReg = firstReg;
#endif
        foreach (var use in fieldList.Uses)
        {
            count++;
#if DEBUG
            assert(argReg == use.Node.RegNum);
            if (excludedReg != REG_NA)
            {
                assert(excludedReg != argReg);
            }
            argReg = getNextSIMDRegWithWraparound(argReg);
#endif
        }

        return count;
    }

    private static void Arm64IntrinsicEmitRegisterLabel(instruction ins, emitAttr attr,
        BasicBlock dst, regNumber reg)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitIns_R_L recording is not ported.");

    private static void Arm64IntrinsicEmitRegisterBranch(instruction ins, emitAttr attr,
        BasicBlock dst, regNumber reg)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitIns_J_R recording is not ported.");

    private static void Arm64IntrinsicEmitFiveRegisters(instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, regNumber reg3, regNumber reg4, regNumber reg5,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = INS_SCALABLE_OPTS_NONE,
        insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitInsSve_R_R_R_R_R recording is not ported.");

    private static void Arm64IntrinsicEmitFiveRegistersImmediate(instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, regNumber reg3, regNumber reg4, regNumber reg5, nint imm,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = INS_SCALABLE_OPTS_NONE,
        insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitInsSve_R_R_R_R_R_I recording is not ported.");

    private static void Arm64IntrinsicEmitFourRegistersTwoImmediates(instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, regNumber reg3, regNumber reg4, nint imm1, nint imm2,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = INS_SCALABLE_OPTS_NONE,
        insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitInsSve_R_R_R_R_I_I recording is not ported.");

    private static void Arm64IntrinsicEmitRegisterPatternImmediate(instruction ins, emitAttr attr,
        regNumber reg, insSvePattern pattern, nint imm, insOpts opt = INS_OPTS_NONE)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitIns_R_PATTERN_I recording is not ported.");

    private static void Arm64IntrinsicEmitTwoRegistersPatternImmediate(instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, insSvePattern pattern, nint imm,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = INS_SCALABLE_OPTS_NONE,
        insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitIns_R_R_PATTERN_I recording is not ported.");

    private static void Arm64IntrinsicEmitPrefetchThreeRegisters(instruction ins, emitAttr attr,
        insSvePrfop prfop, regNumber reg1, regNumber reg2, regNumber reg3,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = INS_SCALABLE_OPTS_NONE)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitIns_PRFOP_R_R_R recording is not ported.");

    private static void Arm64IntrinsicEmitPrefetchTwoRegistersImmediate(instruction ins, emitAttr attr,
        insSvePrfop prfop, regNumber reg1, regNumber reg2, int imm, insOpts opt = INS_OPTS_NONE)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitIns_PRFOP_R_R_I recording is not ported.");

#if DEBUG
    private static int Arm64IntrinsicSveReg1ListSize(instruction ins)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 insGetSveReg1ListSize is not ported.");
#endif

#if DEBUG
    private static void checkRMWRegisters(Arm64HWIntrinsic intrin, regNumber targetReg)
    {
        var canRepairTargetOverlap = intrin.Id == NI_Sve_MultiplyAddRotateComplex;
        GenTree rmwOp;
        if (HWIntrinsicInfo.IsFmaIntrinsic(intrin.Id) && intrin.NumOperands == 3)
        {
            if (targetReg == intrin.Op2.RegNum)
            {
                rmwOp = intrin.Op2;
            }
            else if (targetReg == intrin.Op3.RegNum)
            {
                rmwOp = intrin.Op3;
            }
            else
            {
                rmwOp = intrin.Op1;
            }
        }
        else
        {
            switch (intrin.Id)
            {
                case NI_Sve2_AddCarryWideningEven:
                case NI_Sve2_AddCarryWideningOdd:
                {
                    rmwOp = intrin.Op3;
                    break;
                }
                case NI_Sve_CreateBreakPropagateMask:
                case NI_Sve2_BitwiseSelect:
                case NI_Sve2_BitwiseSelectLeftInverted:
                case NI_Sve2_BitwiseSelectRightInverted:
                {
                    rmwOp = intrin.Op2;
                    break;
                }
                default:
                {
                    rmwOp = HWIntrinsicInfo.IsExplicitMaskedOperation(intrin.Id) ? intrin.Op2 : intrin.Op1;
                    break;
                }
            }
        }

        var rmwReg = rmwOp.RegNum;
        if (targetReg != rmwReg)
        {
            switch (intrin.NumOperands)
            {
                case 5:
                {
                    assert(targetReg != intrin.Op5.RegNum || genIsSameLocalVar(rmwOp, intrin.Op5));
                    goto case 4;
                }
                case 4:
                {
                    assert(targetReg != intrin.Op4.RegNum || genIsSameLocalVar(rmwOp, intrin.Op4));
                    goto case 3;
                }
                case 3:
                {
                    if (rmwReg != intrin.Op3.RegNum)
                    {
                        assert(canRepairTargetOverlap || targetReg != intrin.Op3.RegNum ||
                            genIsSameLocalVar(rmwOp, intrin.Op3));
                    }
                    goto case 2;
                }
                case 2:
                {
                    if (rmwReg != intrin.Op2.RegNum)
                    {
                        assert(canRepairTargetOverlap || targetReg != intrin.Op2.RegNum ||
                            genIsSameLocalVar(rmwOp, intrin.Op2));
                    }
                    if (rmwReg != intrin.Op1.RegNum)
                    {
                        assert(targetReg != intrin.Op1.RegNum || genIsSameLocalVar(rmwOp, intrin.Op1));
                    }
                    break;
                }
                default:
                {
                    break;
                }
            }
        }
    }
#endif
}
#endif
