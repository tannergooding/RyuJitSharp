// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
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

    // compiler.cpp: FindReachableNodesInNodeTestData; call arguments retain annotations
    // even when their original nodes are no longer in the statement's threaded tree list.
    public NodeToIntMap FindReachableNodesInNodeTestData()
    {
        NodeToIntMap reachable = [];
        if (_nodeTestData is null)
        {
            return reachable;
        }

        foreach (var block in Blocks)
        {
            for (var statement = block.GetFirstNonPhiDef(); statement is not null; statement = statement.NextStmt)
            {
                foreach (var tree in statement.TreeList)
                {
                    if (tree.Oper.IsCall)
                    {
                        foreach (var argument in tree.AsCall().Args.Args)
                        {
                            var argNode = argument.Node;
                            if (NodeTestData.ContainsKey(argNode))
                            {
                                reachable[argNode] = 0;
                            }
                        }
                    }

                    if (NodeTestData.ContainsKey(tree))
                    {
                        reachable[tree] = 0;
                    }
                }
            }
        }

        return reachable;
    }

    public void JitTestCheckSSA()
    {
        if (_nodeTestData is null)
        {
            return;
        }

        var testData = NodeTestData;
        var reachable = FindReachableNodesInNodeTestData();
        Dictionary<nint, (int Local, int Ssa)> labelToSsa = [];
        Dictionary<(int Local, int Ssa), nint> ssaToLabel = [];

        if (verbose)
        {
            jitprintf("\nJit Testing: SSA names.\n");
        }

        foreach (var (node, tlAndN) in testData)
        {
            if (tlAndN._tl != TL_SsaName)
            {
                continue;
            }

            if (node.Oper is not GT_LCL_VAR)
            {
                jitprintf("SSAName constraint put on non-lcl-var expression ");
                printTreeId(node);
                jitprintf($" (of type {node.Type.Name}).\n");
                unreached();
            }

            var lcl = node.AsLclVarCommon();
            if (!reachable.ContainsKey(lcl))
            {
                jitprintf("Node ");
                printTreeId(lcl);
                jitprintf(" had a test constraint declared, but has become unreachable at the time the constraint is tested.\n" +
                          "(This is probably as a result of some optimization -- \n" +
                          "you may need to modify the test case to defeat this opt.)\n");
                unreached();
            }

            var name = (Local: lcl.LclNum, Ssa: lcl.SsaNum);
            var label = tlAndN._num;
            if (verbose)
            {
                jitprintf("  Node: ");
                printTreeId(lcl);
                jitprintf($", SSA name = <{name.Local}, {name.Ssa}> -- SSA name class {unchecked((int)label)}.\n");
            }

            if (labelToSsa.TryGetValue(label, out var priorName))
            {
                if (verbose)
                {
                    jitprintf("      Already in hash tables.\n");
                }

                var exists = ssaToLabel.TryGetValue(priorName, out var priorLabel);
                assert(exists);
                if (label != priorLabel)
                {
                    jitprintf("Node: ");
                    printTreeId(lcl);
                    jitprintf($", SSA name = <{name.Local}, {name.Ssa}> was declared in SSA name class {unchecked((int)label)},\n");
                    jitprintf($"but this SSA name <{priorName.Local},{priorName.Ssa}> has already been associated with a different SSA name class: {unchecked((int)priorLabel)}.\n");
                    unreached();
                }

                if (name != priorName)
                {
                    jitprintf("Node: ");
                    printTreeId(lcl);
                    jitprintf($", SSA name = <{name.Local}, {name.Ssa}> was declared in SSA name class {unchecked((int)label)},\n");
                    jitprintf($"but that name class was previously bound to a different SSA name: <{priorName.Local},{priorName.Ssa}>.\n");
                    unreached();
                }
            }
            else
            {
                if (ssaToLabel.TryGetValue(name, out var priorLabel))
                {
                    jitprintf("Node: ");
                    printTreeId(lcl);
                    jitprintf($", SSA name = <{name.Local}, {name.Ssa}> was declared in SSA name class {unchecked((int)label)},\n");
                    jitprintf($"but this SSA name has already been associated with a different name class: {unchecked((int)priorLabel)}.\n");
                    unreached();
                }

                labelToSsa.Add(label, name);
                ssaToLabel.Add(name, label);
                if (verbose)
                {
                    jitprintf("      added to hash tables.\n");
                }
            }
        }
    }
}
#endif
