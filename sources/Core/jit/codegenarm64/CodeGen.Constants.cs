// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.InteropServices;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.SveMaskPattern;
using static RyuJitSharp.insSvePattern;

namespace RyuJitSharp;

public partial class CodeGen
{
    public unsafe void genSetRegToConst(regNumber targetReg, var_types targetType, GenTree tree)
    {
        switch (tree.Oper)
        {
            case GT_CNS_INT:
            {
                var constant = tree.AsIntCon();
                var value = constant.IconValue;
                var attr = targetType.EmitActualSize;
                if (constant.ImmedValNeedsReloc(_compiler))
                {
                    attr |= EA_CNS_RELOC_FLG;
                    if (tree.IsTlsIconHandle())
                    {
                        // TLS addresses are emitted as part of GT_CALL.
                        break;
                    }
                }

                if (targetType == TYP_BYREF)
                {
                    attr |= EA_BYREF_FLG;
                }

                if (_compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI))
                {
                    if (constant.IsIconHandle(GTF_ICON_SECREL_OFFSET))
                    {
                        attr |= EA_CNS_SEC_RELOC;
                    }
                }

                instGen_Set_Reg_To_Imm(attr, targetReg, value, INS_FLAGS_DONT_CARE
#if DEBUG
                    , unchecked((nuint)constant.TargetHandle), constant.Flags
#endif
                    );
                _regSet.verifyRegUsed(targetReg);
                break;
            }

            case GT_CNS_DBL:
            {
                var emit = Emitter;
                var size = tree.Type.EmitActualSize;
                var value = tree.AsDblCon().DconVal;

                // Only positive zero can use MOVI without losing the sign bit.
                if (BitConverter.DoubleToInt64Bits(value) == 0)
                {
                    emit.emitIns_R_I(INS_movi, EA_16BYTE, targetReg, 0, INS_OPTS_16B);
                }
                else if (Emitter.emitIns_valid_imm_for_fmov(value))
                {
                    emit.emitIns_R_F(INS_fmov, size, targetReg, value);
                }
                else
                {
                    var addrReg = _internalRegisters.GetSingle(tree);
                    var handle = emit.emitFltOrDblConst(value, size);
                    emit.emitIns_R_C(INS_ldr, size, targetReg, addrReg, handle, 0);
                }
                break;
            }

#if FEATURE_SIMD
            case GT_CNS_VEC:
            {
                var vector = tree.AsVecCon();
                var emit = Emitter;

                switch (tree.Type)
                {
                    case TYP_SIMD8:
                    case TYP_SIMD12:
                    case TYP_SIMD16:
                    {
                        var attr = targetType.EmitSize;
                        var is8 = tree.Type == TYP_SIMD8;
                        if (vector.IsAllBitsSet)
                        {
                            emit.emitIns_R_I(INS_mvni, attr, targetReg, 0, is8 ? INS_OPTS_2S : INS_OPTS_4S);
                        }
                        else if (vector.IsZero)
                        {
                            emit.emitIns_R_I(INS_movi, attr, targetReg, 0, is8 ? INS_OPTS_2S : INS_OPTS_4S);
                        }
                        else
                        {
                            // SIMD12 uses the full SIMD16 payload for native broadcast selection.
                            var value = vector.SimdVal;
                            if (!value.AsSpan<int>()[..(is8 ? 2 : 4)].ContainsAnyExcept(value.i32[0]) &&
                                Emitter.emitIns_valid_imm_for_movi(value.i32[0], EA_4BYTE))
                            {
                                emit.emitIns_R_I(INS_movi, attr, targetReg, value.i32[0],
                                    is8 ? INS_OPTS_2S : INS_OPTS_4S);
                            }
                            else if (!value.AsSpan<short>()[..(is8 ? 4 : 8)].ContainsAnyExcept(value.i16[0]) &&
                                     Emitter.emitIns_valid_imm_for_movi(value.i16[0], EA_2BYTE))
                            {
                                emit.emitIns_R_I(INS_movi, attr, targetReg, value.i16[0],
                                    is8 ? INS_OPTS_4H : INS_OPTS_8H);
                            }
                            else if (!value.AsSpan<sbyte>()[..(is8 ? 8 : 16)].ContainsAnyExcept(value.i8[0]) &&
                                     Emitter.emitIns_valid_imm_for_movi(value.i8[0], EA_1BYTE))
                            {
                                emit.emitIns_R_I(INS_movi, attr, targetReg, value.i8[0],
                                    is8 ? INS_OPTS_8B : INS_OPTS_16B);
                            }
                            else
                            {
                                var addrReg = _internalRegisters.GetSingle(tree);
                                CORINFO_FIELD_HANDLE handle;
                                if (is8)
                                {
                                    handle = emit.emitSimd8Const(value.v64[0]);
                                }
                                else
                                {
                                    handle = emit.emitSimd16Const(value.v128[0]);
                                }

                                emit.emitIns_R_C(INS_ldr, attr, targetReg, addrReg, handle, 0);
                            }
                        }
                        break;
                    }

                    case TYP_SIMD:
                    {
                        var value = vector.SimdScalableVal;
                        var info = Arm64SimdScalableConstInfo.Decode(value);
                        var baseType = info.baseType;
                        var opt = Emitter.optGetSveInsOpt(baseType.EmitSize);
                        var emitSize = info.Has64BitElements() ? EA_8BYTE : EA_4BYTE;

                        void LoadConstant(regNumber addrReg, ulong constant)
                        {
                            var data = MemoryMarshal.AsBytes(new ReadOnlySpan<ulong>(in constant));
                            var offset = emit.emitDataConst(data, sizeof(ulong), TYP_LONG);
                            var handle = Compiler.eeFindJitDataOffs(offset);
                            emit.emitIns_R_C(INS_ldr, emitSize, addrReg, addrReg, handle, 0);
                        }

                        if (vector.IsZero)
                        {
                            emit.emitInsSve_R_I(INS_sve_dup, EA_SCALABLE, targetReg, 0, opt);
                        }
                        else if (vector.IsAllBitsSet)
                        {
                            emit.emitInsSve_R_I(INS_sve_dup, EA_SCALABLE, targetReg, -1, opt);
                        }
                        else
                        {
                            switch (value.Kind)
                            {
                                case SimdScalableRepeated:
                                {
                                    if (info.CanEncodeRepeated(value))
                                    {
                                        if (varTypeIsIntegral(baseType))
                                        {
                                            emit.emitInsSve_R_I(INS_sve_dup, EA_SCALABLE, targetReg,
                                                unchecked((nint)info.indexImm), opt);
                                        }
                                        else if (baseType == TYP_FLOAT)
                                        {
                                            emit.emitIns_R_F(INS_sve_fdup, EA_SCALABLE, targetReg,
                                                value.Index.f32[0], INS_OPTS_SCALABLE_S);
                                        }
                                        else
                                        {
                                            assert(baseType == TYP_DOUBLE);
                                            emit.emitIns_R_F(INS_sve_fdup, EA_SCALABLE, targetReg,
                                                value.Index.f64[0], INS_OPTS_SCALABLE_D);
                                        }
                                    }
                                    else
                                    {
                                        var indexReg = _internalRegisters.Extract(tree, new regMaskTP(SRBM_ALLINT));
                                        LoadConstant(indexReg, info.indexVal);
                                        emit.emitInsSve_R_R(INS_sve_dup, emitSize, targetReg, indexReg, opt);
                                    }
                                    break;
                                }

                                case SimdScalableSequence:
                                {
                                    assert(varTypeIsIntegral(baseType));
                                    if (info.CanEncodeSequence())
                                    {
                                        emit.emitInsSve_R_I_I(INS_sve_index, EA_SCALABLE, targetReg,
                                            unchecked((nint)info.indexImm), unchecked((nint)info.stepImm), opt);
                                    }
                                    else if (info.CanEncodeSequenceIndex())
                                    {
                                        var stepReg = _internalRegisters.Extract(tree, new regMaskTP(SRBM_ALLINT));
                                        LoadConstant(stepReg, info.stepVal);
                                        emit.emitInsSve_R_R_I(INS_sve_index, emitSize, targetReg, stepReg,
                                            unchecked((nint)info.indexImm), opt, insScalableOpts.INS_SCALABLE_OPTS_IMM_FIRST);
                                    }
                                    else if (info.CanEncodeSequenceStep())
                                    {
                                        var indexReg = _internalRegisters.Extract(tree, new regMaskTP(SRBM_ALLINT));
                                        LoadConstant(indexReg, info.indexVal);
                                        emit.emitInsSve_R_R_I(INS_sve_index, emitSize, targetReg, indexReg,
                                            unchecked((nint)info.stepImm), opt);
                                    }
                                    else
                                    {
                                        var indexReg = _internalRegisters.Extract(tree, new regMaskTP(SRBM_ALLINT));
                                        var stepReg = _internalRegisters.Extract(tree, new regMaskTP(SRBM_ALLINT));
                                        LoadConstant(indexReg, info.indexVal);
                                        LoadConstant(stepReg, info.stepVal);
                                        emit.emitInsSve_R_R_R(INS_sve_index, emitSize, targetReg, indexReg,
                                            stepReg, opt);
                                    }
                                    break;
                                }

                                case SimdScalableScalar:
                                {
                                    // Clear scalable lanes before using a NEON scalar instruction.
                                    emit.emitInsSve_R_I(INS_sve_dup, EA_SCALABLE, targetReg, 0, opt);
                                    if (info.CanEncodeScalar(value, emitSize))
                                    {
                                        if (baseType == TYP_FLOAT)
                                        {
                                            emit.emitIns_R_F(INS_fmov, emitSize, targetReg, value.Index.f32[0]);
                                        }
                                        else
                                        {
                                            assert(baseType == TYP_DOUBLE);
                                            emit.emitIns_R_F(INS_fmov, emitSize, targetReg, value.Index.f64[0]);
                                        }
                                    }
                                    else
                                    {
                                        var indexReg = _internalRegisters.Extract(tree, new regMaskTP(SRBM_ALLINT));
                                        LoadConstant(indexReg, info.indexVal);
                                        emit.emitIns_R_R_I(INS_ins, emitSize, targetReg, indexReg, 0);
                                    }
                                    break;
                                }

                                default:
                                {
                                    unreached();
                                    break;
                                }
                            }
                        }
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case GT_CNS_MSK:
            {
                var mask = tree.AsMskCon();
                var emit = Emitter;
                if (mask.IsZero)
                {
                    emit.emitInsSve_R(INS_sve_pfalse, EA_SCALABLE, targetReg, INS_OPTS_SCALABLE_B);
                    break;
                }

#if DEBUG
                if (JitConfig.JitUseScalableVectorT == 1)
                {
                    assert(mask.SimdScalableMaskVal.Index == 1);
                    var scalableOpt = Emitter.optGetSveInsOpt(mask.SimdScalableMaskVal.BaseType.EmitSize);
                    emit.emitIns_R_PATTERN(INS_sve_ptrue, EA_SCALABLE, targetReg, scalableOpt, SVE_PATTERN_ALL);
                    break;
                }
#endif

                var opt = INS_OPTS_SCALABLE_B;
                var pattern = EvaluateSimdMaskToPattern<simd16_t>(TYP_BYTE, mask.SimdMaskVal);
                if (pattern == SveMaskPatternNone)
                {
                    opt = INS_OPTS_SCALABLE_H;
                    pattern = EvaluateSimdMaskToPattern<simd16_t>(TYP_SHORT, mask.SimdMaskVal);
                }

                if (pattern == SveMaskPatternNone)
                {
                    opt = INS_OPTS_SCALABLE_S;
                    pattern = EvaluateSimdMaskToPattern<simd16_t>(TYP_INT, mask.SimdMaskVal);
                }

                if (pattern == SveMaskPatternNone)
                {
                    opt = INS_OPTS_SCALABLE_D;
                    pattern = EvaluateSimdMaskToPattern<simd16_t>(TYP_LONG, mask.SimdMaskVal);
                }

                if (pattern == SveMaskPatternNone)
                {
                    unreached();
                }

                emit.emitIns_R_PATTERN(INS_sve_ptrue, EA_SCALABLE, targetReg, opt, (insSvePattern)pattern);
                break;
            }
#endif

            default:
            {
                unreached();
                break;
            }
        }
    }
}
#endif
