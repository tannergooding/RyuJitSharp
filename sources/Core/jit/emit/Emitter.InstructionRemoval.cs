// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;

namespace RyuJitSharp;

public partial class Emitter
{
    // The replacement instruction must account for the removed instruction's GC effect.
    // Captured locations can retain a now-invalid instruction number; mark the group so
    // emitCodeOffset can resolve a replacement that spills into an extension group.
    private unsafe void emitRemoveLastInstruction()
    {
        assert(emitLastIns is not null);
        assert(emitLastInsIG is not null);
        assert(emitCurIGfreeBase is not null);

        var lastInstruction = emitLastIns;
        var lastGroup = emitLastInsIG;
        var lastCodeSize = unchecked((ushort)lastInstruction.idCodeSize());
        var lastInstructionStart = unchecked(lastInstruction.StorageOffset - (nuint)_debugInfoSize);

        // Native address-range membership maps to membership in the accumulating descriptor
        // list. Saved groups retain the same managed descriptor objects, but not that list.
        var isInCurrentBuffer = (lastInstruction.StorageIndex < emitCurIGfreeBase.Count)
            && (emitCurIGfreeBase[lastInstruction.StorageIndex] == lastInstruction);

        if (isInCurrentBuffer)
        {
            JITDUMP($"Removing saved instruction in current IG {emitLabelString(lastGroup)}:\n> ");
#if DEBUG
            assert(_compiler is not null);
            if (_compiler.verbose)
            {
                emitDispIns(lastInstruction, isNew: false, doffs: false, asmfm: false);
            }
#endif
            assert(emitCurIGnonEmpty());
            assert(lastInstructionStart < emitCurIGfreeNext);
            assert(emitCurIGinsCnt >= 1);
            assert((uint)emitCurIGsize >= lastInstruction.idCodeSize());
            assert(lastInstruction.StorageIndex == emitCurIGfreeBase.Count - 1);
            assert(emitCurIG is not null);

            emitCurIGfreeNext = lastInstructionStart;
            emitCurIGinsCnt = unchecked(emitCurIGinsCnt - 1);
            emitCurIGsize = unchecked(emitCurIGsize - lastCodeSize);

            // Dropping the removed descriptor clears the logical buffer region that native
            // memset zeroes before reuse. Its storage offset is reused by the replacement.
            emitCurIGfreeBase.RemoveAt(lastInstruction.StorageIndex);
            emitCurIG.igFlags |= InsGroupFlags.HasRemovedInstruction;
        }
        else
        {
            JITDUMP($"Removing saved instruction in saved IG {emitLabelString(lastGroup)}:\n> ");
#if DEBUG
            assert(_compiler is not null);
            if (_compiler.verbose)
            {
                emitDispIns(lastInstruction, isNew: false, doffs: false, asmfm: false);
            }
            assert(lastGroup.igDataSize
                == lastInstruction.StorageOffset + (nuint)emitSizeOfInsDsc(lastInstruction));
#endif
            assert(lastGroup.igInsCnt >= 1);
            assert(lastGroup.igSize >= lastCodeSize);
            assert(lastGroup.igData is not null);
            assert(lastInstruction.StorageIndex == lastGroup.igInsCnt - 1);

            lastGroup.igInsCnt = unchecked((byte)(lastGroup.igInsCnt - 1));
            lastGroup.igSize = unchecked((ushort)(lastGroup.igSize - lastCodeSize));

            // Saved storage is not reused. Remove the inactive tail reference, retaining the
            // original logical allocation size just as native zeroing does.
            Array.Resize(ref lastGroup.igData, lastGroup.igInsCnt);
            lastGroup.igFlags |= InsGroupFlags.HasRemovedInstruction;
        }

        emitInsCount = unchecked(emitInsCount - 1);
        emitLastIns = null;
        emitLastInsIG = null;
    }
}
#endif
