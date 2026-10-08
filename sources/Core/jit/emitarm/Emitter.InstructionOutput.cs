// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe nuint emitOutputInstrArm32(insGroup ig, instrDesc id, byte** dp)
    {
        unchecked
        {
            var compiler = _compiler ?? throw new InvalidOperationException("Emitter is not initialized.");
            var dst = *dp;
            var originalDst = dst;
            uint code = 0;
            nuint size = 0;
            var ins = id.idIns();
            var format = id.idInsFmt();
            var opSize = id.idOpSize();
            byte callInstructionSize = 0;
#if DEBUG
            var displayOffsets = compiler.opts.dspGCtbls || !compiler.opts.disDiffable;
#endif

            assert((int)REG_NA == (int)REG_NA);

            VARSET_TP gcVars = [];
            regMaskTP gcRefRegisters = default;
            regMaskTP byRefRegisters = default;
            int immediate;
            byte* address;

            switch (format)
            {
                case IF_T1_A:
                {
                    size = SMALL_IDSC_SIZE;
                    code = emitInsCode(ins, format);
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

#if FEATURE_ITINSTRUCTION
                case IF_T1_B:
                {
                    assert(id.idGCref() == GCT_NONE);
                    dst = emitOutputIT(dst, ins, format, (uint)emitGetInsSC(id));
                    size = SMALL_IDSC_SIZE;
                    break;
                }
#endif

                case IF_T1_C:
                {
                    size = SMALL_IDSC_SIZE;
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_D3(id.idReg1());
                    code |= insEncodeRegT1_N3(id.idReg2());
                    immediate = insUnscaleImm(ins, immediate);
                    assert((immediate & 0x001F) == immediate);
                    code |= (uint)immediate << 6;
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T1_D0:
                {
                    size = SMALL_IDSC_SIZE;
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_D4(id.idReg1());
                    code |= insEncodeRegT1_M4(id.idReg2());
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T1_E:
                {
                    size = SMALL_IDSC_SIZE;
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_D3(id.idReg1());
                    code |= insEncodeRegT1_N3(id.idReg2());
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T1_F:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    assert(ins is INS_add or INS_sub);
                    assert((immediate & 0x0003) == 0);
                    immediate >>= 2;
                    assert((immediate & 0x007F) == immediate);
                    code |= (uint)immediate;
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T1_G:
                {
                    size = SMALL_IDSC_SIZE;
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_D3(id.idReg1());
                    code |= insEncodeRegT1_N3(id.idReg2());
                    assert((immediate & 0x0007) == immediate);
                    code |= (uint)immediate << 6;
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T1_H:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_D3(id.idReg1());
                    code |= insEncodeRegT1_N3(id.idReg2());
                    code |= insEncodeRegT1_M3(id.idReg3());
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T1_I:
                {
                    assert(id.idIsBound());
                    dst = emitOutputLJ(ig, dst, id);
                    size = (nuint)DescriptorSizes.Jump;
                    break;
                }

                case IF_T1_J0:
                case IF_T1_J1:
                case IF_T1_J2:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_DI(id.idReg1());
                    if (format == IF_T1_J2)
                    {
                        assert(ins is INS_add or INS_ldr or INS_str);
                        assert((immediate & 0x0003) == 0);
                        immediate >>= 2;
                    }
                    assert((immediate & 0x00FF) == immediate);
                    code |= (uint)immediate;
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T1_L0:
                case IF_T1_L1:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    if (format == IF_T1_L1)
                    {
                        assert((immediate & 0x3) != 0x3);
                        if ((immediate & 0x3) != 0)
                        {
                            code |= 0x0100;
                        }
                        immediate >>= 2;
                    }
                    assert((immediate & 0x00FF) == immediate);
                    code |= (uint)immediate;
                    dst += emitOutput_Thumb1Instr(dst, code);
                    break;
                }

                case IF_T2_A:
                {
                    size = SMALL_IDSC_SIZE;
                    code = emitInsCode(ins, format);
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_B:
                {
                    size = SMALL_IDSC_SIZE;
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    assert((immediate & 0x000F) == immediate);
                    code |= (uint)immediate;
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_C0:
                case IF_T2_C4:
                case IF_T2_C5:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    code |= insEncodeRegT2_N(id.idReg2());
                    code |= insEncodeRegT2_M(id.idReg3());
                    if (format != IF_T2_C5)
                    {
                        code |= insEncodeSetFlags(id.idInsFlags());
                    }
                    if (format == IF_T2_C0)
                    {
                        immediate = unchecked((int)emitGetInsSC(id));
                        code |= insEncodeShiftCount(immediate);
                        code |= insEncodeShiftOpts(id.idInsOpt());
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_C1:
                case IF_T2_C2:
                case IF_T2_C6:
                {
                    size = SMALL_IDSC_SIZE;
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    code |= insEncodeRegT2_M(id.idReg2());
                    if (format == IF_T2_C6)
                    {
                        assert((immediate & 0x0018) == immediate);
                        code |= (uint)immediate << 1;
                    }
                    else
                    {
                        code |= insEncodeSetFlags(id.idInsFlags());
                        code |= insEncodeShiftCount(immediate);
                        if (format == IF_T2_C1)
                        {
                            code |= insEncodeShiftOpts(id.idInsOpt());
                        }
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_C3:
                {
                    size = SMALL_IDSC_SIZE;
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    code |= insEncodeRegT2_M(id.idReg2());
                    code |= insEncodeSetFlags(id.idInsFlags());
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_C7:
                case IF_T2_C8:
                {
                    size = SMALL_IDSC_SIZE;
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_N(id.idReg1());
                    code |= insEncodeRegT2_M(id.idReg2());
                    if (format == IF_T2_C7)
                    {
                        assert((immediate & 0x0003) == immediate);
                        code |= (uint)immediate << 4;
                    }
                    else
                    {
                        code |= insEncodeShiftCount(immediate);
                        code |= insEncodeShiftOpts(id.idInsOpt());
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_C9:
                {
                    size = SMALL_IDSC_SIZE;
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_N(id.idReg1());
                    code |= insEncodeRegT2_M(id.idReg2());
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_C10:
                {
                    size = SMALL_IDSC_SIZE;
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    code |= insEncodeRegT2_M(id.idReg2());
                    code |= insEncodeRegT2_N(id.idReg2());
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_D0:
                case IF_T2_D1:
                {
                    size = SMALL_IDSC_SIZE;
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    if (format == IF_T2_D0)
                    {
                        code |= insEncodeRegT2_N(id.idReg2());
                    }
                    code |= insEncodeBitFieldImm(immediate);
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_E0:
                case IF_T2_E1:
                case IF_T2_E2:
                {
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_T(id.idReg1());
                    if (format == IF_T2_E0)
                    {
                        size = (nuint)emitSizeOfInsDsc(id);
                        code |= insEncodeRegT2_N(id.idReg2());
                        if (id.idIsLclVar())
                        {
                            code |= insEncodeRegT2_M(codeGen.rsGetRsvdReg());
                            immediate = 0;
                        }
                        else
                        {
                            code |= insEncodeRegT2_M(id.idReg3());
                            immediate = unchecked((int)emitGetInsSC(id));
                            assert((immediate & 0x0003) == immediate);
                            code |= (uint)immediate << 4;
                        }
                    }
                    else
                    {
                        size = SMALL_IDSC_SIZE;
                        if (format != IF_T2_E2)
                        {
                            code |= insEncodeRegT2_N(id.idReg2());
                        }
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_F1:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_T(id.idReg1());
                    code |= insEncodeRegT2_D(id.idReg2());
                    code |= insEncodeRegT2_N(id.idReg3());
                    code |= insEncodeRegT2_M(id.idReg4());
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_F2:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    code |= insEncodeRegT2_N(id.idReg2());
                    code |= insEncodeRegT2_M(id.idReg3());
                    code |= insEncodeRegT2_T(id.idReg4());
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_G0:
                case IF_T2_G1:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_T(id.idReg1());
                    code |= insEncodeRegT2_D(id.idReg2());
                    code |= insEncodeRegT2_N(id.idReg3());
                    if (format == IF_T2_G0)
                    {
                        immediate = unchecked((int)emitGetInsSC(id));
                        assert(unsigned_abs(immediate) <= 0x00FF);
                        code |= (uint)Math.Abs(immediate);
                        code |= insEncodePUW_G0(id.idInsOpt(), immediate);
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_H0:
                case IF_T2_H1:
                case IF_T2_H2:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_T(id.idReg1());
                    if (format != IF_T2_H2)
                    {
                        code |= insEncodeRegT2_N(id.idReg2());
                    }
                    if (format == IF_T2_H0)
                    {
                        assert(unsigned_abs(immediate) <= 0x00FF);
                        code |= insEncodePUW_H0(id.idInsOpt(), immediate);
                        code |= unchecked((uint)Math.Abs(immediate));
                    }
                    else
                    {
                        assert((immediate & 0x00FF) == immediate);
                        code |= (uint)immediate;
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_I0:
                case IF_T2_I1:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    if (format == IF_T2_I0)
                    {
                        code |= insEncodeRegT2_N(id.idReg1());
                        code |= 1u << 21;
                    }
                    immediate = unchecked((int)emitGetInsSC(id));
                    assert((immediate & 0x3) != 0x3);
                    if ((immediate & 0x2) != 0)
                    {
                        code |= 0x8000;
                    }
                    if ((immediate & 0x1) != 0)
                    {
                        code |= 0x4000;
                    }
                    immediate >>= 2;
                    assert(immediate <= 0x1FFF);
                    code |= (uint)immediate;
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_K1:
                case IF_T2_K4:
                case IF_T2_K3:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    if (format != IF_T2_K3)
                    {
                        code |= insEncodeRegT2_T(id.idReg1());
                    }
                    if (format == IF_T2_K1)
                    {
                        code |= insEncodeRegT2_N(id.idReg2());
                        assert(immediate <= 0x0FFF);
                        code |= (uint)immediate;
                    }
                    else
                    {
                        assert(unsigned_abs(immediate) <= 0x0FFF);
                        code |= (uint)Math.Abs(immediate);
                        if (immediate >= 0)
                        {
                            code |= 1u << 23;
                        }
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_K2:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_N(id.idReg1());
                    assert(immediate <= 0x0FFF);
                    code |= (uint)immediate;
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_L0:
                case IF_T2_L1:
                case IF_T2_L2:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    if (format == IF_T2_L2)
                    {
                        code |= insEncodeRegT2_N(id.idReg1());
                    }
                    else
                    {
                        code |= insEncodeSetFlags(id.idInsFlags());
                        code |= insEncodeRegT2_D(id.idReg1());
                        if (format == IF_T2_L0)
                        {
                            code |= insEncodeRegT2_N(id.idReg2());
                        }
                    }
                    assert(isModImmConst(immediate));
                    immediate = encodeModImmConst(immediate);
                    assert(immediate <= 0x0FFF);
                    code |= (uint)immediate & 0x00FF;
                    code |= ((uint)immediate & 0x0700) << 4;
                    code |= ((uint)immediate & 0x0800) << 15;
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_M0:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    immediate = unchecked((int)emitGetInsSC(id));
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    code |= insEncodeRegT2_N(id.idReg2());
                    assert(immediate <= 0x0FFF);
                    code |= (uint)immediate & 0x00FF;
                    code |= ((uint)immediate & 0x0700) << 4;
                    code |= ((uint)immediate & 0x0800) << 15;
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_N:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    immediate = unchecked((int)emitGetInsSC(id));
                    if (id.idIsLclVar())
                    {
                        if (ins == INS_movw)
                        {
                            immediate &= 0xFFFF;
                        }
                        else
                        {
                            assert(ins == INS_movt);
                            immediate = (immediate >> 16) & 0xFFFF;
                        }
                    }
                    assert(!id.idIsReloc());
                    code |= insEncodeImmT2_Mov(immediate);
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_N2:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    immediate = unchecked((int)emitGetInsSC(id));
                    address = emitDataOffsetToPtr(unchecked((uint)immediate));
                    if (!id.idIsReloc())
                    {
                        assert(TARGET_POINTER_SIZE == sizeof(uint));
                        immediate = unchecked((int)(uint)(nuint)address);
                        if (ins == INS_movw)
                        {
                            immediate &= 0xFFFF;
                        }
                        else
                        {
                            assert(ins == INS_movt);
                            immediate = (immediate >> 16) & 0xFFFF;
                        }
                        code |= insEncodeImmT2_Mov(immediate);
                        dst += emitOutput_Thumb2Instr(dst, code);
                    }
                    else
                    {
                        assert(ins is INS_movt or INS_movw);
                        dst += emitOutput_Thumb2Instr(dst, code);
                        if ((ins == INS_movt) && compiler.info.compMatchedVM)
                        {
                            emitHandlePCRelativeMov32(dst - 8, address);
                        }
                    }
                    break;
                }

                case IF_T2_N3:
                {
                    size = INSTR_DESC_SIZE + sizeof(uint);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_D(id.idReg1());
                    assert(ins is INS_movt or INS_movw);
                    assert(id.idIsReloc());
                    address = emitGetInsRelocValue(id);
                    dst += emitOutput_Thumb2Instr(dst, code);
                    if ((ins == INS_movt) && compiler.info.compMatchedVM)
                    {
                        emitHandlePCRelativeMov32(dst - 8, address);
                    }
                    break;
                }

                case IF_T2_VFP3:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_VectorN(id.idReg2(), (int)opSize, true);
                    code |= insEncodeRegT2_VectorM(id.idReg3(), (int)opSize, true);
                    code |= insEncodeRegT2_VectorD(id.idReg1(), (int)opSize, true);
                    if (opSize == EA_8BYTE)
                    {
                        code |= 1u << 8;
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_VFP2:
                {
                    emitAttr sourceSize;
                    emitAttr destinationSize;
                    uint sizeCode = 0;
                    switch (ins)
                    {
                        case INS_vcvt_i2d:
                        case INS_vcvt_u2d:
                        case INS_vcvt_f2d:
                        {
                            sourceSize = EA_4BYTE;
                            destinationSize = EA_8BYTE;
                            break;
                        }

                        case INS_vcvt_d2i:
                        case INS_vcvt_d2u:
                        case INS_vcvt_d2f:
                        {
                            sourceSize = EA_8BYTE;
                            destinationSize = EA_4BYTE;
                            break;
                        }

                        case INS_vmov:
                        case INS_vabs:
                        case INS_vsqrt:
                        case INS_vcmp:
                        case INS_vneg:
                        {
                            if (opSize == EA_8BYTE)
                            {
                                sizeCode |= 1u << 8;
                            }
                            goto default;
                        }

                        default:
                        {
                            sourceSize = destinationSize = opSize;
                            break;
                        }
                    }

                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= sizeCode;
                    code |= insEncodeRegT2_VectorD(id.idReg1(), (int)destinationSize, true);
                    code |= insEncodeRegT2_VectorM(id.idReg2(), (int)sourceSize, true);
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_VLDST:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT2_N(id.idReg2());
                    code |= insEncodeRegT2_VectorD(id.idReg1(), (int)opSize, true);
                    immediate = unchecked((int)emitGetInsSC(id));
                    if (immediate < 0)
                    {
                        immediate = -immediate;
                    }
                    else
                    {
                        code |= 1u << 23;
                    }
                    assert((immediate % 4) == 0);
                    assert((immediate >> 10) == 0);
                    code |= (uint)immediate >> 2;
                    if (opSize == EA_8BYTE)
                    {
                        code |= 1u << 8;
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_VMOVD:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    if (ins == INS_vmov_i2d)
                    {
                        code |= insEncodeRegT2_VectorM(id.idReg1(), (int)opSize, true);
                        code |= (uint)id.idReg2() << 12;
                        code |= (uint)id.idReg3() << 16;
                    }
                    else
                    {
                        assert(ins == INS_vmov_d2i);
                        code |= (uint)id.idReg1() << 12;
                        code |= (uint)id.idReg2() << 16;
                        code |= insEncodeRegT2_VectorM(id.idReg3(), (int)opSize, true);
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T2_VMOVS:
                {
                    size = (nuint)emitSizeOfInsDsc(id);
                    code = emitInsCode(ins, format);
                    if (ins == INS_vmov_f2i)
                    {
                        code |= insEncodeRegT2_VectorN(id.idReg2(), (int)EA_4BYTE, true);
                        code |= (uint)id.idReg1() << 12;
                    }
                    else
                    {
                        assert(ins == INS_vmov_i2f);
                        code |= insEncodeRegT2_VectorN(id.idReg1(), (int)EA_4BYTE, true);
                        code |= (uint)id.idReg2() << 12;
                    }
                    dst += emitOutput_Thumb2Instr(dst, code);
                    break;
                }

                case IF_T1_J3:
                case IF_T2_M1:
                {
                    assert(id.idGCref() == GCT_NONE);
                    assert(id.idIsBound());
                    dst = emitOutputLJ(ig, dst, id);
                    size = (nuint)DescriptorSizes.Label;
                    break;
                }

                case IF_T1_K:
                case IF_T1_M:
                case IF_T2_J1:
                case IF_T2_J2:
                case IF_T2_N1:
                case IF_LARGEJMP:
                {
                    assert(id.idGCref() == GCT_NONE);
                    assert(id.idIsBound());
                    dst = emitOutputLJ(ig, dst, id);
                    size = (nuint)DescriptorSizes.Jump;
                    break;
                }

                case IF_T1_D1:
                {
                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_M4(id.idReg1());
                    dst += emitOutput_Thumb1Instr(dst, code);
                    size = SMALL_IDSC_SIZE;
                    break;
                }

                case IF_T1_D2:
                {
                    if (id.idIsLargeCall())
                    {
                        var call = (instrDescCGCA)id;
                        gcRefRegisters = call.idcGcrefRegs;
                        byRefRegisters = call.idcByrefRegs;
                        VarSetOps.Assign(compiler, ref gcVars, call.idcGCvars);
                        size = (nuint)call.NativeLogicalSize;
                    }
                    else
                    {
                        assert(!id.idIsLargeDsp());
                        assert(!id.idIsLargeCns());
                        gcRefRegisters = new regMaskTP((regMask)emitDecodeCallGCregs(id));
                        byRefRegisters = default;
                        VarSetOps.AssignNoCopy(compiler, ref gcVars, VarSetOps.MakeEmpty(compiler));
                        size = INSTR_DESC_SIZE;
                    }

                    code = emitInsCode(ins, format);
                    code |= insEncodeRegT1_M4(id.idReg3());
                    callInstructionSize = (byte)emitOutput_Thumb1Instr(dst, code);
                    dst += callInstructionSize;
                    emitOutputCallGCArm32(id, gcVars, gcRefRegisters, byRefRegisters, *dp, dst, callInstructionSize);
                    break;
                }

                case IF_T2_J3:
                {
                    if (id.idIsLargeCall())
                    {
                        var call = (instrDescCGCA)id;
                        gcRefRegisters = call.idcGcrefRegs;
                        byRefRegisters = call.idcByrefRegs;
                        VarSetOps.Assign(compiler, ref gcVars, call.idcGCvars);
                        size = (nuint)call.NativeLogicalSize;
                    }
                    else
                    {
                        assert(!id.idIsLargeDsp());
                        assert(!id.idIsLargeCns());
                        gcRefRegisters = new regMaskTP((regMask)emitDecodeCallGCregs(id));
                        byRefRegisters = default;
                        VarSetOps.AssignNoCopy(compiler, ref gcVars, VarSetOps.MakeEmpty(compiler));
                        size = INSTR_DESC_SIZE;
                    }

                    address = id.idAddr().iiaAddr == null ? emitCodeBlock : id.idAddr().iiaAddr;
                    code = emitInsCode(ins, format);
                    if (id.idIsDspReloc())
                    {
                        callInstructionSize = (byte)emitOutput_Thumb2Instr(dst, code);
                        dst += callInstructionSize;
                        if (compiler.info.compMatchedVM)
                        {
                            emitRecordRelocation(dst - 4, address, CorInfoReloc.ARM32_THUMB_BRANCH24);
                        }
                    }
                    else
                    {
                        address = (byte*)((nuint)address & ~(nuint)1);
                        var displacement = unchecked((nint)(address - (dst + 4)));
                        var sign = displacement < 0;
                        var i1 = (displacement & 0x00800000) == 0;
                        var i2 = (displacement & 0x00400000) == 0;
                        if (sign)
                        {
                            code |= 1u << 26;
                        }
                        if (sign ^ i1)
                        {
                            code |= 1u << 13;
                        }
                        if (sign ^ i2)
                        {
                            code |= 1u << 11;
                        }

                        var immLo = (displacement & 0x00000FFE) >> 1;
                        var immHi = (displacement & 0x003FF000) >> 12;
                        code |= (uint)immHi << 16;
                        code |= (uint)immLo;
                        assert((Math.Abs(displacement) & 0x00FFFFFE) == Math.Abs(displacement));

                        callInstructionSize = (byte)emitOutput_Thumb2Instr(dst, code);
                        dst += callInstructionSize;
                    }

                    emitOutputCallGCArm32(id, gcVars, gcRefRegisters, byRefRegisters, *dp, dst, callInstructionSize);
                    break;
                }

                default:
                {
#if DEBUG
                    jitprintf($"unexpected format {emitIfName(id.idInsFmt())}\n");
                    assert(false, "don't know how to encode this instruction");
#endif
                    break;
                }
            }

            if (emitInsMayWriteToGCReg(id))
            {
                if (emitInsMayWriteMultipleRegs(id))
                {
                    switch (ins)
                    {
                        case INS_smull:
                        case INS_umull:
                        case INS_smlal:
                        case INS_umlal:
                        case INS_vmov_d2i:
                        {
                            emitGCregDeadUpd(id.idReg1(), dst);
                            emitGCregDeadUpd(id.idReg2(), dst);
                            break;
                        }

                        default:
                        {
                            assert(false);
                            break;
                        }
                    }
                }
                else if (id.idGCref() != GCT_NONE)
                {
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                }
                else
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                }
            }

            if (emitInsWritesToLclVarStackLoc(id))
            {
                var varNum = id.idAddr().iiaLclVar.lvaVarNum();
                var offset = id.idAddr().iiaLclVar.lvaOffset() & ~((uint)TARGET_POINTER_SIZE - 1);
                var addressOffset = compiler.lvaFrameAddress(varNum, true, out _, 0, false);
                if (id.idGCref() != GCT_NONE)
                {
                    emitGCvarLiveUpd(addressOffset + (int)offset, varNum, id.idGCref(), dst
#if DEBUG
                        , (uint)varNum
#endif
                    );
                }
                else
                {
                    var variableType = varNum >= 0
                        ? compiler.lvaTable[varNum].Type
                        : (codeGen.RegSet.tmpFindNum(varNum)
                            ?? throw new InvalidOperationException("The spill temporary must exist.")).tdTempType;
                    if (variableType is TYP_REF or TYP_BYREF)
                    {
                        emitGCvarDeadUpd(addressOffset + (int)offset, dst
#if DEBUG
                            , (uint)varNum
#endif
                        );
                    }
                }
            }

#if DEBUG
            var expectedSize = (nuint)emitSizeOfInsDsc(id);
            assert(size == expectedSize);
            if (compiler.opts.disAsm || compiler.verbose)
            {
                emitDispIns(id, false, displayOffsets, true, emitCurCodeOffs(originalDst), *dp, (nuint)(dst - *dp), ig);
            }
            if (compiler.compDebugBreak
                && (uint)JitConfig.JitBreakEmitOutputInstr == (id.idDebugOnlyInfo()
                    ?? throw new InvalidOperationException("Instruction debug information must exist.")).idNum)
            {
                assert(false, "!\"JitBreakEmitOutputInstr reached\"");
            }
            if (compiler.verbose || compiler.opts.disasmWithGC)
            {
                emitDispGCInfoDelta();
            }
#else
            if (compiler.opts.disAsm)
            {
                var expectedSize = (nuint)emitSizeOfInsDsc(id);
                assert(size == expectedSize);
                emitDispIns(id, false, false, true, emitCurCodeOffs(originalDst), *dp, (nuint)(dst - *dp), ig);
            }
#endif
            assert(*dp != dst);
            *dp = dst;

            return size;
        }
    }

    private unsafe void emitOutputCallGCArm32(instrDesc id, VARSET_TP gcVars, regMaskTP gcRefRegisters,
        regMaskTP byRefRegisters, byte* callStart, byte* dst, byte callInstructionSize)
    {
        var compiler = _compiler ?? throw new InvalidOperationException("Emitter is not initialized.");
        emitUpdateLiveGCvars(gcVars, callStart);
#if DEBUG
        if (compiler.verbose || compiler.opts.disasmWithGC)
        {
            emitDispGCVarDelta();
        }
#endif
        if (id.idGCref() == GCT_GCREF)
        {
            gcRefRegisters |= RBM_R0;
        }
        else if (id.idGCref() == GCT_BYREF)
        {
            byRefRegisters |= RBM_R0;
        }
        if (id.idIsLargeCall() && ((instrDescCGCA)id).hasAsyncContinuationRet())
        {
            gcRefRegisters |= new regMaskTP(SRBM_ASYNC_CONTINUATION_RET);
        }
        if (gcRefRegisters != new regMaskTP(emitThisGCrefRegs))
        {
            emitUpdateLiveGCregs(GCT_GCREF, gcRefRegisters, dst);
        }
        if (byRefRegisters != new regMaskTP(emitThisByrefRegs))
        {
            emitUpdateLiveGCregs(GCT_BYREF, byRefRegisters, dst);
        }
        if (!id.idIsNoGC())
        {
            emitStackPop(dst, true, callInstructionSize, 0);
            if (!emitFullGCinfo)
            {
                emitRecordGCcall(dst, callInstructionSize);
            }
        }
    }
}
#endif
