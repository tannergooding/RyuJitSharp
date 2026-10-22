// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitJumpDistBindLoongArch64()
    {
        if (Configuration.DEBUG)
        {
            var compiler = _compiler;
            if (compiler is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64JumpBindingRequiresAnActiveCompiler);
                return;
            }
            if (compiler.verbose)
            {
                jitprintf("*************** In emitJumpDistBind()\n");
            }
        }

        var linkingEndState = unchecked((uint)emitTotalCodeSize) <= (0x7fffu << 2) ? 2u : 0u;
        const int smallJumpDelaySlot = 0;
        const int smallJumpMaxNegativeDistance = B_DIST_SMALL_MAX_NEG_LOONGARCH64;
        var smallJumpMaxPositiveDistance = B_DIST_SMALL_MAX_POS_LOONGARCH64 -
            emitCounts_INS_OPTS_J * (3 << 2);

        while (true)
        {
            emitCheckIGList();
            insGroup? previousJumpGroup = null;
            uint localAdjustment = 0;
            uint groupAdjustment = 0;

            for (var jump = emitJumpList; jump is not null; jump = jump.idjNext)
            {
                var jumpGroup = jump.idjIG;
                if (jumpGroup is null)
                {
                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64JumpMustBeAssociatedWithAnInstructionGroup);
                    return;
                }
                var sourceInstructionOffset = unchecked(jumpGroup.igOffs + jump.idjOffs + localAdjustment);
                int sizeAdjustment;

                if (!ReferenceEquals(previousJumpGroup, jumpGroup))
                {
                    if (previousJumpGroup is not null)
                    {
                        do
                        {
                            var nextJumpGroup = previousJumpGroup.igNext;
                            if (nextJumpGroup is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64JumpGroupIsMissingFromTheGroupList);
                                return;
                            }

                            previousJumpGroup = nextJumpGroup;
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

                insGroup targetGroup;
                if (jump.idIsBound())
                {
                    var boundTargetGroup = jump.idjTargetIG;
                    if (boundTargetGroup is null)
                    {
                        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.BoundLoongArch64JumpRequiresATargetGroup);
                        return;
                    }

                    targetGroup = boundTargetGroup;
                }
                else
                {
                    targetGroup = emitBindJump(jump);
                }

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
                assert((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_bceqz, INS_bl)));
                if (InstructionOrdinalMaps.IsNativeInstructionOrdinalLessThan(ins, INS_beqz))
                {
                    assert((distance + emitCounts_INS_OPTS_J * sizeof(uint)) < 0x8000000);
                    sizeAdjustment = distance + emitCounts_INS_OPTS_J * sizeof(uint) < 0x8000000 ? 4 : 8;
                }
                else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalLessThan(ins, INS_b))
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

        emitCheckIGList();
    }
}
