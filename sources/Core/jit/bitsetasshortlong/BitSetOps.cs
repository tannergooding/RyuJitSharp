// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md input the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using BitSetShortLongRep = nuint[];

namespace RyuJitSharp;

public struct BitSetOps<TEnv, TBitSetTraits>
    where TEnv : class
    where TBitSetTraits : IBitSetTraits<TEnv>
{
    private static unsafe uint BitsInSizeT => unchecked((uint)(sizeof(nuint))) * 8;

    private static bool IsShort(TEnv env) => TBitSetTraits.GetArrSize(env) <= 1;

    private static void AssignLong(TEnv env, BitSetShortLongRep lhs, BitSetShortLongRep rhs)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        rhs.AsSpan(0, len).CopyTo(lhs);
    }

    private static BitSetShortLongRep MakeSingletonLong(TEnv env, uint bitNum)
    {
        assert(!IsShort(env));

        var res = MakeEmptyArrayBits(env);
        var index = bitNum / BitsInSizeT;

        res[index] = (nuint)(1) << (int)(bitNum % BitsInSizeT);
        return res;
    }

    private static BitSetShortLongRep MakeCopyLong(TEnv env, BitSetShortLongRep bs)
    {
        assert(!IsShort(env));

        var res = MakeUninitArrayBits(env);
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        bs.AsSpan(0, len).CopyTo(res);
        return res;
    }

    private static bool IsEmptyLong(TEnv env, BitSetShortLongRep bs)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        return !bs.AsSpan(0, len).ContainsAnyExcept((nuint)(0));
    }

    private static uint CountLong(TEnv env, BitSetShortLongRep bs)
    {
        assert(!IsShort(env));

        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        var res = 0u;

        for (var i = 0; i < len; i++)
        {
            res += (uint)(nuint.PopCount(bs[i]));
        }
        return res;
    }

    private static bool IsEmptyUnionLong(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (var i = 0; i < len; i++)
        {
            if ((bs1[i] | bs2[i]) != 0)
            {
                return false;
            }
        }
        return true;
    }

    private static void UnionDLong(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (var i = 0; i < len; i++)
        {
            bs1[i] |= bs2[i];
        }
    }

    private static bool UnionDLongChanged(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));

        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        var changed = false;

        for (var i = 0; i < len; i++)
        {
            var bsCurrent = bs1[i];
            var bsNew = bsCurrent | bs2[i];

            changed |= (bsNew != bsCurrent);
            bs1[i] = bsNew;
        }
        return changed;
    }

    private static void DiffDLong(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (var i = 0; i < len; i++)
        {
            bs1[i] &= ~bs2[i];
        }
    }

    private static void AddElemDLong(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(!IsShort(env));
        var index = i / BitsInSizeT;

        var mask = ((nuint)(1)) << (int)((i % BitsInSizeT));
        bs[index] |= mask;
    }

    private static bool TryAddElemDLong(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(!IsShort(env));

        var index = i / BitsInSizeT;
        var mask = ((nuint)(1)) << (int)(i % BitsInSizeT);
        var bits = bs[index];

        var added = (bits & mask) == 0;
        bs[index] = bits | mask;
        return added;
    }

    private static void RemoveElemDLong(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(!IsShort(env));

        var index = i / BitsInSizeT;
        var mask = ((nuint)(1)) << (int)(i % BitsInSizeT);

        mask = ~mask;
        bs[index] &= mask;
    }

    private static void ClearDLong(TEnv env, BitSetShortLongRep bs)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        bs.AsSpan(0, len).Clear();
    }

    private static BitSetShortLongRep MakeUninitArrayBits(TEnv env)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        return GC.AllocateUninitializedArray<nuint>(len);
    }

    private static BitSetShortLongRep MakeEmptyArrayBits(TEnv env)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        return new nuint[len];
    }

    private static BitSetShortLongRep MakeFullArrayBits(TEnv env)
    {
        assert(!IsShort(env));

        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        var res = GC.AllocateUninitializedArray<nuint>(len);
        res.AsSpan(0, len).Fill(nuint.MaxValue);

        // Start with all ones, shift in zeros in the last elem.
        var lastElemBits = ((TBitSetTraits.GetSize(env) - 1) % BitsInSizeT) + 1;
        res[len - 1] = nuint.MaxValue >>> (int)((BitsInSizeT - lastElemBits));
        return res;
    }

    private static bool IsMemberLong(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(!IsShort(env));
        var mask = ((nuint)(1)) << (int)(i % BitsInSizeT);
        return (bs[(int)(i / BitsInSizeT)] & mask) != 0;
    }

    private static bool EqualLong(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        return bs2.AsSpan(0, len).SequenceEqual(bs1.AsSpan(0, len));
    }

    private static bool IsSubsetLong(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (var i = 0; i < len; i++)
        {
            var bs1Val = bs1[i];

            if ((bs1Val & bs2[i]) != bs1Val)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsEmptyIntersectionLong(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (var i = 0; i < len; i++)
        {
            if ((bs1[i] & bs2[i]) != 0)
            {
                return false;
            }
        }
        return true;
    }

    private static void IntersectionDLong(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (var i = 0; i < len; i++)
        {
            bs1[i] &= bs2[i];
        }
    }

    private static void DataFlowDLong(TEnv env, BitSetShortLongRep output, BitSetShortLongRep gen, BitSetShortLongRep input)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (var i = 0; i < len; i++)
        {
            output[i] &= (gen[i] | input[i]);
        }
    }

    private static void LivenessDLong(TEnv env, BitSetShortLongRep input, BitSetShortLongRep def, BitSetShortLongRep use, BitSetShortLongRep output)
    {
        assert(!IsShort(env));
        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));

        for (uint i = 0; i < len; i++)
        {
            input[i] = use[i] | (output[i] & ~def[i]);
        }
    }

#if DEBUG
    private static unsafe string ToStringLong(TEnv env, BitSetShortLongRep bs)
    {
        assert(!IsShort(env));

        var len = unchecked((int)(TBitSetTraits.GetArrSize(env)));
        var stringBuilder = new StringBuilder(len * (sizeof(nuint) * 2) + 4);

        for (var i = len; 0 < i; i--)
        {
            var bits = bs[i - 1];

            if (sizeof(nuint) == sizeof(long))
            {
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $"{bits:X16}");
            }
            else
            {
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $"{bits:X8}");
            }
        }
        return stringBuilder.ToString();
    }
#endif

    public static BitSetShortLongRep? UninitVal() => null;

    public static bool MaybeUninit([NotNullWhen(false)] BitSetShortLongRep? bs) => bs is null;

    public static void Assign(TEnv env, ref BitSetShortLongRep? lhs, BitSetShortLongRep rhs)
    {
        // We can't assert that rhs != UninitVal input the Short case, because input that case it's a legal value.
        if (IsShort(env))
        {
            // Both are short.
            lhs = rhs;
        }
        else if (MaybeUninit(lhs))
        {
            assert(!MaybeUninit(rhs));
            lhs = MakeCopy(env, rhs);
        }
        else
        {
            AssignLong(env, lhs, rhs);
        }
    }

    public static void AssignAllowUninitRhs(TEnv env, ref BitSetShortLongRep? lhs, BitSetShortLongRep? rhs)
    {
        if (IsShort(env))
        {
            // Both are short.
            lhs = rhs;
        }
        else if (MaybeUninit(rhs))
        {
            lhs = rhs;
        }
        else if (MaybeUninit(lhs))
        {
            lhs = MakeCopy(env, rhs);
        }
        else
        {
            AssignLong(env, lhs, rhs);
        }
    }

    public static void AssignNoCopy(TEnv env, ref BitSetShortLongRep lhs, BitSetShortLongRep rhs)
    {
        lhs = rhs;
    }

    public static void ClearD(TEnv env, ref BitSetShortLongRep? bs)
    {
        if (IsShort(env))
        {
            bs = null;
        }
        else
        {
            assert(!MaybeUninit(bs));
            ClearDLong(env, bs);
        }
    }

    public static BitSetShortLongRep MakeSingleton(TEnv env, uint bitNum)
    {
        assert(bitNum < TBitSetTraits.GetSize(env));

        if (IsShort(env))
        {
            return [((nuint)(1)) << (int)(bitNum)];
        }
        else
        {
            return MakeSingletonLong(env, bitNum);
        }
    }

    public static BitSetShortLongRep MakeCopy(TEnv env, BitSetShortLongRep bs)
    {
        if (IsShort(env))
        {
            return bs;
        }
        else
        {
            return MakeCopyLong(env, bs);
        }
    }

    public static bool IsEmpty(TEnv env, BitSetShortLongRep? bs)
    {
        if (IsShort(env))
        {
            return bs is null;
        }
        else
        {
            assert(!MaybeUninit(bs));
            return IsEmptyLong(env, bs);
        }
    }

    public static uint Count(TEnv env, BitSetShortLongRep bs)
    {
        if (IsShort(env))
        {
            return (uint)(nuint.PopCount(bs[0]));
        }
        else
        {
            assert(!MaybeUninit(bs));
            return CountLong(env, bs);
        }
    }

    public static bool IsEmptyUnion(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            return (bs1[0] | bs2[0]) == 0;
        }
        else
        {
            return IsEmptyUnionLong(env, bs1, bs2);
        }
    }

    public static void UnionD(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            bs1[0] |= bs2[0];
        }
        else
        {
            UnionDLong(env, bs1, bs2);
        }
    }

    public static bool UnionDChanged(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            var bsCurrent = bs1[0];
            var bsNew = bsCurrent | bs2[0];
            var changed = bsNew != bsCurrent;
            bs1[0] = bsNew;
            return changed;
        }
        else
        {
            return UnionDLongChanged(env, bs1, bs2);
        }
    }

    public static BitSetShortLongRep Union(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        var res = MakeCopy(env, bs1);
        UnionD(env, res, bs2);
        return res;
    }

    public static void DiffD(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            bs1[0] &= ~bs2[0];
        }
        else
        {
            DiffDLong(env, bs1, bs2);
        }
    }
    public static BitSetShortLongRep Diff(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        var res = MakeCopy(env, bs1);
        DiffD(env, res, bs2);
        return res;
    }

    public static void RemoveElemD(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(i < TBitSetTraits.GetSize(env));

        if (IsShort(env))
        {
            var mask = (nuint)(1) << (int)(i);
            bs[0] &= ~mask;
        }
        else
        {
            assert(!MaybeUninit(bs));
            RemoveElemDLong(env, bs, i);
        }
    }
    public static BitSetShortLongRep RemoveElem(TEnv env, BitSetShortLongRep bs, uint i)
    {
        var res = MakeCopy(env, bs);
        RemoveElemD(env, res, i);
        return res;
    }

    public static void AddElemD(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(i < TBitSetTraits.GetSize(env));

        if (IsShort(env))
        {
            var mask = (nuint)(1) << (int)(i);
            bs[0] |= mask;
        }
        else
        {
            AddElemDLong(env, bs, i);
        }
    }
    public static BitSetShortLongRep AddElem(TEnv env, BitSetShortLongRep bs, uint i)
    {
        var res = MakeCopy(env, bs);
        AddElemD(env, res, i);
        return res;
    }

    public static bool TryAddElemD(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(i < TBitSetTraits.GetSize(env));

        if (IsShort(env))
        {
            var mask = (nuint)(1) << (int)(i);
            var bits = bs[0];
            var added = (bits & mask) == 0;
            bs[0] = bits | mask;
            return added;
        }
        else
        {
            return TryAddElemDLong(env, bs, i);
        }
    }

    public static bool IsMember(TEnv env, BitSetShortLongRep bs, uint i)
    {
        assert(i < TBitSetTraits.GetSize(env));

        if (IsShort(env))
        {
            var mask = (nuint)(1) << (int)(i);
            return (bs[0] & mask) != 0;
        }
        else
        {
            assert(!MaybeUninit(bs));
            return IsMemberLong(env, bs, i);
        }
    }

    public static void IntersectionD(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            bs1[0] &= bs2[0];
        }
        else
        {
            IntersectionDLong(env, bs1, bs2);
        }
    }

    public static BitSetShortLongRep Intersection(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        var res = MakeCopy(env, bs1);
        IntersectionD(env, res, bs2);
        return res;
    }
    public static bool IsEmptyIntersection(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            return (bs1[0] & bs2[0]) == 0;
        }
        else
        {
            return IsEmptyIntersectionLong(env, bs1, bs2);
        }
    }

    public static void DataFlowD(TEnv env, BitSetShortLongRep output, BitSetShortLongRep gen, BitSetShortLongRep input)
    {
        if (IsShort(env))
        {
            output[0] &= (gen[0] | input[0]);
        }
        else
        {
            DataFlowDLong(env, output, gen, input);
        }
    }

    public static void LivenessD(TEnv env, BitSetShortLongRep input, BitSetShortLongRep def, BitSetShortLongRep use, BitSetShortLongRep output)
    {
        if (IsShort(env))
        {
            input[0] = use[0] | (output[0] & ~def[0]);
        }
        else
        {
            LivenessDLong(env, input, def, use, output);
        }
    }

    public static bool IsSubset(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            var bs1Val = bs1[0];
            return (bs1Val & bs2[0]) == bs1Val;
        }
        else
        {
            return IsSubsetLong(env, bs1, bs2);
        }
    }

    public static bool Equal(TEnv env, BitSetShortLongRep bs1, BitSetShortLongRep bs2)
    {
        if (IsShort(env))
        {
            return bs1[0] == bs2[0];
        }
        else
        {
            return EqualLong(env, bs1, bs2);
        }
    }

#if DEBUG
    // Returns a string valid until the allocator releases the memory.
    public static unsafe string ToString(TEnv env, BitSetShortLongRep bs)
    {
        if (IsShort(env))
        {
            return (sizeof(nuint) == sizeof(long)) ? $"{bs[0]:X16}" : $"{bs[0]:X8}";
        }
        else
        {
            return ToStringLong(env, bs);
        }
    }
#endif

    public static BitSetShortLongRep MakeEmpty(TEnv env)
    {
        if (IsShort(env))
        {
            return [0];
        }
        else
        {
            return MakeEmptyArrayBits(env);
        }
    }

    public static BitSetShortLongRep MakeFull(TEnv env)
    {
        if (IsShort(env))
        {
            // Can't just shift by numBits+1, since that might be 32 (and (1 << 32) == 1, for an uint).
            var numBits = TBitSetTraits.GetSize(env);

            if (numBits == BitsInSizeT)
            {
                // Can't use the implementation below to get all 1's...
                return [nuint.MaxValue];
            }
            else
            {
                return [((nuint)(1) << (int)(numBits)) - 1];
            }
        }
        else
        {
            return MakeFullArrayBits(env);
        }
    }
}
