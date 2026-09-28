// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public struct Arm64SimdScalableConstInfo
{
    public var_types baseType;
    public long indexImm;
    public long stepImm;
    public bool indexHasImm;
    public bool stepHasImm;
    public ulong indexVal;
    public ulong stepVal;

    public static Arm64SimdScalableConstInfo Decode(in simdscalable_t simdVal)
    {
        var info = new Arm64SimdScalableConstInfo {
            baseType = simdVal.BaseType,
            indexImm = -1,
            stepImm = -1,
            indexHasImm = true,
            stepHasImm = true,
        };

        switch (info.baseType)
        {
            case TYP_BYTE:
            {
                info.indexImm = simdVal.Index.i8[0];
                info.stepImm = simdVal.Step.i8[0];
                info.indexVal = unchecked((ulong)info.indexImm);
                info.stepVal = unchecked((ulong)info.stepImm);
                break;
            }

            case TYP_SHORT:
            {
                info.indexImm = simdVal.Index.i16[0];
                info.stepImm = simdVal.Step.i16[0];
                info.indexVal = unchecked((ulong)info.indexImm);
                info.stepVal = unchecked((ulong)info.stepImm);
                break;
            }

            case TYP_INT:
            {
                info.indexImm = simdVal.Index.i32[0];
                info.stepImm = simdVal.Step.i32[0];
                info.indexVal = unchecked((ulong)info.indexImm);
                info.stepVal = unchecked((ulong)info.stepImm);
                break;
            }

            case TYP_LONG:
            {
                info.indexImm = simdVal.Index.i64[0];
                info.stepImm = simdVal.Step.i64[0];
                info.indexVal = simdVal.Index.u64[0];
                info.stepVal = simdVal.Step.u64[0];
                break;
            }

            case TYP_UBYTE:
            {
                info.indexImm = simdVal.Index.u8[0];
                info.stepImm = simdVal.Step.u8[0];
                info.indexVal = simdVal.Index.u8[0];
                info.stepVal = simdVal.Step.u8[0];
                break;
            }

            case TYP_USHORT:
            {
                info.indexImm = simdVal.Index.u16[0];
                info.stepImm = simdVal.Step.u16[0];
                info.indexVal = simdVal.Index.u16[0];
                info.stepVal = simdVal.Step.u16[0];
                break;
            }

            case TYP_UINT:
            {
                info.indexImm = simdVal.Index.u32[0];
                info.stepImm = simdVal.Step.u32[0];
                info.indexVal = simdVal.Index.u32[0];
                info.stepVal = simdVal.Step.u32[0];
                break;
            }

            case TYP_ULONG:
            {
                info.indexVal = simdVal.Index.u64[0];
                info.stepVal = simdVal.Step.u64[0];
                if (info.indexVal <= long.MaxValue)
                {
                    info.indexImm = (long)info.indexVal;
                }
                else
                {
                    info.indexHasImm = false;
                }

                if (info.stepVal <= long.MaxValue)
                {
                    info.stepImm = (long)info.stepVal;
                }
                else
                {
                    info.stepHasImm = false;
                }
                break;
            }

            case TYP_FLOAT:
            {
                info.indexVal = simdVal.Index.u32[0];
                info.stepVal = simdVal.Step.u32[0];
                info.indexHasImm = false;
                info.stepHasImm = false;
                break;
            }

            case TYP_DOUBLE:
            {
                info.indexVal = simdVal.Index.u64[0];
                info.stepVal = simdVal.Step.u64[0];
                info.indexHasImm = false;
                info.stepHasImm = false;
                break;
            }

            default:
            {
                unreached();
                throw new FatalJitException("Unexpected ARM64 scalable constant base type.");
            }
        }

        return info;
    }

    public readonly bool Has64BitElements() => baseType is TYP_LONG or TYP_ULONG or TYP_DOUBLE;

    public readonly bool CanEncodeRepeated(in simdscalable_t simdVal)
    {
        if (varTypeIsIntegral(baseType))
        {
            return indexHasImm && ((indexImm is >= -128 and <= 127) ||
                ((indexImm % 256 == 0) && (indexImm / 256 is >= -128 and <= 127)));
        }

        if (baseType == TYP_FLOAT)
        {
            return Emitter.emitIns_valid_imm_for_fmov(simdVal.Index.f32[0]);
        }

        assert(baseType == TYP_DOUBLE);

        return Emitter.emitIns_valid_imm_for_fmov(simdVal.Index.f64[0]);
    }

    public readonly bool CanEncodeSequenceIndex() => indexHasImm && indexImm is >= -16 and <= 15;

    public readonly bool CanEncodeSequenceStep() => stepHasImm && stepImm is >= -16 and <= 15;

    public readonly bool CanEncodeSequence() => CanEncodeSequenceIndex() && CanEncodeSequenceStep();

    public readonly bool IndexNeedsSequenceReg() => !CanEncodeSequenceIndex();

    public readonly bool StepNeedsSequenceReg() => !CanEncodeSequenceStep();

    public readonly bool CanEncodeScalar(in simdscalable_t simdVal, emitAttr emitSize)
    {
        if (varTypeIsIntegral(baseType))
        {
            // Integral SIMD immediates are vector-wide, not scalar element forms.
            return false;
        }

        if (baseType == TYP_FLOAT)
        {
            return Emitter.emitIns_valid_imm_for_fmov(simdVal.Index.f32[0]);
        }

        assert(baseType == TYP_DOUBLE);

        return Emitter.emitIns_valid_imm_for_fmov(simdVal.Index.f64[0]);
    }
}
#endif
