// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitJumpDistBindLoongArch64()
    {
#if DEBUG
        var compiler = _compiler
            ?? throw new FatalJitException("LoongArch64 jump binding requires an active compiler.");
        if (compiler.verbose)
        {
            jitprintf("*************** In emitJumpDistBind()\n");
        }
#endif

        var linkingEndState = unchecked((uint)emitTotalCodeSize) <= (0x7fffu << 2) ? 2u : 0u;
        const int smallJumpDelaySlot = 0;
        const int smallJumpMaxNegativeDistance = B_DIST_SMALL_MAX_NEG;
        var smallJumpMaxPositiveDistance = B_DIST_SMALL_MAX_POS -
            emitCounts_INS_OPTS_J * (3 << 2);

        while (true)
        {
#if DEBUG
            emitCheckIGList();
#endif
            insGroup? previousJumpGroup = null;
            uint localAdjustment = 0;
            uint groupAdjustment = 0;

            for (var jump = emitJumpList; jump is not null; jump = jump.idjNext)
            {
                var jumpGroup = jump.idjIG
                    ?? throw new FatalJitException("A LoongArch64 jump must be associated with an instruction group.");
                var sourceInstructionOffset = unchecked(jumpGroup.igOffs + jump.idjOffs + localAdjustment);
                var sizeAdjustment = 0;

                if (!ReferenceEquals(previousJumpGroup, jumpGroup))
                {
                    if (previousJumpGroup is not null)
                    {
                        do
                        {
                            previousJumpGroup = previousJumpGroup.igNext
                                ?? throw new FatalJitException("A LoongArch64 jump group is missing from the group list.");
                            previousJumpGroup.igOffs = unchecked(previousJumpGroup.igOffs + groupAdjustment);
                            assert(IsCodeAligned(previousJumpGroup.igOffs));
                        }
                        while (!ReferenceEquals(previousJumpGroup, jumpGroup));
                    }

                    localAdjustment = 0;
                    previousJumpGroup = jumpGroup;
                    sourceInstructionOffset = unchecked(jumpGroup.igOffs + jump.idjOffs);
                }

                jump.idjOffs = unchecked(jump.idjOffs + localAdjustment);

                var targetGroup = jump.idIsBound()
                    ? jump.idjTargetIG
                        ?? throw new FatalJitException("A bound LoongArch64 jump requires a target group.")
                    : emitBindJump(jump);
                emitCheckFuncletBranch(jump, jumpGroup);

                var sourceEncodingOffset = unchecked(sourceInstructionOffset + smallJumpDelaySlot);
                var destinationOffset = targetGroup.igOffs;
                int distance;
                int extra;
                if (jumpGroup.IsBefore(targetGroup))
                {
                    destinationOffset = unchecked(destinationOffset + groupAdjustment);
                    distance = unchecked((int)(destinationOffset - sourceEncodingOffset));
                    extra = unchecked(distance - smallJumpMaxPositiveDistance);
                    assert(distance >= 0);
                }
                else
                {
                    distance = unchecked((int)(sourceEncodingOffset - destinationOffset));
                    extra = unchecked(distance + smallJumpMaxNegativeDistance);
                    assert(distance >= 0);
                }
                assert((distance & 3) == 0);

                if ((linkingEndState & 2) != 0)
                {
                    jump.idAddr().iiaSetJmpOffset(jumpGroup.IsBefore(targetGroup) ? distance : -distance);
                    continue;
                }

                if (extra <= 0 || jump.idInsOpt() != INS_OPTS_J)
                {
                    continue;
                }

                var ins = jump.idIns();
                assert((INS_bceqz <= ins) && (ins <= INS_bl));
                if (ins < INS_beqz)
                {
                    assert((distance + emitCounts_INS_OPTS_J * sizeof(uint)) < 0x8000000);
                    sizeAdjustment = distance + emitCounts_INS_OPTS_J * sizeof(uint) < 0x8000000 ? 4 : 8;
                }
                else if (ins < INS_b)
                {
                    if (distance + emitCounts_INS_OPTS_J * sizeof(uint) < 0x200000)
                    {
                        continue;
                    }

                    assert((distance + emitCounts_INS_OPTS_J * sizeof(uint)) < 0x8000000);
                    sizeAdjustment = 4;
                }
                else
                {
                    assert(ins is INS_b or INS_bl);
                    assert((distance + emitCounts_INS_OPTS_J * sizeof(uint)) < 0x8000000);
                    continue;
                }

                jump.idInsOpt(INS_OPTS_JIRL);
                jump.idCodeSize(unchecked(jump.idCodeSize() + (uint)sizeAdjustment));
                jumpGroup.igSize = unchecked((ushort)(jumpGroup.igSize + sizeAdjustment));
                localAdjustment = unchecked(localAdjustment + (uint)sizeAdjustment);
                groupAdjustment = unchecked(groupAdjustment + (uint)sizeAdjustment);
                emitTotalCodeSize = unchecked(emitTotalCodeSize + sizeAdjustment);
                jumpGroup.igFlags |= InsGroupFlags.UpdatedInstructionSize;
            }

            if (linkingEndState >= 2)
            {
                break;
            }

            linkingEndState = 2;
            if (previousJumpGroup is not null)
            {
                for (var group = previousJumpGroup.igNext; group is not null; group = group.igNext)
                {
                    group.igOffs = unchecked(group.igOffs + groupAdjustment);
                    assert(IsCodeAligned(group.igOffs));
                }
            }
        }

#if DEBUG
        emitCheckIGList();
#endif
    }
}
#endif
