// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, morph.cpp.

using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class MorphMDArrayTempCache(Compiler compiler)
    {
        private sealed class TempList(Compiler compiler)
        {
            private readonly List<int> _temps = [];
            private int _nextAvailable;

            public int GetTemp()
            {
                if (_nextAvailable < _temps.Count)
                {
                    var temp = _temps[_nextAvailable++];
                    JITDUMP($"Reusing temp V{temp:D2}\n");
                    return temp;
                }

                var newTemp = compiler.lvaGrabTemp(true, "MD array shared temp");
                _temps.Add(newTemp);
                _nextAvailable++;
                return newTemp;
            }

            public void Reset() => _nextAvailable = 0;
        }

        private readonly TempList _intTemps = new(compiler);
        private readonly TempList _refTemps = new(compiler);

        public int GrabTemp(var_types type) => type.ActualType switch {
            TYP_INT => _intTemps.GetTemp(),
            TYP_REF => _refTemps.GetTemp(),
            _ => throw new FatalJitException("An MD-array temporary must be an int or object reference."),
        };

        public void Reset()
        {
            _intTemps.Reset();
            _refTemps.Reset();
        }
    }

    private bool fgMorphArrayOpsStmt(MorphMDArrayTempCache tempCache, BasicBlock block, Statement stmt)
    {
        var visitor = new MorphMDArrayVisitor(this, tempCache
#if DEBUG
            , block
#endif
        );
        _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        return visitor.Changed;
    }

    public PhaseStatus fgMorphArrayOps()
    {
        if ((optMethodFlags & OMF_HAS_MDARRAYREF) == 0)
        {
            JITDUMP("No multi-dimensional array references in the function\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var changed = false;
        var tempCache = new MorphMDArrayTempCache(this);
        foreach (var block in Blocks)
        {
            if (!block.HasFlag(BBF_HAS_MDARRAYREF))
            {
                continue;
            }

            compCurBB = block;
            foreach (var stmt in block.Statements)
            {
                if (fgMorphArrayOpsStmt(tempCache, block, stmt))
                {
                    changed = true;
                    var morphedTree = fgMorphTree(stmt.RootNode);
                    JITDUMP("fgMorphArrayOps (after remorph):\n");
                    DISPTREE(morphedTree);
                    stmt.RootNode = morphedTree;
                }
            }
            tempCache.Reset();
        }

        return changed ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
