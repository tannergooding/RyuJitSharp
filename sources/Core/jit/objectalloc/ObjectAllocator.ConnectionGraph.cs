// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
#if DEBUG
    private static ConfigMethodRange s_jitObjectStackAllocationConditionalEscapeRange;
    private static ConfigMethodRange s_jitObjectStackAllocationTrackFieldsRange;
#endif

    private void MarkLclVarAsEscaping(int lclNum)
    {
        var bvIndex = LocalToIndex(lclNum);
        MarkIndexAsEscaping(bvIndex);
    }

    private void MarkIndexAsEscaping(int bvIndex)
    {
        BitVecOps.AddElemD(_bitVecTraits, _escapingPointers, bvIndex);
    }

    private void MarkIndexAsUsed(int bvIndex)
    {
        BitVecOps.AddElemD(_bitVecTraits, _definitelyUsedPointers, bvIndex);
    }

    private void MarkLclVarAsPossiblyStackPointing(int lclNum)
    {
        var bvIndex = LocalToIndex(lclNum);
        MarkIndexAsPossiblyStackPointing(bvIndex);
    }

    private void MarkIndexAsPossiblyStackPointing(int bvIndex)
    {
        BitVecOps.AddElemD(_bitVecTraits, _possiblyStackPointingPointers, bvIndex);
    }

    private void MarkLclVarAsDefinitelyStackPointing(int lclNum)
    {
        var bvIndex = LocalToIndex(lclNum);
        MarkIndexAsDefinitelyStackPointing(bvIndex);
    }

    private void MarkIndexAsDefinitelyStackPointing(int bvIndex)
    {
        BitVecOps.AddElemD(_bitVecTraits, _definitelyStackPointingPointers, bvIndex);
    }

    private void AddConnGraphEdge(int sourceLclNum, int targetLclNum)
    {
        var sourceBvIndex = LocalToIndex(sourceLclNum);
        var targetBvIndex = LocalToIndex(targetLclNum);
        AddConnGraphEdgeIndex(sourceBvIndex, targetBvIndex);
    }

    private void AddConnGraphEdgeIndex(int sourceBvIndex, int targetBvIndex)
    {
        var adjacencyMatrix = _connGraphAdjacencyMatrix;
        assert(adjacencyMatrix is not null);
        BitVecOps.AddElemD(_bitVecTraits, adjacencyMatrix[sourceBvIndex], targetBvIndex);
    }

    private unsafe void PrepareAnalysis()
    {
        var compiler = CompilerInstance;
        var localCount = compiler.lvaCount;
        var bvNext = 0;

        for (var lclNum = 0; lclNum < localCount; lclNum++)
        {
            ref var varDsc = ref compiler.lvaGetDesc(lclNum);

            if (IsTrackedType(varDsc.Type))
            {
                varDsc.lvTracked = true;
                varDsc._varIndex = unchecked((ushort)bvNext);
                bvNext++;
            }
            else
            {
                varDsc.lvTracked = false;
                varDsc._varIndex = 0;
            }
        }

        _nextLocalIndex = bvNext;

        if (compiler.hasImpEnumeratorGdvLocalMap)
        {
            var enumeratorLocalCount = compiler.ImpEnumeratorGdvLocalMap.Count;
            assert(enumeratorLocalCount > 0);

            var enableConditionalEscape = JitConfig.JitObjectStackAllocationConditionalEscape > 0;
            var isOSR = compiler.opts.IsOSR;

            if (enableConditionalEscape && !isOSR)
            {
#if DEBUG
                s_jitObjectStackAllocationConditionalEscapeRange.EnsureInit(JitConfig.JitObjectStackAllocationConditionalEscapeRange);
                var inRange = s_jitObjectStackAllocationConditionalEscapeRange.Contains(compiler.info.compMethodHash());
#else
                const bool inRange = true;
#endif
                if (inRange)
                {
                    JITDUMP($"Enabling conditional escape analysis [{enumeratorLocalCount} pseudos]\n");
                    _maxPseudos = enumeratorLocalCount;
                }
                else
                {
                    JITDUMP("Not enabling conditional escape analysis (disabled by range config)\n");
                }
            }
            else
            {
                JITDUMP($"Not enabling conditional escape analysis [{enumeratorLocalCount} pseudos]: " +
                    $"{(enableConditionalEscape ? "OSR" : "disabled by config")}\n");
            }
        }

#if DEBUG
        if (_trackFields)
        {
            s_jitObjectStackAllocationTrackFieldsRange.EnsureInit(JitConfig.JitObjectStackAllocationTrackFieldsRange);
            var inRange = s_jitObjectStackAllocationTrackFieldsRange.Contains(compiler.info.compMethodHash());

            if (!inRange)
            {
                JITDUMP("Disabling field wise escape analysis per range config\n");
                _trackFields = false;
            }
        }
#endif

        // With N tracked locals and M possible clones, [N, N+M) holds new locals,
        // [N+M, N+2M) holds pseudos, and N+2M denotes the unknown source.
        var maxTrackedLclNum = localCount + _maxPseudos;
        _firstPseudoIndex = bvNext + _maxPseudos;
        bvNext += 2 * _maxPseudos;

        _unknownSourceIndex = bvNext;
        bvNext++;

        _bvCount = bvNext;
        _bitVecTraits = new BitVecTraits(compiler, _bvCount);

        if ((compiler.lvaTrackedToVarNum is null) || (compiler.lvaTrackedToVarNum.Length < maxTrackedLclNum))
        {
            compiler.lvaTrackedToVarNum = new int[maxTrackedLclNum];
        }

        for (var lclNum = 0; lclNum < localCount; lclNum++)
        {
            ref var varDsc = ref compiler.lvaGetDesc(lclNum);
            if (varDsc.lvTracked)
            {
                compiler.lvaTrackedToVarNum[varDsc._varIndex] = lclNum;
            }
        }

        JITDUMP($"{localCount} locals, {_nextLocalIndex} tracked by escape analysis\n");
        JITDUMP($"Local field tracking is {(_trackFields ? "enabled" : "disabled")}\n");

        if (_nextLocalIndex > 0)
        {
            JITDUMP($"\nLocal      var    range [{0:D2}...{localCount - 1:D2}]\n");
            if (_maxPseudos > 0)
            {
                JITDUMP($"Enumerator var    range [{localCount:D2}...{localCount + _maxPseudos - 1:D2}]\n");
            }

            JITDUMP($"\nLocal      var bv range [{0:D2}...{_nextLocalIndex - 1:D2}]\n");
            if (_maxPseudos > 0)
            {
                JITDUMP($"Enumerator var bv range [{_nextLocalIndex:D2}...{_nextLocalIndex + _maxPseudos - 1:D2}]\n");
                JITDUMP($"Pseudo     var bv range [{_nextLocalIndex + _maxPseudos:D2}...{_nextLocalIndex + 2 * _maxPseudos - 1:D2}]\n");
            }
            JITDUMP($"Unknown    var bv range [{_unknownSourceIndex:D2}...{_unknownSourceIndex:D2}]\n");
        }
    }

    private void ComputeConnGraphClosure(BitVecTraits bitVecTraits, nint[] nodes, string setName)
    {
        var adjacencyMatrix = _connGraphAdjacencyMatrix;
        assert(adjacencyMatrix is not null);

        var nodesToProcess = BitVecOps.MakeCopy(bitVecTraits, nodes);
        JITDUMP($"\nComputing {setName} closure\n\n");

        var doOneMoreIteration = true;
        var newNodes = BitVecOps.UninitVal();

        while (doOneMoreIteration)
        {
            doOneMoreIteration = false;

            BitVecOps.VisitBits(bitVecTraits, nodesToProcess, lclIndex => {
                if (adjacencyMatrix[lclIndex] is not null)
                {
                    doOneMoreIteration = true;

                    BitVecOps.Assign(bitVecTraits, ref newNodes, adjacencyMatrix[lclIndex]);
                    BitVecOps.DiffD(bitVecTraits, newNodes, nodes);
                    BitVecOps.UnionD(bitVecTraits, nodesToProcess, newNodes);
                    BitVecOps.UnionD(bitVecTraits, nodes, newNodes);
                    BitVecOps.RemoveElemD(bitVecTraits, nodesToProcess, lclIndex);

#if DEBUG
                    if (!BitVecOps.IsEmpty(bitVecTraits, newNodes) && CompilerInstance.verbose)
                    {
                        BitVecOps.VisitBits(bitVecTraits, newNodes, newLclIndex => {
                            DumpIndex(lclIndex);
                            JITDUMP(" causes ");
                            DumpIndex(newLclIndex);
                            JITDUMP($" to be {setName}\n");
                            return true;
                        });
                    }
#endif
                }

                return true;
            });
        }
    }

    private void ComputeStackObjectPointers(BitVecTraits bitVecTraits)
    {
        var adjacencyMatrix = _connGraphAdjacencyMatrix;
        assert(adjacencyMatrix is not null);

        var possiblyHeapPointingPointers = BitVecOps.MakeEmpty(_bitVecTraits);
        BitVecOps.AddElemD(bitVecTraits, possiblyHeapPointingPointers, _unknownSourceIndex);

        var changed = true;
        var pass = 0;
        while (changed)
        {
            JITDUMP($"\n---- computing stack pointing locals, pass {pass++}\n");
            changed = false;

            for (var index = 0; index < _bvCount; index++)
            {
                if (!MayIndexPointToStack(index) &&
                    !BitVecOps.IsEmptyIntersection(bitVecTraits, _possiblyStackPointingPointers, adjacencyMatrix[index]))
                {
#if DEBUG
                    if (CompilerInstance.verbose)
                    {
                        DumpIndex(index);
                    }
#endif
                    JITDUMP(" may point to the stack\n");
                    MarkIndexAsPossiblyStackPointing(index);
                    changed = true;
                }

                if (!BitVecOps.IsMember(bitVecTraits, possiblyHeapPointingPointers, index) &&
                    !BitVecOps.IsEmptyIntersection(bitVecTraits, possiblyHeapPointingPointers, adjacencyMatrix[index]))
                {
#if DEBUG
                    if (CompilerInstance.verbose)
                    {
                        DumpIndex(index);
                    }
#endif
                    JITDUMP(" may point to the heap\n");
                    BitVecOps.AddElemD(bitVecTraits, possiblyHeapPointingPointers, index);
                    changed = true;
                }
            }
        }
        JITDUMP("\n---- done computing stack pointing locals\n");

        var newDefinitelyStackPointingPointers = BitVecOps.UninitVal();
        BitVecOps.Assign(bitVecTraits, ref newDefinitelyStackPointingPointers, _possiblyStackPointingPointers);
        BitVecOps.DiffD(bitVecTraits, newDefinitelyStackPointingPointers, possiblyHeapPointingPointers);

        assert(BitVecOps.IsSubset(bitVecTraits, _definitelyStackPointingPointers, newDefinitelyStackPointingPointers));
        BitVecOps.AssignNoCopy(bitVecTraits, ref _definitelyStackPointingPointers, newDefinitelyStackPointingPointers);

#if DEBUG
        if (CompilerInstance.verbose)
        {
            jitprintf("Definitely stack-pointing locals:");
            BitVecOps.VisitBits(bitVecTraits, _definitelyStackPointingPointers, index => {
                DumpIndex(index);
                return true;
            });
            jitprintf("\n");

            jitprintf("Possibly stack-pointing locals:");
            BitVecOps.VisitBits(bitVecTraits, _possiblyStackPointingPointers, index => {
                if (!BitVecOps.IsMember(bitVecTraits, _definitelyStackPointingPointers, index))
                {
                    DumpIndex(index);
                }
                return true;
            });
            jitprintf("\n");
        }
#endif
    }
}
