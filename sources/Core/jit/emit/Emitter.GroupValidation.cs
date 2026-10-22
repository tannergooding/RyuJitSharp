// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Numerics;

namespace RyuJitSharp;

public partial class Emitter
{
    [System.Diagnostics.Conditional("DEBUG")]
    private void emitCheckIGList()
    {
        var checkBackwardNavigation = Configuration.EMIT_BACKWARDS_NAVIGATION || Configuration.TARGET_XARCH;
        List<(insGroup Group, instrDesc Descriptor)>? instructions =
            checkBackwardNavigation ? [] : null;
        nuint currentOffset = 0;
        insGroup? previous = null;
        const InsGroupFlags prologEpilogFlags = InsGroupFlags.Prolog | InsGroupFlags.FuncletProlog
            | InsGroupFlags.FuncletEpilog | InsGroupFlags.Epilog;

        for (var current = emitIGlist; current is not null; previous = current, current = current.igNext)
        {
            if (checkBackwardNavigation)
            {
                assert(ReferenceEquals(previous, current.igPrev));
            }

            if (current.igOffs != currentOffset)
            {
                jitprintf($"IG{current.GetDisplayId():D2} has offset {current.igOffs:X8}, expected {currentOffset:X8}\n");
                assert(false);
            }

            currentOffset = unchecked(currentOffset + current.igSize);

            if (previous is null)
            {
                assert((current.igFlags & InsGroupFlags.Extend) == 0);
                assert((current.igFlags & InsGroupFlags.Prolog) != 0);
            }

            if ((current.igFlags & InsGroupFlags.Prolog) != 0)
            {
                assert((current.igFlags & (InsGroupFlags.FuncletProlog | InsGroupFlags.FuncletEpilog
                    | InsGroupFlags.Epilog)) == 0);
            }

            assert(BitOperations.PopCount((uint)(current.igFlags & prologEpilogFlags)) <= 1);
            assert(BitOperations.PopCount((uint)(current.igFlags
                & (InsGroupFlags.HasAlign | InsGroupFlags.RemovedAlign))) <= 1);

            if ((current.igFlags & InsGroupFlags.Extend) != 0)
            {
                assert((current.igFlags & (InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs)) == 0);
                assert(previous is not null);
                assert((current.igFlags & (InsGroupFlags.Prolog | InsGroupFlags.FuncletProlog))
                    == (previous.igFlags & (InsGroupFlags.Prolog | InsGroupFlags.FuncletProlog)));

                if ((current.igFlags & InsGroupFlags.Epilog) != 0)
                {
                    assert((previous.igFlags & (InsGroupFlags.FuncletProlog | InsGroupFlags.FuncletEpilog)) == 0);
                }
                if ((current.igFlags & InsGroupFlags.FuncletEpilog) != 0)
                {
                    assert((previous.igFlags & (InsGroupFlags.FuncletProlog | InsGroupFlags.Epilog)) == 0);
                }
            }

            if (checkBackwardNavigation && (current.igInsCnt != 0))
            {
                var savedInstructions = instructions;
                if (savedInstructions is null)
                {
                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.BackwardInstructionValidationRequiresDescriptorStorage);
                    return;
                }
                var descriptors = current.igData;
                if (descriptors is null)
                {
                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SavedInstructionGroupRequiresDescriptorStorage);
                    return;
                }
                assert(descriptors.Length == current.igInsCnt);
                var first = descriptors[0];
                savedInstructions.Add((current, first));
                assert(ReferenceEquals(first.StorageGroup, current));
                assert(first.StorageIndex == 0);
                assert(first.idPrevSize() == 0);

                for (var i = 1; i < current.igInsCnt; i++)
                {
                    var prior = descriptors[i - 1];
                    var descriptor = descriptors[i];
                    var previousSize = unchecked((uint)(emitSizeOfInsDsc(prior) + _debugInfoSize));
                    savedInstructions.Add((current, descriptor));
                    assert(ReferenceEquals(descriptor.StorageGroup, current));
                    assert(descriptor.StorageIndex == i);
                    assert(prior.StorageOffset + (nuint)previousSize == descriptor.StorageOffset);
                    assert(descriptor.idPrevSize() == previousSize);
                }
            }
        }

        if ((emitTotalCodeSize != 0) && (unchecked((uint)emitTotalCodeSize) != currentOffset))
        {
            jitprintf($"Total code size is {unchecked((uint)emitTotalCodeSize):X8}, expected {currentOffset:X8}\n");
            assert(false);
        }

        if (checkBackwardNavigation && emitGetLastIns(out var group, out var id))
        {
            var savedInstructions = instructions;
            if (savedInstructions is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.BackwardInstructionValidationRequiresDescriptorStorage);
                return;
            }
            var index = savedInstructions.Count - 1;

            do
            {
                assert(index >= 0);
                assert(ReferenceEquals(group, savedInstructions[index].Group));
                assert(ReferenceEquals(id, savedInstructions[index].Descriptor));
                index--;
            }
            while (emitPrevID(ref group, ref id));
        }
    }
}
