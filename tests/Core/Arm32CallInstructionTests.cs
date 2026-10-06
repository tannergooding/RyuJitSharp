// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32CallInstructionTests
{
    [Test]
    public static void DirectCallNodesMaterializeOutOfRangeTargets()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            var call = new GenTreeCall(TYP_VOID)
            {
                _callType = CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x2000,
                _directCallAddress = (void*)0x1234,
            };

            codeGen.InternalRegisters.Add(call, new regMaskTP(SRBM_R12));
            codeGen.genCall(call);

            var id = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("Missing direct call descriptor.");
            Assert.That(id.idIns(), Is.EqualTo(INS_blx));
            Assert.That(id.idReg3(), Is.EqualTo(REG_R12));
        });
    }

    [TestCase(false, INS_bl)]
    [TestCase(true, INS_b)]
    public static void DirectCallsRecordThumb2BranchesAndGcState(bool isJump, instruction expectedInstruction)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            compiler.opts.compReloc = true;
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);
            parameters.isJump = isJump;
            parameters.retSize = EA_GCREF;
            parameters.gcrefRegs = new(SRBM_R0 | SRBM_R4);
            parameters.byrefRegs = new(SRBM_R5);

            codeGen.Emitter.emitIns_Call(parameters);

            var id = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("Missing direct call descriptor.");
            Assert.That(id.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_T2_J3));
            Assert.That(id.idInsSize(), Is.EqualTo(ISZ_32BIT));
            Assert.That((nuint)id.idAddr().iiaAddr, Is.EqualTo((nuint)parameters.addr));
            Assert.That(id.idIsNoGC(), Is.EqualTo(isJump));
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(id.NativeLogicalSize, Is.EqualTo(48));
            Assert.That(ThisRefs(codeGen.Emitter), Is.EqualTo(SRBM_R0 | SRBM_R4));
            Assert.That(ThisByrefs(codeGen.Emitter), Is.EqualTo(SRBM_R5));
#if !DEBUG
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
            Assert.That(LastMemBarrier(codeGen.Emitter), Is.Null);
#endif
        });
    }

    [Test]
    public static void DirectCallWithoutLiveGcReferencesUsesTheBaseDescriptor()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);

            codeGen.Emitter.emitIns_Call(parameters);

            var id = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("Missing direct call descriptor.");
            Assert.That(id.idIns(), Is.EqualTo(INS_bl));
            Assert.That(id.idIsCall(), Is.True);
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(id.idIsLargeCall(), Is.False);
            Assert.That(id.NativeLogicalSize, Is.EqualTo(12));
            Assert.That(id.idCodeSize(), Is.EqualTo(4u));
#if !DEBUG
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
#endif
        });
    }

    [TestCase(false, INS_blx)]
    [TestCase(true, INS_bx)]
    public static void RegisterCallsRecordThumbBranchesAndClearRelocation(bool isJump, instruction expectedInstruction)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, EC_INDIR_R);
            parameters.ireg = REG_R7;
            parameters.isJump = isJump;
            parameters.noSafePoint = !isJump;
            LastMemBarrier(codeGen.Emitter) = new Emitter.instrDescJmp();

            codeGen.Emitter.emitIns_Call(parameters);

            var id = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("Missing register call descriptor.");
            Assert.That(id.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_T1_D2));
            Assert.That(id.idInsSize(), Is.EqualTo(ISZ_16BIT));
            Assert.That(id.idReg3(), Is.EqualTo(REG_R7));
            Assert.That(id.idIsNoGC(), Is.True);
            Assert.That(id.idIsDspReloc(), Is.False);
#if !DEBUG
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(2));
            Assert.That(LastMemBarrier(codeGen.Emitter), Is.Null);
#endif
        });
    }

    [TestCase(CORINFO_HELP_ASSIGN_REF, SRBM_R0 | SRBM_R3 | SRBM_LR | SRBM_R12)]
    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, SRBM_NONE)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, SRBM_NONE)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, SRBM_NONE)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, SRBM_INT_CALLEE_TRASH)]
    public static void NoGcCallMasksMatchTheArm32HelperContracts(CorInfoHelpFunc helper, regMask expected)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var mask = codeGen.Emitter.emitGetGCRegsKilledByNoGCCall(helper);
            Assert.That(mask, Is.EqualTo(new regMaskTP(expected)));
        });
    }

    private static EmitCallParams Parameters(Compiler compiler, EmitCallType type) => new()
    {
        callType = type,
        methHnd = (CORINFO_METHOD_STRUCT_*)0x2000,
        addr = type == EC_FUNC_TOKEN ? (void*)0x1234 : null,
        ptrVars = VarSetOps.MakeEmpty(compiler),
    };

    private static Emitter.instrDesc? LastInstruction(Emitter emitter)
        => Last(emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? Last(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentGroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastMemBarrier")]
    private static extern ref Emitter.instrDesc? PendingMemBarrier(Emitter emitter);

    private static int GroupSize(Emitter emitter) => CurrentGroupSize(emitter);

    private static ref Emitter.instrDesc? LastMemBarrier(Emitter emitter) => ref PendingMemBarrier(emitter);
}
#endif
