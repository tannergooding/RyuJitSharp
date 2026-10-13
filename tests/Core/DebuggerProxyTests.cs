// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class DebuggerProxyTests
{
    private static readonly int[] s_expectedListItems = [10, 20];
    private static readonly int[] s_expectedVectorItems = [30, 40];
    private static readonly int[] s_expectedArrayItems = [1, 0, 3];
    private static readonly int[] s_expectedStackItems = [4, 5, 0, 0, 0, 10];
#if DEBUG && TARGET_XARCH
    private static readonly regNumber[] s_expectedRax = [regNumber.REG_X64_RAX];
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

    [Test]
    public static void CollectionDebuggerMetadataIsAvailableInAllConfigurations()
    {
        AssertDebuggerMetadata<JitExpandArray<int>>(
            "{DebuggerDisplayText,nq}",
            typeof(JitExpandArrayDebuggerProxy<>));
        AssertDebuggerMetadata<JitExpandArrayStack<int>>(
            "{DebuggerStackDisplayText,nq}",
            typeof(JitExpandArrayStackDebuggerProxy<>));
        AssertDebuggerMetadata<JitStdVector<int>>(
            "{DebuggerDisplayText,nq}",
            typeof(JitStdVectorDebuggerProxy<>));
        AssertDebuggerMetadata<JitStdList<int>>(
            "{DebuggerDisplayText,nq}",
            typeof(JitStdListDebuggerProxy<>));
    }

    [Test]
    public static void SimpleCompilerDebuggerDisplaysAreAvailableInAllConfigurations()
    {
        AssertDebuggerDisplay<EHblkDsc>("type={ebdHandlerType}");
        AssertDebuggerDisplay<Scev>("[{Oper}, {Type}]");
        AssertDebuggerDisplay<ValueNumPair>("VNP=[L: {Liberal,h}, C: {Conservative,h}]");
    }

    [Test]
    public static void StatementAndFlowGraphDebuggerMetadataIsAvailableInAllConfigurations()
    {
        AssertDebuggerProxy<Statement>(typeof(StatementDebuggerProxy));
        AssertDebuggerDisplay<FlowEdge>("{DebuggerDisplayText,nq}");
        AssertDebuggerMetadata<FlowGraphNaturalLoop>(
            "{DebuggerDisplayText,nq}",
            typeof(FlowGraphNaturalLoopDebuggerProxy));
        AssertDebuggerProxy<FlowGraphNaturalLoops>(typeof(FlowGraphNaturalLoopsDebuggerProxy));
    }

    [Test]
    public static void RangeDebuggerDisplaysPreserveTheToStringContract()
    {
        AssertDebuggerDisplay<Limit>("{DebuggerDisplayText,nq}");
        AssertDebuggerDisplay<Range>("{DebuggerDisplayText,nq}");

        const BindingFlags declaredInstanceMethod =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
        Assert.That(
            typeof(Limit).GetMethod(nameof(object.ToString), declaredInstanceMethod) is not null,
            Is.EqualTo(Configuration.DEBUG));
        Assert.That(
            typeof(Range).GetMethod(nameof(object.ToString), declaredInstanceMethod) is not null,
            Is.EqualTo(Configuration.DEBUG));

#if DEBUG
        Assert.That(new Limit(LimitType.Unknown).ToString(), Is.EqualTo("Unknown"));
        Assert.That(
            new Range(new Limit(LimitType.Unknown), new Limit(LimitType.Constant, 3)).ToString(),
            Is.EqualTo("<Unknown, 3>"));
#endif
    }

    private static void AssertDebuggerMetadata<T>(string displayValue, Type proxyType)
    {
        AssertDebuggerDisplay<T>(displayValue);
        AssertDebuggerProxy<T>(proxyType);
    }

    private static void AssertDebuggerProxy<T>(Type proxyType)
    {
        Assert.That(
            typeof(T).GetCustomAttribute<DebuggerTypeProxyAttribute>(inherit: false)?.ProxyTypeName,
            Does.Contain(proxyType.Name));
    }

    private static void AssertDebuggerDisplay<T>(string displayValue)
    {
        Assert.That(
            typeof(T).GetCustomAttribute<DebuggerDisplayAttribute>(inherit: false)?.Value,
            Is.EqualTo(displayValue));
    }

#if DEBUG && TARGET_XARCH
    [Test]
    public static void RefPositionProxyMapsTheRegisterAssignmentUsingItsReferentType()
    {
        var record = new RegRecord();
        record.init(regNumber.REG_X64_RAX);

        var reference = new RefPosition(0, 0, null, RefType.RefTypeFixedReg);
        reference.setReg(record);

        Assert.That(new RefPositionDebuggerProxy(reference).Registers, Is.EqualTo(s_expectedRax));
    }
#endif
}
