// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitJumpDistBind()
    {
#if TARGET_LOONGARCH64
        emitJumpDistBindLoongArch64();
#else
#if TARGET_ARM
        const uint smallConditionalSize = 2;
        const uint smallJumpSize = 2;
        const uint smallLabelSize = 2;
        const uint mediumConditionalSize = 4;
#elif TARGET_ARM64
        const uint smallConditionalSize = 4;
        const uint smallJumpSize = 4;
        const uint smallLabelSize = 4;
        const uint smallConstantSize = 4;
#endif
#if DEBUG
        var compiler = _compiler ?? throw new FatalJitException("Jump distance binding requires an active compiler.");
        var interestingJumpNumber = -1;
        if (compiler.verbose)
        {
            jitprintf("*************** In emitJumpDistBind()\n");
            emitDispJumpList();
        }
#endif
        for (var fixedJump = emitFixedSizeJumpList; fixedJump is not null; fixedJump = fixedJump.idjNext)
        {
            if (!fixedJump.idIsBound())
            {
                _ = emitBindJump(fixedJump);
            }
        }

#if DEBUG
        var iteration = 1;
#endif
        while (true)
        {
#if DEBUG
            emitCheckIGList();
            insGroup? lastIG = null;
            instrDescJmp? lastJump = null;
#endif
            insGroup? previousJumpIG = null;
            uint groupAdjustment = 0;
            uint localAdjustment = 0;
            var minShortExtra = uint.MaxValue;
#if TARGET_ARM || TARGET_RISCV64
            var minMediumExtra = uint.MaxValue;
#endif

            for (var jump = emitJumpList; jump is not null; jump = jump.idjNext)
            {
                var jumpIG = jump.idjIG ?? throw new FatalJitException("A saved jump requires an instruction group.");
                var size = jump.idCodeSize();
#if DEBUG
                var debugInfo = jump.idDebugOnlyInfo();
                assert(debugInfo is not null);
#endif
                uint shortSize = 0;
                var maxNegativeDistance = 0;
                var maxPositiveDistance = 0;
#if TARGET_ARM || TARGET_RISCV64
                uint mediumSize = 0;
                var maxMediumNegativeDistance = 0;
                var maxMediumPositiveDistance = 0;
#endif

#if TARGET_XARCH
                var format = jump.idInsFmt();
                assert(format is IF_LABEL or IF_RWR_LABEL or IF_SWR_LABEL);
                if (format == IF_LABEL)
                {
                    if (jump.idIns() is not (INS_call or INS_jmp))
                    {
                        shortSize = JCC_SIZE_SMALL;
                        maxNegativeDistance = JCC_DIST_SMALL_MAX_NEG;
                        maxPositiveDistance = JCC_DIST_SMALL_MAX_POS;
                    }
                    else
                    {
                        shortSize = JMP_SIZE_SMALL;
                        maxNegativeDistance = JMP_DIST_SMALL_MAX_NEG;
                        maxPositiveDistance = JMP_DIST_SMALL_MAX_POS;
                    }
                }
#elif TARGET_ARM
                assert(jump.idInsFmt() is IF_T2_J1 or IF_T2_J2 or IF_T1_I or IF_T1_K or IF_T1_M
                    or IF_T2_M1 or IF_T2_N1 or IF_T1_J3 or IF_LARGEJMP);
                if (emitIsCondJump(jump))
                {
                    shortSize = smallConditionalSize;
                    maxNegativeDistance = -256;
                    maxPositiveDistance = 254;
                    mediumSize = mediumConditionalSize;
                    maxMediumNegativeDistance = -1048576;
                    maxMediumPositiveDistance = 1048574;
                }
                else if (emitIsCmpJump(jump))
                {
                    shortSize = smallJumpSize;
                    maxPositiveDistance = 126;
                }
                else if (emitIsUncondJump(jump))
                {
                    shortSize = smallJumpSize;
                    maxNegativeDistance = -2048;
                    maxPositiveDistance = 2046;
                }
                else if (emitIsLoadLabel(jump))
                {
                    shortSize = smallLabelSize;
                    maxPositiveDistance = 1020;
                }
                else
                {
                    assert(false, "Unknown jump instruction");
                }
#elif TARGET_ARM64
                if (emitIsCondJump(jump))
                {
                    shortSize = smallConditionalSize;
                    var isTest = jump.idIns() is INS_tbz or INS_tbnz;
                    maxNegativeDistance = isTest ? -32768 : -1048576;
                    maxPositiveDistance = isTest ? 32767 : 1048575;
                }
                else if (emitIsUncondJump(jump))
                {
                    shortSize = smallJumpSize;
                }
                else if (emitIsLoadLabel(jump))
                {
                    shortSize = smallLabelSize;
                    maxNegativeDistance = -1048576;
                    maxPositiveDistance = 1048575;
                }
                else if (emitIsLoadConstant(jump))
                {
                    shortSize = smallConstantSize;
                    maxNegativeDistance = -1048576;
                    maxPositiveDistance = 1048575;
                }
                else
                {
                    assert(false, "Unknown jump instruction");
                }
#elif TARGET_RISCV64
                if (emitIsCmpJump(jump))
                {
                    shortSize = 4;
                    maxNegativeDistance = -4096;
                    maxPositiveDistance = 4095;
                    mediumSize = 8;
                    maxMediumNegativeDistance = -1048576 + unchecked((int)shortSize);
                    maxMediumPositiveDistance = 1048575 + unchecked((int)shortSize);
                }
                else if (emitIsUncondJump(jump))
                {
                    shortSize = 4;
                    maxNegativeDistance = -1048576;
                    maxPositiveDistance = 1048575;
                }
                else
                {
                    assert(false, "Unknown jump instruction");
                }
#endif

#if DEBUG
                assert(lastJump is null || !ReferenceEquals(lastIG, jumpIG) || lastJump.idjOffs < jump.idjOffs);
                lastJump = ReferenceEquals(lastIG, jumpIG) ? jump : null;
                assert(lastIG is null || ReferenceEquals(lastIG, jumpIG) || lastIG.IsBefore(jumpIG)
                    || emitIGisInProlog(jumpIG));
                lastIG = jumpIG;
#endif
                if (!ReferenceEquals(previousJumpIG, jumpIG))
                {
                    if (previousJumpIG is not null)
                    {
                        do
                        {
                            previousJumpIG = previousJumpIG.igNext
                                ?? throw new FatalJitException("A jump's instruction group is absent from the group list.");
#if DEBUG
                            if (compiler.verbose)
                            {
                                jitprintf($"Adjusted offset of {FMT_BB(checked((int)previousJumpIG.GetDisplayId()))} " +
                                    $"from {previousJumpIG.igOffs:X4} to {unchecked(previousJumpIG.igOffs - groupAdjustment):X4}\n");
                            }
#endif
                            previousJumpIG.igOffs = unchecked(previousJumpIG.igOffs - groupAdjustment);
                            assert(IsCodeAligned(previousJumpIG.igOffs));
                        }
                        while (!ReferenceEquals(previousJumpIG, jumpIG));
                    }

                    localAdjustment = 0;
                    previousJumpIG = jumpIG;
                }

                jump.idjOffs = unchecked(jump.idjOffs - localAdjustment);

                var distance = 0;
#if TARGET_ARM64
                if (jump.idAddr().iiaIsJitDataOffset())
                {
                    assert(jump.idIsBound());
                    if (jump.idjShort)
                    {
                        assert(jump.idCodeSize() == shortSize);
                        continue;
                    }

                    var source = unchecked(jumpIG.igOffs + jump.idjOffs);
                    var dataOffset = jump.idAddr().iiaGetJitDataOffset();
                    assert(dataOffset >= 0);
                    var immediate = emitGetInsSC(jump);
                    assert(immediate >= 0 && immediate < 0x1000);
                    var data = unchecked((uint)((nint)dataOffset + immediate));
                    assert(data < emitDataSize());
                    assert(unchecked((uint)emitTotalCodeSize) > 0);
                    var destination = unchecked((uint)emitTotalCodeSize + data);
                    distance = unchecked((int)(destination - source));
                    var dataExtra = unchecked(distance - maxPositiveDistance);
                    if (dataExtra <= 0)
                    {
                        goto SHORT_JMP;
                    }

                    continue;
                }
#endif

                insGroup targetIG;
                if (jump.idIsBound())
                {
                    if (jump.idjShort)
                    {
                        assert(jump.idCodeSize() == shortSize);
                        emitCheckFuncletBranch(jump, jumpIG);
                        continue;
                    }

                    targetIG = jump.idjTargetIG
                        ?? throw new FatalJitException("A bound jump requires an instruction-group target.");
                }
                else
                {
                    targetIG = emitBindJump(jump);
                }

                emitCheckFuncletBranch(jump, jumpIG);
#if TARGET_XARCH
                if (jump.idIns() is INS_push or INS_mov or INS_call or INS_push_hide)
                {
                    continue;
                }
#elif TARGET_ARM
                if (jump.idIns() is INS_push or INS_mov or INS_movt or INS_movw)
                {
                    continue;
                }
#elif TARGET_ARM64
                if (emitIsUncondJump(jump))
                {
                    continue;
                }
#endif

                var sourceOffset = unchecked(jumpIG.igOffs + jump.idjOffs);
                var destinationOffset = targetIG.igOffs;
#if TARGET_ARM
                var encodingOffset = unchecked(sourceOffset + 4);
#elif TARGET_ARM64 || TARGET_RISCV64
                var encodingOffset = sourceOffset;
#else
                var encodingOffset = unchecked(sourceOffset + shortSize);
#endif
                int extra;

                if (jumpIG.IsBefore(targetIG))
                {
                    destinationOffset = unchecked(destinationOffset - groupAdjustment);
                    distance = unchecked((int)(destinationOffset - encodingOffset));
                    extra = unchecked(distance - maxPositiveDistance);
                }
                else
                {
                    distance = unchecked((int)(encodingOffset - destinationOffset));
                    extra = unchecked(distance + maxNegativeDistance);
                }

#if DEBUG
                if (jumpIG.IsBefore(targetIG))
                {
                    if (debugInfo.idNum == unchecked((uint)interestingJumpNumber)
                        || interestingJumpNumber == 0)
                    {
                        if (interestingJumpNumber == 0)
                        {
                            jitprintf($"[1] Jump {debugInfo.idNum}:\n");
                        }

                        jitprintf($"[1] Jump  block is at {jumpIG.igOffs:X8}\n");
                        jitprintf($"[1] Jump reloffset is {jump.idjOffs:X4}\n");
                        jitprintf($"[1] Jump source is at {encodingOffset:X8}\n");
                        jitprintf($"[1] Label block is at {destinationOffset:X8}\n");
                        jitprintf($"[1] Jump  dist. is    {unchecked((uint)distance):X4}\n");
                        if (extra > 0)
                        {
                            jitprintf($"[1] Dist excess [S] = {extra}  \n");
                        }
                    }

                    if (compiler.verbose)
                    {
                        var jumpForDisplay = jump;
                        jitprintf($"Estimate of fwd jump [{FMT_PTR((void*)dspPtr((void*)Unsafe.As<instrDescJmp, nint>(ref jumpForDisplay)))}/{debugInfo.idNum:D3}]: " +
                            $"{sourceOffset:X4} -> {destinationOffset:X4} = {unchecked((uint)distance):X4}\n");
                    }
                }
                else
                {
                    if (debugInfo.idNum == unchecked((uint)interestingJumpNumber)
                        || interestingJumpNumber == 0)
                    {
                        if (interestingJumpNumber == 0)
                        {
                            jitprintf($"[2] Jump {debugInfo.idNum}:\n");
                        }

                        jitprintf($"[2] Jump  block is at {jumpIG.igOffs:X8}\n");
                        jitprintf($"[2] Jump reloffset is {jump.idjOffs:X4}\n");
                        jitprintf($"[2] Jump source is at {encodingOffset:X8}\n");
                        jitprintf($"[2] Label block is at {destinationOffset:X8}\n");
                        jitprintf($"[2] Jump  dist. is    {unchecked((uint)distance):X4}\n");
                        if (extra > 0)
                        {
                            jitprintf($"[2] Dist excess [S] = {extra}  \n");
                        }
                    }

                    if (compiler.verbose)
                    {
                        var jumpForDisplay = jump;
                        jitprintf($"Estimate of bwd jump [{FMT_PTR((void*)dspPtr((void*)Unsafe.As<instrDescJmp, nint>(ref jumpForDisplay)))}/{debugInfo.idNum:D3}]: " +
                            $"{sourceOffset:X4} -> {destinationOffset:X4} = {unchecked((uint)distance):X4}\n");
                    }
                }
#endif

                if (extra > 0)
                {
                    assert(!jump.idjShort);
                    if (minShortExtra > unchecked((uint)extra))
                    {
                        minShortExtra = unchecked((uint)extra);
                    }

#if TARGET_ARM || TARGET_RISCV64
                    if (emitIsCmpJump(jump))
                    {
                        var mediumExtra = jumpIG.IsBefore(targetIG)
                            ? unchecked(distance - maxMediumPositiveDistance)
                            : unchecked(distance + maxMediumNegativeDistance);
#if DEBUG
                        if (debugInfo.idNum == unchecked((uint)interestingJumpNumber)
                            || interestingJumpNumber == 0)
                        {
                            if (mediumExtra > 0)
                            {
                                var prefix = jumpIG.IsBefore(targetIG) ? 6 : 7;
                                if (interestingJumpNumber == 0)
                                {
                                    jitprintf($"[{prefix}] Jump {debugInfo.idNum}:\n");
                                }

                                jitprintf($"[{prefix}] Dist excess [S] = {mediumExtra}  \n");
                            }
                        }
#endif
                        if (mediumExtra <= 0)
                        {
                            goto MEDIUM_JMP;
                        }

                        if (minMediumExtra > unchecked((uint)mediumExtra))
                        {
                            minMediumExtra = unchecked((uint)mediumExtra);
                        }
                    }
#endif
                    continue;
                }

#if TARGET_ARM64
            SHORT_JMP:
#endif
                emitSetShortJump(jump);
                if (!jump.idjShort)
                {
                    continue;
                }

                var difference = unchecked(size - shortSize);
                assert(size >= shortSize);
#if TARGET_XARCH
                jump.idCodeSize(shortSize);
#elif TARGET_ARM
                // emitSetShortJump updates the ARM format and instruction size.
#elif TARGET_ARM64
                assert(difference is 4 or 8);
#elif TARGET_RISCV64
                assert(difference is 0 or 4 or 8);
#elif TARGET_WASM
                unreached();
#else
#error Unsupported or unset target architecture
#endif
#if TARGET_ARM || TARGET_RISCV64
                goto NEXT_JMP;

            MEDIUM_JMP:
                emitSetMediumJump(jump);
                if (jump.idCodeSize() > mediumSize)
                {
                    continue;
                }

                assert(jump.idCodeSize() == mediumSize);
                shortSize = mediumSize;
                assert(size >= shortSize);
                difference = unchecked(size - shortSize);

            NEXT_JMP:
#endif
                assert(((shortSize | unchecked((uint)distance)) == 0) || (shortSize == jump.idCodeSize()));
#if DEBUG
                if (compiler.verbose)
                {
                    var jumpForDisplay = jump;
                    jitprintf($"Shrinking jump [{FMT_PTR((void*)dspPtr((void*)Unsafe.As<instrDescJmp, nint>(ref jumpForDisplay)))}/{debugInfo.idNum:D3}]\n");
                }
#endif
                noway_assert(unchecked((ushort)difference) == difference);

                groupAdjustment = unchecked(groupAdjustment + difference);
                localAdjustment = unchecked(localAdjustment + difference);
                jumpIG.igSize = unchecked((ushort)(jumpIG.igSize - difference));
                emitTotalCodeSize = unchecked(emitTotalCodeSize - (int)difference);
                jumpIG.igFlags |= InsGroupFlags.UpdatedInstructionSize;
            }

            if (groupAdjustment == 0)
            {
                break;
            }

            assert(previousJumpIG is not null);
            for (var group = previousJumpIG.igNext; group is not null; group = group.igNext)
            {
#if DEBUG
                if (compiler.verbose)
                {
                    jitprintf($"Adjusted offset of {FMT_BB(checked((int)group.GetDisplayId()))} from {group.igOffs:X4} " +
                        $"to {unchecked(group.igOffs - groupAdjustment):X4}\n");
                }
#endif
                group.igOffs = unchecked(group.igOffs - groupAdjustment);
                assert(IsCodeAligned(group.igOffs));
            }

#if DEBUG
            emitCheckIGList();
            if (compiler.verbose)
            {
#if TARGET_ARM
                jitprintf($"Total shrinkage = {groupAdjustment,3}, min extra short jump size = {minShortExtra,3}, " +
                    $"min extra medium jump size = {minMediumExtra}\n");
#else
                jitprintf($"Total shrinkage = {groupAdjustment,3}, min extra jump size = {minShortExtra,3}\n");
#endif
            }
#endif
            if (minShortExtra > groupAdjustment
#if TARGET_ARM
                && minMediumExtra > groupAdjustment
#endif
                )
            {
                break;
            }

#if DEBUG
            iteration++;
            if (compiler.verbose)
            {
                jitprintf($"Iterating branch shortening. Iteration = {iteration}\n");
            }
#endif
        }

#if DEBUG
        emitCheckIGList();
#endif
#endif
    }
}
