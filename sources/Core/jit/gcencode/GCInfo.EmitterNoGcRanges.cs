// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public bool emitGenNoGCLst(Func<uint, uint, uint, uint, bool, bool> callback,
        bool skipMainPrologsAndEpilogs = false)
    {
        for (var group = emitIGlist; group is not null; group = group.igNext)
        {
            if (skipMainPrologsAndEpilogs &&
                (group.igFlags & (InsGroupFlags.Prolog | InsGroupFlags.Epilog)) != 0)
            {
                continue;
            }
            if (((group.igFlags & InsGroupFlags.NoGCInterrupt) != 0) && (group.igSize > 0))
            {
                var descriptors = group.igData
                    ?? throw new FatalJitException(CORJIT_SKIPPED, "Non-interruptible group has no instructions.");
                assert(descriptors.Length > 0);
                var firstSize = descriptors[0].idCodeSize();
                assert(firstSize > 0);
                if (!callback(group.igFuncIdx, group.igOffs, group.igSize, firstSize,
                    (group.igFlags & InsGroupFlags.FuncletProlog) != 0))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
