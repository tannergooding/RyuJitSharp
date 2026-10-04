// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
#if DEBUG
using System.Globalization;
#endif

namespace RyuJitSharp;

public static class BitSetUInt64Ops<TEnv, TBitSetTraits>
    where TEnv : class
    where TBitSetTraits : IBitSetTraits<TEnv>
{
    // Native ValArgType and RetValType are both UINT64, represented here by ulong parameters and returns.

    public static void Assign(TEnv env, ref ulong lhs, ulong rhs)
    {
        lhs = rhs;
    }

    public static void AssignNouninit(TEnv env, ref ulong lhs, ulong rhs)
    {
        lhs = rhs;
    }

    public static void AssignAllowUninitRhs(TEnv env, ref ulong lhs, ulong rhs)
    {
        lhs = rhs;
    }

    public static void AssignNoCopy(TEnv env, ref ulong lhs, ulong rhs)
    {
        lhs = rhs;
    }

    public static void ClearD(TEnv env, ref ulong bitSet)
    {
        bitSet = 0;
    }

    public static ulong MakeSingleton(TEnv env, uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        return Singleton(bitNum);
    }

    public static ulong MakeCopy(TEnv env, ulong bitSet)
    {
        return bitSet;
    }

    public static bool IsEmpty(TEnv env, ulong bitSet)
    {
        return bitSet == 0;
    }

    public static uint Count(TEnv env, ulong bitSet)
    {
        return (uint)BitOperations.PopCount(bitSet);
    }

    public static bool IsEmptyUnion(TEnv env, ulong bitSet1, ulong bitSet2)
    {
        return (bitSet1 | bitSet2) == 0;
    }

    public static void UnionD(TEnv env, ref ulong bitSet1, ulong bitSet2)
    {
        bitSet1 |= bitSet2;
    }

    public static ulong Union(TEnv env, ref ulong bitSet1, ulong bitSet2)
    {
        return bitSet1 | bitSet2;
    }

    public static void DiffD(TEnv env, ref ulong bitSet1, ulong bitSet2)
    {
        bitSet1 &= ~bitSet2;
    }

    public static ulong Diff(TEnv env, ulong bitSet1, ulong bitSet2)
    {
        return bitSet1 & ~bitSet2;
    }

    public static void RemoveElemD(TEnv env, ref ulong bitSet, uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        bitSet &= ~Singleton(bitNum);
    }

    public static ulong RemoveElem(TEnv env, ulong bitSet, uint bitNum)
    {
        return bitSet & ~Singleton(bitNum);
    }

    public static void AddElemD(TEnv env, ref ulong bitSet, uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        bitSet |= Singleton(bitNum);
    }

    public static ulong AddElem(TEnv env, ulong bitSet, uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        return bitSet | Singleton(bitNum);
    }

    public static bool IsMember(TEnv env, ulong bitSet, uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        return (bitSet & Singleton(bitNum)) != 0;
    }

    public static void IntersectionD(TEnv env, ref ulong bitSet1, ulong bitSet2)
    {
        bitSet1 &= bitSet2;
    }

    public static ulong Intersection(TEnv env, ulong bitSet1, ulong bitSet2)
    {
        return bitSet1 & bitSet2;
    }

    public static bool IsEmptyIntersection(TEnv env, ulong bitSet1, ulong bitSet2)
    {
        return (bitSet1 & bitSet2) == 0;
    }

    public static void LivenessD(TEnv env, ref ulong input, ulong def, ulong use, ulong output)
    {
        input = use | (output & ~def);
    }

    public static bool IsSubset(TEnv env, ulong bitSet1, ulong bitSet2)
    {
        return (bitSet1 & bitSet2) == bitSet1;
    }

    public static bool Equal(TEnv env, ulong bitSet1, ulong bitSet2)
    {
        return bitSet1 == bitSet2;
    }

    public static ulong MakeEmpty(TEnv env)
    {
        return 0;
    }

    public static ulong MakeFull(TEnv env)
    {
        var size = unchecked((uint)TBitSetTraits.GetSize(env));

        if (size == 64)
        {
            return ulong.MaxValue;
        }
        else
        {
            return (1UL << (int)size) - 1;
        }
    }

#if DEBUG
    public static string ToString(TEnv env, ulong bitSet)
    {
        return Format(bitSet);
    }

    internal static string Format(ulong bitSet)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{unchecked((uint)bitSet):X8}{unchecked((uint)(bitSet >> 32)):X8}");
    }
#endif

    public static ulong UninitVal()
    {
        return 0;
    }

    public static bool MayBeUninit(ulong bitSet)
    {
        return bitSet == UninitVal();
    }

    public struct Iter
    {
        private ulong _bits;
        private uint _bitNum;

        public Iter(TEnv env, ulong bitSet)
        {
            _bits = bitSet;
            _bitNum = 0;
        }

        public bool NextElem(ref uint element)
        {
            if (_bits != 0)
            {
                var bitNum = _bitNum;
                while ((_bits & 1) == 0)
                {
                    bitNum++;
                    _bits >>= 1;
                }

                element = bitNum;
                _bitNum = bitNum + 1;
                _bits >>= 1;
                return true;
            }

            return false;
        }
    }

    private static ulong Singleton(uint bitNum)
    {
        assert(bitNum < sizeof(ulong) * BitSetSupport.BitsInByte);
        return 1UL << (int)bitNum;
    }
}

public struct BitSetUint64<TEnv, TBitSetTraits>
    where TEnv : class
    where TBitSetTraits : IBitSetTraits<TEnv>
{
    private ulong _bits;
#if DEBUG
    private uint _epoch;
#endif

    public BitSetUint64()
    {
        _bits = 0;
#if DEBUG
        _epoch = 0;
#endif
    }

    public BitSetUint64(TEnv env, bool full = false)
    {
        _bits = full ? BitSetUInt64Ops<TEnv, TBitSetTraits>.MakeFull(env) : 0;
#if DEBUG
        _epoch = GetEpoch(env);
#endif
    }

    public BitSetUint64(TEnv env, uint bitNum)
    {
        _bits = BitSetUInt64Ops<TEnv, TBitSetTraits>.MakeSingleton(env, bitNum);
#if DEBUG
        _epoch = GetEpoch(env);
#endif
    }

    internal ulong Bits
    {
        readonly get
        {
            return _bits;
        }
        set
        {
            _bits = value;
        }
    }

#if DEBUG
    // Default struct storage is zero; offset epochs so it represents the native UINT32_MAX uninitialized marker.
    private static uint GetEpoch(TEnv env) => unchecked((uint)TBitSetTraits.GetEpoch(env) + 1);
#endif

    internal readonly void CheckEpoch(TEnv env)
    {
#if DEBUG
        assert(_epoch == GetEpoch(env));
#endif
    }

#if DEBUG
    internal readonly bool HasCurrentEpoch(TEnv env) => _epoch == GetEpoch(env);

    public override readonly string ToString()
    {
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.Format(_bits);
    }
#endif

    internal readonly bool IsEqualTo(in BitSetUint64<TEnv, TBitSetTraits> other)
    {
        return (_bits == other._bits)
#if DEBUG
            && (_epoch == other._epoch)
#endif
            ;
    }

    internal void Assign(in BitSetUint64<TEnv, TBitSetTraits> other)
    {
        _bits = other._bits;
#if DEBUG
        _epoch = other._epoch;
#endif
    }
}

public readonly struct BitSetUint64ValueRetType<TEnv, TBitSetTraits>
    where TEnv : class
    where TBitSetTraits : IBitSetTraits<TEnv>
{
    private readonly BitSetUint64<TEnv, TBitSetTraits> _bitSet;

    public BitSetUint64ValueRetType(BitSetUint64<TEnv, TBitSetTraits> bitSet)
    {
        _bitSet = bitSet;
    }

    public static implicit operator BitSetUint64<TEnv, TBitSetTraits>(
        BitSetUint64ValueRetType<TEnv, TBitSetTraits> value) => value._bitSet;

    public static implicit operator BitSetUint64ValueRetType<TEnv, TBitSetTraits>(
        BitSetUint64<TEnv, TBitSetTraits> bitSet) => new(bitSet);
}

public static class BitSetUint64Ops<TEnv, TBitSetTraits>
    where TEnv : class
    where TBitSetTraits : IBitSetTraits<TEnv>
{
    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> UninitVal()
    {
        return default;
    }

    public static bool MayBeUninit(BitSetUint64<TEnv, TBitSetTraits> bitSet)
    {
        var uninitialized = (BitSetUint64<TEnv, TBitSetTraits>)UninitVal();
        return bitSet.IsEqualTo(in uninitialized);
    }

    public static void Assign(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> lhs,
        BitSetUint64<TEnv, TBitSetTraits> rhs)
    {
        lhs.Assign(in rhs);
    }

    public static void AssignNouninit(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> lhs,
        BitSetUint64<TEnv, TBitSetTraits> rhs)
    {
        lhs.Assign(in rhs);
    }

    public static void AssignAllowUninitRhs(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> lhs,
        BitSetUint64<TEnv, TBitSetTraits> rhs)
    {
        lhs.Assign(in rhs);
    }

    public static void AssignNoCopy(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> lhs,
        BitSetUint64<TEnv, TBitSetTraits> rhs)
    {
        lhs.Assign(in rhs);
    }

    public static void ClearD(TEnv env, ref BitSetUint64<TEnv, TBitSetTraits> bitSet)
    {
#if DEBUG
        assert(bitSet.HasCurrentEpoch(env));
#endif
        var bits = bitSet.Bits;
        BitSetUInt64Ops<TEnv, TBitSetTraits>.ClearD(env, ref bits);
        bitSet.Bits = bits;
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> MakeSingleton(TEnv env, uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        return new BitSetUint64<TEnv, TBitSetTraits>(env, bitNum);
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> MakeCopy(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet)
    {
        return bitSet;
    }

    public static bool IsEmpty(TEnv env, BitSetUint64<TEnv, TBitSetTraits> bitSet)
    {
        bitSet.CheckEpoch(env);
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.IsEmpty(env, bitSet.Bits);
    }

    public static uint Count(TEnv env, BitSetUint64<TEnv, TBitSetTraits> bitSet)
    {
        bitSet.CheckEpoch(env);
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.Count(env, bitSet.Bits);
    }

    public static bool IsEmptyUnion(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.IsEmptyUnion(env, bitSet1.Bits, bitSet2.Bits);
    }

    public static void UnionD(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        var bits = bitSet1.Bits;
        BitSetUInt64Ops<TEnv, TBitSetTraits>.UnionD(env, ref bits, bitSet2.Bits);
        bitSet1.Bits = bits;
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> Union(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        var bits = bitSet1.Bits;
        bitSet1.Bits = BitSetUInt64Ops<TEnv, TBitSetTraits>.Union(env, ref bits, bitSet2.Bits);
        return bitSet1;
    }

    public static void DiffD(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        var bits = bitSet1.Bits;
        BitSetUInt64Ops<TEnv, TBitSetTraits>.DiffD(env, ref bits, bitSet2.Bits);
        bitSet1.Bits = bits;
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> Diff(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        bitSet1.Bits = BitSetUInt64Ops<TEnv, TBitSetTraits>.Diff(env, bitSet1.Bits, bitSet2.Bits);
        return bitSet1;
    }

    public static void RemoveElemD(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> bitSet,
        uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        bitSet.CheckEpoch(env);
        var bits = bitSet.Bits;
        BitSetUInt64Ops<TEnv, TBitSetTraits>.RemoveElemD(env, ref bits, bitNum);
        bitSet.Bits = bits;
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> RemoveElem(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet,
        uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        bitSet.CheckEpoch(env);
        bitSet.Bits = BitSetUInt64Ops<TEnv, TBitSetTraits>.RemoveElem(env, bitSet.Bits, bitNum);
        return bitSet;
    }

    public static void AddElemD(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> bitSet,
        uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        bitSet.CheckEpoch(env);
        var bits = bitSet.Bits;
        BitSetUInt64Ops<TEnv, TBitSetTraits>.AddElemD(env, ref bits, bitNum);
        bitSet.Bits = bits;
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> AddElem(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet,
        uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        bitSet.CheckEpoch(env);
        bitSet.Bits = BitSetUInt64Ops<TEnv, TBitSetTraits>.AddElem(env, bitSet.Bits, bitNum);
        return bitSet;
    }

    public static bool IsMember(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet,
        uint bitNum)
    {
        assert(bitNum < unchecked((uint)TBitSetTraits.GetSize(env)));
        bitSet.CheckEpoch(env);
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.IsMember(env, bitSet.Bits, bitNum);
    }

    public static void IntersectionD(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        var bits = bitSet1.Bits;
        BitSetUInt64Ops<TEnv, TBitSetTraits>.IntersectionD(env, ref bits, bitSet2.Bits);
        bitSet1.Bits = bits;
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> Intersection(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        bitSet1.Bits = BitSetUInt64Ops<TEnv, TBitSetTraits>.Intersection(env, bitSet1.Bits, bitSet2.Bits);
        return bitSet1;
    }

    public static bool IsEmptyIntersection(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.IsEmptyIntersection(env, bitSet1.Bits, bitSet2.Bits);
    }

    public static void LivenessD(
        TEnv env,
        ref BitSetUint64<TEnv, TBitSetTraits> input,
        BitSetUint64<TEnv, TBitSetTraits> def,
        BitSetUint64<TEnv, TBitSetTraits> use,
        BitSetUint64<TEnv, TBitSetTraits> output)
    {
        input.CheckEpoch(env);
        def.CheckEpoch(env);
        use.CheckEpoch(env);
        output.CheckEpoch(env);
        var inputBits = input.Bits;
        BitSetUInt64Ops<TEnv, TBitSetTraits>.LivenessD(
            env,
            ref inputBits,
            def.Bits,
            use.Bits,
            output.Bits);
        input.Bits = inputBits;
    }

    public static bool IsSubset(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.IsSubset(env, bitSet1.Bits, bitSet2.Bits);
    }

    public static bool Equal(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        bitSet1.CheckEpoch(env);
        bitSet2.CheckEpoch(env);
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.Equal(env, bitSet1.Bits, bitSet2.Bits);
    }

    public static bool NotEqual(
        TEnv env,
        BitSetUint64<TEnv, TBitSetTraits> bitSet1,
        BitSetUint64<TEnv, TBitSetTraits> bitSet2)
    {
        return !Equal(env, bitSet1, bitSet2);
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> MakeEmpty(TEnv env)
    {
        return new BitSetUint64<TEnv, TBitSetTraits>(env);
    }

    public static BitSetUint64ValueRetType<TEnv, TBitSetTraits> MakeFull(TEnv env)
    {
        return new BitSetUint64<TEnv, TBitSetTraits>(env, full: true);
    }

#if DEBUG
    public static string ToString(TEnv env, BitSetUint64<TEnv, TBitSetTraits> bitSet)
    {
        return BitSetUInt64Ops<TEnv, TBitSetTraits>.ToString(env, bitSet.Bits);
    }
#endif

    public struct Iter
    {
        private ulong _bits;
        private uint _bitNum;

        public Iter(TEnv env, BitSetUint64<TEnv, TBitSetTraits> bitSet)
        {
            _bits = bitSet.Bits;
            _bitNum = 0;
        }

        public bool NextElem(ref uint element)
        {
            if ((_bits & 1) != 0)
            {
                element = _bitNum;
                _bitNum++;
                _bits >>= 1;
                return true;
            }

            // Skip groups of four zero bits before scanning the next set bit.
            while ((_bitNum < 64) && ((_bits & 0xF) == 0))
            {
                _bitNum += 4;
                _bits >>= 4;
            }

            while ((_bitNum < 64) && ((_bits & 1) == 0))
            {
                _bitNum++;
                _bits >>= 1;
            }

            if (_bitNum < 64)
            {
                element = _bitNum;
                _bitNum++;
                _bits >>= 1;
                return true;
            }

            return false;
        }
    }
}
