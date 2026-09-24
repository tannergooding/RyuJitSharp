// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;

namespace RyuJitSharp;

internal sealed class LoopDefinitions
{
    private readonly FlowGraphNaturalLoops _loops;
    private readonly LocalDefinitionsMap?[] _maps;
    private readonly BitVec _visitedBlocks;

    internal LoopDefinitions(FlowGraphNaturalLoops loops)
    {
        _loops = loops;
        _maps = new LocalDefinitionsMap[loops.NumLoops];
        _visitedBlocks = BitVecOps.MakeEmpty(loops.DfsTree.PostOrderTraits());
    }

    private LocalDefinitionsMap GetOrCreateMap(FlowGraphNaturalLoop loop)
    {
        var map = _maps[loop.Index];

        if (map is not null)
        {
            return map;
        }

        var traits = _loops.DfsTree.PostOrderTraits();
#if DEBUG
        // Each map excludes descendant loops, whose blocks must already be visited.
        for (var child = loop.Child; child is not null; child = child.Sibling)
        {
            assert(BitVecOps.IsMember(traits, _visitedBlocks, child.Header.bbPostorderNum));
        }
#endif
        var compiler = _loops.DfsTree.GetCompiler();
        map = new LocalDefinitionsMap();
        _maps[loop.Index] = map;
        var visitor = new LocalsVisitor(compiler, map);

        _ = loop.VisitLoopBlocksReversePostOrder(block => {
            if (!BitVecOps.TryAddElemD(traits, _visitedBlocks, block.bbPostorderNum))
            {
                return BasicBlockVisit.Continue;
            }

            for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
            {
                _ = visitor.WalkTree(ref stmt.RootNodeRef, user: null);
            }

            return BasicBlockVisit.Continue;
        });

        return map;
    }

    private bool VisitLoopNestMaps(FlowGraphNaturalLoop loop, Func<LocalDefinitionsMap, bool> func)
    {
        for (var child = loop.Child; child is not null; child = child.Sibling)
        {
            if (!VisitLoopNestMaps(child, func))
            {
                return false;
            }
        }

        return func(GetOrCreateMap(loop));
    }

    internal void VisitDefinedLocalNums(FlowGraphNaturalLoop loop, Action<int> func)
    {
        _ = VisitLoopNestMaps(loop, map => {
            map.VisitKeys(func);
            return true;
        });
    }

    private struct LocalsVisitor : IGenTreeVisitor<LocalsVisitor>
    {
        private readonly Compiler _compiler;
        private readonly LocalDefinitionsMap _map;
        private readonly GenTreeStack _ancestors;

        internal LocalsVisitor(Compiler compiler, LocalDefinitionsMap map)
        {
            _compiler = compiler;
            _map = map;
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public static bool DoLclVarsOnly => true;

        public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var local = use.AsLclVarCommon();

            if (!local.Oper.IsLocalStore)
            {
                return Compiler.WALK_CONTINUE;
            }

            _map.Set(local.LclNum);
            ref var varDsc = ref _compiler.lvaGetDesc(local.LclNum);

            if (_compiler.lvaIsImplicitByRefLocal(local.LclNum) && varDsc.lvPromoted)
            {
                // Implicit-byref retyping created a promoted struct local whose
                // stores will be rewritten later by morph.
                assert(varDsc.lvFieldLclStart is not 0);
                _map.Set(varDsc.lvFieldLclStart);
                varDsc = ref _compiler.lvaGetDesc(varDsc.lvFieldLclStart);
            }

            if (varDsc.lvPromoted)
            {
                for (var i = 0; i < varDsc.lvFieldCnt; i++)
                {
                    _map.Set(varDsc.lvFieldLclStart + i);
                }
            }
            else if (varDsc.lvIsStructField)
            {
                _map.Set(varDsc.lvParentLcl);
            }

            return Compiler.WALK_CONTINUE;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => Compiler.WALK_CONTINUE;

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<LocalsVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    // This set specializes the native unsigned-to-bool JitHashTable, whose
    // values are always true here. Its iteration order is visible in jitdump.
    internal sealed class LocalDefinitionsMap
    {
        // jithashtable.cpp::jitPrimeInfo, including the native initial size of 9.
        private static ReadOnlySpan<int> BucketCounts => [
            9, 23, 59, 131, 239, 433, 761, 1399, 2473, 4327, 7499, 12973, 22433,
            46559, 96581, 200341, 415517, 861719, 1787021, 3705617, 7684087,
            15933877, 33040633, 68513161, 142069021, 294594427, 733045421,
        ];

        private Node?[] _buckets = [];
        private int _count;
        private int _maximum;

        private sealed class Node(int key, Node? next)
        {
            internal readonly int Key = key;
            internal Node? Next = next;
        }

        internal void Set(int key)
        {
            // Native checks growth before lookup, including overwrites.
            if (_count == _maximum)
            {
                Grow();
            }

            var bucket = key % _buckets.Length;

            for (var node = _buckets[bucket]; node is not null; node = node.Next)
            {
                if (node.Key == key)
                {
                    return;
                }
            }

            _buckets[bucket] = new Node(key, _buckets[bucket]);
            _count++;
        }

        private void Grow()
        {
            // Retain native integer truncation and overflow checks.
            var newSize = unchecked((uint)_count * 3 / 2 * 4 / 3);
            newSize = uint.Max(newSize, 7);

            if (newSize < (uint)_count)
            {
                NOMEM();
            }

            var size = 0;

            foreach (var candidate in BucketCounts)
            {
                if ((uint)candidate >= newSize)
                {
                    size = candidate;
                    break;
                }
            }

            if (size is 0)
            {
                NOMEM();
            }

            var buckets = new Node[size];

            foreach (var bucket in _buckets)
            {
                var node = bucket;

                while (node is not null)
                {
                    var next = node.Next;
                    var index = node.Key % size;
                    node.Next = buckets[index];
                    buckets[index] = node;
                    node = next;
                }
            }

            _buckets = buckets;
            _maximum = (int)((uint)size * 3 / 4);
        }

        internal void VisitKeys(Action<int> func)
        {
            foreach (var bucket in _buckets)
            {
                for (var node = bucket; node is not null; node = node.Next)
                {
                    func(node.Key);
                }
            }
        }
    }
}
