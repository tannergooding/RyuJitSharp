// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public void lvaDispVarSet(ReadOnlySpan<nint> set)
    {
        var allVars = VarSetOps.MakeEmpty(this);
        lvaDispVarSet(set, allVars);
    }

    public void lvaDispVarSet(ReadOnlySpan<nint> set, ReadOnlySpan<nint> allVars)
    {
        jitprintf("{");
        var needSpace = false;

        for (var index = 0; index < lvaTrackedCount; index++)
        {
            if (VarSetOps.IsMember(this, set, index))
            {
                var lclNum = 0;
                for (; lclNum < lvaCount; lclNum++)
                {
                    ref var descriptor = ref lvaTable[lclNum];
                    if ((descriptor._varIndex == index) && descriptor.lvTracked)
                    {
                        break;
                    }
                }

                if (needSpace)
                {
                    jitprintf(" ");
                }
                else
                {
                    needSpace = true;
                }

                jitprintf($"V{lclNum:D2}");
            }
            else if (VarSetOps.IsMember(this, allVars, index))
            {
                if (needSpace)
                {
                    jitprintf(" ");
                }
                else
                {
                    needSpace = true;
                }

                jitprintf("   ");
            }
        }

        jitprintf("}");
    }
#endif
}
