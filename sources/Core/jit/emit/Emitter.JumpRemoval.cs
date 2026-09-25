// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitRemoveJumpToNextInst()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Removing jumps to the next instruction group requires AMD64.");
#else
        if (!emitContainsRemovableJmpCandidates)
        {
            return;
        }

        assert(_compiler is not null);
        JITDUMP("*************** In emitRemoveJumpToNextInst()\n");
#if DEBUG
        // EMIT_INSTLIST_VERBOSE is disabled in the pinned emitter.
        if (_compiler.verbose)
        {
            emitDispJumpList();
        }
#endif

        uint totalRemovedSize = 0;
        var jmp = emitJumpList;
        instrDescJmp? previousJmp = null;
#if DEBUG
        insGroup? previousJumpIG = null;
        uint previousJumpInsNum = 0;
#endif

        while (jmp is not null)
        {
            var jmpGroup = jmp.idjIG ?? throw new FatalJitException("A saved jump must belong to an instruction group.");
            var nextJmp = jmp.idjNext;

            if (jmp.idjIsRemovableJmpCandidate)
            {
#if DEBUG
                assert(jmp.idInsFmt() == IF_LABEL);
                assert(jmp.idIns() == INS_jmp);
                var debugInfo = jmp.idDebugOnlyInfo();
                assert(debugInfo is not null);
                assert((previousJumpIG is null) || jmpGroup.IsAfter(previousJumpIG)
                    || (ReferenceEquals(jmpGroup, previousJumpIG) && (debugInfo.idNum > previousJumpInsNum)));
                previousJumpIG = jmpGroup;
                previousJumpInsNum = debugInfo.idNum;
#endif
                var targetGroup = (jmp.idjTarget is BasicBlock target
                    ? emitCodeGetCookie(target)
                    : null) ?? throw new FatalJitException(CORJIT_SKIPPED,
                        "A removable jump requires a resolved basic-block label cookie.");

                if (ReferenceEquals(jmpGroup.igNext, targetGroup)
                    && ((jmpGroup.igFlags & InsGroupFlags.HasRemovableJump) != 0))
                {
                    assert(!jmpGroup.endsWithAlignInstr());
#if DEBUG
                    assert(jmpGroup.igInsCnt > 0);
                    assert(jmpGroup.igData is not null);
                    assert(jmpGroup.igData.Length == jmpGroup.igInsCnt);
                    assert(ReferenceEquals(jmpGroup.igData[^1], jmp));
                    JITDUMP($"IG{jmpGroup.GetDisplayId():D2} IN{debugInfo.idNum:x4} is the last instruction in the group " +
                        $"and jumps to the next instruction group IG{targetGroup.GetDisplayId():D2} " +
                        $"{emitLabelString(targetGroup)}, removing.\n");
#endif
                    if (previousJmp is not null)
                    {
                        previousJmp.idjNext = nextJmp;
                        if (ReferenceEquals(jmp, emitJumpLast))
                        {
                            emitJumpLast = previousJmp;
                        }
                    }
                    else
                    {
                        assert(ReferenceEquals(jmp, emitJumpList));
                        emitJumpList = nextJmp;
                        if (nextJmp is null)
                        {
                            emitJumpLast = null;
                        }
                    }

                    var codeSize = jmp.idCodeSize();
                    jmp.idCodeSize(0);

                    if (jmp.idjIsAfterCallBeforeEpilog)
                    {
                        if ((targetGroup.igFlags & InsGroupFlags.Epilog) != 0)
                        {
                            jmp.idCodeSize(1);
                            codeSize--;
                        }
                        else
                        {
                            jmp.idjIsAfterCallBeforeEpilog = false;
                        }
                    }

                    jmpGroup.igSize = unchecked((ushort)(jmpGroup.igSize - codeSize));
                    jmpGroup.igFlags |= InsGroupFlags.UpdatedInstructionSize;

                    emitTotalCodeSize = unchecked(emitTotalCodeSize - (int)codeSize);
                    totalRemovedSize = unchecked(totalRemovedSize + codeSize);
                }
                else
                {
                    jmp.idjIsRemovableJmpCandidate = false;
                    previousJmp = jmp;
#if DEBUG
                    if (!ReferenceEquals(jmpGroup.igNext, targetGroup))
                    {
                        JITDUMP($"IG{jmpGroup.GetDisplayId():D2} IN{debugInfo.idNum:x4} does not jump to the next " +
                            "instruction group, keeping.\n");
                    }
                    else
                    {
                        JITDUMP($"IG{jmpGroup.GetDisplayId():D2} IN{debugInfo.idNum:x4} containing instruction " +
                            "group is not marked with IGF_HAS_REMOVABLE_JMP, keeping.\n");
                    }
#endif
                }
            }
            else
            {
                previousJmp = jmp;
            }

            if (totalRemovedSize > 0)
            {
                var adjOffIG = jmpGroup.igNext;
                var adjOffUptoIG = nextJmp is not null
                    ? nextJmp.idjIG ?? throw new FatalJitException("A saved jump must belong to an instruction group.")
                    : emitIGlast ?? throw new FatalJitException("Jump removal requires a final instruction group.");

                while ((adjOffIG is not null)
                    && (ReferenceEquals(adjOffIG, adjOffUptoIG) || adjOffIG.IsBefore(adjOffUptoIG)))
                {
                    var previousOffset = adjOffIG.igOffs;
                    adjOffIG.igOffs = unchecked(previousOffset - totalRemovedSize);
                    JITDUMP($"Adjusted offset of IG{adjOffIG.GetDisplayId():D2} " +
                        $"from {previousOffset:X4} to {adjOffIG.igOffs:X4}\n");
                    adjOffIG = adjOffIG.igNext;
                }
            }

            jmp = nextJmp;
        }

#if DEBUG
        if (totalRemovedSize > 0)
        {
            emitCheckIGList();
            if (_compiler.verbose)
            {
                emitDispJumpList();
            }

            JITDUMP($"emitRemoveJumpToNextInst removed {totalRemovedSize} bytes of unconditional jumps\n");
        }
        else
        {
            JITDUMP("emitRemoveJumpToNextInst removed no unconditional jumps\n");
        }
#endif
#endif
    }
}
