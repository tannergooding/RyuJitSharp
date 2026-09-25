// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanNodeReferenceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedNodesDoNotBuildReferences(bool contained)
    {
        WithAllocator((compiler, allocator) => {
            var constant = compiler.gtNewIconNode(TYP_INT, 17);
            constant.IsContained = contained;
            BuildAt(allocator, constant, 4);

            Assert.That(allocator.refPositions.Count, Is.EqualTo(contained ? 0 : 1));
            if (!contained)
            {
                Assert.That(allocator.refPositions[0].nodeLocation, Is.EqualTo(5u));
                Assert.That(allocator.refPositions[0].refType, Is.EqualTo(RefType.RefTypeDef));
#if DEBUG
                Assert.That(allocator.refPositions[0].buildNode, Is.SameAs(constant));
#endif
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedDyingLocalIsRemovedOnlyWhenItIsAnAllocationCandidate(bool candidate)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [
                new() { Type = TYP_INT, lvTracked = true, lvLRACandidate = candidate, _varIndex = 0 },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            var localInterval = new Interval(TYP_INT, SRBM_ALLINT_INIT);
            allocator.localVarIntervals = [localInterval];
            localInterval.setLocalNumber(compiler, 0, allocator);
            LiveVariables(allocator) = [1];

            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.IsContained = true;
            local.Flags |= GTF_VAR_DEATH;
            BuildAt(allocator, local, 4);

            Assert.That(LiveVariables(allocator)[0], Is.EqualTo(candidate ? (nint)0 : (nint)1));
            Assert.That(allocator.refPositions, Is.Empty);
        });
    }

    [Test]
    public static void NoncontainedNodeConsumesItsSourcesAndDefinesItsResult()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 3);
            var right = compiler.gtNewIconNode(TYP_INT, 5);
            BuildAt(allocator, left, 1);
            var leftDefinition = allocator.refPositions[^1];
            BuildAt(allocator, right, 3);
            var rightDefinition = allocator.refPositions[^1];
            var add = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            BuildAt(allocator, add, 5);

            Assert.That(leftDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(add));
            Assert.That(allocator.refPositions[^1].nodeLocation, Is.EqualTo(6u));
#if DEBUG
            Assert.That(leftDefinition.nextRefPosition?.buildNode, Is.SameAs(add));
            Assert.That(rightDefinition.nextRefPosition?.buildNode, Is.Null);
#endif
        });
    }

#if DEBUG
    [Test]
    public static void DebugCountsContainedSourceRegistersTransitively()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var memory = compiler.gtNewIndir(TYP_INT, address);
            memory.IsContained = true;
            var other = compiler.gtNewIconNode(TYP_INT, 3);
            var add = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, memory, other);

            Assert.That(ComputeOperandDstCount(allocator, memory), Is.EqualTo(1));
            Assert.That(ComputeAvailableSrcCount(allocator, add), Is.EqualTo(2));
            other.IsUnusedValue = true;
            Assert.That(ComputeOperandDstCount(allocator, other), Is.Zero);
            Assert.That(ComputeAvailableSrcCount(allocator, add), Is.EqualTo(1));
        });
    }

    [TestCase(0)]
    [TestCase(0x03)]
    [TestCase(0x08)]
    public static void DebugStressCountsAndReverseCallerCalleePreferences(int stressMask)
    {
        WithAllocator((compiler, allocator) => {
            StressMask(allocator) = stressMask;
            var input = compiler.gtNewIconNode(TYP_INT, 7);
            BuildAt(allocator, input, 1);
            var inputDefinition = allocator.refPositions[^1];
            var negate = new GenTreeUnOp(GT_NEG, TYP_INT, input);
            BuildAt(allocator, negate, 3);

            var inputUse = inputDefinition.nextRefPosition;
            var resultDefinition = allocator.refPositions[^1];
            Assert.That(inputUse?.minRegCandidateCount, Is.EqualTo(stressMask != 0 ? 2u : 1u));
            Assert.That(resultDefinition.minRegCandidateCount, Is.EqualTo(stressMask != 0 ? 2u : 1u));
            if (stressMask == 0x08)
            {
                Assert.That(inputUse?.registerAssignment & ~SRBM_INT_CALLEE_SAVED, Is.EqualTo(SRBM_NONE));
                Assert.That(resultDefinition.registerAssignment & ~SRBM_INT_CALLEE_SAVED, Is.EqualTo(SRBM_NONE));
            }
        });
    }

    [TestCase(false, 2u)]
    [TestCase(true, 3u)]
    public static void StressMinimumIncludesSpecialPutArgSource(bool specialPutArg, uint expectedMinimum)
    {
        WithAllocator((compiler, allocator) => {
            var input = compiler.gtNewIconNode(TYP_INT, 7);
            BuildAt(allocator, input, 1);
            var inputDefinition = allocator.refPositions[^1];
            inputDefinition.getInterval().isSpecialPutArg = specialPutArg;
            StressMask(allocator) = 0x08;
            var negate = new GenTreeUnOp(GT_NEG, TYP_INT, input);
            BuildAt(allocator, negate, 3);

            Assert.That(inputDefinition.nextRefPosition?.minRegCandidateCount, Is.EqualTo(expectedMinimum));
            Assert.That(allocator.refPositions[^1].minRegCandidateCount, Is.EqualTo(expectedMinimum));
        });
    }

    [Test]
    public static void DelayFreeStressCountsTheConsumingNodesKillRegisters()
    {
        WithAllocator((compiler, allocator) => {
            StressMask(allocator) = 0x08;
            var dividend = compiler.gtNewIconNode(TYP_INT, 101);
            var divisor = compiler.gtNewIconNode(TYP_INT, 7);
            BuildAt(allocator, dividend, 1);
            BuildAt(allocator, divisor, 3);
            var divisorDefinition = allocator.refPositions[^1];
            var division = compiler.gtNewBinaryNode(GT_DIV, TYP_INT, dividend, divisor);
            BuildAt(allocator, division, 5);

            var divisorUse = divisorDefinition.nextRefPosition;
            Assert.That(divisorUse?.delayRegFree, Is.True);
            Assert.That(divisorUse?.minRegCandidateCount, Is.EqualTo(5u));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildRefPositionsForNode")]
    private static extern void BuildRefPositionsForNode(LinearScan allocator, GenTree tree, uint currentLocation);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "computeOperandDstCount")]
    private static extern int ComputeOperandDstCount(LinearScan allocator, GenTree operand);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "computeAvailableSrcCount")]
    private static extern int ComputeAvailableSrcCount(LinearScan allocator, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentLiveVariables")]
    private static extern ref nint[] LiveVariables(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void BuildAt(LinearScan allocator, GenTree tree, uint location)
    {
        ReferenceBuildLocation(allocator) = location;
        BuildRefPositionsForNode(allocator, tree, location);
    }

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
        codeGen.RegSet.rsClearRegsModified();
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
