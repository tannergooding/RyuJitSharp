// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitAdvanceInstrDesc(ref instrDesc? descriptor, nuint size)
    {
        assert(descriptor is not null);
        assert(size == (nuint)emitSizeOfInsDsc(descriptor));
        var group = descriptor.StorageGroup
            ?? throw new FatalJitException("Instruction traversal requires descriptor ownership.");
        IReadOnlyList<instrDesc> storage;
        if ((group == emitCurIG) && emitCurIGnonEmpty())
        {
            assert(emitCurIGfreeBase is not null);
            storage = emitCurIGfreeBase;
        }
        else
        {
            storage = group.igData
                ?? throw new FatalJitException("Instruction traversal requires saved descriptor storage.");
        }

        var nextOffset = descriptor.StorageOffset + size + (nuint)_debugInfoSize;
        var nextIndex = descriptor.StorageIndex + 1;
        // A null descriptor represents the native one-past-end address, not a missing buffer.
        descriptor = nextIndex == storage.Count ? null : storage[nextIndex];
        assert((descriptor is null) || (descriptor.StorageOffset == nextOffset));
    }

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
    private static instrDesc? emitFirstInstrDesc(IReadOnlyList<instrDesc> storage)
    {
        return storage.Count == 0 ? null : storage[0];
    }
#else
    private static instrDesc? emitFirstInstrDesc(instrDesc[] storage)
    {
        return storage.Length == 0 ? null : storage[0];
    }
#endif

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
    private void emitGetInstrDescs(insGroup group, out instrDesc? descriptor, out int count)
    {
        assert((group.igFlags & InsGroupFlags.Placeholder) == 0);
        if (group == emitCurIG)
        {
            assert(emitCurIGfreeBase is not null);
            descriptor = emitFirstInstrDesc(emitCurIGfreeBase);
            count = emitCurIGinsCnt;
        }
        else
        {
            var storage = group.igData
                ?? throw new FatalJitException("A saved instruction group requires descriptor storage.");
            descriptor = emitFirstInstrDesc(storage);
            count = group.igInsCnt;
        }

        assert((descriptor is not null) || (count == 0));
    }

    private bool emitGetLocationInfo(emitLocation location, out insGroup? group,
        out instrDesc? descriptor, out int remaining)
    {
        assert(location.Valid());
        group = location.GetIG();
        assert(group is not null);
        var number = location.GetInsNum();
        emitGetInstrDescs(group, out descriptor, out var count);
        remaining = 0;
        assert(number <= count);

        // A captured end location can become the beginning of a later nonempty group.
        if (number == count)
        {
            if (group == emitCurIG)
            {
                return false;
            }

            for (group = group.igNext; group is not null; group = group.igNext)
            {
                emitGetInstrDescs(group, out descriptor, out count);
                if (count > 0)
                {
                    number = 0;
                    break;
                }
                if (group == emitCurIG)
                {
                    return false;
                }
            }

            if (group is null)
            {
                noway_assert(false, "!\"corrupt emitter location\"");
                return false;
            }
        }

        assert(number < count);
        for (var index = 0; index != number; index++)
        {
            assert(descriptor is not null);
            emitAdvanceInstrDesc(ref descriptor, (nuint)emitSizeOfInsDsc(descriptor));
        }

        remaining = count - number - 1;
        return true;
    }

    private bool emitNextID(ref insGroup? group, ref instrDesc? descriptor, ref int remaining)
    {
        assert(group is not null);
        assert(descriptor is not null);
        if (remaining > 0)
        {
            emitAdvanceInstrDesc(ref descriptor, (nuint)emitSizeOfInsDsc(descriptor));
            --remaining;
            return true;
        }
        if (group == emitCurIG)
        {
            return false;
        }

        for (group = group.igNext; group is not null; group = group.igNext)
        {
            emitGetInstrDescs(group, out descriptor, out var count);
            if (count > 0)
            {
                remaining = count - 1;
                return true;
            }
            if (group == emitCurIG)
            {
                return false;
            }
        }

        return false;
    }

    private void emitWalkIDs(emitLocation from, Action<instrDesc, Compiler> process, Compiler context)
    {
        if (!emitGetLocationInfo(from, out var group, out var descriptor, out var remaining))
        {
            return;
        }

        do
        {
            assert(descriptor is not null);
            process(descriptor, context);
        }
        while (emitNextID(ref group, ref descriptor, ref remaining));
    }

    private static void emitGenerateUnwindNop(instrDesc descriptor, Compiler compiler)
    {
#if TARGET_ARM
        compiler.unwindNop(descriptor.idCodeSize());
#else
        compiler.unwindNop();
#endif
    }

    public void emitUnwindNopPadding(emitLocation from, Compiler compiler)
    {
        emitWalkIDs(from, emitGenerateUnwindNop, compiler);
    }
#endif

#if TARGET_ARM
    public uint emitGetInstructionSize(emitLocation location)
    {
        var anyInstructions = emitGetLocationInfo(location, out _, out var descriptor, out _);
        assert(anyInstructions);
        assert(descriptor is not null);

        return descriptor.idCodeSize();
    }
#endif
}
