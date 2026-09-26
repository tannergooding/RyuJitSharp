// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Remove PHIs, or all SSA artifacts when rebuilding SSA.</summary>
    public void fgResetForSsa(bool deepClean)
    {
        JITDUMP($"Removing {(deepClean ? "all SSA artifacts" : "PHI functions")}\n");

        if (deepClean)
        {
            for (var index = 0; index < lvaCount; index++)
            {
                lvaTable[index].lvPerSsaData.Reset();
            }
            lvMemoryPerSsaData.Reset();
            foreach (var kind in new AllMemoryKinds())
            {
                _memorySsaMap[(int)kind] = null;
            }
            _outlinedCompositeSsaNums?.Clear();
        }

        foreach (var block in Blocks)
        {
            foreach (var kind in new AllMemoryKinds())
            {
                block.bbMemorySsaPhiFunc[(int)kind] = null;
            }
            if (block.FirstStmt is not null)
            {
                var last = block.LastStmt;
                block.FirstStmt = block.GetFirstNonPhiDef();
                block.FirstStmt?.PrevStmt = last;
            }

            if (deepClean)
            {
                foreach (var statement in block.Statements)
                {
                    foreach (var tree in statement.TreeList)
                    {
                        if (tree.Oper.IsAnyLocal)
                        {
                            tree.AsLclVarCommon().SsaNum = SsaConfig.RESERVED_SSA_NUM;
                        }
                    }
                }
            }
        }
    }
}
