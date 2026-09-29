// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitDisableGC()
    {
        assert(_compiler is not null);

        // At stack-empty debug sequence points, adjacent NoGC regions need an interruptible instruction.
        if (_compiler.opts.compDbgCode && (emitNoGCRequestCount == 0) && emitLastCodeIsNoGC() &&
            (_compiler.genIPmappings.Last is not null))
        {
            var mapping = _compiler.genIPmappings.Last.Value;
            if ((mapping.ipmdKind == IPmappingDscKind.Normal) &&
                ((mapping.ipmdLoc.SourceTypes & ICorDebugInfo.STACK_EMPTY) != 0) &&
                mapping.ipmdNativeLoc.IsCurrentLocation(this))
            {
                emitIns(INS_nop);
            }
        }

        assert(emitNoGCRequestCount < 10);
        ++emitNoGCRequestCount;
        if (emitNoGCRequestCount == 1)
        {
            JITDUMP("Disable GC\n");
            assert(!emitNoGCIG);
            emitNoGCIG = true;

            if (emitCurIGnonEmpty())
            {
                emitNxtIG(extend: true);
            }
            else
            {
                assert(emitCurIG is not null);
                emitCurIG.igFlags |= InsGroupFlags.NoGCInterrupt;
            }
        }
        else
        {
            JITDUMP($"Disable GC: {emitNoGCRequestCount} no-gc requests\n");
            assert(emitNoGCIG);
        }
    }

    public bool emitLastCodeIsNoGC()
    {
        if ((emitCurIG is not null) && (emitCurIGsize != 0))
        {
            return (emitCurIG.igFlags & InsGroupFlags.NoGCInterrupt) != 0;
        }

        return emitLastSavedIGWasNoGC;
    }

    public void emitEnableGC()
    {
        assert(emitNoGCRequestCount > 0);
        assert(emitNoGCIG);
        --emitNoGCRequestCount;

        if (emitNoGCRequestCount == 0)
        {
            JITDUMP("Enable GC\n");
            emitNoGCIG = false;

            // Delay group creation: a label may start the next group, or no instructions may follow.
            emitForceNewIG = true;
        }
        else
        {
            JITDUMP($"Enable GC: still {emitNoGCRequestCount} no-gc requests\n");
        }
    }
}
