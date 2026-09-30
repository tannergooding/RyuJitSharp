// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64LinearScanConstructionTests
{
    [Test]
    public static void HelperCallKillSetsMatchArm64CallingConventions()
    {
        WithCompiler(false, false, (compiler, _) => {
            var profilerTrash = new regMaskTP(
                (SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH) &
                ~(SRBM_ARG_REGS | SRBM_ARG_RET_BUFF | SRBM_FLTARG_REGS | SRBM_FP),
                SRBM_MSK_CALLEE_TRASH);

            Assert.Multiple(() => {
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_ASSIGN_REF),
                    Is.EqualTo(new regMaskTP(SRBM_CALLEE_TRASH_WRITEBARRIER)));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_CHECKED_ASSIGN_REF),
                    Is.EqualTo(new regMaskTP(SRBM_CALLEE_TRASH_WRITEBARRIER)));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_ENTER),
                    Is.EqualTo(profilerTrash));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_LEAVE),
                    Is.EqualTo(profilerTrash));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_TAILCALL),
                    Is.EqualTo(profilerTrash));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC),
                    Is.EqualTo(SRBM_STOP_FOR_GC_TRASH));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_INIT_PINVOKE_FRAME),
                    Is.EqualTo(SRBM_INIT_PINVOKE_FRAME_TRASH));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_VALIDATE_INDIRECT_CALL),
                    Is.EqualTo(new regMaskTP(SRBM_VALIDATE_INDIRECT_CALL_TRASH)));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT),
                    Is.EqualTo(SRBM_INTERFACELOOKUP_FOR_SLOT_TRASH));
                Assert.That(compiler.compHelperCallKillSet(CORINFO_HELP_UNDEF),
                    Is.EqualTo(SRBM_CALLEE_TRASH));
            });
        });
    }

    [Test]
    public static void VoidCallBuildsCallerSavedKillsWithoutSources()
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            codeGen.RegSet.rsClearRegsModified();
            var allocator = new LinearScan(compiler);
            var call = new GenTreeCall(TYP_VOID);

            Assert.That(BuildCall(allocator, call), Is.Zero);
            Assert.That(allocator.intervals, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallKillSetIncludesOnlyRequiredRegisterBanks(bool killFloatRegisters)
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            NeedToKillFloatRegisters(allocator) = killFloatRegisters;

            var expected = killFloatRegisters
                ? SRBM_CALLEE_TRASH
                : new regMaskTP(SRBM_INT_CALLEE_TRASH);
            Assert.That(GetKillSetForCall(allocator, new GenTreeCall(TYP_VOID)), Is.EqualTo(expected));
        });
    }

#if DEBUG
    [TestCase(GT_MULHI)]
    [TestCase(GT_MUL_LONG)]
    [TestCase(GT_MOD)]
    [TestCase(GT_UMOD)]
    [TestCase(GT_UDIV)]
    public static void DebugArithmeticKillSetsDoNotReserveFixedRegisters(genTreeOps operation)
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var value = compiler.gtNewIconNode(TYP_INT, 3);
            var arithmetic = new GenTreeOp(operation, TYP_INT, value, value);

            Assert.That(GetKillSetForNode(allocator, arithmetic), Is.EqualTo(new regMaskTP(SRBM_NONE)));
        });
    }

    [Test]
    public static void DebugKillSetDispatcherPreservesArm64ShiftAndCallMasks()
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var value = compiler.gtNewIconNode(TYP_INT, 3);
            var shift = new GenTreeOp(GT_LSH, TYP_INT, value, value);
            var multiply = new GenTreeOp(GT_MUL, TYP_INT, value, value);
            var divide = new GenTreeOp(GT_DIV, TYP_INT, value, value);
            var call = new GenTreeCall(TYP_VOID);

            Assert.Multiple(() => {
                Assert.That(GetKillSetForNode(allocator, shift), Is.EqualTo(new regMaskTP(SRBM_NONE)));
                Assert.That(GetKillSetForNode(allocator, multiply), Is.EqualTo(new regMaskTP(SRBM_NONE)));
                Assert.That(GetKillSetForNode(allocator, divide), Is.EqualTo(new regMaskTP(SRBM_NONE)));
                Assert.That(GetKillSetForNode(allocator, call),
                    Is.EqualTo(new regMaskTP(SRBM_INT_CALLEE_TRASH)));
                Assert.That(GetKillSetForNode(allocator, new GenTreeUnOp(GT_RETURNTRAP, TYP_INT, value)),
                    Is.EqualTo(compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC)));
            });
        });
    }
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void FastTailCallTargetExcludesGsCookieTemporaries(bool needsCookie)
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            codeGen.RegSet.rsClearRegsModified();
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            if (needsCookie)
            {
                compiler.NeedsGSSecurityCookie = true;
            }
            var allocator = new LinearScan(compiler);
            var target = new GenTreeIntCon(TYP_I_IMPL, 0x1234);
            _ = BuildNode(allocator, target);
            var definition = allocator.refPositions.Find(position =>
                (position.treeNode == target) && (position.refType is RefType.RefTypeDef))
                ?? throw new AssertionException("The call target has no register definition.");
            var call = new GenTreeCall(TYP_VOID) { _callType = CT_INDIRECT, ControlExpr = target };
            call._callMoreFlags |= GTF_CALL_M_TAILCALL;

            var cookieTemporaries = new regMaskTP(SRBM_GSCOOKIE_TMP);
            Assert.Multiple(() => {
                Assert.That(codeGen.genGetGSCookieTempRegs(false, null), Is.EqualTo(cookieTemporaries));
                Assert.That(codeGen.genGetGSCookieTempRegs(true, call), Is.EqualTo(cookieTemporaries));
            });

            Assert.That(BuildCall(allocator, call), Is.EqualTo(1));
            var expected = AvailableIntRegs(allocator) & SRBM_INT_CALLEE_TRASH & ~SRBM_LR;
            if (needsCookie)
            {
                expected &= ~SRBM_GSCOOKIE_TMP;
            }
            Assert.That(definition.nextRefPosition?.registerAssignment, Is.EqualTo(expected));
            Assert.That(definition.nextRefPosition?.registerAssignment & SRBM_LR, Is.EqualTo(SRBM_NONE));
        });
    }

    [Test]
    public static void NodeBuildingTracksConstantAndVectorOperand()
    {
        WithCompiler(false, false, (compiler, _) => {
            compiler.compFloatingPointUsed = true;
            var allocator = new LinearScan(compiler);
            var scalar = new GenTreeIntCon(TYP_INT, 7);
            var vector = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_Vector_Create, TYP_INT, 16, scalar);

            Assert.That(BuildNode(allocator, scalar), Is.Zero);
            Assert.That(BuildNode(allocator, vector), Is.EqualTo(1));
            Assert.That(allocator.intervals, Has.Count.EqualTo(2));
            Assert.That(allocator.intervals[0].isConstant, Is.True);
        });
    }

    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_INT, 0UL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_INT, 0xFFFF_FFFFUL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_INT, 127UL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_INT, 128UL, 0UL, 1)]
    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_INT, 256UL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_ULONG, 0x8000_0000_0000_0000UL, 0UL, 1)]
    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_FLOAT, 0x3F80_0000UL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableRepeated, TYP_FLOAT, 0x8000_0000UL, 0UL, 1)]
    [TestCase(SimdScalableKind.SimdScalableSequence, TYP_INT, 0UL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableSequence, TYP_INT, 15UL, 15UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableSequence, TYP_INT, 16UL, 15UL, 1)]
    [TestCase(SimdScalableKind.SimdScalableSequence, TYP_INT, 15UL, 16UL, 1)]
    [TestCase(SimdScalableKind.SimdScalableSequence, TYP_INT, 16UL, 16UL, 2)]
    [TestCase(SimdScalableKind.SimdScalableSequence, TYP_LONG, 0xFFFF_FFFF_FFFF_FFF0UL, 15UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableSequence, TYP_LONG, 0xFFFF_FFFF_FFFF_FFEFUL, 0UL, 1)]
    [TestCase(SimdScalableKind.SimdScalableScalar, TYP_INT, 0UL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableScalar, TYP_INT, 1UL, 0UL, 1)]
    [TestCase(SimdScalableKind.SimdScalableScalar, TYP_DOUBLE, 0x3FF0_0000_0000_0000UL, 0UL, 0)]
    [TestCase(SimdScalableKind.SimdScalableScalar, TYP_DOUBLE, 0x8000_0000_0000_0000UL, 0UL, 1)]
    public static void ScalableConstantsReserveOnlyTheRequiredIntegerTemporaries(
        SimdScalableKind kind, var_types baseType, ulong index, ulong step, int temporaries)
    {
        WithCompiler(false, false, (compiler, _) => {
            compiler.compFloatingPointUsed = true;
            var allocator = new LinearScan(compiler);
            var node = compiler.gtNewSimdVconNode(TYP_SIMD, baseType, kind, index, step);

            Assert.That(BuildNode(allocator, node), Is.Zero);
            Assert.That(allocator.intervals.FindAll(interval => interval.isInternal), Has.Count.EqualTo(temporaries));
            var definitions = allocator.refPositions.FindAll(position =>
                position.refType == RefType.RefTypeDef && !position.getInterval().isInternal);
            Assert.That(definitions, Has.Count.EqualTo(1));
            Assert.That(definitions[0].getInterval().isConstant, Is.True);
            Assert.That(definitions[0].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions.FindAll(position =>
                position.refType == RefType.RefTypeUse && position.getInterval().isInternal),
                Has.Count.EqualTo(temporaries));
        });
    }

#if DEBUG
    [TestCase(TYP_BYTE, false)]
    [TestCase(TYP_BYTE, true)]
    [TestCase(TYP_LONG, false)]
    [TestCase(TYP_LONG, true)]
    public static void ScalableMasksNeedNoConstantScratch(var_types type, bool index)
    {
        WithCompiler(false, false, (compiler, _) => {
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            compiler.compFloatingPointUsed = true;
            var allocator = new LinearScan(compiler);
            var mask = compiler.gtNewMskConNode(TYP_MASK, type, index);

            Assert.That(BuildNode(allocator, mask), Is.Zero);
            Assert.That(allocator.intervals.FindAll(interval => interval.isInternal), Is.Empty);
            Assert.That(allocator.intervals.FindAll(interval => interval.isConstant), Has.Count.EqualTo(1));
            Assert.That(s_assertions, Is.Empty);
        }, captureAssertions: true);
    }
#endif

    [Test]
    public static void ReferenceTraversalBuildsArm64NodeDefinitions()
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var constant = new GenTreeIntCon(TYP_INT, 42);

            BuildRefPositionsForNode(allocator, constant, 2);

            Assert.That(allocator.intervals, Has.Count.EqualTo(1));
            Assert.That(allocator.intervals[0].isConstant, Is.True);
            Assert.That(allocator.refPositions.Exists(
                position => (position.treeNode == constant) &&
                    (position.refType is RefType.RefTypeDef)), Is.True);
        });
    }

    [Test]
    public static void ContainedArm64NodeDoesNotBuildReferences()
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var constant = new GenTreeIntCon(TYP_INT, 42) { IsContained = true };

            BuildRefPositionsForNode(allocator, constant, 2);

            Assert.That(allocator.refPositions, Is.Empty);
        });
    }

#if DEBUG
    [Test]
    public static void Amd64OnlyRegisterStressDoesNotConstrainArm64NodeReferences()
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var constant = new GenTreeIntCon(TYP_INT, 42);
            ReferenceBuildLocation(allocator) = 2;
            BuildRefPositionsForNode(allocator, constant, 2);
            StressMask(allocator) = 0x2000;
            var negate = new GenTreeUnOp(GT_NEG, TYP_INT, constant);

            ReferenceBuildLocation(allocator) = 4;
            BuildRefPositionsForNode(allocator, negate, 4);

            Assert.That(allocator.refPositions[^1].minRegCandidateCount, Is.EqualTo(1u));
            Assert.That(allocator.refPositions[0].nextRefPosition?.minRegCandidateCount, Is.EqualTo(1u));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConsecutiveDefinitionSkipsReversePreferenceConflicts(bool consecutive)
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var constant = new GenTreeIntCon(TYP_INT, 42);
            ReferenceBuildLocation(allocator) = 2;
            BuildRefPositionsForNode(allocator, constant, 2);
            var definition = allocator.refPositions[^1];
            definition.registerAssignment = SRBM_R0;
            definition.needsConsecutive = consecutive;
            StressMask(allocator) = 0x08;
            var negate = new GenTreeUnOp(GT_NEG, TYP_INT, constant);

            ReferenceBuildLocation(allocator) = 4;
            BuildRefPositionsForNode(allocator, negate, 4);

            var use = definition.nextRefPosition
                ?? throw new AssertionException("Missing use of consecutive definition.");
            Assert.That(use.registerAssignment & SRBM_R0, Is.EqualTo(SRBM_NONE));
            Assert.That(definition.getInterval().hasConflictingDefUse, Is.EqualTo(!consecutive));
        });
    }
#endif

    [Test]
    public static void FiniteCheckDefinesResultBeforeInternalRegisterUse()
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            compiler.compFloatingPointUsed = true;
            var allocator = new LinearScan(compiler);
            var input = new GenTreeDblCon(TYP_DOUBLE, 1.0);
            var finite = new GenTreeUnOp(GT_CKFINITE, TYP_DOUBLE, input);
            _ = BuildNode(allocator, input);
            var start = allocator.refPositions.Count;

            Assert.That(BuildNode(allocator, finite), Is.EqualTo(1));
            var references = allocator.refPositions.GetRange(start, allocator.refPositions.Count - start);
            Assert.That(references, Has.Count.EqualTo(4));
            Assert.Multiple(() => {
                Assert.That(references[0].refType, Is.EqualTo(RefType.RefTypeDef));
                Assert.That(references[0].getInterval().isInternal, Is.True);
                Assert.That(references[1].refType, Is.EqualTo(RefType.RefTypeUse));
                Assert.That(references[1].getInterval(), Is.SameAs(allocator.intervals[0]));
                Assert.That(references[2].refType, Is.EqualTo(RefType.RefTypeDef));
                Assert.That(references[2].treeNode, Is.SameAs(finite));
                Assert.That(references[2].getInterval().isInternal, Is.False);
                Assert.That(references[3].refType, Is.EqualTo(RefType.RefTypeUse));
                Assert.That(references[3].getInterval(), Is.SameAs(references[0].getInterval()));
            });
        });
    }

    [Test]
    public static void ContainedBlockAddressModesConsumeOnlyBases()
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            var allocator = new LinearScan(compiler);
            var destinationBase = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var destinationIndex = new GenTreeIntCon(TYP_I_IMPL, 2);
            var sourceBase = new GenTreeIntCon(TYP_I_IMPL, 0x2000);
            var sourceIndex = new GenTreeIntCon(TYP_I_IMPL, 3);
            _ = BuildNode(allocator, destinationBase);
            _ = BuildNode(allocator, destinationIndex);
            _ = BuildNode(allocator, sourceBase);
            _ = BuildNode(allocator, sourceIndex);
            var definitions = allocator.refPositions.GetRange(0, 4);

            var destinationAddress = new GenTreeAddrMode(TYP_I_IMPL, destinationBase, destinationIndex, 1, 8) {
                IsContained = true,
            };
            var sourceAddress = new GenTreeAddrMode(TYP_I_IMPL, sourceBase, sourceIndex, 1, 8) {
                IsContained = true,
            };
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, source, new ClassLayout(4)) {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            var start = allocator.refPositions.Count;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(2));
            var uses = allocator.refPositions.GetRange(start, allocator.refPositions.Count - start)
                .FindAll(position => position.refType is RefType.RefTypeUse &&
                    !position.getInterval().isInternal);
            Assert.That(uses, Has.Count.EqualTo(2));
            Assert.Multiple(() => {
                Assert.That(uses[0].getInterval(), Is.SameAs(definitions[0].getInterval()));
                Assert.That(uses[1].getInterval(), Is.SameAs(definitions[2].getInterval()));
                Assert.That(definitions[1].nextRefPosition, Is.Null);
                Assert.That(definitions[3].nextRefPosition, Is.Null);
            });
        });
    }

    [Test]
    public static void HalfwordIndexedRmwKeepsSourceOperandsDelayFree()
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            compiler.compFloatingPointUsed = true;
            var allocator = new LinearScan(compiler);
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            var third = new GenTreeVecCon(TYP_SIMD16);
            _ = BuildNode(allocator, first);
            _ = BuildNode(allocator, second);
            _ = BuildNode(allocator, third);
            var definitions = allocator.refPositions.GetRange(0, 3);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_MultiplyAddByScalar,
                TYP_SHORT, 16, first, second, third);

            Assert.That(BuildNode(allocator, intrinsic), Is.EqualTo(3));
            Assert.Multiple(() => {
                Assert.That(definitions[0].nextRefPosition?.delayRegFree, Is.False);
                Assert.That(definitions[1].nextRefPosition?.delayRegFree, Is.True);
                Assert.That(definitions[2].nextRefPosition?.delayRegFree, Is.True);
            });
        });
    }

    [Test]
    public static void MultiRegisterVectorLoadLinksConsecutiveDefinitions()
    {
        WithCompiler(false, false, (compiler, _) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = new LinearScan(compiler);
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);

            Assert.That(BuildNode(allocator, address), Is.Zero);
            Assert.That(BuildNode(allocator, load), Is.EqualTo(1));

            var definitions = allocator.refPositions.FindAll(
                position => (position.treeNode == load) && (position.refType is RefType.RefTypeDef));
            Assert.That(definitions, Has.Count.EqualTo(2));
            Assert.Multiple(() => {
                Assert.That(definitions[0].needsConsecutive, Is.True);
                Assert.That(definitions[0].regCount, Is.EqualTo(2));
                Assert.That(definitions[1].needsConsecutive, Is.True);
                Assert.That(definitions[1].regCount, Is.Zero);
                Assert.That(NextConsecutive(allocator, definitions[0]), Is.SameAs(definitions[1]));
                Assert.That(NextConsecutive(allocator, definitions[1]), Is.Null);
            });
        });
    }

    [TestCase(SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V4, 2u, SRBM_V0 | SRBM_V1,
        SRBM_V0 | SRBM_V1 | SRBM_V2)]
    [TestCase(SRBM_V0 | SRBM_V31, 2u, SRBM_V31, SRBM_V0 | SRBM_V31)]
    [TestCase(SRBM_V0 | SRBM_V1 | SRBM_V30 | SRBM_V31, 3u, SRBM_V30 | SRBM_V31,
        SRBM_V0 | SRBM_V1 | SRBM_V30 | SRBM_V31)]
    [TestCase(SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V29 | SRBM_V30 | SRBM_V31, 4u,
        SRBM_V29 | SRBM_V30 | SRBM_V31,
        SRBM_V0 | SRBM_V29 | SRBM_V30 | SRBM_V31)]
    [TestCase(SRBM_V0 | SRBM_V2 | SRBM_V4, 2u, SRBM_NONE, SRBM_NONE)]
    public static void ConsecutiveCandidateStartsStayInFloatBank(
        regMask candidates, uint count, regMask starts, regMask all)
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            Assert.That(FilterConsecutiveCandidates(allocator, candidates, count, out var covered),
                Is.EqualTo(starts));
            Assert.That(covered, Is.EqualTo(all));
        });
    }

    [TestCase(REG_V0, SRBM_V0 | SRBM_V31)]
    [TestCase(REG_V1, SRBM_V0)]
    public static void ConsecutiveReuseUsesNativePredecessorBit(
        regNumber assignedNext, regMask expected)
    {
        WithCompiler(false, false, (compiler, _) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = new LinearScan(compiler);
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            Assert.That(BuildNode(allocator, address), Is.Zero);
            Assert.That(BuildNode(allocator, load), Is.EqualTo(1));
            var definitions = allocator.refPositions.FindAll(position =>
                position.treeNode == load && position.refType is RefType.RefTypeDef);
            var next = definitions[1].getInterval();
            var assignedRegister = allocator.physRegs[(int)assignedNext];
            assignedRegister.init(assignedNext);
            next.assignedReg = assignedRegister;
            next.isActive = true;

            var candidates = SRBM_V0 | SRBM_V1 | SRBM_V31;
            Assert.That(GetConsecutiveCandidates(allocator, candidates, definitions[0], out var busy),
                Is.EqualTo(expected));
            Assert.That(busy, Is.EqualTo(SRBM_NONE));
        });
    }

    [Test]
    public static void ConsecutiveAssignmentWrapsAndReservesWholeSequence()
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = new LinearScan(compiler);
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            _ = BuildNode(allocator, address);
            _ = BuildNode(allocator, load);
            var definitions = allocator.refPositions.FindAll(
                position => position.treeNode == load && position.refType is RefType.RefTypeDef);
            var first = definitions[0];
            var second = definitions[1];
            first.registerAssignment = SRBM_V31;

            Assert.That(CanAssignNext(allocator, first, REG_V31), Is.True);
            AssignConsecutive(allocator, first, REG_V31);
            Assert.Multiple(() => {
                Assert.That(second.registerAssignment, Is.EqualTo(SRBM_V0));
                Assert.That(ConsecutiveInUse(allocator), Is.EqualTo(SRBM_V31 | SRBM_V0));
            });
        });
    }

    [Test]
    public static void PartiallySpilledRestoreExcludesEntireConsecutiveSequence()
    {
        WithCompiler(false, false, (compiler, _) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = new LinearScan(compiler);
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            Assert.That(BuildNode(allocator, address), Is.Zero);
            Assert.That(BuildNode(allocator, load), Is.EqualTo(1));
            var definitions = allocator.refPositions.FindAll(position =>
                position.treeNode == load && position.refType is RefType.RefTypeDef);
            var first = definitions[0];
            var second = definitions[1];
            var local = NewInterval(allocator, TYP_SIMD16);
            local.isPartiallySpilled = true;
            var upper = NewInterval(allocator, TYP_SIMD16);
            upper.isUpperVector = true;
            upper.relatedInterval = local;
            var restore = allocator.newRefPosition(upper, 1, RefType.RefTypeUpperVectorRestore,
                load, SRBM_V31 | SRBM_V0 | SRBM_V3);
            restore.needsConsecutive = true;
            NextConsecutiveMap(allocator)[first] = restore;
            NextConsecutiveMap(allocator)[restore] = second;
            first.registerAssignment = SRBM_V31;

            AssignConsecutive(allocator, first, REG_V31);
            Assert.Multiple(() => {
                Assert.That(restore.registerAssignment, Is.EqualTo(SRBM_V3));
                Assert.That(second.registerAssignment, Is.EqualTo(SRBM_V0));
            });
        });
    }

    [Test]
    public static void BusyNextRegisterCanBeKeptOnlyByItsAssignedInterval()
    {
        WithCompiler(false, false, (compiler, _) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = new LinearScan(compiler);
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            Assert.That(BuildNode(allocator, address), Is.Zero);
            Assert.That(BuildNode(allocator, load), Is.EqualTo(1));
            var definitions = allocator.refPositions.FindAll(position =>
                position.treeNode == load && position.refType is RefType.RefTypeDef);
            var nextInterval = definitions[1].getInterval();
            var register = allocator.physRegs[(int)REG_V0];
            register.init(REG_V0);
            nextInterval.assignedReg = register;
            nextInterval.isActive = true;
            register.assignedInterval = nextInterval;

            Assert.That(CanAssignNext(allocator, definitions[0], REG_V31), Is.True);
            InUse(allocator) = new regMaskTP(SRBM_V0);
            Assert.That(CanAssignNext(allocator, definitions[0], REG_V31), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FullAllocationAssignsWrappedDefinitionsAndClearsReservation(bool spillNextRegister)
    {
        WithCompiler(false, false, (compiler, codeGen) => {
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            compiler.lvaRefCountState = RCS_NORMAL;
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            compiler.fgPredsComputed = true;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 1;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgBBcount = 1;
            compiler.fgBBNumMax = block.bbNum;
            codeGen.RegSet.rsClearRegsModified();

            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
            RefPosition? occupiedDefinition = null;
            RefPosition? occupiedUse = null;
            Interval? occupied = null;
            if (spillNextRegister)
            {
                occupied = NewInterval(allocator, TYP_SIMD16);
                occupiedDefinition = allocator.newRefPosition(occupied, 1, RefType.RefTypeDef,
                    new GenTreeVecCon(TYP_SIMD16), SRBM_V0);
            }
            var firstInterval = NewInterval(allocator, TYP_SIMD16);
            var secondInterval = NewInterval(allocator, TYP_SIMD16);
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var first = allocator.newRefPosition(
                firstInterval, 2, RefType.RefTypeDef, address, SRBM_V31 | SRBM_V0);
            var second = allocator.newRefPosition(
                secondInterval, 2, RefType.RefTypeDef, address, SRBM_V0 | SRBM_V1);
            first.needsConsecutive = true;
            first.regCount = 2;
            second.needsConsecutive = true;
            NextConsecutiveMap(allocator)[first] = second;
            NextConsecutiveMap(allocator)[second] = null;
            if (occupied is not null)
            {
                occupiedUse = allocator.newRefPosition(occupied, 4, RefType.RefTypeUse,
                    new GenTreeVecCon(TYP_SIMD16), SRBM_V0);
            }
            var lastInterval = NewInterval(allocator, TYP_INT);
            var last = allocator.newRefPosition(lastInterval, 6, RefType.RefTypeDef,
                new GenTreeIntCon(TYP_INT, 1), SRBM_R0);

            AllocateRegisters(allocator);
            Assert.Multiple(() => {
                Assert.That(first.registerAssignment, Is.EqualTo(SRBM_V31));
                Assert.That(second.registerAssignment, Is.EqualTo(SRBM_V0));
                Assert.That(last.registerAssignment, Is.EqualTo(SRBM_R0));
                Assert.That(ConsecutiveInUse(allocator), Is.EqualTo(SRBM_NONE));
                if (spillNextRegister)
                {
                    Assert.That(occupiedDefinition?.spillAfter, Is.True);
                    Assert.That(occupiedUse?.reload, Is.True);
                }
            });
        });
    }

    [Test]
    public static void FullAllocationAssignsPartiallySpilledUpperVectorRestore()
    {
        WithCompiler(false, false, (compiler, codeGen) => {
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            compiler.lvaRefCountState = RCS_NORMAL;
            compiler.compFloatingPointUsed = true;
            compiler.fgPredsComputed = true;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_SIMD16 }];
            compiler.lvaCount = 1;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 1;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgBBcount = 1;
            compiler.fgBBNumMax = block.bbNum;
            codeGen.RegSet.rsClearRegsModified();

            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
            var local = NewInterval(allocator, TYP_SIMD16);
            local.isLocalVar = true;
            local.varNum = 0;
            local.physReg = REG_V0;
            local.isPartiallySpilled = true;
            var upper = NewInterval(allocator, TYP_DOUBLE);
            upper.isUpperVector = true;
            upper.relatedInterval = local;
            var restore = allocator.newRefPosition(upper, 2, RefType.RefTypeUpperVectorRestore,
                new GenTreeVecCon(TYP_SIMD16), SRBM_V3);
            restore.setRegOptional(true);

            AllocateRegisters(allocator);
            Assert.Multiple(() => {
                Assert.That(restore.registerAssignment, Is.EqualTo(SRBM_V3));
                Assert.That(upper.physReg, Is.EqualTo(REG_V3));
                Assert.That(upper.assignedReg, Is.SameAs(allocator.physRegs[(int)REG_V3]));
                Assert.That(local.isPartiallySpilled, Is.False);
            });
        });
    }

    [Test]
    public static void BusyConsecutiveCandidatesRetainLeastSpillTies()
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = new LinearScan(compiler);
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            _ = BuildNode(allocator, address);
            _ = BuildNode(allocator, load);
            var first = allocator.refPositions.Find(position =>
                position.treeNode == load && position.refType is RefType.RefTypeDef &&
                position.regCount == 2) ?? throw new AssertionException("Missing first vector definition.");

            SetRegInUse(allocator, REG_V1, TYP_FLOAT);
            SetRegInUse(allocator, REG_V2, TYP_FLOAT);
            Assert.That(GetConsecutiveCandidates(allocator, SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V3,
                first, out var busy), Is.EqualTo(SRBM_NONE));
            Assert.That(busy, Is.EqualTo(SRBM_V0 | SRBM_V2));
        });
    }

    [TestCase(SRBM_V31 | SRBM_V0, SRBM_V31)]
    [TestCase(SRBM_V0 | SRBM_V1 | SRBM_V2, SRBM_V0)]
    public static void AllocatorSelectsOnlyCompleteFreeSequences(regMask candidates, regMask expected)
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            codeGen.RegSet.rsClearRegsModified();
            var allocator = new LinearScan(compiler);
            BlockInfo(allocator) = [new LsraBlockInfo()];
            for (var register = REG_V0; register <= REG_V31; register++)
            {
                allocator.physRegs[(int)register].init(register);
            }
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            Assert.That(BuildNode(allocator, address), Is.Zero);
            Assert.That(BuildNode(allocator, load), Is.EqualTo(1));
            var first = allocator.refPositions.Find(position =>
                position.treeNode == load && position.regCount == 2)
                ?? throw new AssertionException("Missing consecutive first reference.");
            first.registerAssignment = candidates;

            Assert.That(GetConsecutiveCandidates(allocator, candidates, first, out _) & expected,
                Is.EqualTo(expected));
            Assert.That(first.isFixedRegRef, Is.False);
            var selected = AllocateConsecutive(allocator, first.getInterval(), first, true);
            Assert.Multiple(() => {
                Assert.That(selected, Is.EqualTo(expected == SRBM_V31 ? REG_V31 : REG_V0));
                Assert.That(first.registerAssignment, Is.EqualTo(expected));
            });
        }, minOpts: false);
    }

#if DEBUG
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void ConsecutiveStressFallbackExcludesFixedConflicts(
        bool delayRegFree, bool hasAvailablePair)
    {
        WithCompiler(false, false, (compiler, codeGen) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            codeGen.RegSet.rsClearRegsModified();
            var allocator = new LinearScan(compiler);
            BlockInfo(allocator) = [new LsraBlockInfo()];
            for (var register = REG_V0; register <= REG_V31; register++)
            {
                allocator.physRegs[(int)register].init(register);
            }

            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            Assert.That(BuildNode(allocator, address), Is.Zero);
            Assert.That(BuildNode(allocator, load), Is.EqualTo(1));
            var first = allocator.refPositions.Find(position =>
                position.treeNode == load && position.regCount == 2)
                ?? throw new AssertionException("Missing consecutive first reference.");
            first.registerAssignment = hasAvailablePair
                ? SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V3 | SRBM_V4
                : SRBM_V0 | SRBM_V1 | SRBM_V2;
            first.delayRegFree = delayRegFree;
            StressMask(allocator) = 3;
            InUse(allocator) = new regMaskTP(SRBM_V2);

            FixedLow(allocator) = SRBM_V0 | SRBM_V1;
            var nextFixed = NextFixed(allocator);
            nextFixed[(int)REG_V0] = first.nodeLocation;
            nextFixed[(int)REG_V1] = unchecked(first.nodeLocation + (delayRegFree ? 1u : 0u));

            if (hasAvailablePair)
            {
                Assert.That(AllocateConsecutive(allocator, first.getInterval(), first, true), Is.EqualTo(REG_V3));
                Assert.That(first.registerAssignment, Is.EqualTo(SRBM_V3));
            }
            else
            {
                var exception = Assert.Throws<FatalJitException>(
                    () => AllocateConsecutive(allocator, first.getInterval(), first, true));
                Assert.That(exception, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_SKIPPED));
                Assert.That(s_assertions, Has.Count.EqualTo(1));
                Assert.That(s_assertions[0], Does.Contain("starts != SRBM_NONE"));
            }
        }, captureAssertions: !hasAvailablePair);
    }

    [Test]
    public static void RegisterStressPreservesConsecutiveAndPredicateBanks()
    {
        WithCompiler(false, false, (compiler, _) => {
            compiler.compFloatingPointUsed = true;
            compiler.info.compNeedsConsecutiveRegisters = true;
            var allocator = new LinearScan(compiler);
            StressMask(allocator) = 3;
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1000);
            var load = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_AdvSimd_Arm64_Load2xVector128, TYP_INT, 16, address);
            Assert.That(BuildNode(allocator, address), Is.Zero);
            Assert.That(BuildNode(allocator, load), Is.EqualTo(1));
            var first = allocator.refPositions.Find(position =>
                position.treeNode == load && position.regCount == 2)
                ?? throw new AssertionException("Missing consecutive first reference.");

            Assert.Multiple(() => {
                Assert.That(StressLimit(allocator, null, TYP_FLOAT, SRBM_V0 | SRBM_V1 | SRBM_V10),
                    Is.EqualTo(SRBM_V0 | SRBM_V1));
                Assert.That(StressLimit(allocator, first, TYP_FLOAT, SRBM_V0 | SRBM_V1 | SRBM_V10),
                    Is.EqualTo(SRBM_V0 | SRBM_V1 | SRBM_V10));
                Assert.That(StressLimit(allocator, null, TYP_MASK, SRBM_P0 | SRBM_P8),
                    Is.EqualTo(SRBM_P0 | SRBM_P8));
            });
        });
    }
#endif

    [Test]
    public static void ScalableIntrinsicBuildsMaskDefinition()
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var intrinsic = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_GetFfrByte, TYP_BYTE, 16);
            Assert.That(BuildNode(allocator, intrinsic), Is.Zero);
            Assert.That(allocator.refPositions.Exists(
                position => (position.treeNode == intrinsic) &&
                    (position.refType is RefType.RefTypeDef)), Is.True);
        });
    }

    [TestCase(TYP_SHORT, SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V3 | SRBM_V4 | SRBM_V5 | SRBM_V6 | SRBM_V7)]
    [TestCase(TYP_DOUBLE, SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V3 | SRBM_V4 | SRBM_V5 | SRBM_V6 |
        SRBM_V7 | SRBM_V8 | SRBM_V9 | SRBM_V10 | SRBM_V11 | SRBM_V12 | SRBM_V13 | SRBM_V14 | SRBM_V15)]
    public static void IndexedSveVectorRestrictsOnlySelectedOperand(var_types elementType, regMask expected)
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var vector = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16,
                new GenTreeIntCon(TYP_INT, 1));
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_MultiplyBySelectedScalar,
                elementType, 16, vector, vector, new GenTreeIntCon(TYP_INT, 0));
            var category = HWIntrinsicInfo.lookupCategory(intrinsic.HWIntrinsicId);

            Assert.Multiple(() => {
                Assert.That(IntrinsicCandidates(allocator, intrinsic, 1, category), Is.EqualTo(SRBM_NONE));
                Assert.That(IntrinsicCandidates(allocator, intrinsic, 2, category), Is.EqualTo(expected));
            });
        });
    }

    [Test]
    public static void SveMaskedFmaUsesLowPredicateBank()
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var mask = new GenTreeMskCon(default);
            var vector = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16,
                new GenTreeIntCon(TYP_INT, 1));
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_FusedMultiplyAdd,
                TYP_INT, 16, mask, vector, vector, vector);
            var category = HWIntrinsicInfo.lookupCategory(intrinsic.HWIntrinsicId);

            Assert.That(IntrinsicCandidates(allocator, intrinsic, 1, category), Is.EqualTo(SRBM_LOWMASK));
        });
    }

    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    public static void EmbeddedFmaSelectsResultOperandForRmw(int overwrittenOperand, int expected)
    {
        WithCompiler(false, false, (_, _) => {
            var first = new GenTreeLclVar(TYP_SIMD16, 1);
            var second = new GenTreeLclVar(TYP_SIMD16, 2);
            var third = new GenTreeLclVar(TYP_SIMD16, 3);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_FusedMultiplyAdd,
                TYP_INT, 16, first, second, third);
            var user = overwrittenOperand == 0
                ? null
                : new GenTreeLclVar(TYP_SIMD16, overwrittenOperand, intrinsic);

            Assert.That(intrinsic.GetResultOpNumForRmwIntrinsic(user, first, second, third),
                Is.EqualTo(expected));
        });
    }

    [TestCase(NI_AdvSimd_Extract, 2, 2, false)]
    [TestCase(NI_Sve_Count16BitElements, 1, 1, false)]
    [TestCase(NI_Sve_ShiftRightArithmeticForDivide, 2, 2, false)]
    [TestCase(NI_Sve_Prefetch32Bit, 3, 3, false)]
    [TestCase(NI_Sve_SaturatingIncrementBy32BitElementCount, 3, 2, true)]
    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, 5, 4, true)]
    public static void VariableArm64ImmediateNeedsBranchTargetRegister(
        NamedIntrinsic id, int operandCount, int immediateIndex, bool paired)
    {
        WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            var operands = new GenTree[operandCount];
            for (var index = 1; index <= operandCount; index++)
            {
                operands[index - 1] = index == immediateIndex || (paired && index == immediateIndex + 1)
                    ? new GenTreeLclVar(TYP_INT, index)
                    : new GenTreeLclVar(TYP_SIMD16, index);
            }

            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16, operands);
            BuildIntrinsicImmediate(allocator, intrinsic, HWIntrinsicInfo.lookupCategory(id));
            Assert.That(allocator.intervals, Has.Count.EqualTo(1), id.ToString());
            Assert.That(allocator.intervals[0].isInternal, Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void IntervalsUseNativeRegisterBanks(bool debugEnC, bool hasPatchpoint)
    {
        WithCompiler(debugEnC, hasPatchpoint, (compiler, codeGen) => {
            var reserved = SRBM_R0 | SRBM_R19;
            codeGen.RegSet.rsMaskResvd = new regMaskTP(reserved);
            var allocator = new LinearScan(compiler);

            var integers = SRBM_ALLINT & ~(SRBM_PR | SRBM_FP | SRBM_LR | reserved);
            var floats = SRBM_ALLFLOAT;
            if (debugEnC)
            {
                integers &= ~SRBM_INT_CALLEE_SAVED | SRBM_ENC_CALLEE_SAVED;
                floats &= ~SRBM_FLT_CALLEE_SAVED;
            }

            (var_types Type, regMask Available, regMask CalleeSaved, regMask CallerSaved)[] banks =
            [
                (TYP_INT, integers, SRBM_INT_CALLEE_SAVED, SRBM_INT_CALLEE_TRASH),
                (TYP_REF, integers, SRBM_INT_CALLEE_SAVED, SRBM_INT_CALLEE_TRASH),
                (TYP_FLOAT, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_DOUBLE, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_SIMD16, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_SIMD, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_MASK, SRBM_ALLMASK, SRBM_MSK_CALLEE_SAVED, SRBM_MSK_CALLEE_TRASH),
            ];

            foreach (var bank in banks)
            {
                var interval = NewInterval(allocator, bank.Type);
                Assert.Multiple(() => {
                    Assert.That(interval.registerPreferences, Is.EqualTo(bank.Available), bank.Type.ToString());
                    Assert.That(LinearScan.calleeSaveRegs(bank.Type), Is.EqualTo(bank.CalleeSaved));
                    Assert.That(CallerSaveRegs(allocator, bank.Type), Is.EqualTo(bank.CallerSaved));
                    Assert.That(allocator.intervals[^1], Is.SameAs(interval));
                });
            }

            var indices = RegisterIndices(allocator);
            Assert.That(AvailableRegCount(allocator), Is.EqualTo((int)ACTUAL_REG_COUNT));
            Assert.That(indices, Has.Length.EqualTo((int)ACTUAL_REG_COUNT + 1));
            for (var index = 0; index < indices.Length; index++)
            {
                Assert.That(indices[index], Is.EqualTo((regNumber)index));
            }

            Assert.That(allocator.intervals, Has.Count.EqualTo(banks.Length));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCall")]
    private static extern int BuildCall(LinearScan allocator, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildNode")]
    private static extern int BuildNode(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildBlockStore")]
    private static extern int BuildBlockStore(LinearScan allocator, GenTreeBlk block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildRefPositionsForNode")]
    private static extern void BuildRefPositionsForNode(LinearScan allocator, GenTree tree, uint location);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getNextConsecutiveRefPosition")]
    private static extern RefPosition? NextConsecutive(LinearScan allocator, RefPosition position);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nextConsecutiveRefPositions")]
    private static extern ref System.Collections.Generic.Dictionary<RefPosition, RefPosition?>
        NextConsecutiveMap(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "filterConsecutiveCandidates")]
    private static extern regMask FilterConsecutiveCandidates(
        LinearScan allocator, regMask candidates, uint count, out regMask allCandidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getConsecutiveCandidates")]
    private static extern regMask GetConsecutiveCandidates(
        LinearScan allocator, regMask candidates, RefPosition first, out regMask busy);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "canAssignNextConsecutiveRegisters")]
    private static extern bool CanAssignNext(LinearScan allocator, RefPosition first, regNumber register);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignConsecutiveRegisters")]
    private static extern void AssignConsecutive(LinearScan allocator, RefPosition first, regNumber register);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setRegInUse")]
    private static extern void SetRegInUse(LinearScan allocator, regNumber register, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_consecutiveRegsInUseThisLocation")]
    private static extern ref regMask ConsecutiveInUse(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fixedRegsLow")]
    private static extern ref regMask FixedLow(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nextFixedRef")]
    private static extern ref uint[] NextFixed(LinearScan allocator);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsInUseThisLocation")]
    private static extern ref regMaskTP InUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateReg")]
    private static extern regNumber AllocateConsecutive(
        LinearScan allocator, Interval interval, RefPosition reference, bool needsConsecutive);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateRegisters")]
    private static extern void AllocateRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setBlockSequence")]
    private static extern void SetBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockNumber")]
    private static extern ref uint CurrentBlockNumber(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);

    private static readonly System.Collections.Generic.List<string> s_assertions = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int AltJitSkipOnAssert(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "stressLimitRegs")]
    private static extern regMask StressLimit(
        LinearScan allocator, RefPosition? reference, var_types type, regMask mask);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "getArm64IntrinsicCandidates")]
    private static extern regMask IntrinsicCandidates(LinearScan allocator,
        GenTreeHWIntrinsic intrinsic, int operandNumber, HWIntrinsicCategory category);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildArm64IntrinsicImmediate")]
    private static extern void BuildIntrinsicImmediate(LinearScan allocator,
        GenTree intrinsic, HWIntrinsicCategory category);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getKillSetForCall")]
    private static extern regMaskTP GetKillSetForCall(LinearScan allocator, GenTreeCall call);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getKillSetForNode")]
    private static extern regMaskTP GetKillSetForNode(LinearScan allocator, GenTree tree);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_needToKillFloatRegisters")]
    private static extern ref bool NeedToKillFloatRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "callerSaveRegs")]
    private static extern regMask CallerSaveRegs(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regIndices")]
    private static extern ref regNumber[] RegisterIndices(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableRegCount")]
    private static extern ref int AvailableRegCount(LinearScan allocator);

    internal static void WithCompiler(
        bool debugEnC, bool hasPatchpoint, Action<Compiler, CodeGen> action,
        bool minOpts = true, bool captureAssertions = false)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(captureAssertions ? &ee : null);
        var previousConfig = JitConfig;
        if (captureAssertions)
        {
            JitConfig = new JitConfigValues();
            AltJitSkipOnAssert(ref JitConfig) = 1;
            s_assertions.Clear();
        }
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.opts.compDbgEnC = debugEnC;
#if DEBUG
        compiler.info.compFullName = nameof(Arm64LinearScanConstructionTests);
#endif
        if (debugEnC)
        {
            flags.Set(JitFlags.JIT_FLAG_DEBUG_EnC);
        }
#if DEBUG
        if (captureAssertions)
        {
            flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
        }
#endif

        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            compiler.MethodHasPatchpoint = hasPatchpoint;
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
#if DEBUG
            if (captureAssertions)
            {
                JitConfig = previousConfig;
            }
#endif
        }
    }
}
#endif
