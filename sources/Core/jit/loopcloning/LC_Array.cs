// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.

using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public struct LC_Array
{
    public enum ArrType
    {
        Invalid,
        Jagged,
        MdArray,
    }

    public enum OperType
    {
        None,
        ArrLen,
    }

    public ArrType Type;
    public ArrIndex ArrIndex;
    public OperType Oper;
    public int Dim;

    public LC_Array(ArrType type, ArrIndex arrIndex, int dim, OperType oper)
    {
        Type = type;
        ArrIndex = arrIndex;
        Dim = dim;
        Oper = oper;
    }

    public LC_Array(ArrType type, ArrIndex arrIndex, OperType oper)
        : this(type, arrIndex, -1, oper)
    {
    }

    public readonly int GetDimRank() => (Dim < 0) ? ArrIndex.Rank : Dim;

#if DEBUG
    public readonly void Print()
    {
        ArrIndex.Print(Dim);
        if (Oper is OperType.ArrLen)
        {
            jitprintf(".Length");
        }
    }
#endif

    public readonly bool Matches(in LC_Array that)
    {
        assert(Type is not ArrType.Invalid && that.Type is not ArrType.Invalid);
        if ((Type != that.Type) || (ArrIndex.ArrLcl != that.ArrIndex.ArrLcl) ||
            (ArrIndex.ArrType != that.ArrIndex.ArrType) || (Oper != that.Oper))
        {
            return false;
        }

        var rank = GetDimRank();
        if (rank != that.GetDimRank())
        {
            return false;
        }

        for (var i = 0; i < rank; i++)
        {
            if (ArrIndex.IndLcls[i] != that.ArrIndex.IndLcls[i])
            {
                return false;
            }
        }
        return true;
    }

    public readonly unsafe GenTree ToGenTree(Compiler compiler, BasicBlock block)
    {
        if (Type is not ArrType.Jagged)
        {
            throw new FatalJitException("LC_Array.ToGenTree does not support MD arrays in the pinned native implementation.");
        }

        GenTree array = compiler.gtNewLclvNode(compiler.lvaGetDesc(ArrIndex.ArrLcl).Type, ArrIndex.ArrLcl);
        var rank = GetDimRank();
        for (var i = 0; i < rank; i++)
        {
            var local = ArrIndex.IndLcls[i];
            var index = compiler.gtNewLclvNode(compiler.lvaGetDesc(local).Type, local);
            var address = compiler.gtNewArrayIndexAddr(array, index, TYP_REF, NO_CLASS_HANDLE);
            address.Flags &= ~GTF_INX_RNGCHK;
            address.Flags |= GTF_INX_ADDR_NONNULL;
            array = compiler.fgMorphTree(compiler.gtNewIndexIndir(address));
        }

        if (Oper is OperType.ArrLen)
        {
            return compiler.gtNewArrLen(TYP_INT, array, OFFSETOF__CORINFO_Array__length);
        }

        assert(Oper is OperType.None);
        return array;
    }
}

public readonly struct LC_Span
{
    public readonly SpanIndex SpanIndex;

    public LC_Span(SpanIndex spanIndex)
    {
        SpanIndex = spanIndex;
    }

#if DEBUG
    public void Print() => SpanIndex.Print();
#endif

    public bool Matches(in LC_Span that)
        => (SpanIndex.LenLcl == that.SpanIndex.LenLcl) && (SpanIndex.IndLcl == that.SpanIndex.IndLcl);

    public GenTree ToGenTree(Compiler compiler)
        => compiler.gtNewLclvNode(compiler.lvaGetDesc(SpanIndex.LenLcl).Type, SpanIndex.LenLcl);
}
