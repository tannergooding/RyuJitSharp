// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanEntryDefinitionsTests
{
    [TestCase(TYP_INT, false, false)]
    [TestCase(TYP_INT, true, true)]
    [TestCase(TYP_REF, false, true)]
    [TestCase(TYP_REF, true, true)]
    [TestCase(TYP_BYREF, false, true)]
    public static void EntryLocalsReceiveZeroDefinitionsOrStackHomes(var_types type, bool initMemory, bool initialize)
    {
        WithEntry(type, true, false, (compiler, allocator, interval) => {
            compiler.info.compInitMem = initMemory;
            InsertZeroDefinitions(allocator);

            Assert.That(compiler.lvaTable[0].lvMustInit, Is.EqualTo(initialize));
            Assert.That(interval.isSpilled, Is.EqualTo(!initialize));
            Assert.That(allocator.refPositions, Has.Count.EqualTo(initialize ? 1 : 0));
            if (initialize)
            {
                AssertEntryDefinition(allocator.refPositions[0], RefType.RefTypeZeroInit);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FinallyReferencesReceiveOnlyOneEntryDefinition(bool liveIn)
    {
        WithEntry(TYP_REF, liveIn, true, (compiler, allocator, interval) => {
            InsertZeroDefinitions(allocator);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.True);
            Assert.That(allocator.refPositions, Has.Count.EqualTo(1));
            AssertEntryDefinition(allocator.refPositions[0], RefType.RefTypeZeroInit);
            Assert.That(interval.recentRefPosition, Is.SameAs(allocator.refPositions[0]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ParameterLocalsAreNotZeroInitialized(bool registerTarget)
    {
        WithEntry(TYP_REF, true, true, (compiler, allocator, _) => {
            compiler.lvaTable[0].lvIsParam = !registerTarget;
            compiler.lvaTable[0].lvIsParamRegTarget = registerTarget;
            InsertZeroDefinitions(allocator);
            Assert.That(allocator.refPositions, Is.Empty);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OsrLocalHasEntryDefinitionWithoutPrologInitialization(bool finallyLive)
    {
        WithEntry(TYP_REF, true, finallyLive, (compiler, allocator, _) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.info.compLocalsCount = 1;
            compiler.lvaTable[0].lvIsOSRLocal = true;
            InsertZeroDefinitions(allocator);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.False);
            Assert.That(allocator.refPositions, Has.Count.EqualTo(1));
            AssertEntryDefinition(allocator.refPositions[0], RefType.RefTypeZeroInit);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FinallyOnlyIntegerNeedsInitializationOnlyWithInitMemory(bool initMemory)
    {
        WithEntry(TYP_INT, false, true, (compiler, allocator, interval) => {
            compiler.info.compInitMem = initMemory;
            InsertZeroDefinitions(allocator);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.EqualTo(initMemory));
            Assert.That(allocator.refPositions, Has.Count.EqualTo(initMemory ? 1 : 0));
            Assert.That(interval.isSpilled, Is.False);
        });
    }

    [TestCase(TYP_INT, REG_NA)]
    [TestCase(TYP_INT, REG_RCX)]
    [TestCase(TYP_DOUBLE, REG_XMM0)]
    public static void ParameterDefinitionPrefersIncomingRegister(var_types type, regNumber register)
    {
        WithEntry(type, false, false, (compiler, allocator, interval) => {
            var codeGen = compiler.codeGen ?? throw new AssertionException("Codegen state is missing.");
            codeGen.RegSet.rsClearRegsModified();
            BuildPhysicalRegisters(allocator);
            BuildParameterDefinition(allocator, in compiler.lvaTable[0], register);
            Assert.That(allocator.refPositions, Has.Count.EqualTo(1));
            var position = allocator.refPositions[0];
            AssertEntryDefinition(position, RefType.RefTypeParamDef);
            if (register != REG_NA)
            {
                Assert.That(interval.physReg, Is.EqualTo(register));
                Assert.That(interval.isActive, Is.True);
                Assert.That(position.registerAssignment, Is.EqualTo(LsraGlobals.genSingleTypeRegMask(register)));
            }
            else
            {
                Assert.That(interval.assignedReg, Is.Null);
                Assert.That(position.isFixedRegRef, Is.False);
            }
        });
    }

    private static void AssertEntryDefinition(RefPosition position, RefType type)
    {
        Assert.That(position.refType, Is.EqualTo(type));
        Assert.That(position.nodeLocation, Is.Zero);
        Assert.That(position.bbNum, Is.Zero);
        Assert.That(position.regOptional, Is.True);
        Assert.That(position.treeNode, Is.Null);
    }

    private static void WithEntry(var_types type, bool liveIn, bool finallyLive,
        Action<Compiler, LinearScan, Interval> action)
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            compiler.lvaTable[0].Type = type;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            block.bbLiveIn = SetOps.MakeEmpty(compiler);
            Identify(allocator);
            CurrentLiveVariables(allocator) = SetOps.MakeEmpty(compiler);
            FinallyVariables(allocator) = SetOps.MakeEmpty(compiler);
            if (liveIn)
            {
                SetOps.AddElemD(compiler, block.bbLiveIn, 0);
                SetOps.AddElemD(compiler, CurrentLiveVariables(allocator), 0);
            }
            if (finallyLive)
            {
                compiler.lvaEnregEHVars = true;
                SetOps.AddElemD(compiler, FinallyVariables(allocator), 0);
            }

            var interval = allocator.localVarIntervals?[0]
                ?? throw new AssertionException("Entry local interval is missing.");
            action(compiler, allocator, interval);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "identifyCandidatesWithLocals")]
    private static extern void Identify(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysicalRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildInitialParamDef")]
    private static extern void BuildParameterDefinition(LinearScan allocator, in LclVarDsc local, regNumber register);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insertZeroInitRefPositions")]
    private static extern void InsertZeroDefinitions(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentLiveVariables")]
    private static extern ref nint[] CurrentLiveVariables(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_finallyVars")]
    private static extern ref nint[] FinallyVariables(LinearScan allocator);
}
