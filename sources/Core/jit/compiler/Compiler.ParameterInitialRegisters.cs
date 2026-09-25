// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private void lvaUpdateArgWithInitialReg(ref LclVarDsc local)
    {
        assert(local.lvIsParam || local.lvIsParamRegTarget);
        if (local.lvIsRegCandidate)
        {
            local.RegNum = local.ArgInitReg;
        }
    }

    public void lvaUpdateArgsWithInitialReg()
    {
        if (!compRegAllocDone)
        {
            return;
        }

        for (var localNumber = 0; localNumber < lvaCount; localNumber++)
        {
            ref var local = ref lvaGetDesc(localNumber);
            if (local.lvIsParam || local.lvIsParamRegTarget)
            {
                lvaUpdateArgWithInitialReg(ref local);
            }
        }
    }
}
