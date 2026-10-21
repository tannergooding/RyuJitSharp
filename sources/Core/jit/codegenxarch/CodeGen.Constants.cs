// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genSetRegToConst(regNumber targetReg, var_types targetType, in simd_t value)
    {
        if (!Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "SIMD constants require SIMD support.");
            return;
        }

        var emit = Emitter;
        emit.RequireSupportedInstructionRecording();
        var attr = targetType.EmitSize;

        switch (targetType)
        {
            case TYP_SIMD8:
            {
                var value8 = value.v64[0];
                if (value8.IsAllBitsSet)
                {
                    if (Emitter.isHighSimdReg(targetReg))
                    {
                        assert(_compiler.canUseEvexEncodingDebugOnly());
                        emit.emitIns_SIMD_R_R_R_I(INS_vpternlogd, attr, targetReg, targetReg, targetReg, -1, INS_OPTS_NONE);
                    }
                    else
                    {
                        emit.emitIns_SIMD_R_R_R(INS_pcmpeqd, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                    }
                }
                else if (value8.IsZero)
                {
                    emit.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                }
                else
                {
                    emit.emitSimdConstCompressedLoad(in value, attr, targetReg);
                }
                break;
            }

            case TYP_SIMD12:
            {
                if (value.v64[0].IsAllBitsSet && (value.u32[2] == uint.MaxValue))
                {
                    if (Emitter.isHighSimdReg(targetReg))
                    {
                        assert(_compiler.canUseEvexEncodingDebugOnly());
                        emit.emitIns_SIMD_R_R_R_I(INS_vpternlogd, attr, targetReg, targetReg, targetReg, -1, INS_OPTS_NONE);
                    }
                    else
                    {
                        emit.emitIns_SIMD_R_R_R(INS_pcmpeqd, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                    }
                }
                else if (value.v64[0].IsZero && (value.u32[2] == 0))
                {
                    emit.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                }
                else
                {
                    emit.emitSimdConstCompressedLoad(in value, EA_16BYTE, targetReg);
                }
                break;
            }

            case TYP_SIMD16:
            {
                var value16 = value.v128[0];
                if (value16.IsAllBitsSet)
                {
                    if (Emitter.isHighSimdReg(targetReg))
                    {
                        assert(_compiler.canUseEvexEncodingDebugOnly());
                        emit.emitIns_SIMD_R_R_R_I(INS_vpternlogd, attr, targetReg, targetReg, targetReg, -1, INS_OPTS_NONE);
                    }
                    else
                    {
                        emit.emitIns_SIMD_R_R_R(INS_pcmpeqd, attr, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                    }
                }
                else if (value16.IsZero)
                {
                    emit.emitIns_SIMD_R_R_R(INS_xorps, attr, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                }
                else
                {
                    emit.emitSimdConstCompressedLoad(in value, attr, targetReg);
                }
                break;
            }

            case TYP_SIMD32:
            {
                var value32 = value.v256[0];
                if (value32.IsAllBitsSet && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    if (Emitter.isHighSimdReg(targetReg))
                    {
                        assert(_compiler.canUseEvexEncodingDebugOnly());
                        emit.emitIns_SIMD_R_R_R_I(INS_vpternlogd, attr, targetReg, targetReg, targetReg, -1, INS_OPTS_NONE);
                    }
                    else
                    {
                        emit.emitIns_SIMD_R_R_R(INS_pcmpeqd, attr, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                    }
                }
                else if (value32.IsZero)
                {
                    // A 128-bit VEX/EVEX zero also clears the upper vector state without dirtying it.
                    emit.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                }
                else
                {
                    emit.emitSimdConstCompressedLoad(in value, attr, targetReg);
                }
                break;
            }

            case TYP_SIMD64:
            {
                if (value.IsAllBitsSet && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    emit.emitIns_SIMD_R_R_R_I(INS_vpternlogd, attr, targetReg, targetReg, targetReg, -1, INS_OPTS_NONE);
                }
                else if (value.IsZero)
                {
                    emit.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                }
                else
                {
                    emit.emitSimdConstCompressedLoad(in value, attr, targetReg);
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

    public unsafe void genSetRegToConst(regNumber targetReg, var_types targetType, in simdmask_t value)
    {
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Mask constants require masked hardware-intrinsic support.");
            return;
        }

        var emit = Emitter;
        emit.RequireSupportedInstructionRecording();
        assert(varTypeIsMask(targetType));
        var attr = targetType.EmitSize;

        if (value.IsAllBitsSet)
        {
            emit.emitIns_SIMD_R_R_R(INS_kxnorq, EA_8BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
        }
        else if (value.IsZero)
        {
            emit.emitIns_SIMD_R_R_R(INS_kxorq, EA_8BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
        }
        else
        {
            var handle = emit.emitSimdMaskConst(value);
            emit.emitIns_R_C(ins_Load(targetType), attr, targetReg, handle, 0);
        }
    }

    public unsafe void genSetRegToConstXarch(regNumber targetReg, var_types targetType, GenTree tree)
    {
        Emitter.RequireSupportedInstructionRecording();
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
                    else if (constant.IsIconHandle(GTF_ICON_TLSGD_OFFSET))
                    {
                        attr |= EA_CNS_TLSGD_RELOC;

                        // TLS linker relaxation may trash RAX; native updates GC state before recording.
                        _gcInfo.gcMarkRegSetNpt(RBM_RAX);
                    }
                }

                instGen_Set_Reg_To_ImmXarch(attr, targetReg, value, INS_FLAGS_DONT_CARE,
                    targetHandle: Configuration.DEBUG ? unchecked((nuint)constant.TargetHandle) : 0,
                    gtFlags: Configuration.DEBUG ? constant.Flags : GTF_EMPTY);
                _regSet.verifyRegUsed(targetReg);
                break;
            }

            case GT_CNS_DBL:
            {
                var emit = Emitter;
                var size = targetType.EmitSize;
                if (tree.IsFloatPositiveZero)
                {
                    emit.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                }
                else if (tree.IsFloatAllBitsSet)
                {
                    if (Emitter.isHighSimdReg(targetReg))
                    {
                        assert(_compiler.canUseEvexEncodingDebugOnly());
                        emit.emitIns_SIMD_R_R_R_I(INS_vpternlogd, EA_16BYTE, targetReg, targetReg, targetReg, -1, INS_OPTS_NONE);
                    }
                    else
                    {
                        emit.emitIns_SIMD_R_R_R(INS_pcmpeqd, EA_16BYTE, targetReg, targetReg, targetReg, INS_OPTS_NONE);
                    }
                }
                else
                {
                    var constant = tree.AsDblCon().DconVal;
                    var handle = emit.emitFltOrDblConst(constant, size);
                    emit.emitIns_R_C(ins_Load(targetType), size, targetReg, handle, 0);
                }
                break;
            }

            case GT_CNS_VEC when Configuration.FEATURE_SIMD:
            {
                var vector = tree.AsVecCon();
                genSetRegToConst(vector.RegNum, targetType, in vector.SimdVal);
                break;
            }
            case GT_CNS_MSK when Configuration.FEATURE_MASKED_HW_INTRINSICS:
            {
                var mask = tree.AsMskCon();
                genSetRegToConst(mask.RegNum, targetType, in mask.SimdMaskVal);
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }
    }
}
