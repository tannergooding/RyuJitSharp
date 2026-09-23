// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Tracks reads and writes of locals and addressable locations.</summary>
/// <remarks>Address-exposed locals and all other memory accesses belong to one alias category.</remarks>
public partial struct AliasSet
{
    private LclVarSet _lclVarReads;
    private LclVarSet _lclVarWrites;
    private bool _readsAddressableLocation;
    private bool _writesAddressableLocation;

    public readonly bool WritesAnyLocation => _writesAddressableLocation || !_lclVarWrites.IsEmpty;

    public void AddNode(Compiler compiler, GenTree node)
    {
        // Local reads occur at their user's position, not at the local node's position in LIR.
        foreach (var operand in node.Operands)
        {
            if (operand.Oper.IsLocalRead)
            {
                var lclNum = operand.AsLclVarCommon().LclNum;

                if (compiler.lvaTable[lclNum].IsAddressExposed)
                {
                    _readsAddressableLocation = true;
                }

                _lclVarReads.Add(compiler, lclNum);
            }

            if (operand.IsContained)
            {
                AddNode(compiler, operand);
            }
        }

        var nodeInfo = new NodeInfo(compiler, node);

        if (nodeInfo.ReadsAddressableLocation)
        {
            _readsAddressableLocation = true;
        }

        if (nodeInfo.WritesAddressableLocation)
        {
            _writesAddressableLocation = true;
        }

        if (nodeInfo.IsLclVarRead)
        {
            _lclVarReads.Add(compiler, nodeInfo.LclNum);
        }

        if (nodeInfo.IsLclVarWrite)
        {
            var visitor = new LocalWriteVisitor(compiler, _lclVarWrites);
            _ = node.VisitLogicalLocalDefs(compiler, ref visitor);
            _lclVarWrites = visitor.Writes;

            if (!visitor.AddedLocalDef)
            {
                _lclVarWrites.Add(compiler, nodeInfo.LclNum);
                ref var dsc = ref compiler.lvaGetDesc(nodeInfo.LclNum);

                if (dsc.lvIsStructField)
                {
                    _lclVarWrites.Add(compiler, dsc.lvParentLcl);
                }
                else if (dsc.lvPromoted)
                {
                    for (var i = 0; i < dsc.lvFieldCnt; i++)
                    {
                        _lclVarWrites.Add(compiler, dsc.lvFieldLclStart + i);
                    }
                }
            }
        }
    }

    private struct LocalWriteVisitor(Compiler compiler, LclVarSet writes) : ILocalDefVisitor
    {
        public LclVarSet Writes = writes;
        public bool AddedLocalDef;

        public GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            AddedLocalDef = true;
            var lclNum = def.LclNum;
            Writes.Add(compiler, lclNum);
            ref var dsc = ref compiler.lvaGetDesc(lclNum);

            if (dsc.lvIsStructField)
            {
                Writes.Add(compiler, dsc.lvParentLcl);
            }

            return GenTree.VisitResult.Continue;
        }
    }

    public readonly bool InterferesWith(in AliasSet other)
    {
        if (_writesAddressableLocation && other._writesAddressableLocation)
        {
            return true;
        }

        if ((_readsAddressableLocation && other._writesAddressableLocation) ||
            (_writesAddressableLocation && other._readsAddressableLocation))
        {
            return true;
        }

        if (_lclVarWrites.Intersects(other._lclVarReads) || _lclVarWrites.Intersects(other._lclVarWrites))
        {
            return true;
        }

        return _lclVarReads.Intersects(other._lclVarWrites);
    }

    public readonly bool InterferesWith(in NodeInfo other)
    {
        if (_writesAddressableLocation || !_lclVarWrites.IsEmpty)
        {
            var compiler = other.Compiler;

            foreach (var operand in other.Node.Operands)
            {
                if (operand.Oper.IsLocalRead)
                {
                    var lclNum = operand.AsLclVarCommon().LclNum;

                    if (compiler.lvaTable[lclNum].IsAddressExposed && _writesAddressableLocation)
                    {
                        return true;
                    }

                    if (_lclVarWrites.Contains(lclNum))
                    {
                        return true;
                    }
                }
            }
        }

        if (_writesAddressableLocation && other.WritesAddressableLocation)
        {
            return true;
        }

        if ((_readsAddressableLocation && other.WritesAddressableLocation) ||
            (_writesAddressableLocation && other.ReadsAddressableLocation))
        {
            return true;
        }

        if ((other.IsLclVarRead || other.IsLclVarWrite) && _lclVarWrites.Contains(other.LclNum))
        {
            return true;
        }

        return other.IsLclVarWrite && _lclVarReads.Contains(other.LclNum);
    }

    public readonly bool WritesLocal(int lclNum) => _lclVarWrites.Contains(lclNum);

    public void Clear()
    {
        _readsAddressableLocation = false;
        _writesAddressableLocation = false;
        _lclVarReads.Clear();
        _lclVarWrites.Clear();
    }
}
