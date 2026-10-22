// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class DebuggerProxyTests
{
    private static readonly int[] s_expectedListItems = [10, 20];
    private static readonly int[] s_expectedVectorItems = [30, 40];
    private static readonly int[] s_expectedArrayItems = [1, 0, 3];
    private static readonly int[] s_expectedStackItems = [4, 5, 0, 0, 0, 10];
    private static readonly regNumber[] s_expectedRax = [regNumber.REG_X64_RAX];

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
    public static void CompilerMethodDebuggerDisplayIsAvailableInAllConfigurations()
    {
        AssertDebuggerDisplay<Compiler>(
            Configuration.DEBUG
                ? "[{info.compFullName} ({DebuggerMethodHash})]"
                : "[{info.compFullName}]");

        var property = typeof(Compiler).GetProperty(
            "DebuggerMethodHash",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var info = new Compiler.Info { compFullName = "DebuggerProxyTests.Method" };

        if (TestConfiguration.IsDebug)
        {
            const int methodHash = unchecked((int)0x89ABCDEF);
            object boxedInfo = info;
            typeof(Compiler.Info)
                .GetField("compMethodHashPrivate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(boxedInfo, methodHash);
            info = (Compiler.Info)boxedInfo;
            compiler.info = info;

            var formattedHash = (string)property!.GetValue(compiler)!;
            Assert.That(formattedHash, Is.EqualTo("89abcdef"));
            Assert.That(
                $"[{compiler.info.compFullName} ({formattedHash})]",
                Is.EqualTo("[DebuggerProxyTests.Method (89abcdef)]"));
        }
        else
        {
            var exception = Assert.Throws<TargetInvocationException>(() => property!.GetValue(compiler));
            Assert.That(exception!.InnerException!, Is.TypeOf<InvalidOperationException>());
        }
    }

    [Test]
    public static void DebuggerDisplayEntrypointsRemainInertInRelease()
    {
        Assume.That(!TestConfiguration.IsDebug);
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        Assert.DoesNotThrow(() => Globals.cBlocks(compiler));
        Assert.DoesNotThrow(Globals.dBlocks);
    }

    [Test]
    public static void DebuggerLookupEntrypointsFailExplicitlyInRelease()
    {
        Assume.That(!TestConfiguration.IsDebug);

        var exception = Assert.Throws<InvalidOperationException>(() => Globals.dFindTree(0));

        Assert.That(
            exception!.Message,
            Is.EqualTo(
                Configuration.DETAILED_FATAL_ERROR_MESSAGES
                    ? "Debugger lookups are only available in Debug builds."
                    : "RJS-1229"));
    }

    [Test]
    public static void DebuggerDisplayEntrypointsRemainAvailableInDebug()
    {
        Assume.That(TestConfiguration.IsDebug);

        var output = TestConfiguration.CaptureDebugOutput(() => Globals.dIsa(default));

        Assert.That(output, Does.Contain("dIsa "));
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
    public static void EmitterInstructionDescriptorProxyIsAvailableInAllConfigurations()
    {
        AssertDebuggerProxy<Emitter.instrDesc>(typeof(Emitter.instrDesc.DebuggerProxy));
    }

    [Test]
    public static void LinearScanDebuggerProxyIsAvailableInAllConfigurations()
    {
        AssertDebuggerMetadata<LinearScan>("LinearScan", typeof(LinearScan.DebuggerProxy));
    }

    [Test]
    public static void RegisterAllocationDebuggerDisplaysAreAvailableInAllConfigurations()
    {
        AssertDebuggerDisplay<Interval>("{DebuggerDisplayText,nq}");
        AssertDebuggerDisplay<RegRecord>("[reg={regNum}, type={registerType}]");
        AssertDebuggerProxy<RegRecord>(typeof(RegRecordDebuggerProxy));

        var interval = new Interval(TYP_INT, SRBM_NONE) { isLocalVar = true, varNum = 7 };
        if (TestConfiguration.IsDebug)
        {
            interval.intervalIndex = 2;
        }

        var expected = Configuration.DEBUG
            ? $"[V7, #2, reg={interval.physReg}]"
            : $"[V7, reg={interval.physReg}]";
        Assert.That(GetDebuggerDisplayText(interval, typeof(Interval)), Is.EqualTo(expected));

        if (!TestConfiguration.IsDebug)
        {
            _ = Assert.Throws<InvalidOperationException>(() => _ = interval.intervalIndex);
            _ = Assert.Throws<InvalidOperationException>(() => interval.intervalIndex = 99);
        }

        var constantInterval = new Interval(TYP_INT, SRBM_NONE) { isConstant = true };
        if (TestConfiguration.IsDebug)
        {
            constantInterval.intervalIndex = 4;
        }

        var expectedConstant = Configuration.DEBUG
            ? $"[C4, reg={constantInterval.physReg}]"
            : $"[C, reg={constantInterval.physReg}]";
        Assert.That(GetDebuggerDisplayText(constantInterval, typeof(Interval)), Is.EqualTo(expectedConstant));

        if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE)
        {
            var relatedInterval = new Interval(TYP_INT, SRBM_NONE) { varNum = 11 };
            var upperInterval = new Interval(TYP_INT, SRBM_NONE)
            {
                isUpperVector = true,
                varNum = 7,
                relatedInterval = relatedInterval,
            };

            if (TestConfiguration.IsDebug)
            {
                upperInterval.intervalIndex = 6;
            }

            var expectedUpper = Configuration.DEBUG
                ? $"[U11, #6, reg={upperInterval.physReg}]"
                : $"[U11, reg={upperInterval.physReg}]";
            Assert.That(GetDebuggerDisplayText(upperInterval, typeof(Interval)), Is.EqualTo(expectedUpper));
        }
    }

    [Test]
    public static void GenTreeDebuggerDisplayPreservesDebugIdsAndReleaseOmission()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        using var jitTls = TestConfiguration.CreateJitTlsWithCompiler(compiler);

        var tree = new GenTreeIntCon(TYP_INT, 42);
        var nextTree = new GenTreeIntCon(TYP_INT, 43);
        var copiedTree = new GenTreeIntCon(TYP_INT, 44, fields: null, source: tree);

        if (TestConfiguration.IsDebug)
        {
            Assert.That(tree.TreeId, Is.EqualTo(0));
            Assert.That(nextTree.TreeId, Is.EqualTo(1));
            Assert.That(copiedTree.TreeId, Is.EqualTo(tree.TreeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(2));
        }

        var prefix = Configuration.DEBUG ? "0: " : "";
        var valueNumbers = $"VNP=[L: {tree._vnPair.Liberal:x}, C: {tree._vnPair.Conservative:x}]";
        var expected = $"{prefix}[IntCon={tree.IconValue}], {valueNumbers}";
        Assert.That(GetDebuggerDisplayText(tree, typeof(GenTree)), Is.EqualTo(expected));

        var stringConstant = (GenTree)RuntimeHelpers.GetUninitializedObject(typeof(GenTree));
        typeof(GenTree)
            .GetField("_oper", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(stringConstant, GT_CNS_STR);
        var specialValueNumbers =
            $"VNP=[L: {stringConstant._vnPair.Liberal:x}, C: {stringConstant._vnPair.Conservative:x}]";
        Assert.That(
            GetDebuggerDisplayText(stringConstant, typeof(GenTree)),
            Is.EqualTo($"CNS_STR, {specialValueNumbers}"));

        if (Configuration.FEATURE_SIMD)
        {
            var vectorConstant = (GenTree)RuntimeHelpers.GetUninitializedObject(typeof(GenTree));
            typeof(GenTree)
                .GetField("_oper", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(vectorConstant, GT_CNS_VEC);
            var vectorValueNumbers =
                $"VNP=[L: {vectorConstant._vnPair.Liberal:x}, C: {vectorConstant._vnPair.Conservative:x}]";
            Assert.That(
                GetDebuggerDisplayText(vectorConstant, typeof(GenTree)),
                Is.EqualTo($"CNS_VEC, {vectorValueNumbers}"));
        }

        if (!TestConfiguration.IsDebug)
        {
            _ = Assert.Throws<InvalidOperationException>(() => _ = tree.TreeId);
        }
    }

    [Test]
    public static void LocalVariableDebuggerDisplayIncludesItsOwnedDebugReason()
    {
        var local = default(LclVarDsc);
        local.Type = TYP_INT;
        local.lvSlotNum = 9;

        if (TestConfiguration.IsDebug)
        {
            local.lvReason = "split temporary";

            var copied = local;
            var expected = $"[V9: {local.Type.Name}-split temporary]";
            Assert.That(GetDebuggerDisplayText(copied, typeof(LclVarDsc)), Is.EqualTo(expected));

            var locals = new[] { local };
            Array.Resize(ref locals, 2);
            Assert.That(GetDebuggerDisplayText(locals[0], typeof(LclVarDsc)), Is.EqualTo(expected));

            Array.Clear(locals);
            var clearedLocal = default(LclVarDsc);
            Assert.That(
                GetDebuggerDisplayText(locals[0], typeof(LclVarDsc)),
                Is.EqualTo($"[V{clearedLocal.lvSlotNum}: {clearedLocal.Type.Name}]"));
        }
        else
        {
            Assert.That(
                GetDebuggerDisplayText(local, typeof(LclVarDsc)),
                Is.EqualTo($"[V9: {local.Type.Name}]"));
            _ = Assert.Throws<InvalidOperationException>(() => _ = local.lvReason);
            _ = Assert.Throws<InvalidOperationException>(() => local.lvReason = "release misuse");
        }
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

        if (TestConfiguration.IsDebug)
        {
            Assert.That(new Limit(LimitType.Unknown).ToString(), Is.EqualTo("Unknown"));
            Assert.That(
                new Range(new Limit(LimitType.Unknown), new Limit(LimitType.Constant, 3)).ToString(),
                Is.EqualTo("<Unknown, 3>"));
        }
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

    private static string GetDebuggerDisplayText(
        object value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicProperties)] Type type)
    {
        var displayProperty = type.GetProperty(
            "DebuggerDisplayText",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (string)displayProperty!.GetValue(value)!;
    }

    [Test]
    public static void RefPositionProxyMapsTheRegisterAssignmentUsingItsReferentType()
    {
        Assume.That(Configuration.TARGET_XARCH, "This test requires an xarch target.");

        AssertDebuggerDisplay<RefPosition>(
            Configuration.DEBUG ? "[#{rpNum} - {refType}]" : "[{refType}]");
        AssertDebuggerProxy<RefPosition>(typeof(RefPositionDebuggerProxy));

        var record = new RegRecord();
        record.init(regNumber.REG_X64_RAX);

        var reference = new RefPosition(0, 0, null, RefType.RefTypeFixedReg);
        reference.setReg(record);

        Assert.That(new RefPositionDebuggerProxy(reference).Registers, Is.EqualTo(s_expectedRax));
    }
}
