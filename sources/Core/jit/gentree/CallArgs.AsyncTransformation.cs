// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct CallArgs
{
    public void RemoveUnsafe(CallArg argument)
    {
        ref var lateSlot = ref _lateHead;
        while (lateSlot is not null)
        {
            if (lateSlot == argument)
            {
                lateSlot = argument.LateNext;
                break;
            }

            lateSlot = ref lateSlot.LateNextRef;
        }

        ref var slot = ref _head;
        while (slot is not null)
        {
            if (slot == argument)
            {
                slot = argument.Next;
                RemovedWellKnownArg(argument.WellKnownArg);
                return;
            }

            slot = ref slot.NextRef;
        }

        assert(false, "Did not find arg to remove in CallArgs::Remove");
    }
}
