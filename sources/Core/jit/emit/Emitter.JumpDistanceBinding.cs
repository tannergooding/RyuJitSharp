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
        if (Configuration.TARGET_LOONGARCH64)
        {
            emitJumpDistBindLoongArch64();
            return;
        }

        uint smallConditionalSize = 0;
        uint smallJumpSize = 0;
        uint smallLabelSize = 0;
        uint mediumConditionalSize = 0;
        uint smallConstantSize = 0;
        if (Configuration.TARGET_ARM)
        {
            smallConditionalSize = 2;
            smallJumpSize = 2;
            smallLabelSize = 2;
            mediumConditionalSize = 4;
        }
        else if (Configuration.TARGET_ARM64)
        {
            smallConditionalSize = 4;
            smallJumpSize = 4;
            smallLabelSize = 4;
            smallConstantSize = 4;
        }
        int interestingJumpNumber;
        if (Configuration.DEBUG)
        {
            interestingJumpNumber = -1;
            var compiler = _compiler;
            if (compiler is null)
            {
                FatalJitException.ThrowFatalJitException("Jump distance binding requires an active compiler.");
                return;
            }
            if (compiler.verbose)
            {
                jitprintf("*************** In emitJumpDistBind()\n");
                emitDispJumpList();
            }
        }
        for (var fixedJump = emitFixedSizeJumpList; fixedJump is not null; fixedJump = fixedJump.idjNext)
        {
            if (!fixedJump.idIsBound())
            {
                _ = emitBindJump(fixedJump);
            }
        }

        int iteration;
        if (Configuration.DEBUG)
        {
            iteration = 1;
        }
        while (true)
        {
            emitCheckIGList();
            insGroup? lastIG;
            instrDescJmp? lastJump;
            if (Configuration.DEBUG)
            {
                lastIG = null;
                lastJump = null;
            }
            insGroup? previousJumpIG = null;
            uint groupAdjustment = 0;
            uint localAdjustment = 0;
            var minShortExtra = uint.MaxValue;
            var minMediumExtra = uint.MaxValue;

            for (var jump = emitJumpList; jump is not null; jump = jump.idjNext)
            {
                var jumpIG = jump.idjIG;
                if (jumpIG is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.SavedJumpRequiresInstructionGroup);
                    return;
                }
                var size = jump.idCodeSize();
                instrDescDebugInfo? debugInfo;
                if (Configuration.DEBUG)
                {
                    debugInfo = jump.idDebugOnlyInfo();
                    assert(debugInfo is not null);
                }
                uint shortSize = 0;
                var maxNegativeDistance = 0;
                var maxPositiveDistance = 0;
                uint mediumSize = 0;
                var maxMediumNegativeDistance = 0;
                var maxMediumPositiveDistance = 0;

                if (Configuration.TARGET_XARCH)
                {
                    var format = jump.idInsFmt();
                    assert(format is IF_XARCH_LABEL or IF_XARCH_RWR_LABEL or IF_XARCH_SWR_LABEL);
                    if (format == IF_XARCH_LABEL)
                    {
                        if (jump.idIns() is not (INS_call or INS_jmp))
                        {
                            shortSize = unchecked((uint)JCC_SIZE_SMALL);
                            maxNegativeDistance = JCC_DIST_SMALL_MAX_NEG;
                            maxPositiveDistance = JCC_DIST_SMALL_MAX_POS;
                        }
                        else
                        {
                            shortSize = unchecked((uint)JMP_SIZE_SMALL);
                            maxNegativeDistance = JMP_DIST_SMALL_MAX_NEG;
                            maxPositiveDistance = JMP_DIST_SMALL_MAX_POS;
                        }
                    }
                }
                else if (Configuration.TARGET_ARM)
                {
                    assert(jump.idInsFmt() is IF_ARM32_T2_J1 or IF_ARM32_T2_J2 or IF_ARM32_T1_I or IF_ARM32_T1_K or IF_ARM32_T1_M
                        or IF_ARM32_T2_M1 or IF_ARM32_T2_N1 or IF_ARM32_T1_J3 or IF_ARM32_LARGEJMP);
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
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "Unknown ARM jump instruction.");
                        return;
                    }
                }
                else if (Configuration.TARGET_ARM64)
                {
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
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "Unknown ARM64 jump instruction.");
                        return;
                    }
                }
                else if (Configuration.TARGET_RISCV64)
                {
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
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "Unknown RISC-V jump instruction.");
                        return;
                    }
                }
                else
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Jump distance binding is not supported for this target.");
                    return;
                }

                if (Configuration.DEBUG)
                {
                    assert(lastJump is null || !ReferenceEquals(lastIG, jumpIG) || lastJump.idjOffs < jump.idjOffs);
                    lastJump = ReferenceEquals(lastIG, jumpIG) ? jump : null;
                    assert(lastIG is null || ReferenceEquals(lastIG, jumpIG) || lastIG.IsBefore(jumpIG)
                        || emitIGisInProlog(jumpIG));
                    lastIG = jumpIG;
                }

                if (!ReferenceEquals(previousJumpIG, jumpIG))
                {
                    if (previousJumpIG is not null)
                    {
                        do
                        {
                            var nextJumpIG = previousJumpIG.igNext;
                            if (nextJumpIG is null)
                            {
                                FatalJitException.ThrowFatalJitException(
                                    CORJIT_INTERNALERROR,
                                    FatalJitErrorCode.JumpInstructionGroupMissingFromList
                                );
                                return;
                            }

                            previousJumpIG = nextJumpIG;
                            if (Configuration.DEBUG && _compiler!.verbose)
                            {
                                jitprintf($"Adjusted offset of {FMT_BB(checked((int)previousJumpIG.GetDisplayId()))} " +
                                    $"from {previousJumpIG.igOffs:X4} to {unchecked(previousJumpIG.igOffs - groupAdjustment):X4}\n");
                            }
                            previousJumpIG.igOffs = unchecked(previousJumpIG.igOffs - groupAdjustment);
                            assert(IsCodeAligned(previousJumpIG.igOffs));
                        }
                        while (!ReferenceEquals(previousJumpIG, jumpIG));
                    }

                    localAdjustment = 0;
                    previousJumpIG = jumpIG;
                }

                jump.idjOffs = unchecked(jump.idjOffs - localAdjustment);

                int distance;
                if (Configuration.TARGET_ARM64 && jump.idAddr().iiaIsJitDataOffset())
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
                insGroup targetIG;
                if (jump.idIsBound())
                {
                    if (jump.idjShort)
                    {
                        assert(jump.idCodeSize() == shortSize);
                        emitCheckFuncletBranch(jump, jumpIG);
                        continue;
                    }

                    var boundTargetIG = jump.idjTargetIG;
                    if (boundTargetIG is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.BoundJumpMissingTarget
                        );
                        return;
                    }

                    targetIG = boundTargetIG;
                }
                else
                {
                    targetIG = emitBindJump(jump);
                }

                emitCheckFuncletBranch(jump, jumpIG);
                if (Configuration.TARGET_XARCH &&
                    (jump.idIns() is INS_push or INS_mov or INS_call or INS_push_hide))
                {
                    continue;
                }
                else if (Configuration.TARGET_ARM &&
                    (jump.idIns() is INS_push or INS_mov or INS_movt or INS_movw))
                {
                    continue;
                }
                else if (Configuration.TARGET_ARM64 && emitIsUncondJump(jump))
                {
                    continue;
                }

                var sourceOffset = unchecked(jumpIG.igOffs + jump.idjOffs);
                var destinationOffset = targetIG.igOffs;
                var encodingOffset = Configuration.TARGET_ARM
                    ? unchecked(sourceOffset + 4)
                    : Configuration.TARGET_ARM64 || Configuration.TARGET_RISCV64
                        ? sourceOffset
                        : unchecked(sourceOffset + shortSize);
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

                if (Configuration.DEBUG)
                {
                    if (jumpIG.IsBefore(targetIG))
                    {
                        if (debugInfo!.idNum == unchecked((uint)interestingJumpNumber)
                            || interestingJumpNumber == 0)
                        {
                            if (interestingJumpNumber == 0)
                            {
                                jitprintf($"[1] Jump {debugInfo!.idNum}:\n");
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

                        if (_compiler!.verbose)
                        {
                            var jumpForDisplay = jump;
                            jitprintf($"Estimate of fwd jump [{FMT_PTR((void*)dspPtr((void*)Unsafe.As<instrDescJmp, nint>(ref jumpForDisplay)))}/{debugInfo!.idNum:D3}]: " +
                                $"{sourceOffset:X4} -> {destinationOffset:X4} = {unchecked((uint)distance):X4}\n");
                        }
                    }
                    else
                    {
                        if (debugInfo!.idNum == unchecked((uint)interestingJumpNumber)
                            || interestingJumpNumber == 0)
                        {
                            if (interestingJumpNumber == 0)
                            {
                                jitprintf($"[2] Jump {debugInfo!.idNum}:\n");
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

                        if (_compiler!.verbose)
                        {
                            var jumpForDisplay = jump;
                            jitprintf($"Estimate of bwd jump [{FMT_PTR((void*)dspPtr((void*)Unsafe.As<instrDescJmp, nint>(ref jumpForDisplay)))}/{debugInfo!.idNum:D3}]: " +
                                $"{sourceOffset:X4} -> {destinationOffset:X4} = {unchecked((uint)distance):X4}\n");
                        }
                    }
                }

                if (extra > 0)
                {
                    assert(!jump.idjShort);
                    if (minShortExtra > unchecked((uint)extra))
                    {
                        minShortExtra = unchecked((uint)extra);
                    }

                    if ((Configuration.TARGET_ARM || Configuration.TARGET_RISCV64) && emitIsCmpJump(jump))
                    {
                        var mediumExtra = jumpIG.IsBefore(targetIG)
                            ? unchecked(distance - maxMediumPositiveDistance)
                            : unchecked(distance + maxMediumNegativeDistance);
                        if (Configuration.DEBUG)
                        {
                            if (debugInfo!.idNum == unchecked((uint)interestingJumpNumber)
                                || interestingJumpNumber == 0)
                            {
                                if (mediumExtra > 0)
                                {
                                    var prefix = jumpIG.IsBefore(targetIG) ? 6 : 7;
                                    if (interestingJumpNumber == 0)
                                    {
                                        jitprintf($"[{prefix}] Jump {debugInfo!.idNum}:\n");
                                    }

                                    jitprintf($"[{prefix}] Dist excess [S] = {mediumExtra}  \n");
                                }
                            }
                        }
                        if (mediumExtra <= 0)
                        {
                            goto MEDIUM_JMP;
                        }

                        if (minMediumExtra > unchecked((uint)mediumExtra))
                        {
                            minMediumExtra = unchecked((uint)mediumExtra);
                        }
                    }
                    continue;
                }

            SHORT_JMP:
                emitSetShortJump(jump);
                if (!jump.idjShort)
                {
                    continue;
                }

                var difference = unchecked(size - shortSize);
                assert(size >= shortSize);
                if (Configuration.TARGET_XARCH)
                {
                    jump.idCodeSize(shortSize);
                }
                else if (Configuration.TARGET_ARM)
                {
                    // emitSetShortJump updates the ARM format and instruction size.
                }
                else if (Configuration.TARGET_ARM64)
                {
                    assert(difference is 4 or 8);
                }
                else if (Configuration.TARGET_RISCV64)
                {
                    assert(difference is 0 or 4 or 8);
                }
                else if (Configuration.TARGET_WASM)
                {
                    unreached();
                }
                else
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                        "Short-jump size update is not supported for this target.");
                    return;
                }

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
                assert(((shortSize | unchecked((uint)distance)) == 0) || (shortSize == jump.idCodeSize()));
                if (Configuration.DEBUG && _compiler!.verbose)
                {
                    var jumpForDisplay = jump;
                    jitprintf($"Shrinking jump [{FMT_PTR((void*)dspPtr((void*)Unsafe.As<instrDescJmp, nint>(ref jumpForDisplay)))}/{debugInfo!.idNum:D3}]\n");
                }
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
                if (Configuration.DEBUG && _compiler!.verbose)
                {
                    jitprintf($"Adjusted offset of {FMT_BB(checked((int)group.GetDisplayId()))} from {group.igOffs:X4} " +
                        $"to {unchecked(group.igOffs - groupAdjustment):X4}\n");
                }
                group.igOffs = unchecked(group.igOffs - groupAdjustment);
                assert(IsCodeAligned(group.igOffs));
            }

            emitCheckIGList();
            if (Configuration.DEBUG && _compiler!.verbose)
            {
                if (Configuration.TARGET_ARM)
                {
                    jitprintf($"Total shrinkage = {groupAdjustment,3}, min extra short jump size = {minShortExtra,3}, " +
                        $"min extra medium jump size = {minMediumExtra}\n");
                }
                else
                {
                    jitprintf($"Total shrinkage = {groupAdjustment,3}, min extra jump size = {minShortExtra,3}\n");
                }
            }
            if (minShortExtra > groupAdjustment &&
                (!Configuration.TARGET_ARM || minMediumExtra > groupAdjustment))
            {
                break;
            }

            if (Configuration.DEBUG)
            {
                iteration++;
                if (_compiler!.verbose)
                {
                    jitprintf($"Iterating branch shortening. Iteration = {iteration}\n");
                }
            }
        }

        emitCheckIGList();
    }
}
