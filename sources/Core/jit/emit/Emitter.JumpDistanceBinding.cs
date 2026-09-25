// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitJumpDistBind()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Jump distance binding requires AMD64.");
#else
#if DEBUG
        var compiler = _compiler ?? throw new FatalJitException("Jump distance binding requires an active compiler.");
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

            for (var jump = emitJumpList; jump is not null; jump = jump.idjNext)
            {
                var jumpIG = jump.idjIG ?? throw new FatalJitException("A saved jump requires an instruction group.");
                var size = jump.idCodeSize();
                uint shortSize = 0;
                var maxNegativeDistance = 0;
                var maxPositiveDistance = 0;

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
                            previousJumpIG.igOffs = unchecked(previousJumpIG.igOffs - groupAdjustment);
                            assert((previousJumpIG.igOffs & (CODE_ALIGN - 1)) == 0);
                        }
                        while (!ReferenceEquals(previousJumpIG, jumpIG));
                    }

                    localAdjustment = 0;
                    previousJumpIG = jumpIG;
                }

                jump.idjOffs = unchecked(jump.idjOffs - localAdjustment);

                insGroup targetIG;
                if (jump.idIsBound())
                {
                    if (jump.idjShort)
                    {
                        assert(jump.idCodeSize() == shortSize);
#if DEBUG
                        emitCheckFuncletBranch(jump, jumpIG);
#endif
                        continue;
                    }

                    targetIG = jump.idjTargetIG
                        ?? throw new FatalJitException("A bound jump requires an instruction-group target.");
                }
                else
                {
                    targetIG = emitBindJump(jump);
                }

#if DEBUG
                emitCheckFuncletBranch(jump, jumpIG);
#endif
                if (jump.idIns() is INS_push or INS_mov or INS_call or INS_push_hide)
                {
                    continue;
                }

                var sourceOffset = unchecked(jumpIG.igOffs + jump.idjOffs);
                var destinationOffset = targetIG.igOffs;
                var encodingOffset = unchecked(sourceOffset + shortSize);
                int extra;
                int distance;

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

                if (extra > 0)
                {
                    assert(!jump.idjShort);
                    if (minShortExtra > unchecked((uint)extra))
                    {
                        minShortExtra = unchecked((uint)extra);
                    }

                    continue;
                }

                emitSetShortJump(jump);
                if (!jump.idjShort)
                {
                    continue;
                }

                var difference = unchecked(size - shortSize);
                assert(size >= shortSize);
                jump.idCodeSize(shortSize);
                assert(((shortSize | unchecked((uint)distance)) == 0) || (shortSize == jump.idCodeSize()));
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
                group.igOffs = unchecked(group.igOffs - groupAdjustment);
                assert((group.igOffs & (CODE_ALIGN - 1)) == 0);
            }

#if DEBUG
            emitCheckIGList();
            if (compiler.verbose)
            {
                jitprintf($"Total shrinkage = {groupAdjustment,3}, min extra jump size = {minShortExtra,3}\n");
            }
#endif
            if (minShortExtra > groupAdjustment)
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
