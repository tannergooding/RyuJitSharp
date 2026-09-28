// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.Globals;
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
                : new regMaskTP(SRBM_INT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH);
            Assert.That(GetKillSetForCall(allocator, new GenTreeCall(TYP_VOID)), Is.EqualTo(expected));
        });
    }

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getNextConsecutiveRefPosition")]
    private static extern RefPosition? NextConsecutive(LinearScan allocator, RefPosition position);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "getArm64IntrinsicCandidates")]
    private static extern regMask IntrinsicCandidates(LinearScan allocator,
        GenTreeHWIntrinsic intrinsic, int operandNumber, HWIntrinsicCategory category);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildArm64IntrinsicImmediate")]
    private static extern void BuildIntrinsicImmediate(LinearScan allocator,
        GenTree intrinsic, HWIntrinsicCategory category);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getKillSetForCall")]
    private static extern regMaskTP GetKillSetForCall(LinearScan allocator, GenTreeCall call);

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

    private static void WithCompiler(bool debugEnC, bool hasPatchpoint, Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compDbgEnC = debugEnC;
#if DEBUG
        compiler.info.compFullName = nameof(Arm64LinearScanConstructionTests);
#endif
        if (debugEnC)
        {
            flags.Set(JitFlags.JIT_FLAG_DEBUG_EnC);
        }

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
        }
    }
}
#endif
