// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Stores information associated with a LclVar SSA definition</summary>
public struct LclSsaVarDsc
{
    // TODO-Cleanup: In the case of uninitialized variables the block is set to null by SsaBuilder and changed to fgFirstBB during value numbering.
    // It would be useful to investigate and perhaps eliminate this rather unexpected behavior.

    /// <summary>The basic block where the definition occurs.</summary>
    /// <remarks>Definitions of uninitialized variables are considered to occur at the start of the first basic block (fgFirstBB).</remarks>
    private BasicBlock? _block;

    /// <summary>The store node that generates the definition, or null for definitions of uninitialized variables.</summary>
    private GenTreeLclVarCommon? _defNode;

    /// <summary>The SSA number associated with the previous definition for partial (GTF_USEASG) defs.</summary>
    private int _useDefSsaNum;

    /// <summary>Number of uses of this SSA def (may be an over-estimate).</summary>
    /// <remarks>May not be accurate for for promoted fields.</remarks>
    private ushort _numUses = 0;

    /// <summary>True if there may be phi args uses of this def</summary>
    /// <remarks>
    ///   <para>May not be accurate for for promoted fields.</para>
    ///   <para>false implies all uses are non-phi</para>
    /// </remarks>
    private bool _hasPhiUse = false;
    /// <summary>True if there may be uses of the def in a different block.</summary>
    /// <remarks>May not be accurate for for promoted fields.</remarks>
    private bool _hasGlobalUse = false;

    public ValueNumPair _vnPair;

#if DEBUG
    /// <summary>True if this ssa def VN was updated</summary>
    public bool _updated;

    /// <summary>Originally assigned VN</summary>
    public ValueNumPair _origVNPair;
#endif

    public LclSsaVarDsc(BasicBlock block)
    {
        _block = block;
    }

    public LclSsaVarDsc(BasicBlock block, GenTreeLclVarCommon defNode)
    {
        _block = block;
        DefNode = defNode;
    }

    public BasicBlock? Block
    {
        readonly get
        {
            return _block;
        }

        set
        {
            _block = value;
        }
    }

    public GenTreeLclVarCommon? DefNode
    {
        readonly get
        {
            return _defNode;
        }

        set
        {
            assert((value is null) || value.Oper.IsLocalStore);
            _defNode = value;
        }
    }

    public readonly bool HasGlobalUse => _hasGlobalUse;

    public readonly bool HasPhiUse => _hasPhiUse;

    public readonly ushort NumUses => _numUses;

    public int UseDefSsaNum
    {
        readonly get
        {
            return _useDefSsaNum;
        }

        set
        {
            _useDefSsaNum = value;
        }
    }

    public void AddPhiUse(BasicBlock block)
    {
        _hasPhiUse = true;
        AddUse(block);
    }

    public void AddUse(BasicBlock block)
    {
        if (block != _block)
        {
            _hasGlobalUse = true;
        }

        if (_numUses < ushort.MaxValue)
        {
            _numUses++;
        }
    }
}
