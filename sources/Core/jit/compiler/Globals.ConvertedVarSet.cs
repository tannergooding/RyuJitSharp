// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public static partial class Globals
{
    [Conditional("DEBUG")]
    public static void dumpConvertedVarSet(Compiler compiler, VARSET_TP vars)
    {
#if DEBUG
        var localNumbers = new bool[compiler.lvaCount];
        _ = VarSetOps.VisitBits(compiler, vars, index =>
        {
            assert(compiler.lvaTrackedToVarNum is not null);
            localNumbers[compiler.lvaTrackedToVarNum[index]] = true;

            return true;
        });

        jitprintf("{");
        var first = true;

        for (var localNumber = 0; localNumber < localNumbers.Length; localNumber++)
        {
            if (localNumbers[localNumber])
            {
                if (!first)
                {
                    jitprintf(" ");
                }

                jitprintf($"V{localNumber:D2}");
                first = false;
            }
        }

        jitprintf("}");
#endif
    }
}
