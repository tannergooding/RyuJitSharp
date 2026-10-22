// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    internal void gcCallDescAppend(uint offset, byte instructionSize, regMask gcrefRegs, regMask byrefRegs,
        ushort argCount, uint argMask, uint byrefArgMask)
    {
        var descriptor = new CallDsc {
            cdBlock = 0,
            cdOffs = offset,
            cdCallInstrSize = instructionSize,
            cdGCrefRegs = gcrefRegs,
            cdByrefRegs = byrefRegs,
            cdArgCnt = argCount,
            cdArgMask = argMask,
            cdByrefArgMask = byrefArgMask,
            cdNext = null,
        };

        if (gcCallDescLast is null)
        {
            assert(gcCallDescList is null);
            gcCallDescList = gcCallDescLast = descriptor;
        }
        else
        {
            assert(gcCallDescList is not null);
            gcCallDescLast.cdNext = descriptor;
            gcCallDescLast = descriptor;
        }
    }

    internal readonly uint[] gcCallDescAllocArgTable(ushort count)
    {
        assert(count != 0);
        var descriptor = gcCallDescLast;
        if (descriptor is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.GCCallDescriptorIsRequiredBeforeAllocatingItsArgumentTable
    );
            return default!;
        }
        assert(descriptor.cdArgCnt == count);
        var table = new uint[count];
        descriptor.cdArgTable = table;
        return table;
    }
}
