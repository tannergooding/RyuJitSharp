// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class JitStdContainerTests
{
    [Test]
    public static void VectorUsesScopedContiguousStorageAndInvalidatesItsDataOnReallocation()
    {
        using var scope = new JitStdAllocationScope();
        var allocator = new JitStdAllocator<int>(scope);
        using var vector = new JitStdVector<int>(allocator);

        vector.push_back(10);
        vector.push_back(20);
        var initialIterator = vector.begin();
        Assert.That(vector.end() - vector.begin(), Is.EqualTo((nint)2));
        Assert.That(vector.data()[1], Is.EqualTo(20));

        vector.reserve(16);

        _ = Assert.Throws<InvalidOperationException>(() => _ = initialIterator.value);
        Assert.That(vector.data()[0], Is.EqualTo(10));
        Assert.That(vector.data()[1], Is.EqualTo(20));
        Assert.That(scope.allocationCount, Is.EqualTo((nuint)3));
    }

    [Test]
    public static void VectorGrowthCapacityUsesTwiceTheCurrentSize()
    {
        using var scope = new JitStdAllocationScope();
        using var vector = new JitStdVector<int>([10, 20], new JitStdAllocator<int>(scope));

        vector.reserve(16);
        Assert.That(vector.capacity(), Is.EqualTo((nuint)16));

        vector.reserve(17);
        Assert.That(vector.capacity(), Is.EqualTo((nuint)17));
    }

    [Test]
    public static void VectorMaxSizeMatchesTheNativeUnsignedLimit()
    {
        using var scope = new JitStdAllocationScope();
        using var vector = new JitStdVector<int>(new JitStdAllocator<int>(scope));

        Assert.That(vector.max_size(), Is.EqualTo(nuint.MaxValue >> 1));
    }

    [Test]
    public static void VectorInsertEraseAndReverseIteratorsPreserveElementOrder()
    {
        using var scope = new JitStdAllocationScope();
        using var vector = new JitStdVector<int>(new JitStdAllocator<int>(scope));
        vector.assign([1, 4]);
        _ = vector.insert(vector.begin() + 1, [2, 3]);

        Assert.That(vector.size(), Is.EqualTo((nuint)4));
        Assert.That(vector[0], Is.EqualTo(1));
        Assert.That(vector[1], Is.EqualTo(2));
        Assert.That(vector[2], Is.EqualTo(3));
        Assert.That(vector[3], Is.EqualTo(4));

        var reverse = vector.rbegin();
        Assert.That(reverse.value, Is.EqualTo(4));
        reverse = ++reverse;
        Assert.That(reverse.value, Is.EqualTo(3));

        _ = vector.erase(vector.begin() + 1, vector.begin() + 3);
        Assert.That(vector.size(), Is.EqualTo((nuint)2));
        Assert.That(vector[0], Is.EqualTo(1));
        Assert.That(vector[1], Is.EqualTo(4));
    }

    [Test]
    public static void VectorCountAndIteratorRangeOperationsPreserveSequence()
    {
        using var scope = new JitStdAllocationScope();
        using var vector = new JitStdVector<int>([1, 4], new JitStdAllocator<int>(scope));
        _ = vector.insert(vector.begin() + 1, 2, 2);

        Assert.That(vector.front(), Is.EqualTo(1));
        Assert.That(vector.back(), Is.EqualTo(4));
        Assert.That(vector.at(2), Is.EqualTo(2));
        Assert.That(vector.cend() - vector.cbegin(), Is.EqualTo((nint)4));

        using var assigned = new JitStdVector<int>(new JitStdAllocator<int>(scope));
        assigned.assign(vector.begin() + 1, vector.end() - 1);
        Assert.That(assigned.size(), Is.EqualTo((nuint)2));
        Assert.That(assigned[0], Is.EqualTo(2));
        Assert.That(assigned[1], Is.EqualTo(2));

        using var inserted = new JitStdVector<int>(new JitStdAllocator<int>(scope));
        _ = inserted.insert(inserted.begin(), vector.begin() + 1, vector.end() - 1);
        Assert.That(inserted.size(), Is.EqualTo((nuint)2));
        Assert.That(inserted[0], Is.EqualTo(2));
        Assert.That(inserted[1], Is.EqualTo(2));
    }

    [Test]
    public static void VectorCopyConversionAssignmentAndMovePreserveValues()
    {
        using var scope = new JitStdAllocationScope();
        using var source = new JitStdVector<long>([11, 12], new JitStdAllocator<long>(scope));
        using var converted = JitStdVector<int>.copyFrom(source, static value => checked((int)value));
        using var assigned = new JitStdVector<int>(new JitStdAllocator<int>(scope));
        assigned.assign(source, static value => checked((int)value));

        Assert.That(converted[0], Is.EqualTo(11));
        Assert.That(converted[1], Is.EqualTo(12));
        Assert.That(assigned[0], Is.EqualTo(11));
        Assert.That(assigned[1], Is.EqualTo(12));

        using var moved = new JitStdVector<int>(new JitStdAllocator<int>(scope));
        moved.moveFrom(assigned);
        Assert.That(assigned.empty(), Is.True);
        Assert.That(moved.size(), Is.EqualTo((nuint)2));
        Assert.That(moved[1], Is.EqualTo(12));
    }

    [Test]
    public static void ListPreservesNodeAddressAcrossInsertionAndSplice()
    {
        using var scope = new JitStdAllocationScope();
        var allocator = new JitStdAllocator<int>(scope);
        using var list = new JitStdList<int>(allocator);
        using var other = new JitStdList<int>(allocator);

        list.push_back(1);
        list.push_back(3);
        var stable = list.begin();
        var stableNode = list.nodeIdentity(stable);
        other.push_back(2);

        _ = list.insert(++stable, 2);
        Assert.That(list.nodeIdentity(list.begin()), Is.SameAs(stableNode));
        list.splice(list.end(), other);

        Assert.That(other.empty(), Is.False);
        Assert.That(list.size(), Is.EqualTo((nuint)3));
        Assert.That(list.front(), Is.EqualTo(1));
        Assert.That(list.back(), Is.EqualTo(3));
        Assert.That(list.nodeIdentity(list.begin()), Is.SameAs(stableNode));

        var reverse = list.rbegin();
        Assert.That(reverse.value, Is.EqualTo(3));
        reverse = ++reverse;
        Assert.That(reverse.value, Is.EqualTo(2));
    }

    [Test]
    public static void WholeListSpliceIntoAnEmptyListPreservesNodeStorage()
    {
        using var scope = new JitStdAllocationScope();
        var allocator = new JitStdAllocator<int>(scope);
        using var list = new JitStdList<int>([3, 1, 2], allocator);
        using var other = new JitStdList<int>(allocator);
        var firstNode = list.begin();
        var firstNodeIdentity = list.nodeIdentity(firstNode);

        other.splice(other.begin(), list);
        Assert.That(list.empty(), Is.True);
        Assert.That(other.size(), Is.EqualTo((nuint)3));
        Assert.That(other.front(), Is.EqualTo(3));
        Assert.That(other.nodeIdentity(other.begin()), Is.SameAs(firstNodeIdentity));
    }

    [Test]
    public static void ListMaxSizeMatchesTheNativeNodeSizeLimit()
    {
        using var scope = new JitStdAllocationScope();
        using var list = new JitStdList<int>(new JitStdAllocator<int>(scope));

        Assert.That(list.max_size(), Is.EqualTo((nuint.MaxValue >> 1) / (nuint)Unsafe.SizeOf<IntListNodeLayout>()));
    }

    [Test]
    public static void ListIteratorRangeInsertAndAssignAndSpliceOverloadsPreserveDefinedBehavior()
    {
        using var scope = new JitStdAllocationScope();
        var allocator = new JitStdAllocator<int>(scope);
        using var source = new JitStdList<int>([1, 4], allocator);
        using var list = new JitStdList<int>(allocator);
        _ = list.insert(list.end(), source.begin(), source.end());

        Assert.That(list.size(), Is.EqualTo((nuint)2));
        Assert.That(list.front(), Is.EqualTo(1));
        Assert.That(list.back(), Is.EqualTo(4));
        Assert.That(source.size(), Is.EqualTo((nuint)2));

        using var assigned = new JitStdList<int>([5], allocator);
        assigned.assign(source.begin(), source.end());
        Assert.That(assigned.size(), Is.EqualTo((nuint)2));
        Assert.That(assigned.front(), Is.EqualTo(1));
        Assert.That(assigned.back(), Is.EqualTo(4));
        assigned.assign(3, 9);
        Assert.That(assigned.size(), Is.EqualTo((nuint)3));
        Assert.That(assigned.back(), Is.EqualTo(9));

        using var other = new JitStdList<int>([5], allocator);
        var stableNode = list.nodeIdentity(list.begin());
        other.splice(other.end(), list, list.begin());
        other.splice(other.end(), list, list.begin(), list.end());

        Assert.That(list.size(), Is.EqualTo((nuint)2));
        Assert.That(other.size(), Is.EqualTo((nuint)1));
        Assert.That(list.nodeIdentity(list.begin()), Is.SameAs(stableNode));
    }

    [Test]
    public static void ListMergeMatchesNativeOrderingAndTailAliasing()
    {
        using var scope = new JitStdAllocationScope();
        var allocator = new JitStdAllocator<int>(scope);
        using var list = new JitStdList<int>([1, 4, 6], allocator);
        using var other = new JitStdList<int>([2, 3, 5, 7], allocator);

        list.merge(other);

        Assert.That(string.Join(",", list), Is.EqualTo("1,2,3,4,5,6,7"));
        Assert.That(string.Join(",", other), Is.EqualTo("2,3,5,7"));
        Assert.That(list.size(), Is.EqualTo((nuint)7));
        Assert.That(other.size(), Is.EqualTo((nuint)4));
        Assert.That(list.nodeIdentity(list.backPosition()), Is.SameAs(other.nodeIdentity(other.backPosition())));

        var iteratorValues = new List<int>();
        for (var iterator = list.begin(); iterator != list.end(); iterator++)
        {
            iteratorValues.Add(iterator.value);
        }
        Assert.That(string.Join(",", iteratorValues), Is.EqualTo("1,2,3,4,5,6,7"));

        using var customList = new JitStdList<int>([1, 4], allocator);
        using var customOther = new JitStdList<int>([2, 3], allocator);
        var comparer = Comparer<int>.Create((first, second) => second.CompareTo(first));

        customList.merge(customOther, comparer);

        Assert.That(string.Join(",", customList), Is.EqualTo("3,2,1,4"));
        Assert.That(string.Join(",", customOther), Is.EqualTo("2,3"));
    }

    [Test]
    public static void VectorRangeConstructionUsesPlacementConstruction()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        var allocator = new JitStdAllocator<TrackedValue>(scope, operations);

        var vector = new JitStdVector<TrackedValue>([new TrackedValue(7), new TrackedValue(8)], allocator);

        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("C:7,C:8"));
        vector.Dispose();
        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("C:7,C:8,D:7,D:8"));
    }

    [Test]
    public static void VectorCountAndRangeAssignmentOnlyAssignElements()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        var allocator = new JitStdAllocator<TrackedValue>(scope, operations);
        using var assigned = new JitStdVector<TrackedValue>([new TrackedValue(1), new TrackedValue(2)], allocator);
        using var source = new JitStdVector<TrackedValue>([new TrackedValue(3), new TrackedValue(4), new TrackedValue(5)], allocator);
        assigned.reserve(8);
        operations.Calls.Clear();

        assigned.assign(1, new TrackedValue(9));
        assigned.assign(source.begin(), source.end());

        Assert.That(assigned.size(), Is.EqualTo((nuint)3));
        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("A:1:9,A:9:3,A:2:4,A:0:5"));
    }

    [Test]
    public static void VectorReallocationCopyConstructsWithoutDestroyingOldElements()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        using var vector = new JitStdVector<TrackedValue>(
            [new TrackedValue(1), new TrackedValue(2)],
            new JitStdAllocator<TrackedValue>(scope, operations));
        operations.Calls.Clear();

        vector.reserve(8);

        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("C:1,C:2"));
    }

    [Test]
    public static void VectorValueAndCountInsertionConstructElementsAfterAssignmentShifts()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        using var vector = new JitStdVector<TrackedValue>(
            [new TrackedValue(1), new TrackedValue(3)],
            new JitStdAllocator<TrackedValue>(scope, operations));
        vector.reserve(8);
        operations.Calls.Clear();

        _ = vector.insert(vector.begin() + 1, 2, new TrackedValue(2));

        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("A:0:3,C:2,C:2"));
    }

    [Test]
    public static void VectorSingleValueInsertionPlacementConstructsTheInsertedElement()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        using var vector = new JitStdVector<TrackedValue>(
            [new TrackedValue(1), new TrackedValue(3)],
            new JitStdAllocator<TrackedValue>(scope, operations));
        vector.reserve(8);
        operations.Calls.Clear();

        _ = vector.insert(vector.begin() + 1, new TrackedValue(2));

        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("A:0:3,C:2"));
    }

    [Test]
    public static void VectorIteratorRangeInsertionAssignsInsertedElements()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        var allocator = new JitStdAllocator<TrackedValue>(scope, operations);
        using var vector = new JitStdVector<TrackedValue>([new TrackedValue(1), new TrackedValue(3)], allocator);
        using var source = new JitStdVector<TrackedValue>([new TrackedValue(2), new TrackedValue(4)], allocator);
        vector.reserve(8);
        operations.Calls.Clear();

        _ = vector.insert(vector.begin() + 1, source.begin(), source.end());

        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("A:0:3,A:3:2,A:0:4"));
    }

    [Test]
    public static void VectorEraseDestroysOverwrittenElementsButNotTheOldTail()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        using var vector = new JitStdVector<TrackedValue>(
            [new TrackedValue(1), new TrackedValue(2), new TrackedValue(3), new TrackedValue(4)],
            new JitStdAllocator<TrackedValue>(scope, operations));
        vector.reserve(8);
        operations.Calls.Clear();

        _ = vector.erase(vector.begin() + 1, vector.begin() + 2);

        Assert.That(vector.size(), Is.EqualTo((nuint)3));
        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("D:2,A:0:3,D:3,A:0:4"));
    }

    [Test]
    public static void VectorPopClearAndDisposeDestroyLiveElements()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        var vector = new JitStdVector<TrackedValue>(
            [new TrackedValue(1), new TrackedValue(2), new TrackedValue(3)],
            new JitStdAllocator<TrackedValue>(scope, operations));
        operations.Calls.Clear();

        vector.pop_back();
        vector.clear();
        vector.push_back(new TrackedValue(4));
        vector.Dispose();

        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("D:3,D:1,D:2,C:4,D:4"));
    }

    [Test]
    public static void ListAllocatorLifecycleHooksTrackEachLiveNode()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        var allocator = new JitStdAllocator<TrackedValue>(scope, operations);

        using (var list = new JitStdList<TrackedValue>(allocator))
        {
            list.emplace_back(() => new TrackedValue(1));
            list.emplace_front(() => new TrackedValue(2));
            list.pop_back();
        }

        Assert.That(operations.Constructed, Is.EqualTo(2));
        Assert.That(operations.Destroyed, Is.EqualTo(2));
    }

    [Test]
    public static void ListEraseAndRemoveSkipElementDestructionWhilePopClearAndDisposeRunIt()
    {
        using var scope = new JitStdAllocationScope();
        var operations = new TrackedOperations();
        var list = new JitStdList<TrackedValue>(
            [new TrackedValue(1), new TrackedValue(2), new TrackedValue(3)],
            new JitStdAllocator<TrackedValue>(scope, operations));

        _ = list.erase(list.begin());
        list.remove(new TrackedValue(2));
        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("C:1,C:2,C:3"));

        list.pop_back();
        list.push_back(new TrackedValue(4));
        list.clear();
        list.push_back(new TrackedValue(5));
        list.Dispose();

        Assert.That(string.Join(",", operations.Calls), Is.EqualTo("C:1,C:2,C:3,D:3,C:4,D:4,C:5,D:5"));
    }

#if !DEBUG
    [Test]
    public static void ListSortAndUniquePreserveTheirNativeReleaseNoOp()
    {
        using var scope = new JitStdAllocationScope();
        using var list = new JitStdList<int>([2, 1], new JitStdAllocator<int>(scope));

        list.sort();
        list.sort(Comparer<int>.Default);
        list.unique();
        list.unique(EqualityComparer<int>.Default);

        Assert.That(list.size(), Is.EqualTo((nuint)2));
        Assert.That(list.front(), Is.EqualTo(2));
        Assert.That(list.back(), Is.EqualTo(1));
    }
#endif

    [Test]
    public static void AllocationScopeInvalidatesItsAllocatorsWhenDisposed()
    {
        var scope = new JitStdAllocationScope();
        var allocator = new JitStdAllocator<int>(scope);
        scope.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => _ = allocator.allocate(1));
    }

    [Test]
    public static void VoidAllocatorRebindingRetainsTheAllocationScope()
    {
        using var scope = new JitStdAllocationScope();
        var typed = new JitStdAllocator<int>(scope);
        var erased = JitStdAllocatorVoid.from(typed);
        var rebound = erased.rebind<Box>();
        var converted = JitStdAllocator<Box>.from(typed);

        Assert.That(rebound.scope, Is.SameAs(scope));
        Assert.That(converted.scope, Is.SameAs(scope));
    }

    [Test]
    public static void IteratorTraitsDistinguishPointerAndIntegralCategories()
    {
        Assert.That(JitStdIteratorTraits<int>.Category(42), Is.EqualTo(JitStdIteratorCategory.NotAnIterator));
        Assert.That(JitStdIteratorTraits<JitStdVector<int>.Iterator>.Category(default), Is.EqualTo(JitStdIteratorCategory.RandomAccess));
        Assert.That(JitStdIteratorTraits<JitStdList<int>.Iterator>.Category(default), Is.EqualTo(JitStdIteratorCategory.Bidirectional));
        Assert.That(JitStdIteratorTraits<int>.PointerCategory, Is.EqualTo(JitStdIteratorCategory.RandomAccess));
        Assert.That(new JitStdGreater<int>().Invoke(4, 3), Is.True);
    }

    [Test]
    public static void FloatingPointEqualityUsesNativeOperatorsForNaN()
    {
        using var scope = new JitStdAllocationScope();
        using var list = new JitStdList<double>([double.NaN, double.NaN], new JitStdAllocator<double>(scope));

        list.remove(double.NaN);

        Assert.That(list.size(), Is.EqualTo((nuint)2));
    }

    [Test]
    public static void ContainersSupportManagedReferenceElementsWithoutLosingTheirIdentity()
    {
        using var scope = new JitStdAllocationScope();
        var first = new Box(1);
        var second = new Box(2);
        using var vector = new JitStdVector<Box>([first, second], new JitStdAllocator<Box>(scope));
        using var list = new JitStdList<Box>([first, second], new JitStdAllocator<Box>(scope));

        Assert.That(vector.data()[0], Is.SameAs(first));
        Assert.That(vector.data()[1], Is.SameAs(second));
        Assert.That(list.front(), Is.SameAs(first));
        Assert.That(list.nodeIdentity(list.begin()), Is.Not.Null);
    }

    private struct TrackedValue
    {
        internal int value;

        internal TrackedValue(int value)
        {
            this.value = value;
        }
    }

    private struct IntListNodeLayout
    {
        internal int value;
        internal nint next;
        internal nint previous;

        internal IntListNodeLayout(int value, nint next, nint previous)
        {
            this.value = value;
            this.next = next;
            this.previous = previous;
        }
    }

    private sealed class TrackedOperations : IJitStdElementOperations<TrackedValue>
    {
        internal List<string> Calls { get; } = [];

        internal int Constructed;
        internal int Assigned;
        internal int Destroyed;

        public void construct(ref TrackedValue destination, in TrackedValue value)
        {
            Calls.Add($"C:{value.value}");
            destination = value;
            Constructed++;
        }

        public void assign(ref TrackedValue destination, in TrackedValue value)
        {
            Calls.Add($"A:{destination.value}:{value.value}");
            destination = value;
            Assigned++;
        }

        public void destroy(ref TrackedValue element)
        {
            Calls.Add($"D:{element.value}");
            Destroyed++;
            element = default;
        }
    }

    private sealed class Box(int value)
    {
        internal int value { get; } = value;
    }
}
