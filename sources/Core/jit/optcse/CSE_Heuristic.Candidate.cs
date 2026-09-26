// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class CSE_Candidate
{
    private readonly CSE_HeuristicCommon _context;
    private readonly CSEdsc _descriptor;
    private readonly int _index;
    private weight_t _defCount;
    private weight_t _useCount;
    private uint _cost;
    private uint _size;
    private bool _aggressive;
    private bool _moderate;
    private bool _conservative;
    private bool _stressCSE;
    private bool _random;

    public CSE_Candidate(CSE_HeuristicCommon context, CSEdsc descriptor)
    {
        _context = context;
        _descriptor = descriptor;
        _index = descriptor.csdIndex;
    }

    public CSEdsc CseDsc() => _descriptor;
    public int CseIndex() => _index;
    public weight_t DefCount() => _defCount;
    public weight_t UseCount() => _useCount;
    public GenTree Expr() => _descriptor.csdTreeList.tslTree;
    public uint Cost() => _cost;
    public uint Size() => _size;
    public bool IsSharedConst() => _descriptor.csdIsSharedConst;
    public bool LiveAcrossCall() => _descriptor.csdLiveAcrossCall;
    public void SetAggressive() => _aggressive = true;
    public bool IsAggressive() => _aggressive;
    public void SetModerate() => _moderate = true;
    public bool IsModerate() => _moderate;
    public void SetConservative() => _conservative = true;
    public bool IsConservative() => _conservative;
    public void SetStressCSE() => _stressCSE = true;
    public bool IsStressCSE() => _stressCSE;
    public void SetRandom() => _random = true;
    public bool IsRandom() => _random;

    public void InitializeCounts()
    {
        _size = Expr().CostSz;
        if (_context.CodeOptKind() is Compiler.SMALL_CODE)
        {
            _cost = _size;
            _defCount = _descriptor.csdDefCount;
            _useCount = _descriptor.csdUseCount;
        }
        else
        {
            _cost = Expr().CostEx;
            _defCount = _descriptor.csdDefWtCnt;
            _useCount = _descriptor.csdUseWtCnt;
        }
    }
}
