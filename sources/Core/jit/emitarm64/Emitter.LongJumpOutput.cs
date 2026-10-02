// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
#if DEBUG
using System.Runtime.CompilerServices;
#endif
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe byte* emitOutputLJArm64Core(insGroup? ig, byte* dst, instrDesc i)
    {
        assert(_compiler is not null);
        var id = (instrDescJmp)i;
        var ins = id.idIns();
        var fmt = id.idInsFmt();
        var loadLabel = false;
        var isJump = false;
        var loadConstant = false;

        switch (ins)
        {
            default:
            case INS_tbz:
            case INS_tbnz:
            case INS_cbz:
            case INS_cbnz:
            {
                isJump = true;
                break;
            }

            case INS_ldr:
            case INS_ldrsw:
            {
                loadConstant = true;
                break;
            }

            case INS_adr:
            case INS_adrp:
            {
                loadLabel = true;
                break;
            }
        }

        var srcOffs = emitCurCodeOffs(dst);
        var srcAddr = emitOffsetToPtr(srcOffs);
        nint distVal;
        if (id.idAddr().iiaIsJitDataOffset())
        {
            assert(loadConstant || loadLabel);
            var dataOffset = id.idAddr().iiaGetJitDataOffset();
            assert(dataOffset >= 0);
            var immediate = emitGetInsSC(id);
            assert(immediate >= 0 && immediate < 0x1000);

            var dataOffs = unchecked((uint)((nint)dataOffset + immediate));
            assert(dataOffs < emitDataSize());
            var dataAddr = emitDataOffsetToPtrArm64Core(dataOffs);
            var dstReg = id.idReg1();
            var addrReg = dstReg;
            var opSize = id.idOpSize();

            if (loadConstant)
            {
                if (id.idjShort)
                {
                    assert(ins == INS_ldr);
                    assert(fmt == IF_LS_1A);
                    distVal = unchecked((nint)(dataAddr - srcAddr));
                    dst = emitOutputShortConstantArm64Core(dst, ins, fmt, distVal, dstReg, opSize);
                }
                else
                {
                    assert(fmt == IF_LARGELDC);
                    var relPageAddr = computeRelPageAddrArm64((nuint)dataAddr, (nuint)srcAddr);
                    if (isVectorRegister(dstReg))
                    {
                        addrReg = opSize == EA_16BYTE ? encodingSPtoZR(id.idReg2()) : id.idReg2();
                        assert(isGeneralRegister(addrReg));
                    }

                    ins = INS_adrp;
                    fmt = IF_DI_1E;
                    dst = emitOutputShortAddressArm64Core(dst, ins, fmt, relPageAddr, addrReg);

                    var imm12 = unchecked((nint)dataAddr) & 0xFFF;
                    assert(isValidUimm(imm12, 12));
                    if (isVectorRegister(dstReg) && opSize == EA_16BYTE)
                    {
                        const emitAttr ElementSize = EA_1BYTE;
                        var opt = optMakeArrangement(opSize, ElementSize);
                        assert(isGeneralRegisterOrSP(addrReg));
                        assert(isValidVectorElemsize(ElementSize));
                        assert(isValidArrangement(opSize, opt));

                        dst = emitOutputVectorConstantArm64Core(dst, imm12, dstReg, addrReg, opSize, ElementSize);
                    }
                    else
                    {
                        ins = INS_ldr;
                        fmt = IF_LS_2B;
                        dst = emitOutputShortConstantArm64Core(dst, ins, fmt, imm12, addrReg, opSize);
                        if (addrReg != dstReg)
                        {
                            assert(isVectorRegister(dstReg) && isGeneralRegister(addrReg));
                            ins = INS_fmov;
                            fmt = IF_DV_2I;
                            var code = emitInsCode(ins, fmt);
                            code |= insEncodeReg_V(dstReg, 4, 0);
                            code |= insEncodeReg_R(addrReg, 9, 5);
                            if (id.idOpSize() == EA_8BYTE)
                            {
                                code |= 0x80400000;
                            }
                            dst = unchecked(dst + emitOutputLong(dst, code));
                        }
                    }
                }
            }
            else
            {
                assert(loadLabel);
                dst = emitOutputLoadLabelArm64Core(dst, srcAddr, dataAddr, id);
            }

            return dst;
        }

        assert(loadLabel || isJump);
        var target = id.idjTargetIG;
        assert(target is not null);
        var dstOffs = target.igOffs;
        var dstAddr = emitOffsetToPtr(dstOffs);
        distVal = unchecked((nint)(dstAddr - srcAddr));

        if (dstOffs <= srcOffs)
        {
#if DEBUG_EMIT
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            if (debugInfo.idNum == unchecked((uint)INTERESTING_JUMP_NUM) || INTERESTING_JUMP_NUM == 0)
            {
                assert(id.idjIG is not null);
                var blockOffset = id.idjIG.igOffs;
                if (INTERESTING_JUMP_NUM == 0)
                {
                    jitprintf($"[3] Jump {debugInfo.idNum}:\n");
                }
                jitprintf($"[3] Jump  block is at {blockOffset:X8} - {emitOffsAdj:X2} = {unchecked((nuint)blockOffset - (nuint)emitOffsAdj):X8}\n");
                jitprintf($"[3] Jump        is at {srcOffs:X8} - {emitOffsAdj:X2} = {unchecked(srcOffs - (uint)emitOffsAdj):X8}\n");
                jitprintf($"[3] Label block is at {dstOffs:X8} - {emitOffsAdj:X2} = {unchecked(dstOffs - (uint)emitOffsAdj):X8}\n");
            }
#endif
        }
        else
        {
            emitFwdJumps = true;
            if (!emitJumpCrossHotColdBoundaryArm64(srcOffs, dstOffs))
            {
                dstOffs = unchecked(dstOffs - (uint)emitOffsAdj);
                distVal = unchecked(distVal - (nint)emitOffsAdj);
            }

            id.idjOffs = dstOffs;
            if (id.idjOffs != dstOffs)
            {
                IMPL_LIMITATION("Method is too large");
            }
#if DEBUG_EMIT
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            if (debugInfo.idNum == unchecked((uint)INTERESTING_JUMP_NUM) || INTERESTING_JUMP_NUM == 0)
            {
                assert(id.idjIG is not null);
                var blockOffset = id.idjIG.igOffs;
                if (INTERESTING_JUMP_NUM == 0)
                {
                    jitprintf($"[4] Jump {debugInfo.idNum}:\n");
                }
                jitprintf($"[4] Jump  block is at {blockOffset:X8}\n");
                jitprintf($"[4] Jump        is at {srcOffs:X8}\n");
                jitprintf($"[4] Label block is at {unchecked(dstOffs + (uint)emitOffsAdj):X8} - {emitOffsAdj:X2} = {dstOffs:X8}\n");
            }
#endif
        }

#if DEBUG
        emitTraceLongJumpArm64(id, srcOffs, dstOffs, distVal, enabled: false);
#endif
        id.idjAddr = distVal > 0 ? dst : null;
        assert(insOptsNone(id.idInsOpt()));
        if (isJump)
        {
            if (id.idjShort)
            {
                assert(!id.idjKeepLong);
                assert(!emitJumpCrossHotColdBoundaryArm64(srcOffs, dstOffs));
                assert(fmt is IF_BI_0A or IF_BI_0B or IF_BI_1A or IF_BI_1B);
                dst = emitOutputShortBranchArm64Core(dst, ins, fmt, distVal, id);
            }
            else
            {
                if (fmt == IF_LARGEJMP)
                {
                    instruction reverseIns;
                    insFormat reverseFmt;
                    switch (ins)
                    {
                        case INS_cbz:
                        {
                            reverseIns = INS_cbnz;
                            reverseFmt = IF_BI_1A;
                            break;
                        }

                        case INS_cbnz:
                        {
                            reverseIns = INS_cbz;
                            reverseFmt = IF_BI_1A;
                            break;
                        }

                        case INS_tbz:
                        {
                            reverseIns = INS_tbnz;
                            reverseFmt = IF_BI_1B;
                            break;
                        }

                        case INS_tbnz:
                        {
                            reverseIns = INS_tbz;
                            reverseFmt = IF_BI_1B;
                            break;
                        }

                        default:
                        {
                            reverseIns = emitJumpKindToIns(emitReverseJumpKindArm64(emitInsToJumpKindArm64(ins)));
                            reverseFmt = IF_BI_0B;
                            break;
                        }
                    }

                    // Invert the condition around B, then rebase B's displacement
                    // from the pseudo-instruction's start to its second word.
                    dst = emitOutputShortBranchArm64Core(dst, reverseIns, reverseFmt, 8, id);
                    ins = INS_b;
                    fmt = IF_BI_0A;
                    distVal = unchecked(distVal - 4);
                }

                assert(fmt == IF_BI_0A);
                assert((distVal & 1) == 0);
                var code = emitInsCode(ins, fmt);
                var doRecordRelocation = _compiler.opts.compReloc &&
                    emitJumpCrossHotColdBoundaryArm64(srcOffs, dstOffs);
                if (!doRecordRelocation)
                {
                    noway_assert((distVal & 3) == 0);
                    distVal >>= 2;
                    noway_assert(isValidSimm(distVal, 26));
                    distVal &= 0x3FFFFFF;
                    code |= unchecked((uint)distVal);
                }

                var instrSize = emitOutputLong(dst, code);
                if (doRecordRelocation)
                {
                    assert(id.idjKeepLong);
                    if (_compiler.info.compMatchedVM)
                    {
                        var relocationTarget = emitOffsetToPtr(dstOffs);
                        emitRecordRelocation(dst, relocationTarget, CorInfoReloc.ARM64_BRANCH26);
                    }
                }
                dst = unchecked(dst + instrSize);
            }
        }
        else if (loadLabel)
        {
            dst = emitOutputLoadLabelArm64Core(dst, srcAddr, dstAddr, id);
        }

        return dst;
    }

    private unsafe byte* emitOutputLoadLabelArm64Core(byte* dst, byte* srcAddr, byte* dstAddr, instrDescJmp id)
    {
        var ins = id.idIns();
        var fmt = id.idInsFmt();
        var dstReg = id.idReg1();
        if (id.idjShort)
        {
            assert(ins == INS_adr);
            assert(fmt == IF_DI_1E);
            assert(!id.idIsReloc());
            var distVal = unchecked((nint)(dstAddr - srcAddr));
            dst = emitOutputShortAddressArm64Core(dst, ins, fmt, distVal, dstReg);
        }
        else
        {
            assert(fmt == IF_LARGEADR);
            var relPageAddr = id.idIsReloc() ? 0 : computeRelPageAddrArm64((nuint)dstAddr, (nuint)srcAddr);
            dst = emitOutputShortAddressArm64Core(dst, INS_adrp, IF_DI_1E, relPageAddr, dstReg);
            if (id.idIsReloc())
            {
                emitRecordRelocation(dst - sizeof(uint), dstAddr, CorInfoReloc.ARM64_PAGEBASE_REL21);
            }

            var imm12 = id.idIsReloc() ? 0 : unchecked((nint)dstAddr) & 0xFFF;
            assert(isValidUimm(imm12, 12));
            var code = emitInsCode(INS_add, IF_DI_2A);
            code |= insEncodeDatasize(EA_8BYTE);
            code |= unchecked((uint)imm12 << 10);
            code |= insEncodeReg_R(dstReg, 4, 0);
            code |= insEncodeReg_R(dstReg, 9, 5);
            dst = unchecked(dst + emitOutputLong(dst, code));
            if (id.idIsReloc())
            {
                emitRecordRelocation(dst - sizeof(uint), dstAddr, CorInfoReloc.ARM64_PAGEOFFSET_12A);
            }
        }

        return dst;
    }

    private unsafe byte* emitOutputShortBranchArm64Core(
        byte* dst, instruction ins, insFormat fmt, nint distVal, instrDescJmp id)
    {
        var code = emitInsCode(ins, fmt);
        var loBits = distVal & 3;
        noway_assert(loBits == 0);
        distVal >>= 2;
        if (fmt == IF_BI_0A)
        {
            noway_assert(isValidSimm(distVal, 26));
            distVal &= 0x3FFFFFF;
            code |= unchecked((uint)distVal);
        }
        else if (fmt == IF_BI_0B)
        {
            noway_assert(isValidSimm(distVal, 19));
            distVal &= 0x7FFFF;
            code |= unchecked((uint)(distVal << 5));
        }
        else if (fmt == IF_BI_1A)
        {
            assert(id is not null);
            code |= insEncodeDatasize(id.idOpSize());
            code |= insEncodeReg_R(id.idReg1(), 4, 0);
            noway_assert(isValidSimm(distVal, 19));
            distVal &= 0x7FFFF;
            code |= unchecked((uint)(distVal << 5));
        }
        else if (fmt == IF_BI_1B)
        {
            assert(id is not null);
            var imm = emitGetInsSC(id);
            assert(isValidImmShift(imm, id.idOpSize()));
            if ((imm & 0x20) != 0)
            {
                code |= 0x80000000;
            }
            code |= unchecked((uint)((imm & 0x1F) << 19));
            code |= insEncodeReg_R(id.idReg1(), 4, 0);
            noway_assert(isValidSimm(distVal, 14));
            distVal &= 0x3FFF;
            code |= unchecked((uint)(distVal << 5));
        }
        else
        {
            assert(false, "Unknown fmt for emitOutputShortBranch");
        }

        dst = unchecked(dst + emitOutputLong(dst, code));

        return dst;
    }

    private unsafe byte* emitOutputShortAddressArm64Core(
        byte* dst, instruction ins, insFormat fmt, nint distVal, regNumber reg)
    {
        var loBits = distVal & 3;
        distVal >>= 2;
        var code = emitInsCode(ins, fmt);
        if (fmt == IF_DI_1E)
        {
            code |= insEncodeReg_R(reg, 4, 0);
            noway_assert(isValidSimm(distVal, 19));
            distVal &= 0x7FFFF;
            code |= unchecked((uint)(distVal << 5));
            code |= unchecked((uint)(loBits << 29));
        }
        else
        {
            assert(false, "Unknown fmt for emitOutputShortAddress");
        }

        dst = unchecked(dst + emitOutputLong(dst, code));

        return dst;
    }

    private unsafe byte* emitOutputShortConstantArm64Core(
        byte* dst, instruction ins, insFormat fmt, nint imm, regNumber reg, emitAttr opSize)
    {
        var code = emitInsCode(ins, fmt);
        if (fmt == IF_LS_1A)
        {
            var loBits = imm & 3;
            noway_assert(loBits == 0);
            var distVal = imm >> 2;
            noway_assert(isValidSimm(distVal, 19));
            if (isVectorRegister(reg))
            {
                code |= insEncodeDatasizeVLS(code, opSize);
                code |= insEncodeReg_V(reg, 4, 0);
            }
            else
            {
                assert(isGeneralRegister(reg));
                if (ins == INS_ldr && opSize == EA_8BYTE)
                {
                    code |= 0x40000000;
                }
                code |= insEncodeReg_R(reg, 4, 0);
            }
            distVal &= 0x7FFFF;
            code |= unchecked((uint)(distVal << 5));
        }
        else if (fmt == IF_LS_2B)
        {
            noway_assert(isValidUimm(imm, 12));
            assert(isGeneralRegister(reg));
            if (opSize == EA_8BYTE)
            {
                if (ins == INS_ldr)
                {
                    code |= 0x40000000;
                }
                assert((imm & 7) == 0);
                imm >>= 3;
            }
            else
            {
                assert(opSize == EA_4BYTE);
                assert((imm & 3) == 0);
                imm >>= 2;
            }
            code |= insEncodeReg_R(reg, 4, 0);
            code |= insEncodeReg_R(reg, 9, 5);
            code |= unchecked((uint)(imm << 10));
        }
        else
        {
            assert(false, "Unknown fmt for emitOutputShortConstant");
        }

        dst = unchecked(dst + emitOutputLong(dst, code));

        return dst;
    }

    private unsafe byte* emitOutputVectorConstantArm64Core(
        byte* dst, nint imm, regNumber dstReg, regNumber addrReg, emitAttr opSize, emitAttr elemSize)
    {
        var code = emitInsCode(INS_add, IF_DI_2A);
        code |= insEncodeDatasize(EA_8BYTE);
        code |= unchecked((uint)imm << 10);
        code |= insEncodeReg_R(addrReg, 4, 0);
        code |= insEncodeReg_R(addrReg, 9, 5);
        dst = unchecked(dst + emitOutputLong(dst, code));

        code = emitInsCode(INS_ld1, IF_LS_2D);
        code |= insEncodeVectorsize(opSize);
        code |= insEncodeVLSElemsize(elemSize);
        code |= insEncodeReg_R(addrReg, 9, 5);
        code |= insEncodeReg_V(dstReg, 4, 0);
        dst = unchecked(dst + emitOutputLong(dst, code));

        return dst;
    }

    private static nint computeRelPageAddrArm64(nuint dstAddr, nuint srcAddr)
    {
        return unchecked((nint)((dstAddr >> 12) - (srcAddr >> 12)));
    }

    private bool emitJumpCrossHotColdBoundaryArm64(uint srcOffset, uint dstOffset)
    {
        if (emitTotalColdCodeSize == 0)
        {
            return false;
        }

        var totalSize = unchecked((uint)emitTotalHotCodeSize + (uint)emitTotalColdCodeSize);
        assert(srcOffset < totalSize);
        assert(dstOffset < totalSize);

        return (srcOffset < unchecked((uint)emitTotalHotCodeSize)) !=
            (dstOffset < unchecked((uint)emitTotalHotCodeSize));
    }

    private unsafe byte* emitDataOffsetToPtrArm64Core(uint offset)
    {
        assert(offset < emitDataSize());
        var min = 0u;
        var max = unchecked((uint)emitNumDataChunks);
        while (min < max)
        {
            var mid = min + ((max - min) / 2);
            var chunkOffset = unchecked((uint)emitDataChunkOffsets[mid]);
            if (chunkOffset == offset)
            {
                return emitDataChunks[mid].block;
            }
            if (chunkOffset < offset)
            {
                min = mid + 1;
            }
            else
            {
                max = mid;
            }
        }

        assert(min > 0 && min <= unchecked((uint)emitNumDataChunks));
        var chunkIndex = min - 1;
        var relativeOffset = unchecked(offset - (uint)emitDataChunkOffsets[chunkIndex]);
        assert(relativeOffset < emitDataChunks[chunkIndex].size);

        return unchecked(emitDataChunks[chunkIndex].block + relativeOffset);
    }

    private static emitJumpKind emitInsToJumpKindArm64(instruction ins)
    {
        for (var index = 0; index < emitJumpKindInstructions.Length; index++)
        {
            if (ins == emitJumpKindInstructions[index])
            {
                var result = (emitJumpKind)index;
                assert(EJ_NONE < result && result < EJ_COUNT);

                return result;
            }
        }

        unreached();

        return default;
    }

    private static emitJumpKind emitReverseJumpKindArm64(emitJumpKind jumpKind)
    {
        assert(jumpKind < EJ_COUNT);

        return emitReverseJumpKinds[(int)jumpKind];
    }

#if DEBUG
    private unsafe void emitTraceLongJumpArm64(instrDescJmp id, uint srcOffs, uint dstOffs, nint distVal, bool enabled)
    {
        assert(_compiler is not null);
        if (enabled && _compiler.verbose)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            var descriptor = id;
            var address = dspPtr((void*)Unsafe.As<instrDescJmp, nint>(ref descriptor));
            var width = id.idjShort ? "X4" : "X8";
            jitprintf($"; {(dstOffs <= srcOffs ? "Fwd" : "Bwd")} jump [{FMT_PTR((void*)address)}/{debugInfo.idNum:D3}] " +
                $"from {unchecked((nuint)srcOffs + 4).ToString(width, System.Globalization.CultureInfo.InvariantCulture)} " +
                $"to {dstOffs.ToString(width, System.Globalization.CultureInfo.InvariantCulture)}: " +
                $"dist = 0x{unchecked((nuint)distVal):X8}\n");
        }
    }
#endif
}
#endif
