// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static class BitSetOpsWithCounter<TEnv, TBitSetTraits>
    where TEnv : class
    where TBitSetTraits : IBitSetTraits<TEnv>, IBitSetOpCounterTraits<TEnv>
{
    public static nint[] UninitVal()
        => BitSetOps<TEnv, TBitSetTraits>.UninitVal();

    public static bool MaybeUninit(ReadOnlySpan<nint> bitSet)
        => BitSetOps<TEnv, TBitSetTraits>.MaybeUninit(bitSet);

    public static nint[] MakeEmpty(TEnv env)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_MakeEmpty);
        return BitSetOps<TEnv, TBitSetTraits>.MakeEmpty(env);
    }

    public static nint[] MakeFull(TEnv env)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_MakeFull);
        return BitSetOps<TEnv, TBitSetTraits>.MakeFull(env);
    }

    public static nint[] MakeSingleton(TEnv env, int bitNum)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_MakeSingleton);
        return BitSetOps<TEnv, TBitSetTraits>.MakeSingleton(env, bitNum);
    }

    public static void Assign(TEnv env, ref nint[] lhs, ReadOnlySpan<nint> rhs)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_Assign);
        BitSetOps<TEnv, TBitSetTraits>.Assign(env, ref lhs, rhs);
    }

    public static void AssignAllowUninitRhs(TEnv env, ref nint[] lhs, ReadOnlySpan<nint> rhs)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_AssignAllowUninitRhs);
        BitSetOps<TEnv, TBitSetTraits>.AssignAllowUninitRhs(env, ref lhs, rhs);
    }

    public static void AssignNoCopy(TEnv env, ref nint[] lhs, nint[] rhs)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_AssignNocopy);
        BitSetOps<TEnv, TBitSetTraits>.AssignNoCopy(env, ref lhs, rhs);
    }

    public static void ClearD(TEnv env, Span<nint> bitSet)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_ClearD);
        BitSetOps<TEnv, TBitSetTraits>.ClearD(env, bitSet);
    }

    public static nint[] MakeCopy(TEnv env, ReadOnlySpan<nint> bitSet)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_MakeCopy);
        return BitSetOps<TEnv, TBitSetTraits>.MakeCopy(env, bitSet);
    }

    public static bool IsEmpty(TEnv env, ReadOnlySpan<nint> bitSet)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_IsEmpty);
        return BitSetOps<TEnv, TBitSetTraits>.IsEmpty(env, bitSet);
    }

    public static nint Count(TEnv env, ReadOnlySpan<nint> bitSet)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_Count);
        return BitSetOps<TEnv, TBitSetTraits>.Count(env, bitSet);
    }

    public static bool IsEmptyUnion(TEnv env, ReadOnlySpan<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
        => BitSetOps<TEnv, TBitSetTraits>.IsEmptyUnion(env, bitSet1, bitSet2);

    public static bool IsMember(TEnv env, ReadOnlySpan<nint> bitSet, int bitNum)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_IsMember);
        return BitSetOps<TEnv, TBitSetTraits>.IsMember(env, bitSet, bitNum);
    }

    public static void AddElemD(TEnv env, Span<nint> bitSet, int bitNum)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_AddElemD);
        BitSetOps<TEnv, TBitSetTraits>.AddElemD(env, bitSet, bitNum);
    }

    public static nint[] AddElem(TEnv env, ReadOnlySpan<nint> bitSet, int bitNum)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_AddElem);
        return BitSetOps<TEnv, TBitSetTraits>.AddElem(env, bitSet, bitNum);
    }

    public static bool TryAddElemD(TEnv env, Span<nint> bitSet, int bitNum)
        => BitSetOps<TEnv, TBitSetTraits>.TryAddElemD(env, bitSet, bitNum);

    public static void RemoveElemD(TEnv env, Span<nint> bitSet, int bitNum)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_RemoveElemD);
        BitSetOps<TEnv, TBitSetTraits>.RemoveElemD(env, bitSet, bitNum);
    }

    public static nint[] RemoveElem(TEnv env, ReadOnlySpan<nint> bitSet, int bitNum)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_RemoveElem);
        return BitSetOps<TEnv, TBitSetTraits>.RemoveElem(env, bitSet, bitNum);
    }

    public static void UnionD(TEnv env, Span<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_UnionD);
        BitSetOps<TEnv, TBitSetTraits>.UnionD(env, bitSet1, bitSet2);
    }

    public static bool UnionDChanged(TEnv env, Span<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_UnionDChanged);
        return BitSetOps<TEnv, TBitSetTraits>.UnionDChanged(env, bitSet1, bitSet2);
    }

    public static nint[] Union(TEnv env, ReadOnlySpan<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_Union);
        return BitSetOps<TEnv, TBitSetTraits>.Union(env, bitSet1, bitSet2);
    }

    public static void IntersectionD(TEnv env, Span<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_IntersectionD);
        BitSetOps<TEnv, TBitSetTraits>.IntersectionD(env, bitSet1, bitSet2);
    }

    public static nint[] Intersection(TEnv env, ReadOnlySpan<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_Intersection);
        return BitSetOps<TEnv, TBitSetTraits>.Intersection(env, bitSet1, bitSet2);
    }

    public static bool IsEmptyIntersection(TEnv env, ReadOnlySpan<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_IsEmptyIntersection);
        return BitSetOps<TEnv, TBitSetTraits>.IsEmptyIntersection(env, bitSet1, bitSet2);
    }

    public static void DiffD(TEnv env, Span<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_DiffD);
        BitSetOps<TEnv, TBitSetTraits>.DiffD(env, bitSet1, bitSet2);
    }

    public static nint[] Diff(TEnv env, ReadOnlySpan<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_Diff);
        return BitSetOps<TEnv, TBitSetTraits>.Diff(env, bitSet1, bitSet2);
    }

    public static void DataFlowD(TEnv env, Span<nint> output, ReadOnlySpan<nint> gen, ReadOnlySpan<nint> input)
        => BitSetOps<TEnv, TBitSetTraits>.DataFlowD(env, output, gen, input);

    public static void LivenessD(
        TEnv env,
        Span<nint> output,
        ReadOnlySpan<nint> def,
        ReadOnlySpan<nint> use,
        ReadOnlySpan<nint> input)
        => BitSetOps<TEnv, TBitSetTraits>.LivenessD(env, output, def, use, input);

    public static bool IsSubset(TEnv env, ReadOnlySpan<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_IsSubset);
        return BitSetOps<TEnv, TBitSetTraits>.IsSubset(env, bitSet1, bitSet2);
    }

    public static bool Equal(TEnv env, ReadOnlySpan<nint> bitSet1, ReadOnlySpan<nint> bitSet2)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_Equal);
        return BitSetOps<TEnv, TBitSetTraits>.Equal(env, bitSet1, bitSet2);
    }

#if DEBUG
    public static string ToString(TEnv env, ReadOnlySpan<nint> bitSet)
    {
        RecordOp(env, BitSetSupport.Operation.BSOP_ToString);
        return BitSetOps<TEnv, TBitSetTraits>.ToString(env, bitSet);
    }
#endif

    public static bool VisitBits(TEnv env, ReadOnlySpan<nint> bitSet, Func<int, bool> func)
        => BitSetOps<TEnv, TBitSetTraits>.VisitBits(env, bitSet, func);

    public static bool VisitBitsReverse(TEnv env, ReadOnlySpan<nint> bitSet, Func<int, bool> func)
        => BitSetOps<TEnv, TBitSetTraits>.VisitBitsReverse(env, bitSet, func);

    public ref struct Iter
    {
        private BitSetOps<TEnv, TBitSetTraits>.Iter _iter;
        private readonly TEnv _env;

        public Iter(TEnv env, ReadOnlySpan<nint> bitSet)
        {
            _iter = new(env, bitSet);
            _env = env;
        }

        public bool NextElem(ref uint element)
        {
            RecordOp(_env, BitSetSupport.Operation.BSOP_NextBit);
            return _iter.NextElem(ref element);
        }
    }

    private static void RecordOp(TEnv env, BitSetSupport.Operation operation)
    {
        TBitSetTraits.GetOpCounter(env).RecordOp(operation);
    }
}
