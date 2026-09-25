// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private static string lsraGetOperandStringPre(GenTree tree)
    {
        var lastUse = tree.Oper.IsScalarLocal && ((tree.Flags & GTF_VAR_DEATH) != 0) ? "*" : "";
        return $"t{tree.TreeId}{lastUse}";
    }

    private void lsraDispNodePre(GenTree tree, bool hasDestination)
    {
        jitprintf($"  N{unchecked((uint)tree._seqNum):D3}. ");

        var localNumber = -1;
        if (tree.Oper.IsLocal)
        {
            localNumber = tree.AsLclVarCommon().LclNum;
            if (_compiler.lvaGetDesc(localNumber).lvLRACandidate)
            {
                hasDestination = false;
            }
        }

        if (hasDestination)
        {
            jitprintf($"{lsraGetOperandStringPre(tree),-15} =");
        }
        else
        {
            jitprintf("                 ");
        }

        if (localNumber >= 0)
        {
            if (_compiler.lvaGetDesc(localNumber).lvLRACandidate)
            {
                jitprintf($"  V{localNumber:D2}({lsraGetOperandStringPre(tree)})");
            }
            else
            {
                jitprintf($"  V{localNumber:D2} MEM");
            }
        }
        else
        {
            _compiler.gtDispNodeName(tree);
            if (tree.Oper.IsLeaf)
            {
                var indentStack = new IndentStack(_compiler);
                _compiler.gtDispLeaf(tree, ref indentStack);
            }
        }
    }

    private void dumpOperandDefsPre(GenTree operand, ref bool first)
    {
        var destinationCount = computeOperandDstCount(operand);
        if (destinationCount != 0)
        {
            if (!first)
            {
                jitprintf(",");
            }

            jitprintf(lsraGetOperandStringPre(operand));
            first = false;
        }
        else if (operand.IsContained)
        {
            foreach (var child in operand.Operands)
            {
                dumpOperandDefsPre(child, ref first);
            }
        }
    }

    private void tupleStyleDumpPre()
    {
        jitprintf("TUPLE STYLE DUMP BEFORE LSRA\n");

        for (var block = startBlockSequence(); block is not null; block = moveToNextBlock())
        {
            block.dspBlockHeader();
            jitprintf("=====\n");

            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                var splitBlocks = _splitBBNumToTargetBBNumMap
                    ?? throw new FatalJitException("Resolution blocks require a split-edge mapping.");
                if (!splitBlocks.TryGetValue((uint)block.bbNum, out var split))
                {
                    throw new FatalJitException($"No split-edge mapping exists for BB{block.bbNum:D2}.");
                }

                assert(split.toBBNum <= _bbNumMaxBeforeResolution);
                assert(split.fromBBNum <= _bbNumMaxBeforeResolution);
                jitprintf($"New block introduced for resolution from {FMT_BB(checked((int)split.fromBBNum))} to {FMT_BB(checked((int)split.toBBNum))}\n");
            }

            foreach (var tree in block)
            {
                var produced = tree.IsValue ? computeOperandDstCount(tree) : 0;
                var consumed = computeAvailableSrcCount(tree);

                lsraDispNodePre(tree, produced != 0);

                if (consumed > 0)
                {
                    jitprintf("; ");
                    var first = true;
                    foreach (var operand in tree.Operands)
                    {
                        dumpOperandDefsPre(operand, ref first);
                    }
                }

                jitprintf("\n");
            }

            jitprintf("\n");
        }

        jitprintf("\n\n");
    }
}
#endif
