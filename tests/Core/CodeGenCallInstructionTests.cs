// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenCallInstructionTests
{
    [TestCase(TYP_VOID, false, false)]
    [TestCase(TYP_REF, false, false)]
    [TestCase(TYP_REF, true, false)]
    [TestCase(TYP_BYREF, false, false)]
    [TestCase(TYP_BYREF, true, false)]
    [TestCase(TYP_DOUBLE, false, false)]
    [TestCase(TYP_REF, false, true)]
    public static void DirectCallsRecordReturnGcStateAndAsyncContinuation(var_types type, bool unused, bool async)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var call = DirectCall(type);
            call.IsUnusedValue = unused;
            if (async)
            {
                call._callMoreFlags |= GTF_CALL_M_ASYNC;
            }
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R12, TYP_BYREF);

            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_call));
            Assert.That(id.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_METHOD));
            Assert.That(EmitterCallInstructionTests.CallView.AsyncReturn(id), Is.EqualTo(async));
            Assert.That(EmitterCallInstructionTests.CallView.Refs(id), Is.EqualTo(RBM_RBX));
            Assert.That(EmitterCallInstructionTests.CallView.Byrefs(id), Is.EqualTo(RBM_R12));
            Assert.That(ThisRefs(codeGen.Emitter), Is.EqualTo(
                SRBM_RBX | ((!unused && type == TYP_REF) ? SRBM_RAX : 0)));
            Assert.That(ThisByrefs(codeGen.Emitter), Is.EqualTo(
                SRBM_R12 | ((!unused && type == TYP_BYREF) ? SRBM_RAX : 0)));
#if DEBUG
            var debugInfo = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing call diagnostics.");
            Assert.That(debugInfo.idMemCookie, Is.EqualTo((nint)call._callMethHnd));
            Assert.That(debugInfo.idCallSig?.Value.numArgs, Is.EqualTo(3));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DirectUserAndHelperTailCallsRecordJumps(bool helper)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            if (helper)
            {
                call._callType = CT_HELPER;
                call._callMethHnd = Compiler.eeFindHelper(CORINFO_HELP_STOP_FOR_GC);
            }

            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_l_jmp));
            Assert.That(id.idIsNoGC(), Is.True);
#if DEBUG
            Assert.That(id.idDebugOnlyInfo()?.idCallSig is null, Is.EqualTo(helper));
#endif
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void RegisterTargetsAreConsumedBeforeCallsButNotAgainInEpilogs(bool tailCall, bool indirect)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var target = RegisterTarget(REG_R11);
            var call = DirectCall(TYP_VOID);
            call._callType = indirect ? CT_INDIRECT : CT_USER_FUNC;
            call.ControlExpr = target;
            if (tailCall)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
                _ = codeGen.genConsumeReg(target);
            }

            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(tailCall ? INS_tail_i_jmp : INS_call));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R11));
#if DEBUG
            Assert.That(target._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(id.idDebugOnlyInfo()?.idMemCookie,
                Is.EqualTo(indirect ? (nint)0 : (nint)call._callMethHnd));
#endif
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void MemoryTargetsPreserveAddressingAndTailCallConsumption(bool tailCall, bool indexOnly)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var index = RegisterTarget(REG_R11);
            var address = new GenTreeAddrMode(TYP_I_IMPL,
                indexOnly ? null : RegisterTarget(REG_R10), index, 4, -48)
            {
                IsContained = true,
            };
            var call = DirectCall(TYP_VOID);
            call._callType = CT_INDIRECT;
            call.ControlExpr = new GenTreeIndir(GT_IND, TYP_I_IMPL, address) { IsContained = true };
            if (tailCall)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
                codeGen.genConsumeAddress(address);
            }

            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(tailCall ? INS_tail_i_jmp : INS_call));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(indexOnly ? REG_NA : REG_R10));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_R11));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(2));
            Assert.That(id.idAddr().iiaAddrMode.amDisp, Is.EqualTo(-48));
#if DEBUG
            Assert.That(index._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AbsoluteContainedTargetsUseTokenIndirection(bool tailCall)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
#endif
            var address = new GenTreeIntCon(TYP_I_IMPL, 0x1234) { IsContained = true };
            var call = DirectCall(TYP_VOID);
            call._callType = CT_INDIRECT;
            call.ControlExpr = new GenTreeIndir(GT_IND, TYP_I_IMPL, address) { IsContained = true };
            if (tailCall)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }

            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_METHPTR));
            Assert.That(id.idIns(), Is.EqualTo(tailCall ? INS_tail_i_jmp : INS_call));
            Assert.That((nint)id.idAddr().iiaAddr, Is.EqualTo((nint)0x1234));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndirectionCellsReuseTheirAbiArgumentRegister(bool r2r)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.virtualStubParamInfo = new Compiler.VirtualStubParamInfo();
            var call = DirectCall(TYP_VOID);
            var reg = r2r ? REG_R2R_INDIRECT_PARAM : compiler.virtualStubParamInfo.Reg;
            var kind = r2r ? WellKnownArg.R2RIndirectionCell : WellKnownArg.VirtualStubCell;
            if (r2r)
            {
                call._entryPoint.accessType = InfoAccessType.IAT_PVALUE;
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }
            else
            {
                call.Flags |= GTF_CALL_VIRT_STUB;
            }
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                new GenTreeUnOp(GT_PUTARG_REG, TYP_I_IMPL, RegisterTarget(reg)) { RegNum = reg })
                .WithWellKnownArg(kind));
            argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(reg, 0, 8));

            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(reg));
            Assert.That(id.idIns(), Is.EqualTo(r2r ? INS_tail_i_jmp : INS_call));
        });
    }

    [Test]
    public static void NativeAotTlsCallsRetainBothPrefixesAndTheLinkerSentinel()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            var call = DirectCall(TYP_I_IMPL);
            call.Flags |= GTF_TLS_GET_ADDR;
            call.ControlExpr = new GenTreeIntCon(TYP_I_IMPL, 0x3456) { IsContained = true };

            codeGen.genCallInstruction(call);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(
                (instruction[])[INS_data16, INS_data16, INS_call]));
            Assert.That((nint)ids[^1].idAddr().iiaAddr, Is.EqualTo((nint)0x3456));
            Assert.That(ids[^1].idIsNoGC(), Is.True);
#if DEBUG
            Assert.That(ids[^1].idDebugOnlyInfo()?.idMemCookie, Is.EqualTo((nint)1));
#endif
        });
    }

    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL)]
    [TestCase(CORINFO_HELP_DISPATCH_INDIRECT_CALL)]
    public static void CfgHelpersRetainTheirContainedMemoryCallForm(CorInfoHelpFunc helper)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ENABLE_CFG);
            var call = DirectCall(TYP_VOID);
            call._callType = CT_HELPER;
            call._callMethHnd = Compiler.eeFindHelper(helper);
            call.ControlExpr = new GenTreeIndir(GT_IND, TYP_I_IMPL, RegisterTarget(REG_R11))
            {
                IsContained = true,
            };

            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_call));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R11));
        });
    }

#if DEBUG
    [TestCase(false, true, false, REG_RBX)]
    [TestCase(true, false, false, REG_R10)]
    [TestCase(true, true, false, REG_R11)]
    [TestCase(true, true, true, REG_R10)]
    [TestCase(true, true, false, REG_XMM0)]
    public static void EpilogChecksRetainTheCookieAndSecretArgumentRegisterChoices(
        bool tailCall, bool cookie, bool secret, regNumber reg)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            if (tailCall)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }
            compiler.compNeedsGSSecurityCookie = cookie;
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(RegisterTarget(reg)));
            argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(reg, 0, 8));
            if (secret)
            {
                var secretArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(RegisterTarget(REG_R10))
                    .WithWellKnownArg(WellKnownArg.SecretStubParam));
                secretArg.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(REG_R10, 0, 8));
            }

            codeGen.genCallInstruction(call);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void DisassemblyRejectsBeforeCallTargetConsumption()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            var target = RegisterTarget(REG_R11);
            call.ControlExpr = target;
            compiler.opts.dspCode = true;

            _ = Assert.Throws<FatalJitException>(() => codeGen.genCallInstruction(call));

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(target._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
            compiler.opts.dspCode = false;
            codeGen.genCallInstruction(call);
        });
    }
#endif

    private static GenTreeCall DirectCall(var_types type)
    {
        var call = new GenTreeCall(type)
        {
            _callType = CT_USER_FUNC,
            _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x4000,
            _directCallAddress = (void*)0x1234,
            _returnType = type,
        };
#if DEBUG
        call._callSig.numArgs = 3;
#endif
        return call;
    }

    private static GenTreePhysReg RegisterTarget(regNumber reg)
        => new(reg, TYP_I_IMPL) { RegNum = reg };

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);
}
