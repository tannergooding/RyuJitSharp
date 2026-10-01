// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_WASM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LinearScanLifetimeSpillKillClosureTests
{
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_INT, true)]
    [TestCase(TYP_FLOAT, false)]
    [TestCase(TYP_FLOAT, true)]
    [TestCase(TYP_DOUBLE, false)]
    [TestCase(TYP_DOUBLE, true)]
    public static void CandidateBankPreservesIntervalType(var_types type, bool includeOtherBank)
    {
        var allocator = CreateAllocator();
        var mask = type == TYP_INT ? AvailableIntRegs(allocator) : AvailableFloatRegs(allocator);
        var interval = new Interval(type, mask);
        var reference = new RefPosition(0, 1, null, RefType.RefTypeUse);
        reference.setInterval(interval);
        reference.registerAssignment = includeOtherBank
            ? AvailableIntRegs(allocator) | AvailableFloatRegs(allocator)
            : mask;

        Assert.That(GetRegisterType(allocator, interval, reference), Is.EqualTo(type));
    }

#if TARGET_LOONGARCH64 || TARGET_RISCV64
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void FloatingArgumentsCanUseIntegerCandidates(var_types type)
    {
        var allocator = CreateAllocator();
        var interval = new Interval(type, AvailableFloatRegs(allocator));
        var reference = new RefPosition(0, 1, null, RefType.RefTypeUse);
        reference.setInterval(interval);
        reference.registerAssignment = AvailableIntRegs(allocator);

        Assert.That(GetRegisterType(allocator, interval, reference), Is.EqualTo(TYP_I_IMPL));
    }
#endif

    [TestCase(7, 7, TYP_INT, TYP_UINT, true)]
    [TestCase(-7, -7, TYP_INT, TYP_INT, true)]
    [TestCase(0, 0, TYP_INT, TYP_REF, true)]
    [TestCase(7, 7, TYP_INT, TYP_REF, false)]
    [TestCase(-7, -7, TYP_INT, TYP_REF, false)]
    [TestCase(7, 8, TYP_INT, TYP_INT, false)]
#if TARGET_64BIT
    [TestCase(-7, -7, TYP_INT, TYP_UINT, false)]
#else
    [TestCase(-7, -7, TYP_INT, TYP_UINT, true)]
#endif
    public static unsafe void IntegerConstantMatchingPreservesTargetWidthAndGcRules(
        int previousValue, int value, var_types previousType, var_types type, bool expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#endif
        var allocator = CreateAllocator();
        var mask = AvailableIntRegs(allocator);
        var previous = new Interval(TYP_INT, mask)
        {
            isConstant = true,
            firstRefPosition = new RefPosition(0, 1,
                new GenTreeIntCon(previousType, previousValue), RefType.RefTypeDef),
        };
        var register = new RegRecord { regNum = REG_INT_FIRST, assignedInterval = previous };
        RegistersWithConstants(allocator) = new regMaskTP(mask);

        var interval = new Interval(TYP_INT, mask) { isConstant = true };
        var reference = new RefPosition(0, 2,
            new GenTreeIntCon(type, value), RefType.RefTypeDef);
        reference.setInterval(interval);
        reference.registerAssignment = mask;

        Assert.That(IsMatchingConstant(allocator, register, reference), Is.EqualTo(expected));
    }

    private static LinearScan CreateAllocator()
    {
        var allocator = (LinearScan)RuntimeHelpers.GetUninitializedObject(typeof(LinearScan));
        AvailableIntRegs(allocator) = genSingleTypeRegMask(REG_INT_FIRST);
        AvailableFloatRegs(allocator) = genSingleTypeRegMask(REG_FP_FIRST);
        AvailableDoubleRegs(allocator) = AvailableFloatRegs(allocator);
        return allocator;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getRegisterType")]
    private static extern var_types GetRegisterType(LinearScan allocator, Interval interval, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "isMatchingConstant")]
    private static extern bool IsMatchingConstant(LinearScan allocator, RegRecord register, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableDoubleRegs")]
    private static extern ref regMask AvailableDoubleRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registersWithConstants")]
    private static extern ref regMaskTP RegistersWithConstants(LinearScan allocator);
}
#endif
