// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TARGET_AMD64
using System.Collections.Generic;
using System.Numerics;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitCheckIGList()
    {
        List<(insGroup Group, instrDesc Descriptor)> instructions = [];
        ulong currentOffset = 0;
        insGroup? previous = null;
        const InsGroupFlags prologEpilogFlags = InsGroupFlags.Prolog | InsGroupFlags.FuncletProlog
            | InsGroupFlags.FuncletEpilog | InsGroupFlags.Epilog;

        for (var current = emitIGlist; current is not null; previous = current, current = current.igNext)
        {
            assert(ReferenceEquals(previous, current.igPrev));

            if (current.igOffs != currentOffset)
            {
                jitprintf($"IG{current.GetDisplayId():D2} has offset {current.igOffs:X8}, expected {currentOffset:X8}\n");
                assert(false);
            }

            currentOffset += current.igSize;

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

            if (current.igInsCnt != 0)
            {
                var descriptors = current.igData
                    ?? throw new FatalJitException("A saved instruction group requires descriptor storage.");
                assert(descriptors.Length == current.igInsCnt);
                var first = descriptors[0];
                instructions.Add((current, first));
                assert(ReferenceEquals(first.StorageGroup, current));
                assert(first.StorageIndex == 0);
                assert(first.idPrevSize() == 0);

                for (var i = 1; i < current.igInsCnt; i++)
                {
                    var prior = descriptors[i - 1];
                    var descriptor = descriptors[i];
                    var previousSize = unchecked((uint)(emitSizeOfInsDsc(prior) + _debugInfoSize));
                    instructions.Add((current, descriptor));
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

        if (emitGetLastIns(out var group, out var id))
        {
            var index = instructions.Count - 1;

            do
            {
                assert(index >= 0);
                assert(ReferenceEquals(group, instructions[index].Group));
                assert(ReferenceEquals(id, instructions[index].Descriptor));
                index--;
            }
            while (emitPrevID(ref group, ref id));
        }
    }
}
#endif
