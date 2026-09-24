// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanBinaryUsesTests
{
    [Test]
    public static void UnaryOperandUsesDoNotRequireTheBinaryNodeSubclass()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            ReferenceBuildLocation(allocator) = 1;
            var definition = BuildDef(allocator, value, SRBM_NONE, 0);
            var byteSwap = new GenTreeUnOp(GT_BSWAP, TYP_INT, value);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBinaryUses(allocator, byteSwap, SRBM_NONE), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(TargetPreferredUse(allocator), Is.Null);
        });
    }

    [Test]
    public static void CommutativeRmwUsesPreferenceBothUncontainedOperands()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 11);
            var right = compiler.gtNewIconNode(TYP_INT, 13);
            ReferenceBuildLocation(allocator) = 1;
            var leftDefinition = BuildDef(allocator, left, SRBM_NONE, 0);
            var rightDefinition = BuildDef(allocator, right, SRBM_NONE, 0);
            var add = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBinaryUses(allocator, add, SRBM_NONE), Is.EqualTo(2));
            Assert.That(leftDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(leftDefinition.nextRefPosition));
            Assert.That(TargetPreferredUse2(allocator), Is.SameAs(rightDefinition.nextRefPosition));
            Assert.That(leftDefinition.nextRefPosition?.delayRegFree, Is.False);
            Assert.That(rightDefinition.nextRefPosition?.delayRegFree, Is.False);
        });
    }

    [Test]
    public static void NonCommutativeRmwDelaysTheSecondOperandUntilTheOperationCompletes()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 17);
            var right = compiler.gtNewIconNode(TYP_INT, 5);
            ReferenceBuildLocation(allocator) = 1;
            var leftDefinition = BuildDef(allocator, left, SRBM_NONE, 0);
            var rightDefinition = BuildDef(allocator, right, SRBM_NONE, 0);
            var subtract = compiler.gtNewBinaryNode(GT_SUB, TYP_INT, left, right);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBinaryUses(allocator, subtract, SRBM_NONE), Is.EqualTo(2));
            var leftUse = leftDefinition.nextRefPosition
                ?? throw new AssertionException("The first subtraction use was not created.");
            var rightUse = rightDefinition.nextRefPosition
                ?? throw new AssertionException("The second subtraction use was not created.");
            Assert.That(leftUse.delayRegFree, Is.False);
            Assert.That(rightUse.delayRegFree, Is.True);
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(leftUse));
            Assert.That(PendingDelayFree(allocator), Is.True);
        });
    }

    [Test]
    public static void CommutativeRmwDelaysContainedMemoryAddressUntilTheRegisterOperandIsReady()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var value = compiler.gtNewIconNode(TYP_INT, 37);
            ReferenceBuildLocation(allocator) = 1;
            var addressDefinition = BuildDef(allocator, address, SRBM_NONE, 0);
            var valueDefinition = BuildDef(allocator, value, SRBM_NONE, 0);
            var memory = compiler.gtNewIndir(TYP_INT, address);
            memory.IsContained = true;
            var add = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, memory, value);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBinaryUses(allocator, add, SRBM_NONE), Is.EqualTo(2));
            var addressUse = addressDefinition.nextRefPosition
                ?? throw new AssertionException("The contained-memory address use was not created.");
            var valueUse = valueDefinition.nextRefPosition
                ?? throw new AssertionException("The register-value use was not created.");
            Assert.That(addressUse.delayRegFree, Is.True);
            Assert.That(valueUse.delayRegFree, Is.False);
            Assert.That(TargetPreferredUse2(allocator), Is.SameAs(valueUse));
        });
    }

    [Test]
    public static void MultiplyByContainedImmediateDoesNotUseRmwRegisterPreferences()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            var immediate = compiler.gtNewIconNode(TYP_INT, 7);
            immediate.IsContained = true;
            ReferenceBuildLocation(allocator) = 1;
            var definition = BuildDef(allocator, value, SRBM_NONE, 0);
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_INT, value, immediate);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBinaryUses(allocator, multiply, SRBM_NONE), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(TargetPreferredUse(allocator), Is.Null);
            Assert.That(TargetPreferredUse2(allocator), Is.Null);
        });
    }

    [Test]
    public static void CompareUsesOperandsWithoutRmwPreferences()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 29);
            var right = compiler.gtNewIconNode(TYP_INT, 31);
            ReferenceBuildLocation(allocator) = 1;
            var leftDefinition = BuildDef(allocator, left, SRBM_NONE, 0);
            var rightDefinition = BuildDef(allocator, right, SRBM_NONE, 0);
            var compare = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, left, right);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBinaryUses(allocator, compare, SRBM_NONE), Is.EqualTo(2));
            Assert.That(leftDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(TargetPreferredUse(allocator), Is.Null);
            Assert.That(TargetPreferredUse2(allocator), Is.Null);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildBinaryUses")]
    private static extern int BuildBinaryUses(LinearScan allocator, GenTreeUnOp node, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pendingDelayFree")]
    private static extern ref bool PendingDelayFree(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse")]
    private static extern ref RefPosition? TargetPreferredUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse2")]
    private static extern ref RefPosition? TargetPreferredUse2(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        try
        {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
