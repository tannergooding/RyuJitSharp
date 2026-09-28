// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using TestBitSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp;

public static class BitSetSupport
{
    public static void TestSuite(Compiler compiler)
    {
        // The native storage specializations share the managed nint[] representation.
        var traits = new BitVecTraits(compiler, 64);
        var first = TestBitSetOps.MakeEmpty(traits);
        int[] firstBits = [0, 10, 44, 45];
        foreach (var bit in firstBits)
        {
            TestBitSetOps.AddElemD(traits, first, bit);
        }

        var count = 0;
        _ = TestBitSetOps.VisitBits(traits, first, bit =>
        {
            assert(bit == firstBits[count]);
            count++;
            return true;
        });
        assert(count == 4);

        assert(TestBitSetOps.Equal(traits, first, TestBitSetOps.Union(traits, first, first)));
        assert(TestBitSetOps.Equal(traits, first, TestBitSetOps.Intersection(traits, first, first)));
        assert(TestBitSetOps.IsSubset(traits, first, first));

        var second = TestBitSetOps.MakeEmpty(traits);
        int[] secondBits = [0, 10, 50, 51];
        foreach (var bit in secondBits)
        {
            TestBitSetOps.AddElemD(traits, second, bit);
        }

        int[] unionBits = [0, 10, 44, 45, 50, 51];
        var union = TestBitSetOps.Union(traits, first, second);
        count = 0;
        _ = TestBitSetOps.VisitBits(traits, union, bit =>
        {
            assert(bit == unionBits[count]);
            count++;
            return true;
        });
        assert(count == 6);

        count = 0;
        _ = TestBitSetOps.VisitBits(traits, union, bit =>
        {
            assert(bit == unionBits[count]);
            count++;
            return true;
        });
        assert(count == 6);

        int[] intersectionBits = [0, 10];
        var intersection = TestBitSetOps.Intersection(traits, first, second);
        count = 0;
        _ = TestBitSetOps.VisitBits(traits, intersection, bit =>
        {
            assert(bit == intersectionBits[count]);
            count++;
            return true;
        });
        assert(count == 2);
    }
}
#endif
