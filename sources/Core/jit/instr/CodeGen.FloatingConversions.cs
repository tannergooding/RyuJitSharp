// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_FloatConv(var_types to, var_types from)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Floating conversion instruction selection requires AMD64.");
#else
        switch (from)
        {
            case TYP_INT:
            {
                return to switch
                {
                    TYP_FLOAT => INS_cvtsi2ss32,
                    TYP_DOUBLE => INS_cvtsi2sd32,
                    _ => throw new FatalJitException("Invalid integral-to-floating conversion."),
                };
            }

            case TYP_LONG:
            {
                return to switch
                {
                    TYP_FLOAT => INS_cvtsi2ss64,
                    TYP_DOUBLE => INS_cvtsi2sd64,
                    _ => throw new FatalJitException("Invalid integral-to-floating conversion."),
                };
            }

            case TYP_UINT:
            {
                return to switch
                {
                    TYP_FLOAT => INS_vcvtusi2ss32,
                    TYP_DOUBLE => INS_vcvtusi2sd32,
                    _ => throw new FatalJitException("Invalid integral-to-floating conversion."),
                };
            }

            case TYP_ULONG:
            {
                return to switch
                {
                    TYP_FLOAT => INS_vcvtusi2ss64,
                    TYP_DOUBLE => INS_vcvtusi2sd64,
                    _ => throw new FatalJitException("Invalid integral-to-floating conversion."),
                };
            }

            case TYP_FLOAT:
            {
                assert(to == TYP_DOUBLE);
                return INS_cvtss2sd;
            }

            case TYP_DOUBLE:
            {
                assert(to == TYP_FLOAT);
                return INS_cvtsd2ss;
            }

            default:
            {
                throw new FatalJitException("Invalid floating conversion source type.");
            }
        }
#endif
    }

    public void inst_RV_SH(instruction ins, emitAttr size, regNumber reg, uint value,
        insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if TARGET_ARM
        if (value >= 32)
        {
            value &= 0x1f;
        }

        Emitter.emitIns_R_I(ins, size, reg, unchecked((int)value), flags);
#elif TARGET_XARCH
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
        // X64 JB BE only permits encodable counts here. x86 can encode eight bits,
        // then masks the count to five or six bits according to operand width.
        assert(value < 256);
#endif
        ins = genMapShiftInsToShiftByConstantIns(ins, unchecked((int)value));
        if (value == 1)
        {
            Emitter.emitIns_R(ins, size, reg);
        }
        else
        {
            Emitter.emitIns_R_I(ins, size, reg, unchecked((nint)(int)value));
        }
#else
        NYI("inst_RV_SH - unknown target");
        throw new FatalJitException(CORJIT_SKIPPED, "inst_RV_SH - unknown target");
#endif
    }
}
