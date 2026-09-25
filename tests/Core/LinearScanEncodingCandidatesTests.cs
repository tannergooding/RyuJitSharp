// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanEncodingCandidatesTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ApxRestrictionsPreserveExplicitCandidatesAndEncodingSupport(bool apx, bool useApxRegs)
    {
        WithAllocator((compiler, allocator) => {
            ApxIsSupported(allocator) = apx;
            var integer = compiler.gtNewIconNode(TYP_INT, 1);
            var candidates = SRBM_RAX | SRBM_R16;
            var restricted = apx && !useApxRegs;

            Assert.That(ForceLowGprForApxIfNeeded(allocator, integer, candidates, useApxRegs),
                Is.EqualTo(restricted ? SRBM_RAX : candidates));
            Assert.That(ForceLowGprForApxIfNeeded(allocator, integer, SRBM_NONE, useApxRegs),
                Is.EqualTo(restricted ? LowGprRegs(allocator) : SRBM_NONE));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ApxRestrictionsFollowContainedAddressUses(bool contained)
    {
        WithAllocator((compiler, allocator) => {
            ApxIsSupported(allocator) = true;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var memory = compiler.gtNewIndir(TYP_DOUBLE, address);
            memory.IsContained = contained;
            var load = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_LoadAlignedVector128, TYP_DOUBLE, 16, address) {
                IsContained = contained,
            };
            var broadcast = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_AVX2_BroadcastScalarToVector128, TYP_DOUBLE, 16, address) {
                IsContained = contained,
            };
            var expected = contained ? LowGprRegs(allocator) : SRBM_NONE;

            Assert.That(ForceLowGprForApx(allocator, memory, SRBM_NONE, false), Is.EqualTo(expected));
            Assert.That(ForceLowGprForApx(allocator, load, SRBM_NONE, false), Is.EqualTo(expected));
            Assert.That(ForceLowGprForApx(allocator, broadcast, SRBM_NONE, false), Is.EqualTo(expected));
            Assert.That(ForceLowGprForApx(allocator, memory, SRBM_NONE, true),
                Is.EqualTo(LowGprRegs(allocator)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EvexIncompatibleMasksDistinguishSimdValuesFromTheirAddresses(bool contained)
    {
        WithAllocator((compiler, allocator) => {
            AvailableFloatRegs(allocator) = (SRBM_LOWFLOAT | SRBM_HIGHFLOAT) & ~SRBM_XMM15;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var memory = compiler.gtNewIndir(TYP_DOUBLE, address);
            memory.IsContained = contained;
            var load = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_LoadAlignedVector128, TYP_DOUBLE, 16, address) {
                IsContained = contained,
            };

            Assert.That(BuildEvexIncompatibleMask(allocator, address), Is.EqualTo(SRBM_NONE));
            Assert.That(BuildEvexIncompatibleMask(allocator, memory),
                Is.EqualTo(contained ? SRBM_NONE : SRBM_LOWFLOAT & ~SRBM_XMM15));
            Assert.That(BuildEvexIncompatibleMask(allocator, load),
                Is.EqualTo(contained ? SRBM_NONE : SRBM_LOWFLOAT & ~SRBM_XMM15));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InternalFloatCandidatesOnlyPermitCalleeSavesWhenFloatingPointIsAlreadyUsed(bool floatingPointUsed)
    {
        WithAllocator((compiler, allocator) => {
            compiler.compFloatingPointUsed = floatingPointUsed;
            Assert.That(InternalFloatRegCandidates(allocator),
                Is.EqualTo(floatingPointUsed ? SRBM_ALLFLOAT_INIT : SRBM_FLT_CALLEE_TRASH_INIT));
            Assert.That(NeedNonIntegerRegisters(allocator), Is.True);
            Assert.That(compiler.compFloatingPointUsed, Is.EqualTo(floatingPointUsed));
        });
    }

    [TestCase(false, 0u)]
    [TestCase(false, 32u)]
    [TestCase(true, 0u)]
    [TestCase(true, 16u)]
    [TestCase(true, 32u)]
    [TestCase(true, 64u)]
    public static void AvxUsageFlagsTrackInstructionAndVectorWidth(bool vex, uint size)
    {
        WithAllocator((compiler, allocator) => {
            if (vex)
            {
                EnableInstructionSet(compiler, InstructionSet_AVX);
                EnableInstructionSet(compiler, InstructionSet_AVX512);
            }

            SetContainsAVXFlags(allocator, size);
            var emitter = (compiler.codeGen ?? throw new AssertionException("Code generation was not initialized.")).Emitter;
            Assert.That(emitter.ContainsAvxInstruction, Is.EqualTo(vex));
            Assert.That(emitter.Contains256BitOrMoreAvxInstruction, Is.EqualTo(vex && size >= 32));

            SetContainsAVXFlags(allocator, 0);
            Assert.That(emitter.Contains256BitOrMoreAvxInstruction, Is.EqualTo(vex && size >= 32));
        });
    }

    [Test]
    public static void SimpleLeafDefinesItsValueWithoutOperandUses()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildSimple(allocator, value), Is.Zero);
            Assert.That(allocator.refPositions, Has.Count.EqualTo(1));
            var definition = allocator.refPositions[0];
            Assert.That(definition.refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(definition.nodeLocation, Is.EqualTo(5));
            Assert.That(definition.treeNode, Is.SameAs(value));
        });
    }

    [TestCase(GT_BSWAP, TYP_INT)]
    [TestCase(GT_KEEPALIVE, TYP_VOID)]
    public static void SimpleOperatorsBuildUsesAndOnlyDefineValues(genTreeOps operation, var_types type)
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            ReferenceBuildLocation(allocator) = 1;
            var definition = BuildDef(allocator, value, SRBM_NONE, 0);
            var tree = new GenTreeUnOp(operation, type, value);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildSimple(allocator, tree), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions, Has.Count.EqualTo(type == TYP_VOID ? 2 : 3));
            if (type != TYP_VOID)
            {
                Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(tree));
                Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "forceLowGprForApx")]
    private static extern regMask ForceLowGprForApx(LinearScan allocator, GenTree tree, regMask candidates, bool force);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "forceLowGprForApxIfNeeded")]
    private static extern regMask ForceLowGprForApxIfNeeded(LinearScan allocator, GenTree tree, regMask candidates, bool useApxRegs);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildEvexIncompatibleMask")]
    private static extern regMask BuildEvexIncompatibleMask(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "internalFloatRegCandidates")]
    private static extern regMask InternalFloatRegCandidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setContainsAVXFlags")]
    private static extern void SetContainsAVXFlags(LinearScan allocator, uint size);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildSimple")]
    private static extern int BuildSimple(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_apxIsSupported")]
    private static extern ref bool ApxIsSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lowGprRegs")]
    private static extern ref regMask LowGprRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_needNonIntegerRegisters")]
    private static extern ref bool NeedNonIntegerRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrashRegs(Compiler compiler);

    private static void EnableInstructionSet(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
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
        CompilerFloatCalleeTrashRegs(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
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
