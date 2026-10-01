// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
#if TARGET_ARM || (DEBUG && TARGET_ARM64)
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using NUnit.Framework;
using SingleTypeRegSet = RyuJitSharp.regMask;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRegisterSelectionTests
{
    [Test]
    public static void MinimalSelectionChoosesLowestRegisterOrderAfterBusyFiltering()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);

            allocator.physRegs[(int)regNumber.REG_RAX].regOrder = 1;
            allocator.physRegs[(int)regNumber.REG_RBX].regOrder = 0;
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(selected, Is.EqualTo(SRBM_RBX));
        });
    }

    [Test]
    public static void OptionalReferenceReturnsNoRegisterWhenEveryCandidateIsBusy()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            definition.setRegOptional(true);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX | SRBM_RBX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(selected, Is.EqualTo(SRBM_NONE));
            Assert.That(interval.assignedReg, Is.Null);
        });
    }

    [Test]
    public static void ConflictingDefinitionInheritsFixedUseRegisterAfterPreviousLastUse()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var priorInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(priorInterval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var priorLastUse = allocator.newRefPosition(priorInterval, 9, RefType.RefTypeUse, tree, SRBM_RAX);
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = priorInterval;

            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RBX);
            _ = allocator.newRefPosition(interval, 12, RefType.RefTypeUse, tree, SRBM_RAX);
            UpdateNextFixedReference(allocator, regNumber.REG_RAX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(priorLastUse.lastUse, Is.True);
            Assert.That(interval.hasConflictingDefUse, Is.True);
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(definition.isFixedRegRef, Is.True);
            Assert.That(selected, Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void BusyFixedUseWidensConflictingDefinitionBeforeSelection()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var priorInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(priorInterval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var priorUse = allocator.newRefPosition(priorInterval, 11, RefType.RefTypeUse, tree, SRBM_RAX);
            priorInterval.isActive = true;
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = priorInterval;

            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RBX);
            _ = allocator.newRefPosition(interval, 12, RefType.RefTypeUse, tree, SRBM_RAX);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);
            UpdateNextFixedReference(allocator, regNumber.REG_RAX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(priorUse.nodeLocation, Is.GreaterThan(definition.nodeLocation));
            Assert.That(interval.hasConflictingDefUse, Is.True);
            Assert.That(definition.registerAssignment, Is.EqualTo(AllRegs(allocator, TYP_INT)));
            Assert.That(definition.isFixedRegRef, Is.False);
            Assert.That(selected, Is.Not.EqualTo(SRBM_RAX));
            Assert.That(definition.registerAssignment & selected, Is.EqualTo(selected));
        });
    }

#if TARGET_AMD64 && HAS_MORE_THAN_64_REGISTERS
    [Test]
    public static void MinimalMaskSelectionExcludesConflictingHighBankRegister()
    {
        WithCompiler(compiler => {
            CompilerAllMaskRegs(compiler) = SRBM_K1 | SRBM_K2;
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_MASK);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_K1 | SRBM_K2);
            _ = allocator.newRefPosition(regNumber.REG_K1, 10, RefType.RefTypeFixedReg, null, SRBM_K1);
            UpdateNextFixedReference(allocator, regNumber.REG_K1);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(selected, Is.EqualTo(SRBM_K2));
        });
    }
#endif

#if TARGET_ARM
    [TestCase(8.0, 1.0)]
    [TestCase(1.0, 8.0)]
    public static void DoubleSpillCostUsesTheMoreExpensiveHalf(double lowerCost, double upperCost)
    {
        WithCompiler(compiler =>
        {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_DOUBLE);
            foreach (var register in new[] { regNumber.REG_F0, regNumber.REG_F2 })
            {
                var assigned = NewInterval(allocator, TYP_FLOAT);
                assigned.recentRefPosition = allocator.newRefPosition(
                    assigned, 1, RefType.RefTypeUse, null, genSingleTypeRegMask(register));
                assigned.physReg = register;
                assigned.assignedReg = allocator.physRegs[(int)register];
                assigned.isActive = true;
                assigned.assignedReg.assignedInterval = assigned;
            }
            var reference = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, null, SRBM_F0 | SRBM_F2);
            var selector = GetSelector(allocator);
            InvokeSelector(selector, "reset", interval, reference);
            var costs = SpillCost(allocator);
            costs[(int)regNumber.REG_F0] = lowerCost;
            costs[(int)regNumber.REG_F1] = upperCost;
            costs[(int)regNumber.REG_F2] = 3;
            costs[(int)regNumber.REG_F3] = 2;

            InvokeSelector(selector, "try_SPILL_COST");

            Assert.That(GetSelectorField<SingleTypeRegSet>(selector, "_candidates"), Is.EqualTo(SRBM_F2));
            Assert.That(GetSelectorField<bool>(selector, "_found"), Is.True);
        });
    }

    [TestCase(0, 1, false, true)]
    [TestCase(1, 2, false, true)]
    [TestCase(2, 1, false, false)]
    [TestCase(0, 1, true, true)]
    public static void PreviousOptionalDoubleSelectionPreservesNativeHalfAndTieRules(
        int lowerState, int upperState, bool twoCandidates, bool expectedFound)
    {
        WithCompiler(compiler =>
        {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_DOUBLE);
            var candidates = twoCandidates ? SRBM_F0 | SRBM_F2 : SRBM_F0;
            foreach (var register in twoCandidates
                ? new[] { regNumber.REG_F0, regNumber.REG_F2 }
                : new[] { regNumber.REG_F0 })
            {
                SetPreviousHalf(register, lowerState);
                SetPreviousHalf(register + 1, upperState);
            }
            var reference = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, null, candidates);
            var selector = GetSelector(allocator);
            InvokeSelector(selector, "reset", interval, reference);

            InvokeSelector(selector, "try_PREV_REG_OPT");

            Assert.That(GetSelectorField<bool>(selector, "_found"), Is.EqualTo(expectedFound));
            Assert.That(GetSelectorField<SingleTypeRegSet>(selector, "_candidates"),
                Is.EqualTo(expectedFound && twoCandidates ? SRBM_F2 : candidates));

            void SetPreviousHalf(regNumber register, int state)
            {
                if (state == 0)
                {
                    return;
                }
                var assigned = NewInterval(allocator, TYP_FLOAT);
                var recent = allocator.newRefPosition(
                    assigned, 1, RefType.RefTypeUse, null, genSingleTypeRegMask(register));
                recent.reload = state == 1;
                recent.setRegOptional(state == 1);
                assigned.recentRefPosition = recent;
                assigned.physReg = register;
                assigned.assignedReg = allocator.physRegs[(int)register];
                assigned.assignedReg.assignedInterval = assigned;
            }
        });
    }

    [TestCase("calculateUnassignedSets")]
    [TestCase("calculateCoversSets")]
    public static void DoubleUnassignedClassificationUsesNativeFirstHalfNextReference(string method)
    {
        WithCompiler(compiler =>
        {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_DOUBLE);
            var reference = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, null, SRBM_F0);
            var selector = GetSelector(allocator);
            InvokeSelector(selector, "reset", interval, reference);
            SetSelectorField(selector, "_freeCandidates", SRBM_F0);
            SetSelectorField(selector, "_lastLocation", 30u);
            SetSelectorField(selector, "_found", true);
            NextIntervalRef(allocator)[(int)regNumber.REG_F0] = LsraGlobals.MaxLocation;
            NextIntervalRef(allocator)[(int)regNumber.REG_F1] = 20;

            InvokeSelector(selector, method);

            Assert.That(GetSelectorField<SingleTypeRegSet>(selector, "_unassignedSet"), Is.EqualTo(SRBM_F0));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_spillCost")]
    private static extern ref double[] SpillCost(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nextIntervalRef")]
    private static extern ref uint[] NextIntervalRef(LinearScan allocator);
#endif

#if DEBUG && TARGET_ARM64
    [TestCase(false)]
    [TestCase(true)]
    public static void MinimalOrderingRetainsFullHeuristicsForConsecutiveMethods(bool needsConsecutive)
    {
        WithCompiler(compiler =>
        {
            compiler.info.compNeedsConsecutiveRegisters = needsConsecutive;
            var selector = GetSelector(CreateAllocator(compiler));
            var order = GetSelectorField<Array>(selector, "_regSelectionOrder");
            var actual = new int[order.Length];
            for (var index = 0; index < order.Length; index++)
            {
                actual[index] = Convert.ToInt32(order.GetValue(index));
            }
            var expected = needsConsecutive
                ? new[] { 0x10000, 0x08000, 0x04000, 0x02000, 0x01000, 0x00800,
                    0x00400, 0x00200, 0x00100, 0x00080, 0x00040, 0x00020,
                    0x00010, 0x00008, 0x00004, 0x00002, 0x00001 }
                : new[] { 0x10, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ConsecutivePreviousOptionalHeuristicAcceptsAnUnassignedCandidate()
    {
        WithCompiler(compiler =>
        {
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_FLOAT);
            var reference = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, null, SRBM_V0);
            reference.needsConsecutive = true;
            var selector = GetSelector(allocator);
            InvokeSelector(selector, "reset", interval, reference);

            InvokeSelector(selector, "try_PREV_REG_OPT");

            Assert.That(GetSelectorField<bool>(selector, "_found"), Is.False);
            Assert.That(GetSelectorField<SingleTypeRegSet>(selector, "_candidates"), Is.EqualTo(SRBM_V0));
        });
    }
#endif

#if TARGET_ARM || (DEBUG && TARGET_ARM64)
    private static object GetSelector(LinearScan allocator)
    {
        var field = typeof(LinearScan).GetField("_regSelector", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing register selector.");
        return field.GetValue(allocator) ?? throw new AssertionException("Missing register selector instance.");
    }

    private static T GetSelectorField<T>(object selector, string name)
    {
        var field = selector.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException($"Missing selector field {name}.");
        return field.GetValue(selector) is T value
            ? value : throw new AssertionException($"Unexpected selector field type for {name}.");
    }

#if TARGET_ARM
    private static void SetSelectorField<T>(object selector, string name, T value)
    {
        var field = selector.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException($"Missing selector field {name}.");
        field.SetValue(selector, value);
    }
#endif

    private static void InvokeSelector(object selector, string name, params object[] arguments)
    {
        var method = selector.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException($"Missing selector method {name}.");
        _ = method.Invoke(selector, arguments);
    }
#endif

    private static LinearScan CreateAllocator(Compiler compiler)
    {
        var allocator = new LinearScan(compiler);
        BlockInfo(allocator) = [new LsraBlockInfo()];
        for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
        {
            var register = allocator.physRegs[index];
            register.init((regNumber)index);
            register.regOrder = (byte)index;
        }

        return allocator;
    }

    private static Interval NewInterval(LinearScan allocator, var_types type) => NewIntervalCore(allocator, type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewIntervalCore(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allRegs")]
    private static extern SingleTypeRegSet AllRegs(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "updateNextFixedRef")]
    private static extern void UpdateNextFixedRef(
        LinearScan allocator, RegRecord regRecord, RefPosition? nextRefPosition, RefPosition? nextKill);

    private static void UpdateNextFixedReference(LinearScan allocator, regNumber register)
    {
        var regRecord = allocator.physRegs[(int)register];
        var nextRefPosition = regRecord.lastRefPosition
            ?? throw new InvalidOperationException("The fixed register must have a linked reference.");
        UpdateNextFixedRef(allocator, regRecord, nextRefPosition, null);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
}
