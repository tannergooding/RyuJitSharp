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
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanSharedReferenceBuilderTests
{
    [TestCase(TYP_BYTE, false, false, false)]
    [TestCase(TYP_BYTE, true, false, false)]
    [TestCase(TYP_INT, false, false, false)]
    [TestCase(TYP_INT, true, false, false)]
    [TestCase(TYP_BYTE, false, true, false)]
    [TestCase(TYP_BYTE, true, true, false)]
    [TestCase(TYP_INT, false, true, false)]
    [TestCase(TYP_INT, true, true, false)]
    [TestCase(TYP_INT, false, false, true)]
    [TestCase(TYP_INT, true, false, true)]
    public static void ComparisonsPreserveOperandOrderContainmentAndMaterializedResult(
        var_types operandType, bool containedRight, bool flagsOnly, bool byteResult)
    {
        WithAllocator((compiler, allocator) => {
#if TARGET_X86
            AvailableIntRegs(allocator) |= SRBM_EBP;
#endif
            var left = compiler.gtNewIconNode(operandType, 3);
            var right = compiler.gtNewIconNode(operandType, 5);
            right.IsContained = containedRight;
            ReferenceBuildLocation(allocator) = 2;
            var leftDefinition = BuildDef(allocator, left, SRBM_NONE, 0);
            var rightDefinition = containedRight ? null : BuildDef(allocator, right, SRBM_NONE, 0);
            var compare = compiler.gtNewBinaryNode(flagsOnly ? GT_CMP : GT_EQ,
                flagsOnly ? TYP_VOID : byteResult ? TYP_BYTE : TYP_INT, left, right);
            ReferenceBuildLocation(allocator) = 4;
            var firstReference = allocator.refPositions.Count;

            Assert.That(BuildCmp(allocator, compare), Is.EqualTo(containedRight ? 1 : 2));
            var uses = allocator.refPositions.GetRange(firstReference,
                allocator.refPositions.Count - firstReference).FindAll(
                    reference => reference.refType is RefType.RefTypeUse);
            Assert.That(uses, Has.Count.EqualTo(containedRight ? 1 : 2));
            Assert.That(uses[0].getInterval(), Is.SameAs(leftDefinition.getInterval()));
            if (rightDefinition is not null)
            {
                Assert.That(uses[1].getInterval(), Is.SameAs(rightDefinition.getInterval()));
            }

            var operandCandidates = AvailableIntRegs(allocator);
#if TARGET_X86
            if ((operandType is TYP_BYTE) || byteResult)
            {
                operandCandidates &= SRBM_EAX | SRBM_ECX | SRBM_EDX | SRBM_EBX;
            }
#endif
            foreach (var use in uses)
            {
                Assert.That(use.registerAssignment, Is.EqualTo(operandCandidates));
                Assert.That(use.nodeLocation, Is.EqualTo(4));
                Assert.That(use.delayRegFree, Is.False);
            }

            var definitions = allocator.refPositions.FindAll(reference =>
                ReferenceEquals(reference.treeNode, compare) && reference.refType is RefType.RefTypeDef);
            Assert.That(definitions, Has.Count.EqualTo(flagsOnly ? 0 : 1));
            if (!flagsOnly)
            {
                var destinationCandidates = AvailableIntRegs(allocator);
#if TARGET_X86
                destinationCandidates &= SRBM_EAX | SRBM_ECX | SRBM_EDX | SRBM_EBX;
#endif
                Assert.That(definitions[0].registerAssignment, Is.EqualTo(destinationCandidates));
                Assert.That(definitions[0].nodeLocation, Is.EqualTo(5));
                Assert.That(allocator.refPositions.IndexOf(uses[^1]),
                    Is.LessThan(allocator.refPositions.IndexOf(definitions[0])));
            }

            Assert.That(allocator.refPositions.Exists(reference =>
                reference.refType is RefType.RefTypeDef && reference.getInterval().isInternal), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedComparisonMemoryOperandStillConsumesItsAddress(bool memoryFirst)
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            ReferenceBuildLocation(allocator) = 2;
            var addressDefinition = BuildDef(allocator, address, SRBM_NONE, 0);
            var valueDefinition = BuildDef(allocator, value, SRBM_NONE, 0);
            var memory = compiler.gtNewIndir(TYP_BYTE, address);
            memory.IsContained = true;
            var compare = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                memoryFirst ? memory : value, memoryFirst ? value : memory);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCmp(allocator, compare), Is.EqualTo(2));
            var addressUse = addressDefinition.nextRefPosition
                ?? throw new AssertionException("Missing comparison address use.");
            var valueUse = valueDefinition.nextRefPosition
                ?? throw new AssertionException("Missing comparison value use.");
            Assert.That(addressUse.registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
            var valueCandidates = AvailableIntRegs(allocator);
#if TARGET_X86
            valueCandidates &= SRBM_EAX | SRBM_ECX | SRBM_EDX | SRBM_EBX;
#endif
            Assert.That(valueUse.registerAssignment, Is.EqualTo(valueCandidates));
            Assert.That(allocator.refPositions.IndexOf(memoryFirst ? addressUse : valueUse),
                Is.LessThan(allocator.refPositions.IndexOf(memoryFirst ? valueUse : addressUse)));
        });
    }

#if TARGET_64BIT
    [TestCase(TYP_LONG, false)]
    [TestCase(TYP_LONG, true)]
#endif
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_INT, true)]
    public static void CastUsesPreferOnlyUncontainedLongToIntCopies(var_types sourceType, bool contained)
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(contained ? TYP_I_IMPL : sourceType, 17);
            ReferenceBuildLocation(allocator) = 2;
            var definition = BuildDef(allocator, value, SRBM_NONE, 0);
            GenTree source = value;
            if (contained)
            {
                source = compiler.gtNewIndir(sourceType, value);
                source.IsContained = true;
            }

            var cast = new GenTreeCast(TYP_INT, source, false, TYP_INT);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCastUses(allocator, cast, AvailableIntRegs(allocator)), Is.EqualTo(1));
            var use = definition.nextRefPosition
                ?? throw new AssertionException("Missing cast source use.");
            Assert.That(use.registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
            Assert.That(use.delayRegFree, Is.False);
            Assert.That(TargetPreferredUse(allocator),
                sourceType is TYP_LONG && !contained ? Is.SameAs(use) : Is.Null);
            Assert.That(allocator.refPositions.Exists(reference =>
                ReferenceEquals(reference.treeNode, cast)), Is.False);
        });
    }

#if DEBUG
    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(3, true)]
    [TestCase(0x82, false)]
    [TestCase(0x82, true)]
    [TestCase(0x2002, false)]
    [TestCase(0x2002, true)]
#else
    [TestCase(0, false)]
    [TestCase(0, true)]
#endif
    public static void PassThroughArgumentsRespectStressAndLastUse(int stressMask, bool lastUse)
    {
        WithAllocator((compiler, allocator) => {
#if DEBUG
            StressMask(allocator) = stressMask;
#endif
            compiler.lvaTable = [
                new() { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            var localInterval = new Interval(TYP_INT, AvailableIntRegs(allocator));
            allocator.localVarIntervals = [localInterval];
            localInterval.setLocalNumber(compiler, 0, allocator);
            CurrentLiveVariables(allocator) = VarSetOps.MakeSingleton(compiler, 0);
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            if (lastUse)
            {
                source.Flags |= GTF_VAR_DEATH;
            }

            var register = genRegNumFromMask(SRBM_INTRET, TYP_INT);
            var argument = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, source) { RegNum = register };
            ReferenceBuildLocation(allocator) = 4;
            var supported = true;
#if DEBUG && TARGET_X86
            supported = (stressMask & 0x3) != 0x2;
#endif
            Assert.That(SupportsSpecialPutArg(allocator), Is.EqualTo(supported));
            Assert.That(BuildPutArgReg(allocator, argument), Is.EqualTo(1));
            var definition = allocator.refPositions[^1];
            var special = supported && !lastUse;
            Assert.That(definition.getInterval().isSpecialPutArg, Is.EqualTo(special));
            Assert.That(definition.getInterval().relatedInterval,
                special ? Is.SameAs(localInterval) : Is.Null);
            Assert.That(PlacedArgumentLocalCount(allocator), Is.EqualTo(special ? 1 : 0));
            Assert.That(PlacedArgumentRegisters(allocator).IsSet(register), Is.True);
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_INTRET));
            Assert.That(definition.nodeLocation, Is.EqualTo(5));
            Assert.That(localInterval.firstRefPosition?.registerAssignment, Is.EqualTo(SRBM_INTRET));
            Assert.That(VarSetOps.IsMember(compiler, CurrentLiveVariables(allocator), 0), Is.EqualTo(!lastUse));
        });
    }

#if TARGET_AMD64 && FEATURE_MULTIREG_RET
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void MultiDefinitionsPreserveHighRegisterBitsAndFixedMaskOrder(int maskMode)
    {
        WithAllocator((compiler, allocator) => {
            var call = compiler.gtNewCallNode(TYP_STRUCT, CT_USER_FUNC, null);
            ref var descriptor = ref CallReturnDescriptor(call);
            ReturnTypes(ref descriptor)[0] = TYP_FLOAT;
            ReturnTypes(ref descriptor)[1] = TYP_FLOAT;
#if DEBUG
            ReturnDescriptorInitialized(ref descriptor) = true;
#endif
            var candidates = maskMode switch
            {
                0 => SRBM_NONE,
                1 => SRBM_XMM0 | SRBM_XMM31,
                _ => SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM31,
            };
            ReferenceBuildLocation(allocator) = 4;

            BuildDefs(allocator, call, 2, candidates);

            var definitions = allocator.refPositions.FindAll(reference =>
                ReferenceEquals(reference.treeNode, call) && reference.refType is RefType.RefTypeDef);
            Assert.That(definitions, Has.Count.EqualTo(2));
            for (var index = 0; index < definitions.Count; index++)
            {
                var expected = maskMode switch
                {
                    0 => AvailableFloatRegs(allocator),
                    1 => index == 0 ? SRBM_XMM0 : SRBM_XMM31,
                    _ => candidates,
                };
                Assert.That(definitions[index].registerAssignment, Is.EqualTo(expected));
                Assert.That(definitions[index].getMultiRegIdx(), Is.EqualTo(index));
                Assert.That(definitions[index].nodeLocation, Is.EqualTo(5));
                Assert.That(definitions[index].getInterval().registerType, Is.EqualTo(TYP_FLOAT));
            }

            Assert.That(compiler.compFloatingPointUsed, Is.True);
        }, evex: true);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_returnTypeDesc")]
    private static extern ref ReturnTypeDesc CallReturnDescriptor(GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regType")]
    private static extern ref InlineArrayMaxRetRegCount<var_types> ReturnTypes(ref ReturnTypeDesc descriptor);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_inited")]
    private static extern ref bool ReturnDescriptorInitialized(ref ReturnTypeDesc descriptor);
#endif
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyRegisterKillsStillApplyGcReferenceSemantics(bool killsGcReferences)
    {
        WithAllocator((compiler, allocator) => {
            var node = new GenTree(killsGcReferences ? GT_START_PREEMPTGC : GT_NOP, TYP_VOID);
            ReferenceBuildLocation(allocator) = 4;
            var firstReference = allocator.refPositions.Count;

            BuildKills(allocator, node, new regMaskTP(SRBM_NONE));

            Assert.That(allocator.refPositions.Count - firstReference, Is.EqualTo(killsGcReferences ? 1 : 0));
            if (killsGcReferences)
            {
                var kill = allocator.refPositions[^1];
                Assert.That(kill.refType, Is.EqualTo(RefType.RefTypeKillGCRefs));
                Assert.That(kill.nodeLocation, Is.EqualTo(5));
                Assert.That(kill.registerAssignment,
                    Is.EqualTo(AvailableIntRegs(allocator) & ~SRBM_ARG_REGS));
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCmp")]
    private static extern int BuildCmp(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCastUses")]
    private static extern int BuildCastUses(LinearScan allocator, GenTreeCast tree, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "supportsSpecialPutArg")]
    private static extern bool SupportsSpecialPutArg(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPutArgReg")]
    private static extern int BuildPutArgReg(LinearScan allocator, GenTreeUnOp tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDefs")]
    private static extern void BuildDefs(LinearScan allocator, GenTree tree, int count, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildKills")]
    private static extern void BuildKills(LinearScan allocator, GenTree tree, regMaskTP candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int index);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse")]
    private static extern ref RefPosition? TargetPreferredUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentLiveVariables")]
    private static extern ref nint[] CurrentLiveVariables(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_placedArgumentRegisters")]
    private static extern ref regMaskTP PlacedArgumentRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_placedArgumentLocalCount")]
    private static extern ref int PlacedArgumentLocalCount(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool evex = false)
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
#if TARGET_AMD64
        if (evex)
        {
            CompilerAllFloatRegs(compiler) |= SRBM_HIGHFLOAT;
            CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_EVEX;
            compiler.opts.compSupportsISA.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
        }
#else
        _ = evex;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.CopyRegisterInfo();
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFramePointerUsed = false;
            codeGen.IsFrameRequired = false;
            codeGen.RegSet.rsClearRegsModified();
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
