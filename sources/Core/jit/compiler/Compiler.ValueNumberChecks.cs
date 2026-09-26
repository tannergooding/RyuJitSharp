// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public void JitTestCheckVN()
    {
        if (_nodeTestData is null)
        {
            return;
        }

        var testData = NodeTestData;
        var reachable = FindReachableNodesInNodeTestData();
        Dictionary<nint, ValueNum> labelToVN = [];
        Dictionary<ValueNum, nint> vnToLabel = [];
        if (verbose)
        {
            jitprintf("\nJit Testing: Value numbering.\n");
        }

        foreach (var (node, annotation) in testData)
        {
            var nodeVN = node._vnPair.Liberal;
            if (annotation._tl is not TL_VN and not TL_VNNorm)
            {
                continue;
            }

            if (!reachable.ContainsKey(node))
            {
                jitprintf("Node ");
                printTreeId(node);
                jitprintf(" had a test constraint declared, but has become unreachable at the time the constraint is tested.\n" +
                    "(This is probably as a result of some optimization -- \n" +
                    "you may need to modify the test case to defeat this opt.)\n");
                unreached();
            }

            var label = annotation._num;
            if (verbose)
            {
                jitprintf("  Node ");
                printTreeId(node);
                jitprintf($" -- VN class {unchecked((int)label)}.\n");
            }
            if (annotation._tl is TL_VNNorm)
            {
                assert(vnStore is not null);
                nodeVN = vnStore.VNNormalValue(nodeVN);
            }

            if (labelToVN.TryGetValue(label, out var priorVN))
            {
                if (verbose)
                {
                    jitprintf("      Already in hash tables.\n");
                }
                var found = vnToLabel.TryGetValue(priorVN, out var priorLabel);
                assert(found);
                if (label != priorLabel)
                {
                    jitprintf("Node: ");
                    printTreeId(node);
                    jitprintf($", with value number ${nodeVN:x}, was declared in VN class {unchecked((int)label)},\n");
                    jitprintf($"but this value number ${priorVN:x} has already been associated with a different SSA name class: {unchecked((int)priorLabel)}.\n");
                    unreached();
                }
                if (nodeVN != priorVN)
                {
                    jitprintf("Node: ");
                    printTreeId(node);
                    jitprintf($", ${nodeVN:x} was declared in SSA name class {unchecked((int)label)},\n");
                    jitprintf($"but that name class was previously bound to a different value number: ${priorVN:x}.\n");
                    unreached();
                }
            }
            else
            {
                if (vnToLabel.TryGetValue(nodeVN, out var priorLabel))
                {
                    jitprintf("Node: ");
                    printTreeId(node);
                    jitprintf($", ${nodeVN:x} was declared in value number class {unchecked((int)label)},\n");
                    jitprintf($"but this value number has already been associated with a different value number class: {unchecked((int)priorLabel)}.\n");
                    unreached();
                }

                labelToVN.Add(label, nodeVN);
                vnToLabel.Add(nodeVN, label);
                if (verbose)
                {
                    jitprintf("      added to hash tables.\n");
                }
            }
        }
    }
}
#endif
