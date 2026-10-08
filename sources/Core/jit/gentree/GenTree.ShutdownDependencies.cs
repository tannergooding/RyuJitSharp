// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if NODEBASH_STATS || MEASURE_NODE_SIZE || COUNT_AST_OPERS
#if NODEBASH_STATS
using System.IO;
#endif

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class GenTree
{
#if MEASURE_NODE_SIZE
    internal static void DumpNodeSizes()
    {
        var smallNodeSize = TREE_NODE_SZ_SMALL;
        var largeNodeSize = TREE_NODE_SZ_LARGE;

        jitprintf($"Small tree node size = {smallNodeSize} bytes\n");
        jitprintf($"Large tree node size = {largeNodeSize} bytes\n");
        jitprintf("\n");

        for (var op = (uint)GT_NONE + 1; op < (uint)GT_COUNT; op++)
        {
            var needSize = GetTrueSize(op);
            var nodeSize = GetNodeSize(op);
            var oper = (genTreeOps)op;
            var structName = oper.StructName;
            var operName = GetOperName(oper);

            var repeated = false;
            for (var previousOper = (uint)GT_NONE + 1; previousOper < op; previousOper++)
            {
                if (structName == ((genTreeOps)previousOper).StructName)
                {
                    repeated = true;
                    break;
                }
            }

            if (!repeated || needSize > nodeSize)
            {
                var sizeChar = '?';
                if (nodeSize == smallNodeSize)
                {
                    sizeChar = 'S';
                }
                else if (nodeSize == largeNodeSize)
                {
                    sizeChar = 'L';
                }

                jitprintf($"GT_{operName,-16} ... {structName,-19} = {needSize,3} bytes ({sizeChar})");
                if (needSize > nodeSize)
                {
                    jitprintf($" -- ERROR -- allocation is only {nodeSize} bytes!");
                }
                else if (needSize <= smallNodeSize && nodeSize == largeNodeSize)
                {
                    jitprintf(" ... could be small");
                }

                jitprintf("\n");
            }
        }
    }
#endif

#if NODEBASH_STATS
    internal static void ReportOperBashing(StreamWriter output)
    {
        uint total = 0;

        output.Flush();
        output.Write("\n");
        output.Write("Bashed gtOper stats:\n");
        output.Write("\n");
        output.Write("    Old operator        New operator     #bytes old->new      Count\n");
        output.Write("    ---------------------------------------------------------------\n");

        for (var hash = 0; hash < BASH_HASH_SIZE; hash++)
        {
            var count = BashHash[hash].bhCount;
            if (count == 0)
            {
                continue;
            }

            var opOld = BashHash[hash].bhOperOld;
            var opNew = BashHash[hash].bhOperNew;
            var oldSize = GetTrueSize((uint)opOld);
            var newSize = GetTrueSize((uint)opNew);
            var oldName = GetOperName(opOld);
            var newName = GetOperName(opNew);

            output.Write(
                $"    GT_{oldName,-13} -> GT_{newName,-13} [size: {oldSize,3}->{newSize,3}] " +
                $"{(oldSize < newSize ? 'X' : ' ')} {count,7}\n");
            total += (uint)count;
        }

        output.Write("\n");
        output.Write($"Total bashings: {total}\n");
        output.Write("\n");
        output.Flush();
    }
#endif

#if COUNT_AST_OPERS
    private static readonly uint[] s_gtNodeCounts = new uint[(int)GT_COUNT + 1];

    internal static ref uint GetNodeCount(uint op)
    {
        return ref s_gtNodeCounts[(int)op];
    }
#endif

#if NODEBASH_STATS || MEASURE_NODE_SIZE || COUNT_AST_OPERS
    internal static byte GetTrueSize(uint op)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "GenTree::s_gtTrueSizes native logical sizes are not ported.");
    }
#endif

#if NODEBASH_STATS || MEASURE_NODE_SIZE
    private static string GetOperName(genTreeOps oper)
    {
        var name = oper.ToString();
        return name[3..];
    }
#endif

#if MEASURE_NODE_SIZE
    private static byte GetNodeSize(uint op)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "GenTree::s_gtNodeSizes native allocation sizes are not ported.");
    }
#endif
}
#endif
