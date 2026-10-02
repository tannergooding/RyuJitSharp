// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe nuint emitOutputInstrArm64(insGroup ig, instrDesc id, byte** dp)
    {
        unchecked
        {
            var compiler = _compiler ?? throw new InvalidOperationException("Emitter is not initialized.");
            var dst = *dp;
            var odst = dst;
            uint code;
            var sz = (nuint)emitGetInstrDescSizeArm64(id);
            var ins = id.idIns();
            var fmt = id.idInsFmt();
            var size = id.idOpSize();
#if DEBUG
#if DUMP_GC_TABLES
            var dspOffs = compiler.opts.dspGCtbls;
#else
            var dspOffs = !compiler.opts.disDiffable;
#endif
#endif
            assert((int)REG_NA == (int)REG_NA);
            nint imm;
            nint index;
            nint index2;
            uint cmode;
            uint immShift;
            emitAttr elemsize;
            emitAttr datasize;

            switch (fmt)
            {
                case IF_BI_0A:
                case IF_BI_0B:
                case IF_LARGEJMP:
                {
                    assert(id.idGCref() == GCT_NONE);
                    assert(id.idIsBound());
                    dst = emitOutputLJ(ig, dst, id);
                    sz = DescriptorSizes.Jump;
                    break;
                }

                case IF_BI_0C:
                {
                    code = emitInsCode(ins, fmt);
                    sz = id.idIsLargeCall() ? (nuint)Arm64CallDescriptorSize() : DescriptorSizes.Full;
                    dst += emitOutputCallArm64(ig, dst, id, code);
                    emitRecordRelocation(odst, id.idAddr().iiaAddr, CorInfoReloc.ARM64_BRANCH26);
                    break;
                }

                case IF_BI_1A:
                case IF_BI_1B:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    assert(id.idIsBound());
                    dst = emitOutputLJ(ig, dst, id);
                    sz = DescriptorSizes.Jump;
                    break;
                }

                case IF_BR_0A:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    assert(ins is INS_retaa or INS_retab);
                    code = emitInsCode(ins, fmt);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_BR_1A:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    assert(ins is INS_ret or INS_br);
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_BR_1B:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    assert(ins is INS_br_tail or INS_blr);
                    code = emitInsCode(ins, fmt);
                    if (compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && id.idIsTlsGD())
                    {
                        emitRecordRelocation(odst, id.idAddr().iiaAddr, CorInfoReloc.ARM64_LIN_TLSDESC_CALL);
                        code |= insEncodeReg_R(REG_R2, 9, 5);
                    }
                    else
                    {
                        code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    }
                    dst += emitOutputCallArm64(ig, dst, id, code);
                    sz = id.idIsLargeCall() ? (nuint)Arm64CallDescriptorSize() : DescriptorSizes.Full;
                    break;
                }

                case IF_LS_1A:
                case IF_LARGELDC:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    assert(id.idIsBound());
                    dst = emitOutputLJ(ig, dst, id);
                    sz = DescriptorSizes.Jump;
                    break;
                }

                case IF_LS_2A:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    code = emitInsCode(ins, fmt);
                    if (isVectorRegister(id.idReg1()))
                    {
                        code &= 0x3FFFFFFF;
                        code |= insEncodeDatasizeVLS(code, size);
                        code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    }
                    else
                    {
                        code |= insEncodeDatasizeLS(code, size);
                        code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    }
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    if (id.idIsTlsGD())
                    {
                        emitRecordRelocation(odst, (void*)emitGetInsSC(id), CorInfoReloc.ARM64_LIN_TLSDESC_LD64_LO12);
                    }
                    else if (id.idIsReloc())
                    {
                        emitRecordRelocation(odst, (void*)emitGetInsSC(id), CorInfoReloc.ARM64_PAGEOFFSET_12L);
                    }
                    break;
                }

                case IF_LS_2B:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    imm = emitGetInsSC(id);
                    assert(isValidUimm(imm, 12));
                    code = emitInsCode(ins, fmt);
                    if (isVectorRegister(id.idReg1()))
                    {
                        code &= 0x3FFFFFFF;
                        code |= insEncodeDatasizeVLS(code, size);
                        code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    }
                    else
                    {
                        code |= insEncodeDatasizeLS(code, size);
                        code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    }
                    code |= (uint)imm << 10;
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_2C:
                {
                    assert(insOptsNone(id.idInsOpt()) || insOptsIndexed(id.idInsOpt()));
                    imm = emitGetInsSC(id);
                    assert((imm >= -256) && (imm <= 255));
                    imm &= 0x1FF;
                    code = emitInsCode(ins, fmt);
                    if (isVectorRegister(id.idReg1()))
                    {
                        code &= 0x3FFFFFFF;
                        code |= insEncodeDatasizeVLS(code, size);
                        code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    }
                    else
                    {
                        code |= insEncodeDatasizeLS(code, size);
                        code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    }
                    code |= insEncodeIndexedOpt(id.idInsOpt());
                    if (ins == INS_ldapur && isVectorRegister(id.idReg1()))
                    {
                        assert(insOptsNone(id.idInsOpt()));
                        code |= 0x00000800;
                    }
                    code |= (uint)imm << 12;
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);

                    if (insOptsIndexed(id.idInsOpt()) && !id.idIsSmallDsc())
                    {
                        if (emitInsIsLoad(ins))
                        {
                            if (id.idGCref() != GCT_NONE)
                            {
                                emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                            }
                            else
                            {
                                emitGCregDeadUpd(id.idReg1(), dst);
                            }
                        }
                        if (id.idGCrefReg2() != GCT_NONE)
                        {
                            emitGCregLiveUpd(id.idGCrefReg2(), id.idReg2(), dst);
                        }
                        else
                        {
                            emitGCregDeadUpd(id.idReg2(), dst);
                        }
                        goto SKIP_GC_UPDATE;
                    }
                    break;
                }

                case IF_LS_2D:
                case IF_LS_2E:
                {
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeVLSElemsize(elemsize);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_2F:
                case IF_LS_2G:
                {
                    elemsize = size;
                    index = id.idSmallCns();
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVLSIndex(elemsize, index);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_3A:
                {
                    assert(insOptsLSExtend(id.idInsOpt()));
                    code = emitInsCode(ins, fmt);
                    if (isVectorRegister(id.idReg1()))
                    {
                        code &= 0x3FFFFFFF;
                        code |= insEncodeDatasizeVLS(code, size);
                        code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    }
                    else
                    {
                        code |= insEncodeDatasizeLS(code, size);
                        code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    }
                    code |= insEncodeExtend(id.idInsOpt());
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    if (id.idIsLclVar())
                    {
                        code |= insEncodeReg_R(codeGen.rsGetRsvdReg(), 20, 16);
                    }
                    else
                    {
                        code |= insEncodeReg3Scale(id.idReg3Scaled());
                        code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    }
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_3B:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    code = emitInsCode(ins, fmt);
                    if (isVectorRegister(id.idReg1()))
                    {
                        code &= 0x3FFFFFFF;
                        code |= insEncodeDatasizeVPLS(code, size);
                        code |= insEncodeReg_V(id.idReg1(), 4, 0);
                        code |= insEncodeReg_V(id.idReg2(), 14, 10);
                    }
                    else
                    {
                        code |= insEncodeDatasize(size);
                        code |= insEncodeReg_R(id.idReg1(), 4, 0);
                        code |= insEncodeReg_R(id.idReg2(), 14, 10);
                    }
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_3C:
                {
                    assert(insOptsNone(id.idInsOpt()) || insOptsIndexed(id.idInsOpt()));
                    imm = emitGetInsSC(id);
                    assert((imm >= -64) && (imm <= 63));
                    imm &= 0x7F;
                    code = emitInsCode(ins, fmt);
                    if (isVectorRegister(id.idReg1()))
                    {
                        code &= 0x3FFFFFFF;
                        code |= insEncodeDatasizeVPLS(code, size);
                        code |= insEncodeReg_V(id.idReg1(), 4, 0);
                        code |= insEncodeReg_V(id.idReg2(), 14, 10);
                    }
                    else
                    {
                        code |= insEncodeDatasize(size);
                        code |= insEncodeReg_R(id.idReg1(), 4, 0);
                        code |= insEncodeReg_R(id.idReg2(), 14, 10);
                    }
                    code |= insEncodePairIndexedOpt(ins, id.idInsOpt());
                    code |= (uint)imm << 15;
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_3D:
                {
                    code = emitInsCode(ins, fmt);
                    assert(id.idReg1() != id.idReg2());
                    assert(id.idReg1() != id.idReg3());
                    code |= insEncodeDatasizeLS(code, size);
                    code |= insEncodeReg_R(id.idReg1(), 20, 16);
                    code |= insEncodeReg_R(id.idReg2(), 4, 0);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_3E:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasizeLS(code, size);
                    code |= insEncodeReg_R(id.idReg1(), 20, 16);
                    code |= insEncodeReg_R(id.idReg2(), 4, 0);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    if (emitInsDestIsOp2Arm64(ins))
                    {
                        if (id.idGCref() != GCT_NONE)
                        {
                            emitGCregLiveUpd(id.idGCref(), id.idReg2(), dst);
                        }
                        else
                        {
                            emitGCregDeadUpd(id.idReg2(), dst);
                        }
                        goto SKIP_GC_UPDATE;
                    }
                    break;
                }

                case IF_LS_3F:
                {
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeVLSElemsize(elemsize);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_LS_3G:
                {
                    elemsize = size;
                    index = id.idSmallCns();
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVLSIndex(elemsize, index);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_1A:
                {
                    assert(insOptsNone(id.idInsOpt()) || insOptsLSL12(id.idInsOpt()));
                    imm = emitGetInsSC(id);
                    assert(isValidUimm(imm, 12));
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeShiftImm12(id.idInsOpt());
                    code |= (uint)imm << 10;
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_1B:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmHWVal((nuint)imm, size));
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= (uint)imm << 5;
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_1C:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmNRS((nuint)imm, size));
                    code = emitInsCode(ins, fmt);
                    code |= (uint)imm << 10;
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_1D:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmNRS((nuint)imm, size));
                    code = emitInsCode(ins, fmt);
                    code |= (uint)imm << 10;
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_1E:
                case IF_LARGEADR:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    if (id.idIsReloc() && fmt == IF_DI_1E)
                    {
                        code = emitInsCode(ins, fmt);
                        code |= insEncodeReg_R(id.idReg1(), 4, 0);
                        dst += emitOutputLong(dst, code);
                        emitRecordRelocation(odst, id.idAddr().iiaAddr, id.idIsTlsGD()
                            ? CorInfoReloc.ARM64_LIN_TLSDESC_ADR_PAGE21 : CorInfoReloc.ARM64_PAGEBASE_REL21);
                        if (id.idIsTlsGD())
                        {
                            emitGCregDeadUpd(REG_R0, dst);
                        }
                    }
                    else
                    {
                        assert(id.idIsBound());
                        dst = emitOutputLJ(ig, dst, id);
                    }
                    sz = DescriptorSizes.Jump;
                    break;
                }

                case IF_DI_1F:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmCondFlagsImm5(imm));
                    condFlagsImm cfi = new() { immCFVal = (uint)imm };
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    code |= cfi.imm5 << 16;
                    code |= insEncodeFlags(cfi.flags);
                    code |= insEncodeCond(cfi.cond);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_2A:
                {
                    assert(insOptsNone(id.idInsOpt()) || insOptsLSL12(id.idInsOpt()));
                    imm = emitGetInsSC(id);
                    assert(isValidUimm(imm, 12));
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeShiftImm12(id.idInsOpt());
                    code |= (uint)imm << 10;
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    if (id.idIsReloc() && !compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
                    {
                        assert(sz == DescriptorSizes.Full);
                        assert(id.idAddr().iiaAddr != null);
                        emitRecordRelocation(odst, id.idAddr().iiaAddr, CorInfoReloc.ARM64_PAGEOFFSET_12A);
                    }
                    else if (id.idIsTlsGD())
                    {
                        if (TargetOS.IsWindows)
                        {
                            emitRecordRelocation(odst, id.idAddr().iiaAddr, id.idIsReloc()
                                ? CorInfoReloc.ARM64_WIN_TLS_SECREL_HIGH12A : CorInfoReloc.ARM64_WIN_TLS_SECREL_LOW12A);
                        }
                        else
                        {
                            emitRecordRelocation(odst, id.idAddr().iiaAddr, CorInfoReloc.ARM64_LIN_TLSDESC_ADD_LO12);
                        }
                    }
                    else if (id.idIsReloc())
                    {
                        assert(sz == DescriptorSizes.Full);
                        assert(id.idAddr().iiaAddr != null);
                        emitRecordRelocation(odst, id.idAddr().iiaAddr, CorInfoReloc.ARM64_PAGEOFFSET_12A);
                    }
                    break;
                }

                case IF_DI_2B:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    assert(isValidImmShift(imm, size));
                    code |= insEncodeDatasizeBF(code, size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    code |= insEncodeShiftCount(imm, size);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_2C:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmNRS((nuint)imm, size));
                    code = emitInsCode(ins, fmt);
                    code |= (uint)imm << 10;
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DI_2D:
                {
                    if (ins is INS_asr or INS_lsl or INS_lsr)
                    {
                        imm = emitGetInsSC(id);
                        assert(isValidImmShift(imm, size));
                        bitMaskImm bmi = default;
                        bmi.immN = size == EA_8BYTE ? 1u : 0u;
                        bmi.immR = (uint)imm;
                        bmi.immS = size == EA_8BYTE ? 0x3Fu : 0x1Fu;
                        if (ins == INS_lsl)
                        {
                            bmi.immR = (uint)-imm & bmi.immS;
                            bmi.immS = (uint)((nint)bmi.immS - imm);
                        }
                        imm = (nint)bmi.immNRS;
                    }
                    else
                    {
                        imm = emitGetInsSC(id);
                    }
                    assert(isValidImmNRS((nuint)imm, size));
                    code = emitInsCode(ins, fmt);
                    code |= (uint)imm << 10;
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_1D:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmCond(imm));
                    condFlagsImm cfi = new() { immCFVal = (uint)imm };
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeInvertedCond(cfi.cond);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2A:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2B:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    assert(isValidImmShift(imm, size));
                    code |= insEncodeDatasize(size);
                    code |= insEncodeShiftType(id.idInsOpt());
                    code |= insEncodeShiftCount(imm, size);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2C:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    assert((imm >= 0) && (imm <= 4));
                    code |= insEncodeDatasize(size);
                    code |= insEncodeExtend(id.idInsOpt());
                    code |= insEncodeExtendScale(imm);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2D:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmCond(imm));
                    condFlagsImm cfi = new() { immCFVal = (uint)imm };
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    code |= insEncodeInvertedCond(cfi.cond);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2E:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2F:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    assert(isValidImmShift(imm, size));
                    code |= insEncodeDatasize(size);
                    code |= insEncodeShiftType(id.idInsOpt());
                    code |= insEncodeShiftCount(imm, size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2G:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    if (ins == INS_rev && size == EA_8BYTE)
                    {
                        code |= 0x00000400;
                    }
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2H:
                case IF_PC_2A:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasizeBF(code, size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_2I:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmCondFlags(imm));
                    condFlagsImm cfi = new() { immCFVal = (uint)imm };
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    code |= insEncodeFlags(cfi.flags);
                    code |= insEncodeCond(cfi.cond);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_3A:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idIsLclVar() ? codeGen.rsGetRsvdReg() : id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_3B:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    assert(isValidImmShift(imm, size));
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeShiftType(id.idInsOpt());
                    code |= insEncodeShiftCount(imm, size);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_3C:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    assert((imm >= 0) && (imm <= 4));
                    code |= insEncodeDatasize(size);
                    code |= insEncodeExtend(id.idInsOpt());
                    code |= insEncodeExtendScale(imm);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_3D:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidImmCond(imm));
                    condFlagsImm cfi = new() { immCFVal = (uint)imm };
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeCond(cfi.cond);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_3E:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    assert(isValidImmShift(imm, size));
                    code |= insEncodeDatasizeBF(code, size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeShiftCount(imm, size);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DR_4A:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeDatasize(size);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeReg_R(id.idReg4(), 14, 10);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_1A:
                {
                    imm = emitGetInsSC(id);
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= (uint)imm << 13;
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_1B:
                {
                    imm = emitGetInsSC(id) & 0xFF;
                    immShift = (uint)((emitGetInsSC(id) & 0x700) >> 8);
                    elemsize = optGetElemsize(id.idInsOpt());
                    cmode = 0;
                    switch (elemsize)
                    {
                        case EA_1BYTE:
                        {
                            cmode = 0xE;
                            break;
                        }

                        case EA_2BYTE:
                        {
                            cmode = 0x8;
                            cmode |= immShift << 1;
                            break;
                        }

                        case EA_4BYTE:
                        {
                            if (immShift < 4)
                            {
                                cmode = 0;
                                cmode |= immShift << 1;
                            }
                            else
                            {
                                cmode = 0xC;
                                if ((immShift & 2) != 0)
                                {
                                    cmode |= 1;
                                }
                            }
                            break;
                        }

                        case EA_8BYTE:
                        {
                            cmode = 0xE;
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    if ((ins is INS_fmov or INS_movi) && elemsize == EA_8BYTE)
                    {
                        code |= 0x20000000;
                    }
                    if (ins != INS_fmov)
                    {
                        assert((cmode >= 0) && (cmode <= 0xF));
                        code |= cmode << 12;
                    }
                    code |= ((uint)imm >> 5) << 16;
                    code |= ((uint)imm & 0x1F) << 5;
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_1C:
                {
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2A:
                case IF_DV_2R:
                {
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    if (ins is INS_fcvtl or INS_fcvtl2 or INS_fcvtn or INS_fcvtn2)
                    {
                        if (elemsize == EA_4BYTE)
                        {
                            code |= 0x00400000;
                        }
                        else
                        {
                            assert(elemsize == EA_2BYTE);
                        }
                    }
                    else
                    {
                        code |= insEncodeFloatElemsize(elemsize);
                    }
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2B:
                {
                    elemsize = size;
                    index = emitGetInsSC(id);
                    datasize = elemsize == EA_8BYTE ? EA_16BYTE : EA_8BYTE;
                    if (ins == INS_smov)
                    {
                        datasize = EA_16BYTE;
                    }
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(datasize);
                    code |= insEncodeVectorIndex(elemsize, index);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2C:
                {
                    if (ins == INS_dup)
                    {
                        datasize = size;
                        elemsize = optGetElemsize(id.idInsOpt());
                        index = 0;
                    }
                    else
                    {
                        datasize = EA_16BYTE;
                        elemsize = size;
                        index = emitGetInsSC(id);
                    }
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(datasize);
                    code |= insEncodeVectorIndex(elemsize, index);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2D:
                {
                    index = emitGetInsSC(id);
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeVectorIndex(elemsize, index);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2E:
                {
                    index = emitGetInsSC(id);
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorIndex(elemsize, index);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2F:
                {
                    elemsize = size;
                    imm = emitGetInsSC(id);
                    index = (imm >> 4) & 0xF;
                    index2 = imm & 0xF;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorIndex(elemsize, index);
                    code |= insEncodeVectorIndex2(elemsize, index2);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2G:
                {
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    if (elemsize == EA_2BYTE && ins is INS_frecpe or INS_frsqrte)
                    {
                        // These scalar FP16 operations use sz/opcode, not the ftype=11 encoding.
                        code |= 0x00580000;
                    }
                    else
                    {
                        code |= insEncodeFloatElemsize(elemsize);
                    }
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2H:
                {
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeConvertOpt(fmt, id.idInsOpt());
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2I:
                {
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeConvertOpt(fmt, id.idInsOpt());
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2J:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeConvertOpt(fmt, id.idInsOpt());
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2K:
                {
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 9, 5);
                    code |= insEncodeReg_V(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2L:
                {
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2M:
                case IF_DV_2T:
                {
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2N:
                {
                    imm = emitGetInsSC(id);
                    elemsize = size;
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorShift(elemsize, emitInsIsVectorRightShift(ins), imm);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2O:
                {
                    imm = emitGetInsSC(id);
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeVectorShift(elemsize, emitInsIsVectorRightShift(ins), imm);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2P:
                {
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2Q:
                {
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2S:
                {
                    elemsize = optGetElemsize(id.idInsOpt());
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_2U:
                case IF_DV_2V:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3A:
                {
                    code = emitInsCode(ins, fmt);
                    elemsize = optGetElemsize(id.idInsOpt());
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3AI:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    elemsize = optGetElemsize(id.idInsOpt());
                    assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeElemsize(elemsize);
                    code |= insEncodeVectorIndexLMH(elemsize, imm);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3B:
                {
                    code = emitInsCode(ins, fmt);
                    elemsize = optGetElemsize(id.idInsOpt());
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3BI:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    elemsize = optGetElemsize(id.idInsOpt());
                    assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= insEncodeFloatIndex(elemsize, imm);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3C:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3D:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeFloatElemsize(size);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3DI:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    elemsize = size;
                    assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= insEncodeFloatIndex(elemsize, imm);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3E:
                {
                    code = emitInsCode(ins, fmt);
                    elemsize = size;
                    code |= insEncodeElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3EI:
                {
                    code = emitInsCode(ins, fmt);
                    imm = emitGetInsSC(id);
                    elemsize = size;
                    assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                    code |= insEncodeElemsize(elemsize);
                    code |= insEncodeVectorIndexLMH(elemsize, imm);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3F:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3G:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeVectorsize(size);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= (uint)imm << 11;
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3H:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_3I:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= (uint)imm << 10;
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_4A:
                {
                    code = emitInsCode(ins, fmt);
                    elemsize = size;
                    code |= insEncodeFloatElemsize(elemsize);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeReg_V(id.idReg4(), 14, 10);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_DV_4B:
                {
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeReg_V(id.idReg4(), 14, 10);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_PC_1A:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_PC_0A:
                case IF_SN_0A:
                {
                    var skipIns = false;
#if FEATURE_LOOP_ALIGN
                    if (id.idIns() == INS_align)
                    {
                        if (!ig.endsWithAlignInstr() || id.idIsEmptyAlign())
                        {
                            skipIns = true;
                        }
#if DEBUG
                        if (!ig.endsWithAlignInstr())
                        {
                            assert(id.idIsEmptyAlign());
                        }
#endif
                        sz = DescriptorSizes.Align;
                        ins = INS_nop;
#if DEBUG
                        var alignInstr = (instrDescAlign)id;
                        if (compiler.compStressCompile(Compiler.compStressArea.STRESS_EMITTER, 50)
                            && alignInstr.isPlacedAfterJmp && !skipIns)
                        {
                            ins = INS_BREAKPOINT;
                            fmt = IF_SI_0A;
                        }
#endif
                    }
#endif
                    if (!skipIns)
                    {
                        code = emitInsCode(ins, fmt);
                        dst += emitOutputLong(dst, code);
                    }
                    break;
                }

                case IF_SI_0A:
                {
                    imm = emitGetInsSC(id);
                    assert(isValidUimm(imm, 16));
                    code = emitInsCode(ins, fmt);
                    code |= (uint)imm << 5;
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SI_0B:
                {
                    imm = emitGetInsSC(id);
                    assert((imm >= 0) && (imm <= 15));
                    code = emitInsCode(ins, fmt);
                    code |= (uint)imm << 8;
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SR_1A:
                {
                    assert(insOptsNone(id.idInsOpt()));
                    code = emitInsCode(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                default:
                {
                    dst = emitOutput_InstrSve(dst, id);
                    break;
                }
            }

            if (emitInsMayWriteToGCRegArm64(id))
            {
                assert(!emitInsDestIsOp2Arm64(ins));
                if (id.idGCref() != GCT_NONE)
                {
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                }
                else
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                }
                if (emitInsMayWriteMultipleRegsArm64(id))
                {
                    if (id.idGCrefReg2() != GCT_NONE)
                    {
                        emitGCregLiveUpd(id.idGCrefReg2(), id.idReg2(), dst);
                    }
                    else
                    {
                        emitGCregDeadUpd(id.idReg2(), dst);
                    }
                }
            }

        SKIP_GC_UPDATE:
            if (emitInsWritesToLclVarStackLocArm64(id) || emitInsWritesToLclVarStackLocPairArm64(id))
            {
                var varNum = id.idAddr().iiaLclVar.lvaVarNum();
                var ofs = id.idAddr().iiaLclVar.lvaOffset() & ~((uint)TARGET_POINTER_SIZE - 1);
                var adr = compiler.lvaFrameAddress(varNum, out var FPbased, true);
                if (id.idGCref() != GCT_NONE)
                {
                    emitGCvarLiveUpd(adr + (int)ofs, varNum, id.idGCref(), dst
#if DEBUG
                        , (uint)varNum
#endif
                    );
                }
                else
                {
                    var vt = varNum >= 0
                        ? compiler.lvaTable[varNum].Type
                        : (codeGen.RegSet.tmpFindNum(varNum)
                            ?? throw new InvalidOperationException("The spill temporary must exist.")).tdTempType;
                    if (vt is TYP_REF or TYP_BYREF)
                    {
                        emitGCvarDeadUpd(adr + (int)ofs, dst
#if DEBUG
                            , (uint)varNum
#endif
                        );
                    }
                }
                if (emitInsWritesToLclVarStackLocPairArm64(id))
                {
                    var varNum2 = varNum;
                    var adr2 = adr;
                    var ofs2 = ofs;
                    uint ofs2Dist;
                    if (id.idIsLclVarPair())
                    {
                        ref var lclVarAddr2 = ref emitGetLclVarPairLclVar2(id);
                        varNum2 = lclVarAddr2.lvaVarNum();
                        ofs2 = lclVarAddr2.lvaOffset();
                        adr2 = compiler.lvaFrameAddress(varNum2, out var FPbased2, FPbased);
                        ofs2Dist = EA_SIZE_IN_BYTES(size);
#if DEBUG
                        assert(FPbased == FPbased2);
                        if (!FPbased)
                        {
                            assert(encodingZRtoSP(id.idReg3()) == REG_SP);
                        }
                        assert(varNum2 != -1);
#endif
                    }
                    else
                    {
                        ofs2Dist = TARGET_POINTER_SIZE;
                        ofs2 += ofs2Dist;
                    }
                    ofs2 &= ~(ofs2Dist - 1);
                    if (id.idGCrefReg2() != GCT_NONE)
                    {
#if DEBUG
                        if (id.idGCref() != GCT_NONE)
                        {
                            assert(((uint)adr + ofs + ofs2Dist) == ((uint)adr2 + ofs2));
                        }
#endif
                        emitGCvarLiveUpd(adr2 + (int)ofs2, varNum2, id.idGCrefReg2(), dst
#if DEBUG
                            , (uint)varNum2
#endif
                        );
                    }
                    else
                    {
                        var vt = varNum2 >= 0
                            ? compiler.lvaTable[varNum2].Type
                            : (codeGen.RegSet.tmpFindNum(varNum2)
                                ?? throw new InvalidOperationException("The spill temporary must exist.")).tdTempType;
                        if (vt is TYP_REF or TYP_BYREF)
                        {
                            emitGCvarDeadUpd(adr2 + (int)ofs2, dst
#if DEBUG
                                , (uint)varNum2
#endif
                            );
                        }
                    }
                }
            }

#if DEBUG
            var expected = (nuint)emitSizeOfInsDsc(id);
            assert(sz == expected);
            if (compiler.opts.disAsm || compiler.verbose)
            {
                emitDispIns(id, false, dspOffs, true, emitCurCodeOffs(odst), *dp, (nuint)(dst - *dp), ig);
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
                var expected = (nuint)emitSizeOfInsDsc(id);
                assert(sz == expected);
                emitDispIns(id, false, false, true, emitCurCodeOffs(odst), *dp, (nuint)(dst - *dp), ig);
            }
#endif
            assert(*dp != dst || id.idIsEmptyAlign());
            *dp = dst;

            return sz;
        }
    }

    private static int emitGetInstrDescSizeArm64(instrDesc id)
    {
        if (id.idIsSmallDsc())
        {
            return DescriptorSizes.Small;
        }
        else if (id.idIsLargeCns())
        {
            return id.idIsLclVarPair() ? DescriptorSizes.LocalVarPairConstant : ConstantDescriptorSizes.Constant;
        }
        else
        {
            return id.idIsLclVarPair() ? DescriptorSizes.LocalVarPair : DescriptorSizes.Full;
        }
    }

    private uint emitInsCode(instruction ins, insFormat fmt)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Ordinary ARM64 instruction encoding tables are not ported.");
    }

    private unsafe uint emitOutputCallArm64(insGroup ig, byte* dst, instrDesc id, uint code)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 call output is not ported.");
    }

    private static uint insEncodeCond(insCond cond)
    {
        return unchecked((uint)cond << 12);
    }

    private static uint insEncodeInvertedCond(insCond cond)
    {
        var uimm = (uint)cond;
        uimm ^= 1;

        return uimm << 12;
    }

    private static uint insEncodeFlags(insCFlags flags)
    {
        return (uint)flags;
    }

    private static uint insEncodeShiftCount(nint imm, emitAttr size)
    {
        assert((imm & 0x003F) == imm);
        assert(((imm & 0x0020) == 0) || (size == EA_8BYTE));

        return unchecked((uint)imm << 10);
    }

    private static uint insEncodeDatasize(emitAttr size)
    {
        if (size == EA_8BYTE)
        {
            return 0x80000000;
        }

        assert(size == EA_4BYTE);

        return 0;
    }

    private static uint insEncodeDatasizeLS(uint code, emitAttr size)
    {
        var exclusive = (code & 0x35000000) == 0;
        var atomic = (code & 0x31200C00) == 0x30200000;
        if ((code & 0x00800000) != 0 && !exclusive && !atomic)
        {
            if ((code & 0x80000000) == 0 && EA_SIZE(size) != EA_8BYTE)
            {
                return 0x00400000;
            }
        }
        else if ((code & 0x80000000) != 0 && EA_SIZE(size) == EA_8BYTE)
        {
            return 0x40000000;
        }

        return 0;
    }

    private static uint insEncodeDatasizeVLS(uint code, emitAttr size)
    {
        uint result;
        if ((code & 0x20000000) == 0)
        {
            if (size == EA_16BYTE)
            {
                result = 0x80000000;
            }
            else if (size == EA_8BYTE)
            {
                result = 0x40000000;
            }
            else
            {
                assert(size is EA_4BYTE or EA_2BYTE or EA_1BYTE);
                result = 0;
            }
        }
        else
        {
            if (size == EA_16BYTE)
            {
                result = 0x00800000;
            }
            else if (size == EA_8BYTE)
            {
                result = 0xC0000000;
            }
            else if (size == EA_4BYTE)
            {
                result = 0x80000000;
            }
            else if (size == EA_2BYTE)
            {
                result = 0x40000000;
            }
            else
            {
                assert(size == EA_1BYTE);
                result = 0;
            }
        }
        result |= 0x04000000;

        return result;
    }

    private static uint insEncodeDatasizeVPLS(uint code, emitAttr size)
    {
        uint result = 0;
        if (size == EA_16BYTE)
        {
            result = 0x80000000;
        }
        else if (size == EA_8BYTE)
        {
            result = 0x40000000;
        }
        else if (size == EA_4BYTE)
        {
            result = 0;
        }
        result |= 0x04000000;

        return result;
    }

    private static uint insEncodeDatasizeBF(uint code, emitAttr size)
    {
        if ((code & 0x40000000) == 0 && size == EA_8BYTE)
        {
            return 0x80400000;
        }

        return 0;
    }

    private static uint insEncodeVectorsize(emitAttr size)
    {
        if (size == EA_16BYTE)
        {
            return 0x40000000;
        }
        assert(size == EA_8BYTE);

        return 0;
    }

    private static uint insEncodeVectorIndex(emitAttr elemsize, nint index)
    {
        var bits = unchecked((uint)index);
        if (elemsize == EA_1BYTE)
        {
            bits <<= 1;
            bits |= 1;
        }
        else if (elemsize == EA_2BYTE)
        {
            bits <<= 2;
            bits |= 2;
        }
        else if (elemsize == EA_4BYTE)
        {
            bits <<= 3;
            bits |= 4;
        }
        else
        {
            assert(elemsize == EA_8BYTE);
            bits <<= 4;
            bits |= 8;
        }
        assert((bits >= 1) && (bits <= 0x1F));

        return bits << 16;
    }

    private static uint insEncodeVectorIndex2(emitAttr elemsize, nint index2)
    {
        var bits = unchecked((uint)index2);
        if (elemsize == EA_1BYTE)
        {
        }
        else if (elemsize == EA_2BYTE)
        {
            bits <<= 1;
        }
        else if (elemsize == EA_4BYTE)
        {
            bits <<= 2;
        }
        else
        {
            assert(elemsize == EA_8BYTE);
            bits <<= 3;
        }
        assert((bits >= 0) && (bits <= 0xF));

        return bits << 11;
    }

    private static uint insEncodeVectorIndexLMH(emitAttr elemsize, nint index)
    {
        uint bits = 0;
        if (elemsize == EA_2BYTE)
        {
            assert((index >= 0) && (index <= 7));
            if ((index & 4) != 0)
            {
                bits |= 1u << 11;
            }
            if ((index & 2) != 0)
            {
                bits |= 1u << 21;
            }
            if ((index & 1) != 0)
            {
                bits |= 1u << 20;
            }
        }
        else if (elemsize == EA_4BYTE)
        {
            assert((index >= 0) && (index <= 3));
            if ((index & 2) != 0)
            {
                bits |= 1u << 11;
            }
            if ((index & 1) != 0)
            {
                bits |= 1u << 21;
            }
        }
        else
        {
            assert(false, "!\"Invalid 'elemsize' value\"");
        }

        return bits;
    }

    private static uint insEncodeFloatElemsize(emitAttr size)
    {
        if (size == EA_8BYTE)
        {
            return 0x00400000;
        }
        else if (size == EA_2BYTE)
        {
            return 0x00C00000;
        }
        assert(size == EA_4BYTE);

        return 0;
    }

    private static uint insEncodeFloatIndex(emitAttr elemsize, nint index)
    {
        uint result = 0;
        if (elemsize == EA_8BYTE)
        {
            assert((index >= 0) && (index <= 1));
            if (index == 1)
            {
                result |= 0x00000800;
            }
        }
        else
        {
            assert(elemsize == EA_4BYTE);
            assert((index >= 0) && (index <= 3));
            if ((index & 2) != 0)
            {
                result |= 0x00000800;
            }
            if ((index & 1) != 0)
            {
                result |= 0x00200000;
            }
        }

        return result;
    }

    private static uint insEncodeVLSIndex(emitAttr size, nint index)
    {
        uint result = 0;
        switch (size)
        {
            case EA_1BYTE:
            {
                result |= (uint)(index & 8) << 27;
                result |= (uint)(index & 4) << 10;
                result |= (uint)(index & 3) << 10;
                break;
            }

            case EA_2BYTE:
            {
                result |= (uint)(index & 4) << 28;
                result |= 0x4000;
                result |= (uint)(index & 2) << 11;
                result |= (uint)(index & 1) << 11;
                break;
            }

            case EA_4BYTE:
            {
                result |= (uint)(index & 2) << 29;
                result |= 0x8000;
                result |= (uint)(index & 1) << 12;
                break;
            }

            case EA_8BYTE:
            {
                result |= (uint)(index & 1) << 30;
                result |= 0x8400;
                break;
            }

            default:
            {
                assert(false, "!\"Invalid element size\"");
                break;
            }
        }

        return result;
    }

    private static uint insEncodeConvertOpt(insFormat fmt, insOpts conversion)
    {
        uint result = 0;
        switch (conversion)
        {
            case INS_OPTS_S_TO_D:
            {
                assert(fmt == IF_DV_2J);
                result = 0x00008000;
                break;
            }

            case INS_OPTS_D_TO_S:
            {
                assert(fmt == IF_DV_2J);
                result = 0x00400000;
                break;
            }

            case INS_OPTS_H_TO_S:
            {
                assert(fmt == IF_DV_2J);
                result = 0x00C00000;
                break;
            }

            case INS_OPTS_H_TO_D:
            {
                assert(fmt == IF_DV_2J);
                result = 0x00C08000;
                break;
            }

            case INS_OPTS_S_TO_H:
            {
                assert(fmt == IF_DV_2J);
                result = 0x00018000;
                break;
            }

            case INS_OPTS_D_TO_H:
            {
                assert(fmt == IF_DV_2J);
                result = 0x00418000;
                break;
            }

            case INS_OPTS_S_TO_4BYTE:
            {
                assert(fmt == IF_DV_2H);
                result = 0;
                break;
            }

            case INS_OPTS_D_TO_4BYTE:
            {
                assert(fmt == IF_DV_2H);
                result = 0x00400000;
                break;
            }

            case INS_OPTS_S_TO_8BYTE:
            {
                assert(fmt == IF_DV_2H);
                result = 0x80000000;
                break;
            }

            case INS_OPTS_D_TO_8BYTE:
            {
                assert(fmt == IF_DV_2H);
                result = 0x80400000;
                break;
            }

            case INS_OPTS_H_TO_4BYTE:
            {
                assert(fmt == IF_DV_2H);
                result = 0x00C00000;
                break;
            }

            case INS_OPTS_H_TO_8BYTE:
            {
                assert(fmt == IF_DV_2H);
                result = 0x80C00000;
                break;
            }

            case INS_OPTS_4BYTE_TO_S:
            {
                assert(fmt == IF_DV_2I);
                result = 0;
                break;
            }

            case INS_OPTS_4BYTE_TO_D:
            {
                assert(fmt == IF_DV_2I);
                result = 0x00400000;
                break;
            }

            case INS_OPTS_8BYTE_TO_S:
            {
                assert(fmt == IF_DV_2I);
                result = 0x80000000;
                break;
            }

            case INS_OPTS_8BYTE_TO_D:
            {
                assert(fmt == IF_DV_2I);
                result = 0x80400000;
                break;
            }

            case INS_OPTS_4BYTE_TO_H:
            {
                assert(fmt == IF_DV_2I);
                result = 0x00C00000;
                break;
            }

            case INS_OPTS_8BYTE_TO_H:
            {
                assert(fmt == IF_DV_2I);
                result = 0x80C00000;
                break;
            }

            default:
            {
                assert(false, "!\"Invalid 'conversion' value\"");
                break;
            }
        }

        return result;
    }

    private static bool insOptsPreIndex(insOpts opt)
    {
        return opt == INS_OPTS_PRE_INDEX;
    }

    private static uint insEncodeIndexedOpt(insOpts opt)
    {
        assert(insOptsNone(opt) || insOptsIndexed(opt));
        if (insOptsIndexed(opt))
        {
            if (insOptsPostIndex(opt))
            {
                return 0x00000400;
            }
            assert(insOptsPreIndex(opt));

            return 0x00000C00;
        }
        assert(insOptsNone(opt));

        return 0;
    }

    private static uint insEncodePairIndexedOpt(instruction ins, insOpts opt)
    {
        assert(insOptsNone(opt) || insOptsIndexed(opt));
        if (ins is INS_ldnp or INS_stnp)
        {
            assert(insOptsNone(opt));

            return 0;
        }
        if (insOptsIndexed(opt))
        {
            if (insOptsPostIndex(opt))
            {
                return 0x00800000;
            }
            assert(insOptsPreIndex(opt));

            return 0x01800000;
        }
        assert(insOptsNone(opt));

        return 0x01000000;
    }

    private static uint insEncodeShiftType(insOpts opt)
    {
        if (insOptsNone(opt))
        {
            opt = INS_OPTS_LSL;
        }
        assert(insOptsAnyShift(opt));
        var option = unchecked((uint)opt - (uint)INS_OPTS_LSL);
        assert(option <= 3);

        return option << 22;
    }

    private static uint insEncodeShiftImm12(insOpts opt)
    {
        if (insOptsLSL12(opt))
        {
            return 0x00400000;
        }

        return 0;
    }

    private static uint insEncodeExtend(insOpts opt)
    {
        if (insOptsNone(opt) || opt == INS_OPTS_LSL)
        {
            opt = INS_OPTS_UXTX;
        }
        assert(insOptsAnyExtend(opt));
        var option = unchecked((uint)opt - (uint)INS_OPTS_UXTB);
        assert(option <= 7);

        return option << 13;
    }

    private static uint insEncodeExtendScale(nint imm)
    {
        assert((imm >= 0) && (imm <= 4));

        return unchecked((uint)imm << 10);
    }

    private static uint insEncodeReg3Scale(bool isScaled)
    {
        return isScaled ? 0x00001000u : 0;
    }

    private static bool emitInsDestIsOp2Arm64(instruction ins)
    {
        // WR2 is the native instruction-info bit at emitarm64.cpp:1578.
        return (uint)ins < (uint)CodeGen.instInfo.Length && (CodeGen.instInfo[(int)ins] & 128) != 0;
    }

    private static bool emitInsIsStoreArm64(instruction ins)
    {
        return (uint)ins < (uint)CodeGen.instInfo.Length && (CodeGen.instInfo[(int)ins] & CodeGen.ST) != 0;
    }

    private static bool emitInsWritesToLclVarStackLocArm64(instrDesc id)
    {
        if (!id.idIsLclVar())
        {
            return false;
        }

        return id.idIns() is INS_strb or INS_strh or INS_str or INS_stur or INS_sturb or INS_sturh;
    }

    private static bool emitInsWritesToLclVarStackLocPairArm64(instrDesc id)
    {
        if (!id.idIsLclVar())
        {
            return false;
        }

        return id.idIns() is INS_stnp or INS_stp;
    }

    private static bool emitInsMayWriteMultipleRegsArm64(instrDesc id)
    {
        return id.idIns() is INS_ldp or INS_ldpsw or INS_ldnp;
    }

    private bool emitInsMayWriteToGCRegArm64(instrDesc id)
    {
        var ins = id.idIns();
        switch (id.idInsFmt())
        {
            case IF_DI_1B:
            case IF_DI_1D:
            case IF_DI_1E:
            case IF_DI_2A:
            case IF_DI_2B:
            case IF_DI_2C:
            case IF_DI_2D:
            case IF_DR_1D:
            case IF_DR_2D:
            case IF_DR_2E:
            case IF_DR_2F:
            case IF_DR_2G:
            case IF_DR_2H:
            case IF_DR_3A:
            case IF_DR_3B:
            case IF_DR_3C:
            case IF_DR_3D:
            case IF_DR_3E:
            case IF_DR_4A:
            case IF_DV_2B:
            case IF_DV_2H:
            {
                return true;
            }

            case IF_DV_2C:
            case IF_DV_2D:
            case IF_DV_2E:
            case IF_DV_2F:
            case IF_DV_2G:
            case IF_DV_2I:
            case IF_DV_2J:
            case IF_DV_2K:
            case IF_DV_2L:
            case IF_DV_2M:
            case IF_DV_2P:
            case IF_DV_2Q:
            case IF_DV_2R:
            case IF_DV_2S:
            case IF_DV_3A:
            case IF_DV_3AI:
            case IF_DV_3B:
            case IF_DV_3BI:
            case IF_DV_3C:
            case IF_DV_3D:
            case IF_DV_3DI:
            case IF_DV_3E:
            case IF_DV_3EI:
            case IF_DV_3F:
            case IF_DV_3G:
            case IF_DV_3H:
            case IF_DV_3I:
            case IF_DV_4A:
            case IF_DV_4B:
            {
                return false;
            }

            case IF_LS_1A:
            case IF_LS_2A:
            case IF_LS_2B:
            case IF_LS_2C:
            case IF_LS_2D:
            case IF_LS_2E:
            case IF_LS_2F:
            case IF_LS_2G:
            case IF_LS_3A:
            case IF_LS_3B:
            case IF_LS_3C:
            case IF_LS_3D:
            case IF_LS_3F:
            case IF_LS_3G:
            {
                if (emitInsIsStoreArm64(ins))
                {
                    return false;
                }
                assert(emitInsIsLoad(ins));

                return true;
            }

            case IF_LS_3E:
            {
                assert(emitInsIsStoreArm64(ins));
                assert(emitInsIsLoad(ins));

                return true;
            }

            case IF_PC_1A:
            {
                return ins is INS_autiza or INS_autizb or INS_paciza or INS_pacizb or INS_xpacd or INS_xpaci;
            }

            case IF_PC_2A:
            {
                return ins is INS_autia or INS_autib or INS_pacia or INS_pacib;
            }

            case IF_SR_1A:
            {
                return ins == INS_mrs_tpid0;
            }

            case IF_SVE_CO_3A:
            case IF_SVE_BM_1A:
            case IF_SVE_BO_1A:
            case IF_SVE_CS_3A:
            case IF_SVE_DK_3A:
            case IF_SVE_DL_2A:
            case IF_SVE_DM_2A:
            case IF_SVE_DO_2A:
            case IF_SVE_BB_2A:
            case IF_SVE_BC_1A:
            case IF_SVE_BL_1A:
            case IF_SVE_DS_2A:
            {
                return true;
            }

            default:
            {
                return false;
            }
        }
    }
}
#endif
