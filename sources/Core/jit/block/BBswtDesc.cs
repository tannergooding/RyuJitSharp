// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

// BBswtDesc -- descriptor for a switch block
//
//  Things to know:
//  1. If bbsHasDefault is true, the default case is the last one in the array of basic block addresses
//     namely bbsDstTab[bbsCount - 1].
//  2. bbsCount must be at least 1, for the default case. bbsCount cannot be zero. It appears that the ECMA spec
//     allows for a degenerate switch with zero cases. Normally, the optimizer will optimize degenerate
//     switches with just a default case to a BBJ_ALWAYS branch, and a switch with just two cases to a BBJ_COND.
//     However, in debuggable code, we might not do that, so bbsCount might be 1.
//  3. BBswtDesc makes no promises about the relative positions of the 'succs' and 'cases' arrays.
//     Callers are responsible for allocating these arrays during BBswtDesc creation.
//     A potential optimization is to allocate one array large enough for the two;
//     this is safe, because BBswtDesc does not support adding new cases/successors.
public sealed class BBswtDesc : BBJumpTable
{
    /// <summary>array of non-unique FlowEdge pointing to the switch cases</summary>
    private FlowEdge[] _cases;
    private int[] _caseOffsets;
    private int _caseCount;

    /// <summary>Case number of most likely case</summary>
    /// <remarks>(only known with PGO, only valid if bbsHasDominantCase is true)</remarks>
    private int bbsDominantCase;

    /// <summary>true if last switch case is a default case</summary>
    private bool bbsHasDefault;

    /// <summary>true if switch has a dominant case</summary>
    private bool bbsHasDominantCase;

    internal BBswtDesc(FlowEdge[] succs, int[] caseOffsets, bool hasDefault)
        : base(succs)
    {
        var caseCount = caseOffsets.Length;
        _caseCount = caseCount;

        _cases = new FlowEdge[caseCount];
        _caseOffsets = caseOffsets;
    }

    internal BBswtDesc(FlowEdge[] succs, int[] caseOffsets, bool hasDefault, int dominantCase)
        : base(succs)
    {
        var caseCount = caseOffsets.Length;
        _caseCount = caseCount;

        _cases = new FlowEdge[caseCount];
        _caseOffsets = caseOffsets;
        
        bbsDominantCase = dominantCase;
        bbsHasDefault = hasDefault;
        bbsHasDominantCase = true;
    }

    public BBswtDesc(Compiler comp, BBswtDesc other)
        : base(other)
    {
        _cases = [.. other.Cases];
        _caseOffsets = [.. other.CaseOffsets];
        _caseCount = other._caseCount;

        bbsDominantCase = other.bbsDominantCase;
        bbsHasDefault = other.bbsHasDefault;
        bbsHasDominantCase = other.bbsHasDominantCase;
    }

    public Span<FlowEdge> Cases => _cases.AsSpan(0, _caseCount);

    internal Span<int> CaseOffsets => _caseOffsets.AsSpan(0, _caseCount);

    public FlowEdge DefaultCase
    {
        get
        {
            assert(Debugger.IsAttached || bbsHasDefault);
            return Cases[^1];
        }
    }

    public int DominantCase
    {
        get
        {
            assert(Debugger.IsAttached || bbsHasDominantCase);
            return bbsDominantCase;
        }

        set
        {
            assert(!bbsHasDominantCase);
            bbsDominantCase = value;
            bbsHasDominantCase = true;
        }
    }

    public bool HasDefaultCase => bbsHasDefault;

    public bool HasDominantCase => bbsHasDominantCase;

    public void RemoveDefaultCase()
    {
        assert(bbsHasDefault);
        assert(Cases.Length > 0);

        bbsHasDefault = false;

        _caseCount--;
        Cases[^1] = null!;
    }

    public void RemoveDominantCase()
    {
        assert(bbsHasDominantCase);
        bbsHasDominantCase = false;
    }
}
