// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.SpecialCodeKind;

namespace RyuJitSharp.UnitTests;

internal static class AddCodeDscMapTests
{
    [Test]
    public static void KeyHashAndEqualityMatchNativeEncodingAcrossRegionKinds()
    {
        var tryDescriptor = CreateDescriptor(SCK_RNGCHK_FAIL, Compiler.AcdKeyDesignator.KD_TRY, 3);
        var handlerDescriptor = CreateDescriptor(SCK_RNGCHK_FAIL, Compiler.AcdKeyDesignator.KD_HND, 3);
        var filterDescriptor = CreateDescriptor(SCK_RNGCHK_FAIL, Compiler.AcdKeyDesignator.KD_FLT, 3);
        var tryKey = new Compiler.AddCodeDscKey(tryDescriptor);
        var handlerKey = new Compiler.AddCodeDscKey(handlerDescriptor);
        var filterKey = new Compiler.AddCodeDscKey(filterDescriptor);

        Assert.That(tryKey.Data, Is.EqualTo(3));
        Assert.That(handlerKey.Data, Is.EqualTo(0x4000_0003));
        Assert.That(filterKey.Data, Is.EqualTo(unchecked((int)0x8000_0003u)));
        Assert.That(unchecked((uint)tryKey.GetHashCode()), Is.EqualTo(25u));
        Assert.That(unchecked((uint)handlerKey.GetHashCode()), Is.EqualTo(25u));
        Assert.That(unchecked((uint)filterKey.GetHashCode()), Is.EqualTo(25u));
        Assert.That(tryKey, Is.Not.EqualTo(handlerKey));
        Assert.That(handlerKey, Is.Not.EqualTo(filterKey));
        Assert.That(filterKey, Is.Not.EqualTo(tryKey));

        var map = new AddCodeDscMap();
        map[tryKey] = tryDescriptor;
        map[handlerKey] = handlerDescriptor;
        map[filterKey] = filterDescriptor;

        AssertMapOrder(map,
            [Compiler.AcdKeyDesignator.KD_FLT, Compiler.AcdKeyDesignator.KD_HND, Compiler.AcdKeyDesignator.KD_TRY],
            [filterKey.Data, handlerKey.Data, tryKey.Data]);

        Assert.That(map.Remove(handlerKey), Is.True);
        AssertMapOrder(map,
            [Compiler.AcdKeyDesignator.KD_FLT, Compiler.AcdKeyDesignator.KD_TRY],
            [filterKey.Data, tryKey.Data]);

        map[handlerKey] = handlerDescriptor;
        AssertMapOrder(map,
            [Compiler.AcdKeyDesignator.KD_HND, Compiler.AcdKeyDesignator.KD_FLT, Compiler.AcdKeyDesignator.KD_TRY],
            [handlerKey.Data, filterKey.Data, tryKey.Data]);
    }

    [Test]
    public static void MapIterationMatchesNativeOrderAcrossGrowthAndMutation()
    {
        // 1 and 10 collide in the initial 9 buckets; 1 and 24 collide after growth to 23.
        ushort[] insertionOrder = [1, 10, 2, 3, 4, 5, 6, 24, 7, 8, 9, 11, 12, 13, 14, 15, 16];
        var map = new AddCodeDscMap();
        var descriptors = new System.Collections.Generic.Dictionary<ushort, Compiler.AddCodeDsc>();

        foreach (var data in insertionOrder)
        {
            var descriptor = CreateDescriptor(SCK_RNGCHK_FAIL, Compiler.AcdKeyDesignator.KD_TRY, data);
            descriptors.Add(data, descriptor);
            map[new Compiler.AddCodeDscKey(descriptor)] = descriptor;

            if (descriptors.Count is 6)
            {
                AssertMapOrder(map, [10, 1, 5, 4, 3, 2]);
            }
        }

        AssertMapOrder(map, [3, 6, 9, 12, 15, 24, 1, 4, 7, 10, 13, 16, 2, 5, 8, 11, 14]);

        var key = new Compiler.AddCodeDscKey(descriptors[1]);
        Assert.That(map.Remove(key), Is.True);
        AssertMapOrder(map, [3, 6, 9, 12, 15, 24, 4, 7, 10, 13, 16, 2, 5, 8, 11, 14]);

        map[key] = descriptors[1];
        AssertMapOrder(map, [3, 6, 9, 12, 15, 1, 24, 4, 7, 10, 13, 16, 2, 5, 8, 11, 14]);
    }

    private static Compiler.AddCodeDsc CreateDescriptor(
        SpecialCodeKind kind, Compiler.AcdKeyDesignator designator, ushort data)
    {
        return new Compiler.AddCodeDsc
        {
            acdKind = kind,
            acdTryIndex = designator is Compiler.AcdKeyDesignator.KD_TRY ? data : (ushort)0,
            acdHndIndex = designator is Compiler.AcdKeyDesignator.KD_HND or Compiler.AcdKeyDesignator.KD_FLT
                ? data : (ushort)0,
            acdKeyDsg = designator,
        };
    }

    private static void AssertMapOrder(AddCodeDscMap map, ushort[] expected)
    {
        Assert.That(map.Count, Is.EqualTo(expected.Length));
        Assert.That(map.Values.Select(value => value.acdTryIndex), Is.EqualTo(expected));
        Assert.That(map.Keys.Select(key => key.Data), Is.EqualTo(expected.Select(value => (int)value)));
    }

    private static void AssertMapOrder(
        AddCodeDscMap map, Compiler.AcdKeyDesignator[] expectedDesignators, int[] expectedData)
    {
        Assert.That(map.Count, Is.EqualTo(expectedData.Length));
        Assert.That(map.Values.Select(value => value.acdKeyDsg), Is.EqualTo(expectedDesignators));
        Assert.That(map.Keys.Select(key => key.Data), Is.EqualTo(expectedData));
    }
}
