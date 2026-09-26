// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private sealed class EnumeratorVarAppearance(BasicBlock block, Statement stmt, int lclNum, bool isDef)
    {
        public BasicBlock Block = block;
        public Statement Stmt = stmt;
        public int LclNum = lclNum;
        public bool IsDef = isDef;
        public bool IsGuard;
    }

    private sealed class EnumeratorVar
    {
        public EnumeratorVarAppearance? Def;
        public List<EnumeratorVarAppearance>? Appearances;
        public bool HasMultipleDefs;
        public bool IsAllocTemp;
        public bool IsInitialAllocTemp;
        public bool IsFinalAllocTemp;
        public bool IsUseTemp;
    }

    private unsafe class GuardInfo
    {
        public int Local = BAD_VAR_NUM;
        public CORINFO_CLASS_HANDLE Type = NO_CLASS_HANDLE;
        public BasicBlock? Block;
        public Statement? Stmt;
        public GenTree? Relop;
    }

    private sealed unsafe class CloneInfo : GuardInfo
    {
        public int PseudoIndex = BAD_VAR_NUM;
        public int EnumeratorLocal = BAD_VAR_NUM;
        public Dictionary<int, EnumeratorVar>? AppearanceMap;
        public int AppearanceCount;
        public List<int>? AllocTemps;
        public BasicBlock? GuardBlock;
        public BasicBlock? DefBlock;
        public GenTree? AllocTree;
        public Statement? AllocStmt;
        public BasicBlock? AllocBlock;
        public List<BasicBlock>? BlocksToClone;
        public nint[] Blocks = BitVecOps.UninitVal();
        public weight_t ProfileScale;
        public bool CheckedCanClone;
        public bool CanClone;
        public bool WillClone;
    }

    private void ComputeEscapingNodes(BitVecTraits bitVecTraits, nint[] escapingNodes)
    {
        var adjacencyMatrix = _connGraphAdjacencyMatrix;
        assert(adjacencyMatrix is not null);
        var escapingNodesToProcess = BitVecOps.MakeCopy(bitVecTraits, escapingNodes);

        void ComputeClosure()
        {
            JITDUMP("\nComputing escape closure\n\n");
            var doOneMoreIteration = true;
            var newEscapingNodes = BitVecOps.UninitVal();

            while (doOneMoreIteration)
            {
                doOneMoreIteration = false;
                BitVecOps.VisitBits(bitVecTraits, escapingNodesToProcess, lclIndex => {
                    if (adjacencyMatrix[lclIndex] is not null)
                    {
                        doOneMoreIteration = true;
                        BitVecOps.Assign(bitVecTraits, ref newEscapingNodes, adjacencyMatrix[lclIndex]);
                        BitVecOps.DiffD(bitVecTraits, newEscapingNodes, escapingNodes);
                        BitVecOps.UnionD(bitVecTraits, escapingNodesToProcess, newEscapingNodes);
                        BitVecOps.UnionD(bitVecTraits, escapingNodes, newEscapingNodes);
                        BitVecOps.RemoveElemD(bitVecTraits, escapingNodesToProcess, lclIndex);

#if DEBUG
                        if (!BitVecOps.IsEmpty(bitVecTraits, newEscapingNodes) && CompilerInstance.verbose)
                        {
                            BitVecOps.VisitBits(bitVecTraits, newEscapingNodes, newLclIndex => {
                                DumpIndex(lclIndex);
                                JITDUMP(" causes ");
                                DumpIndex(newLclIndex);
                                JITDUMP(" to escape\n");
                                return true;
                            });
                        }
#endif
                    }

                    return true;
                });
            }
        }

        ComputeClosure();

        if (_numPseudos > 0)
        {
            var newEscapes = AnalyzeIfCloningCanPreventEscape(bitVecTraits, escapingNodes, escapingNodesToProcess);
            if (newEscapes)
            {
                ComputeClosure();
            }
        }
    }

    private bool AnalyzeIfCloningCanPreventEscape(BitVecTraits bitVecTraits, nint[] escapingNodes, nint[] escapingNodesToProcess)
    {
        var newEscapes = false;

        for (var p = 0; p < _numPseudos; p++)
        {
            var pseudoIndex = p + _firstPseudoIndex;

            if (AnalyzePseudoForCloning(bitVecTraits, escapingNodes, pseudoIndex))
            {
                _regionsToClone++;
            }
            else
            {
                JITDUMP("   not optimizing, so will mark");
#if DEBUG
                if (CompilerInstance.verbose)
                {
                    DumpIndex(pseudoIndex);
                }
#endif
                JITDUMP(" as escaping\n");
                MarkIndexAsEscaping(pseudoIndex);
                BitVecOps.AddElemD(bitVecTraits, escapingNodesToProcess, pseudoIndex);
                newEscapes = true;
            }
        }

        return newEscapes;
    }

    private unsafe bool AnalyzePseudoForCloning(BitVecTraits bitVecTraits, nint[] escapingNodes, int pseudoIndex)
    {
        if (!_cloneMap.TryGetValue(pseudoIndex, out var info))
        {
#if DEBUG
            if (CompilerInstance.verbose)
            {
                DumpIndex(pseudoIndex);
            }
#endif
            JITDUMP("  has no guard info\n");
            return false;
        }

        var adjacencyMatrix = _connGraphAdjacencyMatrix;
        assert(adjacencyMatrix is not null);
        var pseudoAdjacencies = adjacencyMatrix[pseudoIndex];

        if (BitVecOps.IsEmpty(bitVecTraits, pseudoAdjacencies))
        {
            JITDUMP("   No conditionally escaping uses under");
#if DEBUG
            if (CompilerInstance.verbose)
            {
                DumpIndex(pseudoIndex);
            }
#endif
            JITDUMP(", so no reason to clone\n");
            return false;
        }

        var independentlyEscaping = false;
        BitVecOps.VisitBits(bitVecTraits, pseudoAdjacencies, lclNumIndex => {
            if (BitVecOps.IsMember(bitVecTraits, escapingNodes, lclNumIndex))
            {
#if DEBUG
                if (CompilerInstance.verbose)
                {
                    DumpIndex(lclNumIndex);
                }
#endif
                JITDUMP("   escapes independently of");
#if DEBUG
                if (CompilerInstance.verbose)
                {
                    DumpIndex(pseudoIndex);
                }
#endif
                JITDUMP("\n");
                independentlyEscaping = true;
                return false;
            }

            return true;
        });

        if (independentlyEscaping)
        {
            return false;
        }

        if (info.AllocTemps is not null)
        {
            foreach (var v in info.AllocTemps)
            {
                if (BitVecOps.IsMember(bitVecTraits, escapingNodes, LocalToIndex(v)))
                {
                    JITDUMP("   alloc temp");
#if DEBUG
                    if (CompilerInstance.verbose)
                    {
                        DumpIndex(LocalToIndex(v));
                    }
#endif
                    JITDUMP("   escapes independently of");
#if DEBUG
                    if (CompilerInstance.verbose)
                    {
                        DumpIndex(pseudoIndex);
                    }
#endif
                    JITDUMP("\n");
                    return false;
                }
            }
        }

#if DEBUG
        if (CompilerInstance.verbose)
        {
            DumpIndex(pseudoIndex);
        }
#endif
        JITDUMP($"   is guarding the escape of V{info.Local:D2}\n");
        if (info.AllocTemps is not null)
        {
            JITDUMP("   along with ");
            foreach (var v in info.AllocTemps)
            {
                JITDUMP($"V{v:D2} ");
            }
            JITDUMP("\n");
        }

        JITDUMP($"   they escape only when V{info.Local:D2}.Type NE {CompilerInstance.eeGetClassName(info.Type)}\n");
        JITDUMP($"   V{info.Local:D2} + secondary vars have {info.AppearanceCount} appearances\n");
        CompilerInstance.Metrics.EnumeratorGDVProvisionalNoEscape++;

        if (!CanClone(info) || CloneOverlaps(info) || !ShouldClone(info))
        {
            return false;
        }

        JITDUMP("\n*** Can prevent escape under");
#if DEBUG
        if (CompilerInstance.verbose)
        {
            DumpIndex(pseudoIndex);
        }
#endif
        JITDUMP(" via cloning ***\n");
        info.WillClone = true;

        return true;
    }
}
