// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    internal unsafe OperandDesc genOperandDesc(instruction ins, GenTree op)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction operand classification requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        if (!op.IsContained && !op.IsUsedFromSpillTemp)
        {
            return new OperandDesc(op.RegNum);
        }

        TempDsc? temp = null;
        var varNum = BAD_VAR_NUM;
        var offset = ushort.MaxValue;

        if (op.IsUsedFromSpillTemp)
        {
            assert(op.IsRegOptional);
            temp = getSpillTempDsc(op);
            varNum = temp.tdTempNum;
            offset = 0;
            _regSet.tmpRlsTemp(temp);
        }
        else if (op.Oper.IsIndir || op.Oper.IsHWIntrinsic)
        {
            GenTree address;
            GenTreeIndir? memIndir = null;

            if (op.Oper.IsIndir)
            {
                memIndir = op.AsIndir();
                address = memIndir.Addr;
            }
            else
            {
#if FEATURE_HW_INTRINSICS
                var intrinsic = op.AsHWIntrinsic();
                var intrinsicId = intrinsic.HWIntrinsicId;
                var simdBaseType = intrinsic.SimdBaseType;

                switch (intrinsicId)
                {
                    case NI_X86Base_LoadAndDuplicateToVector128:
                    case NI_AVX_BroadcastScalarToVector128:
                    case NI_AVX_BroadcastScalarToVector256:
                    {
                        assert(intrinsic.IsContained);
                        assert(intrinsic.IsMemoryLoad());
                        assert(intrinsic.Operands.Length == 1);
                        assert(varTypeIsFloating(simdBaseType));
                        var child = intrinsic.GetOp(1);
                        assert(child.IsContained);
                        if (child.Oper is GT_LCL_ADDR or GT_CNS_INT or GT_LEA)
                        {
                            address = child;
                        }
                        else
                        {
                            assert(child.Oper is GT_LCL_VAR);
                            return new OperandDesc(simdBaseType, child);
                        }
                        break;
                    }

                    case NI_X86Base_MoveAndDuplicate:
                    case NI_AVX2_BroadcastScalarToVector128:
                    case NI_AVX2_BroadcastScalarToVector256:
                    case NI_AVX512_BroadcastScalarToVector512:
                    {
                        assert(intrinsic.IsContained);
                        if (intrinsicId == NI_X86Base_MoveAndDuplicate)
                        {
                            assert(simdBaseType == TYP_DOUBLE);
                        }

                        // Lowering removes scalar wrappers. Broadcast constants become
                        // scalar data; all other scalar operands retain their normal form.
                        var child = intrinsic.GetOp(1);
                        assert(child.IsContained);
                        if (child.Oper.IsIntegralConst)
                        {
                            var value = child.AsIntConCommon().IntegralValue;
                            var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in value, 1));
                            var size = simdBaseType.Size;
                            var offsetInData = Emitter.emitDataConst(bytes[..size], size, simdBaseType);
                            return new OperandDesc(Compiler.eeFindJitDataOffs(offsetInData));
                        }

                        return genOperandDesc(ins, child);
                    }

                    default:
                    {
                        assert(intrinsic.IsMemoryLoad());
                        assert(intrinsic.Operands.Length == 1);
                        address = intrinsic.GetOp(1);
                        break;
                    }
                }
#else
                throw new FatalJitException(CORJIT_SKIPPED, "Hardware instruction operands require hardware intrinsics.");
#endif
            }

            if (address.IsContained && (address.Oper is GT_LCL_ADDR))
            {
                varNum = address.AsLclFld().LclNum;
                offset = address.AsLclFld().LclOffs;
            }
            else
            {
                return memIndir is not null ? new OperandDesc(memIndir) : new OperandDesc(op.Type, address);
            }
        }
        else
        {
            switch (op.Oper)
            {
                case GT_LCL_FLD:
                {
                    varNum = op.AsLclFld().LclNum;
                    offset = op.AsLclFld().LclOffs;
                    break;
                }

                case GT_LCL_VAR:
                {
                    assert(op.IsRegOptional || !_compiler.lvaGetDesc(op.AsLclVar().LclNum).lvIsRegCandidate);
                    varNum = op.AsLclVar().LclNum;
                    offset = 0;
                    break;
                }

                case GT_CNS_DBL:
                {
                    return new OperandDesc(Emitter.emitFltOrDblConst(op.AsDblCon().DconVal, op.Type.EmitSize));
                }

                case GT_CNS_INT:
                {
                    assert(op.IsContainedIntOrIImmed);
                    return new OperandDesc(op.AsIntCon().IconValue, op.AsIntCon().ImmedValNeedsReloc(_compiler));
                }

#if FEATURE_SIMD
                case GT_CNS_VEC:
                {
                    var tupleType = Emitter.insTupleTypeInfo(ins);
                    var size = (int)op.Type.Size;
                    if (tupleType is INS_TT_TUPLE1_SCALAR or INS_TT_TUPLE1_FIXED)
                    {
                        // Scalar instructions read only the initial lane of a vector constant.
                        size = Math.Max(instInputSize(ins), 4);
                        assert(size <= op.Type.Size);
                    }

                    return new OperandDesc(Emitter.emitSimdConst(in op.AsVecCon().SimdVal, (emitAttr)size));
                }
#endif
#if FEATURE_MASKED_HW_INTRINSICS
                case GT_CNS_MSK:
                {
                    return new OperandDesc(Emitter.emitSimdMaskConst(op.AsMskCon().SimdMaskVal));
                }
#endif
                default:
                {
                    unreached();
                    break;
                }
            }
        }

        assert((varNum != BAD_VAR_NUM) || (temp is not null));
        assert(offset != ushort.MaxValue);
        return new OperandDesc(varNum, offset);
#endif
    }

    public unsafe void inst_RV_TT(instruction ins, emitAttr size, regNumber op1Reg, GenTree op2)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register/operand instruction generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var descriptor = genOperandDesc(ins, op2);
        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_R_C(ins, size, op1Reg, descriptor.GetFieldHnd(), 0);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_R_S(ins, size, op1Reg, descriptor.GetVarNum(), descriptor.GetLclOffset());
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_R_A(ins, size, op1Reg, descriptor.GetIndirForm());
                break;
            }

            case OperandKind.Imm:
            {
                Emitter.emitIns_R_I(ins, descriptor.GetEmitAttrForImmediate(size), op1Reg, descriptor.GetImmediate());
                break;
            }

            case OperandKind.Reg:
            {
                if (Emitter.IsMovInstruction(ins))
                {
                    _ = Emitter.emitIns_Mov(ins, size, op1Reg, descriptor.GetReg(), canSkip: true);
                }
                else
                {
                    Emitter.emitIns_R_R(ins, size, op1Reg, descriptor.GetReg());
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif
    }

    public unsafe void inst_RV_TT_IV(instruction ins, emitAttr attr, regNumber reg,
        GenTree operand, int value, insOpts options)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register/memory/immediate instruction generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        noway_assert(Emitter.emitVerifyEncodable(ins, EA_SIZE(attr), reg));

#if FEATURE_HW_INTRINSICS
        if (IsEmbeddedBroadcastEnabled(ins, operand))
        {
            options = AddEmbBroadcastMode(options);
        }
        else if ((options == INS_OPTS_NONE) && !Emitter.IsVexEncodableInstruction(ins))
        {
            ins = ins switch
            {
                INS_vextractf64x2 => INS_vextractf32x4,
                INS_vextracti64x2 => INS_vextracti32x4,
                _ => ins,
            };
        }
#endif

        var descriptor = genOperandDesc(ins, operand);
        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_R_C_I(ins, attr, reg, descriptor.GetFieldHnd(), 0, value, options);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_R_S_I(ins, attr, reg, descriptor.GetVarNum(), descriptor.GetLclOffset(), value, options);
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_R_A_I(ins, attr, reg, descriptor.GetIndirForm(), value, options);
                break;
            }

            case OperandKind.Reg:
            {
                Emitter.emitIns_SIMD_R_R_I(ins, attr, reg, descriptor.GetReg(), value, options);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif
    }

    public unsafe void inst_RV_RV_TT_IV(instruction ins, emitAttr size, regNumber targetReg,
        regNumber op1Reg, GenTree op2, sbyte immediate, bool isRMW, insOpts options)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD register/operand/immediate generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        noway_assert(Emitter.emitVerifyEncodable(ins, EA_SIZE(size), op1Reg));
#if FEATURE_HW_INTRINSICS
        if (IsEmbeddedBroadcastEnabled(ins, op2))
        {
            options = AddEmbBroadcastMode(options);
        }
        else if ((options == INS_OPTS_NONE) && !Emitter.IsVexEncodableInstruction(ins))
        {
            ins = ins switch
            {
                INS_vinsertf64x2 => INS_vinsertf32x4,
                INS_vinserti64x2 => INS_vinserti32x4,
                _ => ins,
            };
        }
#endif

        var descriptor = genOperandDesc(ins, op2);
        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_SIMD_R_R_C_I(ins, size, targetReg, op1Reg,
                    descriptor.GetFieldHnd(), 0, immediate, options);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_SIMD_R_R_S_I(ins, size, targetReg, op1Reg,
                    descriptor.GetVarNum(), descriptor.GetLclOffset(), immediate, options);
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_SIMD_R_R_A_I(ins, size, targetReg, op1Reg,
                    descriptor.GetIndirForm(), immediate, options);
                break;
            }

            case OperandKind.Reg:
            {
                var op2Reg = descriptor.GetReg();
                if ((op1Reg != targetReg) && (op2Reg == targetReg) && isRMW)
                {
                    // Noncommutative operations prevent this alias through delay-free allocation.
                    op2Reg = op1Reg;
                    op1Reg = targetReg;
                }

                Emitter.emitIns_SIMD_R_R_R_I(ins, size, targetReg, op1Reg, op2Reg, immediate, options);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif
    }

#if TARGET_XARCH && FEATURE_HW_INTRINSICS
    public static insOpts AddEmbBroadcastMode(insOpts options)
    {
        assert((options & INS_OPTS_EVEX_b_MASK) == 0);
        return options | INS_OPTS_EVEX_eb;
    }
#endif

    public unsafe void inst_RV_RV_TT(instruction ins, emitAttr size, regNumber targetReg,
        regNumber op1Reg, GenTree op2, bool isRMW, insOpts options)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Three-operand register/memory instruction generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        noway_assert(Emitter.emitVerifyEncodable(ins, EA_SIZE(size), targetReg));

#if FEATURE_HW_INTRINSICS
        if (IsEmbeddedBroadcastEnabled(ins, op2))
        {
            options = AddEmbBroadcastMode(options);
        }
        else if ((options == INS_OPTS_NONE) && !Emitter.IsVexEncodableInstruction(ins))
        {
            ins = ins switch
            {
                INS_vpandq => INS_pandd,
                INS_vpandnq => INS_pandnd,
                INS_vporq => INS_pord,
                INS_vpxorq => INS_pxord,
                _ => ins,
            };
        }
#endif

        var descriptor = genOperandDesc(ins, op2);
        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_SIMD_R_R_C(ins, size, targetReg, op1Reg, descriptor.GetFieldHnd(), 0, options);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_SIMD_R_R_S(ins, size, targetReg, op1Reg,
                    descriptor.GetVarNum(), descriptor.GetLclOffset(), options);
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_SIMD_R_R_A(ins, size, targetReg, op1Reg, descriptor.GetIndirForm(), options);
                break;
            }

            case OperandKind.Reg:
            {
                var op2Reg = descriptor.GetReg();
                if ((op1Reg != targetReg) && (op2Reg == targetReg) && isRMW)
                {
                    // Commutative RMW operations may alias the second source.
                    // Noncommutative operations prevent this through delay-free allocation.
                    op2Reg = op1Reg;
                    op1Reg = targetReg;
                }

                Emitter.emitIns_SIMD_R_R_R(ins, size, targetReg, op1Reg, op2Reg, options);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif
    }
}
