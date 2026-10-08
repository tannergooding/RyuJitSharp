// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe byte* emitOutputLJArm32Core(insGroup? ig, byte* dst, instrDesc i)
    {
        assert(_compiler is not null);

        var id = (instrDescJmp)i;
        var ins = id.idIns();
        var loadLabel = false;
        var isJump = false;
        var relAddr = true;
        uint shortDistance;

        switch (ins)
        {
            default:
            {
                shortDistance = unchecked((uint)JCC_DIST_SMALL_MAX_NEG);
                isJump = true;
                break;
            }

            case INS_cbz:
            case INS_cbnz:
            {
                shortDistance = 0;
                isJump = true;
                break;
            }

            case INS_adr:
            {
                shortDistance = unchecked((uint)LBL_DIST_SMALL_MAX_NEG);
                loadLabel = true;
                break;
            }

            case INS_movw:
            case INS_movt:
            {
                shortDistance = unchecked((uint)LBL_DIST_SMALL_MAX_NEG);
                relAddr = false;
                loadLabel = true;
                break;
            }
        }

        var srcOffs = emitCurCodeOffs(dst);
        var dstOffs = id.idjTargetIG?.igOffs
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "An ARM32 jump requires a bound target group.");
        nint distVal;

        if (relAddr)
        {
            if (ins == INS_adr)
            {
                var alignedSrcAddr = (byte*)((nuint)emitOffsetToPtr(srcOffs) & ~(nuint)3);
                distVal = unchecked((nint)(emitOffsetToPtr(dstOffs) - alignedSrcAddr) + 1);
            }
            else
            {
                distVal = unchecked((nint)(emitOffsetToPtr(dstOffs) - emitOffsetToPtr(srcOffs)));
            }
        }
        else
        {
            assert(ins is INS_movw or INS_movt);
            distVal = (nint)emitOffsetToPtr(dstOffs);
            if (!_compiler.opts.compReloc)
            {
                distVal++;
            }
        }

        if (dstOffs <= srcOffs)
        {
#if DEBUG_EMIT
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            if ((debugInfo.idNum == unchecked((uint)INTERESTING_JUMP_NUM)) || (INTERESTING_JUMP_NUM == 0))
            {
                var jumpGroup = id.idjIG;
                assert(jumpGroup is not null);
                var blockOffset = jumpGroup.igOffs;
                if (INTERESTING_JUMP_NUM == 0)
                {
                    jitprintf($"[3] Jump {debugInfo.idNum}:\n");
                }
                jitprintf($"[3] Jump  block is at {blockOffset:X8} - {emitOffsAdj:X2} = {unchecked(blockOffset - (uint)emitOffsAdj):X8}\n");
                jitprintf($"[3] Jump        is at {srcOffs:X8} - {emitOffsAdj:X2} = {unchecked(srcOffs - (uint)emitOffsAdj):X8}\n");
                jitprintf($"[3] Label block is at {dstOffs:X8} - {emitOffsAdj:X2} = {unchecked(dstOffs - (uint)emitOffsAdj):X8}\n");
            }
#endif
            noway_assert(id.idInsFmt() != IF_T1_I);
            if (isJump && unchecked((uint)(distVal - 4)) >= shortDistance)
            {
                emitSetShortJump(id);
            }
        }
        else
        {
            emitFwdJumps = true;
            if (!emitJumpCrossHotColdBoundary(srcOffs, dstOffs))
            {
                dstOffs = unchecked(dstOffs - (uint)emitOffsAdj);
                distVal = unchecked(distVal - emitOffsAdj);
            }

            id.idjOffs = dstOffs;
            if (id.idjOffs != dstOffs)
            {
                IMPL_LIMITATION("Method is too large");
            }
#if DEBUG_EMIT
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            if ((debugInfo.idNum == unchecked((uint)INTERESTING_JUMP_NUM)) || (INTERESTING_JUMP_NUM == 0))
            {
                var jumpGroup = id.idjIG;
                assert(jumpGroup is not null);
                if (INTERESTING_JUMP_NUM == 0)
                {
                    jitprintf($"[4] Jump {debugInfo.idNum}:\n");
                }
                jitprintf($"[4] Jump  block is at {jumpGroup.igOffs:X8}\n");
                jitprintf($"[4] Jump        is at {srcOffs:X8}\n");
                jitprintf($"[4] Label block is at {unchecked(dstOffs + (uint)emitOffsAdj):X8} - {emitOffsAdj:X2} = {dstOffs:X8}\n");
            }
#endif
        }

        if (relAddr)
        {
            distVal = unchecked(distVal - 4);
        }

        var format = id.idInsFmt();
        uint code;
        if (isJump)
        {
            if (id.idjShort)
            {
                assert(!id.idjKeepLong);
                assert(!emitJumpCrossHotColdBoundary(srcOffs, dstOffs));
                assert(JMP_SIZE_SMALL == JCC_SIZE_SMALL);
                assert(JMP_SIZE_SMALL == 2);

                id.idjAddr = distVal > 0 ? dst : null;
                dst = emitOutputShortBranch(dst, ins, format, distVal, id);
            }
            else
            {
                id.idjAddr = dstOffs > srcOffs ? dst : null;
                if (format == IF_LARGEJMP)
                {
                    dst = emitOutputShortBranch(
                        dst,
                        emitJumpKindToIns(emitReverseJumpKind(emitInsToJumpKind(ins))),
                        IF_T1_K,
                        2,
                        null);
                    ins = INS_b;
                    format = IF_T2_J2;
                    distVal = unchecked(distVal - 2);
                }

                code = emitInsCode(ins, format);
                if (format == IF_T2_J1)
                {
                    assert(!id.idjKeepLong);
                    assert(!emitJumpCrossHotColdBoundary(srcOffs, dstOffs));
                    assert((distVal & 1) == 0);
                    assert(distVal >= -1048576);
                    assert(distVal <= 1048574);

                    if (distVal < 0)
                    {
                        code |= 1u << 26;
                    }
                    code |= unchecked((uint)(distVal >> 1)) & 0x000007FF;
                    code |= (unchecked((uint)(distVal >> 1)) & 0x001F800) << 5;
                    code |= (unchecked((uint)(distVal >> 1)) & 0x020000) >> 4;
                    code |= (unchecked((uint)(distVal >> 1)) & 0x040000) >> 7;
                }
                else if (format == IF_T2_J2)
                {
                    assert((distVal & 1) == 0);
                    if (_compiler.opts.compReloc && emitJumpCrossHotColdBoundary(srcOffs, dstOffs))
                    {
                        // A relocation resolves the distance across separate hot and cold sections.
                    }
                    else
                    {
                        if ((distVal < CALL_DIST_MAX_NEG) || (distVal > CALL_DIST_MAX_POS))
                        {
                            IMPL_LIMITATION("Method is too large");
                        }

                        if (distVal < 0)
                        {
                            code |= 1u << 26;
                        }
                        code |= unchecked((uint)(distVal >> 1)) & 0x000007FF;
                        code |= (unchecked((uint)(distVal >> 1)) & 0x001FF800) << 5;

                        var sign = distVal < 0;
                        var i1 = (distVal & 0x00800000) == 0;
                        var i2 = (distVal & 0x00400000) == 0;
                        if (sign ^ i1)
                        {
                            code |= 1u << 13;
                        }
                        if (sign ^ i2)
                        {
                            code |= 1u << 11;
                        }
                    }
                }
                else
                {
                    throw new FatalJitException(CORJIT_INTERNALERROR, "Unknown ARM32 branch format.");
                }

                var instructionSize = emitOutput_Thumb2Instr(dst, code);
                if (_compiler.opts.compReloc && emitJumpCrossHotColdBoundary(srcOffs, dstOffs))
                {
                    assert(id.idjKeepLong);
                    if (_compiler.info.compMatchedVM)
                    {
                        emitRecordRelocation(dst, emitOffsetToPtr(dstOffs), CorInfoReloc.ARM32_THUMB_BRANCH24);
                    }
                }
                dst = unchecked(dst + instructionSize);
            }
        }
        else if (loadLabel)
        {
            id.idjAddr = distVal > 0 ? dst : null;
            code = emitInsCode(ins, format);

            if (format == IF_T1_J3)
            {
                assert((dstOffs & 3) == 0);
                assert(distVal >= 0);
                assert(distVal <= 1022);
                code |= (uint)(distVal >> 2) & 0xFF;
                dst = unchecked(dst + emitOutput_Thumb1Instr(dst, code));
            }
            else if (format == IF_T2_M1)
            {
                assert(distVal >= -4095);
                assert(distVal <= 4095);
                if (distVal < 0)
                {
                    code |= 0x00A0u << 16;
                    distVal = -distVal;
                }
                assert((distVal & 0x0FFF) == distVal);
                code |= (uint)distVal & 0x00FF;
                code |= ((uint)distVal & 0x0700) << 4;
                code |= ((uint)distVal & 0x0800) << 15;
                code |= (uint)id.idReg1() << 8;
                dst = unchecked(dst + emitOutput_Thumb2Instr(dst, code));
            }
            else if (format == IF_T2_N1)
            {
                assert(ins is INS_movt or INS_movw);
                code |= insEncodeRegT2_D(id.idReg1());
                id.idjAddr = dstOffs > srcOffs ? dst : null;

                if (id.idIsReloc())
                {
                    dst = unchecked(dst + emitOutput_Thumb2Instr(dst, code));
                    if ((ins == INS_movt) && _compiler.info.compMatchedVM)
                    {
                        emitHandlePCRelativeMov32(dst - 8, (void*)distVal);
                    }
                }
                else
                {
                    var immediate = unchecked((uint)distVal);
                    immediate = ins == INS_movw
                        ? immediate & 0xFFFF
                        : (immediate >> 16) & 0xFFFF;
                    code |= insEncodeImmT2_Mov((int)immediate);
                    dst = unchecked(dst + emitOutput_Thumb2Instr(dst, code));
                }
            }
            else
            {
                throw new FatalJitException(CORJIT_INTERNALERROR, "Unknown ARM32 label format.");
            }
        }

        return dst;
    }
}
#endif
