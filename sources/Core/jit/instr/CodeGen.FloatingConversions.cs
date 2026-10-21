// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_FloatConv(var_types to, var_types from)
    {
        if (Configuration.TARGET_XARCH)
        {
            switch (from)
            {
                case TYP_INT:
                {
                    switch (to)
                    {
                        case TYP_FLOAT:
                        {
                            return INS_cvtsi2ss32;
                        }

                        case TYP_DOUBLE:
                        {
                            return INS_cvtsi2sd32;
                        }

                        default:
                        {
                            FatalJitException.ThrowFatalJitException(
                                CORJIT_INTERNALERROR,
                                FatalJitErrorCode.DirectFailureMessage241
                            );
                            return default!;
                        }
                    }
                }

                case TYP_LONG:
                {
                    switch (to)
                    {
                        case TYP_FLOAT:
                        {
                            return INS_cvtsi2ss64;
                        }

                        case TYP_DOUBLE:
                        {
                            return INS_cvtsi2sd64;
                        }

                        default:
                        {
                            FatalJitException.ThrowFatalJitException(
                                CORJIT_INTERNALERROR,
                                FatalJitErrorCode.DirectFailureMessage241
                            );
                            return default!;
                        }
                    }
                }

                case TYP_UINT:
                {
                    switch (to)
                    {
                        case TYP_FLOAT:
                        {
                            return INS_vcvtusi2ss32;
                        }

                        case TYP_DOUBLE:
                        {
                            return INS_vcvtusi2sd32;
                        }

                        default:
                        {
                            FatalJitException.ThrowFatalJitException(
                                CORJIT_INTERNALERROR,
                                FatalJitErrorCode.DirectFailureMessage241
                            );
                            return default!;
                        }
                    }
                }

                case TYP_ULONG:
                {
                    switch (to)
                    {
                        case TYP_FLOAT:
                        {
                            return INS_vcvtusi2ss64;
                        }

                        case TYP_DOUBLE:
                        {
                            return INS_vcvtusi2sd64;
                        }

                        default:
                        {
                            FatalJitException.ThrowFatalJitException(
                                CORJIT_INTERNALERROR,
                                FatalJitErrorCode.DirectFailureMessage241
                            );
                            return default!;
                        }
                    }
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
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage242
                    );
                    return default!;
                }
            }
        }

        if (Configuration.TARGET_ARM)
        {
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
                            FatalJitException.ThrowFatalJitException("Invalid integer-to-floating conversion.");
                            return default!;
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
                            FatalJitException.ThrowFatalJitException("Invalid unsigned-to-floating conversion.");
                            return default!;
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
                            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "long to float");
                            return default!;
                        }

                        case TYP_DOUBLE:
                        {
                            NYI("long to double");
                            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "long to double");
                            return default!;
                        }

                        default:
                        {
                            unreached();
                            FatalJitException.ThrowFatalJitException("Invalid long-to-floating conversion.");
                            return default!;
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
                            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "float to long");
                            return default!;
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
                            FatalJitException.ThrowFatalJitException("Invalid float conversion.");
                            return default!;
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
                            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "double to long");
                            return default!;
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
                            FatalJitException.ThrowFatalJitException("Invalid double conversion.");
                            return default!;
                        }
                    }
                }

                default:
                {
                    unreached();
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage242
                    );
                    return default!;
                }
            }
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Floating conversion instruction selection requires xarch or ARM.");
        return default!;
    }

    public void inst_RV_SH(instruction ins, emitAttr size, regNumber reg, uint value,
        insFlags flags = INS_FLAGS_DONT_CARE)
    {
        if (Configuration.TARGET_ARM)
        {
            if (value >= 32)
            {
                value &= 0x1f;
            }

            Emitter.emitIns_R_I(ins, size, reg, unchecked((int)value), flags);
        }
        else if (Configuration.TARGET_XARCH)
        {
            if (Configuration.TARGET_AMD64)
            {
                Emitter.RequireSupportedInstructionRecording();
                // X64 JB BE only permits encodable counts here. x86 can encode eight bits,
                // then masks the count to five or six bits according to operand width.
                assert(value < 256);
            }

            ins = genMapShiftInsToShiftByConstantIns(ins, unchecked((int)value));
            if (value == 1)
            {
                Emitter.emitIns_R(ins, size, reg);
            }
            else
            {
                Emitter.emitIns_R_I(ins, size, reg, unchecked((nint)(int)value));
            }
        }
        else
        {
            NYI("inst_RV_SH - unknown target");
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "inst_RV_SH - unknown target");
            return;
        }
    }
}
