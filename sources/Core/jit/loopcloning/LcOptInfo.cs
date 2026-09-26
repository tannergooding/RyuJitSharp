// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.

namespace RyuJitSharp;

public abstract unsafe class LcOptInfo
{
    public enum OptType
    {
        LcMdArray,
        LcJaggedArray,
        LcTypeTest,
        LcMethodAddrTest,
        LcSpan,
    }

    protected LcOptInfo(OptType type)
    {
        Type = type;
    }

    public OptType Type { get; }
}

public sealed class LcMdArrayOptInfo : LcOptInfo
{
    public readonly GenTreeArrElem ArrElem;
    public readonly int Dim;
    private ArrIndex? _index;

    public LcMdArrayOptInfo(GenTreeArrElem arrElem, int dim) : base(OptType.LcMdArray)
    {
        ArrElem = arrElem;
        Dim = dim;
    }

    public ArrIndex GetArrIndexForDim()
    {
        if (_index is null)
        {
            _index = new ArrIndex
            {
                Rank = ArrElem.ArrRank,
                ArrLcl = ArrElem.ArrObj.AsLclVarCommon().LclNum,
                ArrType = ArrElem.ArrObj.Type,
            };
            for (var i = 0; i < Dim; i++)
            {
                _index.IndLcls.Add(ArrElem.ArrInds[i].AsLclVarCommon().LclNum);
            }
        }
        return _index;
    }
}

public sealed class LcJaggedArrayOptInfo : LcOptInfo
{
    public readonly ArrIndex ArrIndex;
    public readonly int Dim;
    public readonly Statement Stmt;

    public LcJaggedArrayOptInfo(ArrIndex arrIndex, int dim, Statement stmt) : base(OptType.LcJaggedArray)
    {
        ArrIndex = arrIndex.Copy();
        Dim = dim;
        Stmt = stmt;
    }
}

public sealed class LcSpanOptInfo : LcOptInfo
{
    public readonly SpanIndex SpanIndex;
    public readonly Statement Stmt;

    public LcSpanOptInfo(SpanIndex spanIndex, Statement stmt) : base(OptType.LcSpan)
    {
        SpanIndex = new SpanIndex
        {
            LenLcl = spanIndex.LenLcl,
            IndLcl = spanIndex.IndLcl,
            BndsChk = spanIndex.BndsChk,
            UseBlock = spanIndex.UseBlock,
        };
        Stmt = stmt;
    }
}

public sealed unsafe class LcTypeTestOptInfo : LcOptInfo
{
    public readonly BasicBlock Block;
    public readonly Statement Stmt;
    public readonly GenTreeIndir MethodTableIndir;
    public readonly int LclNum;
    public readonly CORINFO_CLASS_HANDLE ClsHnd;

    public LcTypeTestOptInfo(BasicBlock block, Statement stmt, GenTreeIndir methodTableIndir,
        int lclNum, CORINFO_CLASS_HANDLE clsHnd) : base(OptType.LcTypeTest)
    {
        Block = block;
        Stmt = stmt;
        MethodTableIndir = methodTableIndir;
        LclNum = lclNum;
        ClsHnd = clsHnd;
    }
}

public sealed unsafe class LcMethodAddrTestOptInfo : LcOptInfo
{
    public readonly BasicBlock Block;
    public readonly Statement Stmt;
    public readonly GenTreeIndir DelegateAddressIndir;
    public readonly int DelegateLclNum;
    public readonly void* MethAddr;
    public readonly bool IsSlot;
#if DEBUG
    public readonly CORINFO_METHOD_HANDLE TargetMethHnd;
#endif

    public LcMethodAddrTestOptInfo(BasicBlock block, Statement stmt, GenTreeIndir delegateAddressIndir,
        int delegateLclNum, void* methAddr, bool isSlot
#if DEBUG
        , CORINFO_METHOD_HANDLE targetMethHnd
#endif
        ) : base(OptType.LcMethodAddrTest)
    {
        Block = block;
        Stmt = stmt;
        DelegateAddressIndir = delegateAddressIndir;
        DelegateLclNum = delegateLclNum;
        MethAddr = methAddr;
        IsSlot = isSlot;
#if DEBUG
        TargetMethHnd = targetMethHnd;
#endif
    }
}
