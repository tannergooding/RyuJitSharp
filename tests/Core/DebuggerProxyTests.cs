// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class DebuggerProxyTests
{
#if DEBUG
    private static readonly int[] s_expectedListItems = [10, 20];
    private static readonly int[] s_expectedVectorItems = [30, 40];
    private static readonly int[] s_expectedArrayItems = [1, 0, 3];
    private static readonly int[] s_expectedStackItems = [4, 5, 0, 0, 0, 10];
#if TARGET_XARCH
    private static readonly regNumber[] s_expectedRax = [regNumber.REG_RAX];
#endif

    [Test]
    public static void JitStdCollectionProxiesExposeElementsInOrder()
    {
        using var scope = new JitStdAllocationScope();
        var allocator = new JitStdAllocator<int>(scope);
        using var list = new JitStdList<int>([10, 20], allocator);
        using var vector = new JitStdVector<int>([30, 40], allocator);

        Assert.That(new JitStdListDebuggerProxy<int>(list).Items, Is.EqualTo(s_expectedListItems));
        Assert.That(new JitStdVectorDebuggerProxy<int>(vector).Items, Is.EqualTo(s_expectedVectorItems));
    }

    [Test]
    public static void ExpandArrayProxiesUseCapacityAndStackDepth()
    {
        var array = new JitExpandArray<int>();
        array.Set(0, 1);
        array.Set(2, 3);

        var stack = new JitExpandArrayStack<int>();
        _ = stack.Push(4);
        _ = stack.Push(5);
        stack.Set(5, 10);

        Assert.That(new JitExpandArrayDebuggerProxy<int>(array).Items, Is.EqualTo(s_expectedArrayItems));
        Assert.That(new JitExpandArrayStackDebuggerProxy<int>(stack).Items, Is.EqualTo(s_expectedStackItems));
    }

#if TARGET_XARCH
    [Test]
    public static void RefPositionProxyMapsTheRegisterAssignmentUsingItsReferentType()
    {
        var record = new RegRecord();
        record.init(regNumber.REG_RAX);

        var reference = new RefPosition(0, 0, null, RefType.RefTypeFixedReg);
        reference.setReg(record);

        Assert.That(new RefPositionDebuggerProxy(reference).Registers, Is.EqualTo(s_expectedRax));
    }
#endif
#endif
}
