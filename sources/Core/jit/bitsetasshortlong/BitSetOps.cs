// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md input the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RyuJitSharp;

public struct BitSetOps<TEnv, TBitSetTraits>
    where TEnv : class
    where TBitSetTraits : IBitSetTraits<TEnv>
{
    private static nint[] MakeUninitArrayBits(TEnv env)
        => GC.AllocateUninitializedArray<nint>(TBitSetTraits.GetArrSize(env));

    private static nint[] MakeEmptyArrayBits(TEnv env)
        => new nint[TBitSetTraits.GetArrSize(env)];

    private static nint[] MakeFullArrayBits(TEnv env)
    {
        var res = GC.AllocateUninitializedArray<nint>(TBitSetTraits.GetArrSize(env));
        res.AsSpan().Fill(nint.MaxValue);

        // Start with all ones, shift in zeros in the last elem.
        var lastElemBits = ((res.Length - 1) % (Unsafe.SizeOf<nint>() * 8)) + 1;
        res[^1] = nint.MaxValue >>> ((Unsafe.SizeOf<nint>() * 8) - lastElemBits);
        return res;
    }

    public static nint[] UninitVal() => [];

    public static bool MaybeUninit(ReadOnlySpan<nint> bs) => bs.Length == 0;

    public static void Assign(TEnv env, ref nint[] lhs, ReadOnlySpan<nint> rhs)
    {
        if (MaybeUninit(lhs))
        {
            assert(!MaybeUninit(rhs));
            lhs = MakeCopy(env, rhs);
        }
        else
        {
            rhs[..TBitSetTraits.GetArrSize(env)].CopyTo(lhs);
        }
    }

    public static void AssignAllowUninitRhs(TEnv env, ref nint[] lhs, ReadOnlySpan<nint> rhs)
    {
        if (MaybeUninit(rhs))
        {
            lhs = [];
        }
        else
        {
            Assign(env, ref lhs, rhs);
        }
    }

    public static void AssignNoCopy(TEnv env, ref nint[] lhs, nint[] rhs)
    {
        lhs = rhs;
    }

    public static void ClearD(TEnv env, Span<nint> bs)
        => bs[..TBitSetTraits.GetArrSize(env)].Clear();

    public static nint[] MakeSingleton(TEnv env, int bitNum)
    {
        assert((bitNum >= 0) && (bitNum < TBitSetTraits.GetSize(env)));

        var res = MakeEmptyArrayBits(env);
        (var elemIndex, var bitIndex) = int.DivRem(bitNum, Unsafe.SizeOf<nint>() * 8);

        res[elemIndex] = (nint)(1) << bitIndex;
        return res;
    }

    public static nint[] MakeCopy(TEnv env, ReadOnlySpan<nint> bs)
    {
        var res = MakeUninitArrayBits(env);
        bs[..TBitSetTraits.GetArrSize(env)].CopyTo(res);
        return res;
    }

    public static bool IsEmpty(TEnv env, ReadOnlySpan<nint> bs)
    {
        return !bs[..TBitSetTraits.GetArrSize(env)].ContainsAnyExcept(0);
    }

    public static nint Count(TEnv env, ReadOnlySpan<nint> bs)
    {
        bs = bs[..TBitSetTraits.GetArrSize(env)];

        var res = (nint)(0);

        for (var i = 0; i < bs.Length; i++)
        {
            res += nint.PopCount(bs[i]);
        }
        return res;
    }

    public static bool IsEmptyUnion(TEnv env, ReadOnlySpan<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        for (var i = 0; i < bs1.Length; i++)
        {
            if ((bs1[i] | bs2[i]) != 0)
            {
                return false;
            }
        }
        return true;
    }

    public static void UnionD(TEnv env, Span<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        for (var i = 0; i < bs1.Length; i++)
        {
            bs1[i] |= bs2[i];
        }
    }

    public static bool UnionDChanged(TEnv env, Span<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        var changed = false;

        for (var i = 0; i < bs1.Length; i++)
        {
            var bsCurrent = bs1[i];
            var bsNew = bsCurrent | bs2[i];

            changed |= (bsNew != bsCurrent);
            bs1[i] = bsNew;
        }
        return changed;
    }

    public static nint[] Union(TEnv env, ReadOnlySpan<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        var res = MakeCopy(env, bs1);
        UnionD(env, res, bs2);
        return res;
    }

    public static void DiffD(TEnv env, Span<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        for (var i = 0; i < bs1.Length; i++)
        {
            bs1[i] &= ~bs2[i];
        }
    }

    public static nint[] Diff(TEnv env, ReadOnlySpan<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        var res = MakeCopy(env, bs1);
        DiffD(env, res, bs2);
        return res;
    }

    public static void RemoveElemD(TEnv env, Span<nint> bs, int bitNum)
    {
        assert((bitNum >= 0) && (bitNum < TBitSetTraits.GetSize(env)));

        (var elemIndex, var bitIndex) = int.DivRem(bitNum, Unsafe.SizeOf<nint>() * 8);
        var mask = (nint)(1) << bitIndex;

        mask = ~mask;
        bs[elemIndex] &= mask;
    }

    public static nint[] RemoveElem(TEnv env, ReadOnlySpan<nint> bs, int i)
    {
        var res = MakeCopy(env, bs);
        RemoveElemD(env, res, i);
        return res;
    }

    public static void AddElemD(TEnv env, Span<nint> bs, int bitNum)
    {
        assert((bitNum >= 0) && (bitNum < TBitSetTraits.GetSize(env)));
        (var elemIndex, var bitIndex) = int.DivRem(bitNum, Unsafe.SizeOf<nint>() * 8);

        var mask = (nint)(1) << bitIndex;
        bs[elemIndex] |= mask;
    }
    public static nint[] AddElem(TEnv env, ReadOnlySpan<nint> bs, int i)
    {
        var res = MakeCopy(env, bs);
        AddElemD(env, res, i);
        return res;
    }

    public static bool TryAddElemD(TEnv env, Span<nint> bs, int bitNum)
    {
        assert((bitNum >= 0) && (bitNum < TBitSetTraits.GetSize(env)));
        (var elemIndex, var bitIndex) = int.DivRem(bitNum, Unsafe.SizeOf<nint>() * 8);

        var mask = (nint)(1) << bitIndex;
        var bits = bs[elemIndex];

        var added = (bits & mask) == 0;
        bs[elemIndex] = bits | mask;
        return added;
    }

    public static bool IsMember(TEnv env, ReadOnlySpan<nint> bs, int bitNum)
    {
        assert((bitNum >= 0) && (bitNum < TBitSetTraits.GetSize(env)));
        (var elemIndex, var bitIndex) = int.DivRem(bitNum, Unsafe.SizeOf<nint>() * 8);

        var mask = (nint)(1) << bitIndex;
        return (bs[elemIndex] & mask) != 0;
    }

    public static void IntersectionD(TEnv env, Span<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        for (var i = 0; i < bs1.Length; i++)
        {
            bs1[i] &= bs2[i];
        }
    }

    public static nint[] Intersection(TEnv env, ReadOnlySpan<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        var res = MakeCopy(env, bs1);
        IntersectionD(env, res, bs2);
        return res;
    }

    public static bool IsEmptyIntersection(TEnv env, ReadOnlySpan<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        for (var i = 0; i < bs1.Length; i++)
        {
            if ((bs1[i] & bs2[i]) != 0)
            {
                return false;
            }
        }
        return true;
    }

    public static void DataFlowD(TEnv env, Span<nint> output, ReadOnlySpan<nint> gen, ReadOnlySpan<nint> input)
    {
        output = output[..TBitSetTraits.GetArrSize(env)];
        gen = gen[..output.Length];
        input = input[..output.Length];

        for (var i = 0; i < output.Length; i++)
        {
            output[i] &= (gen[i] | input[i]);
        }
    }

    public static void LivenessD(TEnv env, Span<nint> output, ReadOnlySpan<nint> def, ReadOnlySpan<nint> use, ReadOnlySpan<nint> input)
    {
        output = output[..TBitSetTraits.GetArrSize(env)];
        def = def[..output.Length];
        use = use[..output.Length];
        input = input[..output.Length];

        for (var i = 0; i < output.Length; i++)
        {
            output[i] = use[i] | (input[i] & ~def[i]);
        }
    }

    public static bool IsSubset(TEnv env, ReadOnlySpan<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        for (var i = 0; i < bs1.Length; i++)
        {
            var bs1Val = bs1[i];

            if ((bs1Val & bs2[i]) != bs1Val)
            {
                return false;
            }
        }
        return true;
    }

    public static bool Equal(TEnv env, ReadOnlySpan<nint> bs1, ReadOnlySpan<nint> bs2)
    {
        bs1 = bs1[..TBitSetTraits.GetArrSize(env)];
        bs2 = bs2[..bs1.Length];

        return bs2.SequenceEqual(bs1);
    }

#if DEBUG
    // Returns a string valid until the allocator releases the memory.
    public static unsafe string ToString(TEnv env, ReadOnlySpan<nint> bs)
    {
        bs = bs[..TBitSetTraits.GetArrSize(env)];
        var stringBuilder = new StringBuilder(bs.Length * (sizeof(nint) * 2) + 4);

        for (var i = bs.Length; i > 0; i--)
        {
            var bits = bs[i - 1];

            if (sizeof(nint) == sizeof(long))
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

    public static nint[] MakeEmpty(TEnv env)
        => MakeEmptyArrayBits(env);

    public static nint[] MakeFull(TEnv env)
        => MakeFullArrayBits(env);
}
