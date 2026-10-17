// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    [System.Diagnostics.Conditional("DEBUG")]
    public void DumpSsaSummary()
    {
        for (var lclNum = 0; lclNum < lvaCount; lclNum++)
        {
            ref var varDsc = ref lvaTable[lclNum];
            if (!varDsc.lvInSsa)
            {
                continue;
            }

            ref var ssaDefs = ref varDsc.lvPerSsaData;
            if (ssaDefs.Count == 0)
            {
                jitprintf($"V{lclNum:D2}: in SSA but no defs\n");
            }
            else
            {
                for (var index = 0; index < ssaDefs.Count; index++)
                {
                    ref var ssaVarDsc = ref ssaDefs.GetSsaDefByIndex(index);
                    var ssaNum = ssaDefs.GetSsaNum(in ssaVarDsc);
                    var blockNum = ssaVarDsc.Block?.bbNum ?? 0;
                    jitprintf($"V{lclNum:D2}.{ssaNum}: defined in BB{blockNum:D2} {ssaVarDsc.NumUses} uses " +
                              $"({(ssaVarDsc.HasGlobalUse ? "global" : "local")}){(ssaVarDsc.HasPhiUse ? ", has phi uses" : "")}\n");
                }
            }
        }
    }
}
