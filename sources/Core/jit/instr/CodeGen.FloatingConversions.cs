// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_FloatConv(var_types to, var_types from)
    {
#if TARGET_XARCH
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
#elif TARGET_ARM
        switch (from)
        {
            case TYP_INT:
            {
                switch (to)
                {
                    case TYP_FLOAT:
                    {
                        return INS_vcvt_i2f;
                    }

                    case TYP_DOUBLE:
                    {
                        return INS_vcvt_i2d;
                    }

                    default:
                    {
                        unreached();
                        throw new FatalJitException("Invalid integer-to-floating conversion.");
                    }
                }
            }

            case TYP_UINT:
            {
                switch (to)
                {
                    case TYP_FLOAT:
                    {
                        return INS_vcvt_u2f;
                    }

                    case TYP_DOUBLE:
                    {
                        return INS_vcvt_u2d;
                    }

                    default:
                    {
                        unreached();
                        throw new FatalJitException("Invalid unsigned-to-floating conversion.");
                    }
                }
            }

            case TYP_LONG:
            {
                switch (to)
                {
                    case TYP_FLOAT:
                    {
                        NYI("long to float");
                        throw new FatalJitException(CORJIT_SKIPPED, "long to float");
                    }

                    case TYP_DOUBLE:
                    {
                        NYI("long to double");
                        throw new FatalJitException(CORJIT_SKIPPED, "long to double");
                    }

                    default:
                    {
                        unreached();
                        throw new FatalJitException("Invalid long-to-floating conversion.");
                    }
                }
            }

            case TYP_FLOAT:
            {
                switch (to)
                {
                    case TYP_INT:
                    {
                        return INS_vcvt_f2i;
                    }

                    case TYP_UINT:
                    {
                        return INS_vcvt_f2u;
                    }

                    case TYP_LONG:
                    {
                        NYI("float to long");
                        throw new FatalJitException(CORJIT_SKIPPED, "float to long");
                    }

                    case TYP_DOUBLE:
                    {
                        return INS_vcvt_f2d;
                    }

                    case TYP_FLOAT:
                    {
                        return INS_vmov;
                    }

                    default:
                    {
                        unreached();
                        throw new FatalJitException("Invalid float conversion.");
                    }
                }
            }

            case TYP_DOUBLE:
            {
                switch (to)
                {
                    case TYP_INT:
                    {
                        return INS_vcvt_d2i;
                    }

                    case TYP_UINT:
                    {
                        return INS_vcvt_d2u;
                    }

                    case TYP_LONG:
                    {
                        NYI("double to long");
                        throw new FatalJitException(CORJIT_SKIPPED, "double to long");
                    }

                    case TYP_FLOAT:
                    {
                        return INS_vcvt_d2f;
                    }

                    case TYP_DOUBLE:
                    {
                        return INS_vmov;
                    }

                    default:
                    {
                        unreached();
                        throw new FatalJitException("Invalid double conversion.");
                    }
                }
            }

            default:
            {
                unreached();
                throw new FatalJitException("Invalid floating conversion source type.");
            }
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Floating conversion instruction selection requires xarch or ARM.");
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
