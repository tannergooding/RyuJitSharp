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
    private BasicBlock? m_block;

    /// <summary>The store node that generates the definition, or null for definitions of uninitialized variables.</summary>
    private GenTreeLclVarCommon? m_defNode;

    /// <summary>The SSA number associated with the previous definition for partial (GTF_USEASG) defs.</summary>
    private int m_useDefSsaNum;

    /// <summary>Number of uses of this SSA def (may be an over-estimate).</summary>
    /// <remarks>May not be accurate for for promoted fields.</remarks>
    private ushort m_numUses = 0;

    /// <summary>True if there may be phi args uses of this def</summary>
    /// <remarks>
    ///   <para>May not be accurate for for promoted fields.</para>
    ///   <para>false implies all uses are non-phi</para>
    /// </remarks>
    private bool m_hasPhiUse = false;
    /// <summary>True if there may be uses of the def in a different block.</summary>
    /// <remarks>May not be accurate for for promoted fields.</remarks>
    private bool m_hasGlobalUse = false;

    public ValueNumPair m_vnPair;

#if DEBUG
    /// <summary>True if this ssa def VN was updated</summary>
    public bool m_updated;

    /// <summary>Originally assigned VN</summary>
    public ValueNumPair m_origVNPair;
#endif

    public LclSsaVarDsc(BasicBlock block)
    {
        m_block = block;
    }

    public LclSsaVarDsc(BasicBlock block, GenTreeLclVarCommon defNode)
    {
        m_block = block;
        DefNode = defNode;
    }

    public BasicBlock? Block
    {
        readonly get
        {
            return m_block;
        }

        set
        {
            m_block = value;
        }
    }

    public GenTreeLclVarCommon? DefNode
    {
        readonly get
        {
            return m_defNode;
        }

        set
        {
            assert((value is null) || value.Oper.IsLocalStore);
            m_defNode = value;
        }
    }

    public readonly bool HasGlobalUse => m_hasGlobalUse;

    public readonly bool HasPhiUse => m_hasPhiUse;

    public readonly ushort NumUses => m_numUses;

    public int UseDefSsaNum
    {
        readonly get
        {
            return m_useDefSsaNum;
        }

        set
        {
            m_useDefSsaNum = value;
        }
    }

    public void AddPhiUse(BasicBlock block)
    {
        m_hasPhiUse = true;
        AddUse(block);
    }

    public void AddUse(BasicBlock block)
    {
        if (block != m_block)
        {
            m_hasGlobalUse = true;
        }

        if (m_numUses < ushort.MaxValue)
        {
            m_numUses++;
        }
    }
}
