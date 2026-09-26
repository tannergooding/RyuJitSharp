// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenCallTests
{
    [TestCase(TYP_INT, REG_RAX, REG_RAX)]
    [TestCase(TYP_INT, REG_RCX, REG_RAX)]
    [TestCase(TYP_LONG, REG_RBX, REG_RAX)]
    [TestCase(TYP_REF, REG_RBX, REG_RAX)]
    [TestCase(TYP_BYREF, REG_RBX, REG_RAX)]
    [TestCase(TYP_FLOAT, REG_XMM0, REG_XMM0)]
    [TestCase(TYP_FLOAT, REG_XMM2, REG_XMM0)]
    [TestCase(TYP_DOUBLE, REG_XMM2, REG_XMM0)]
    public static void CallsMoveAbiResultsBeforeProducingTheAllocatedRegister(
        var_types type, regNumber allocated, regNumber abi)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var call = Call(type, allocated);
            codeGen.genCall(call);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(allocated == abi ? 1 : 2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_call));
            if (allocated != abi)
            {
                Assert.That(descriptors[1].idReg1(), Is.EqualTo(allocated));
                Assert.That(descriptors[1].idReg2(), Is.EqualTo(abi));
                Assert.That(descriptors[1].idOpSize(),
                    Is.EqualTo(varTypeIsGC(type) ? emitAttr.EA_PTRSIZE : type.EmitActualSize));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(type == TYP_REF ? Mask(allocated) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(type == TYP_BYREF ? Mask(allocated) : default));
#if DEBUG
            Assert.That(call._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED));
#endif
        });
    }

    [TestCase(TYP_REF, false, false)]
    [TestCase(TYP_REF, false, true)]
    [TestCase(TYP_REF, true, false)]
    [TestCase(TYP_REF, true, true)]
    [TestCase(TYP_BYREF, false, false)]
    [TestCase(TYP_BYREF, true, false)]
    public static void TerminalReturnRootsRemainVisibleOnlyWithoutOptimization(
        var_types type, bool minopts, bool hasNext)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = Call(type, REG_RAX);
            if (hasNext)
            {
                call.Next = new GenTree(GT_NOP, TYP_VOID);
            }

            codeGen.genCall(call);

            var live = minopts || hasNext;
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(live && type == TYP_REF ? Mask(REG_RAX) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(live && type == TYP_BYREF ? Mask(REG_RAX) : default));
        }, minopts);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NullChecksFollowArgumentPlacementAndPrecedeTransfers(bool tail)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = Call(TYP_VOID);
            call.Flags |= GTF_CALL_NULLCHECK;
            if (tail)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }
            var argument = new GenTreeUnOp(GT_PUTARG_REG, TYP_REF, Physical(TYP_REF, REG_RDX))
            {
                RegNum = REG_RDX,
            };
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(argument));
            arg.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(REG_RCX, 0, 8));
            arg.EarlyNode = null;
            arg.LateNode = argument;
            call.Args.PushLateBack(arg);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RDX, TYP_REF);

            codeGen.genCall(call);

            var descriptors = Descriptors(codeGen);
            instruction[] expected = tail ? [INS_mov, INS_cmp] : [INS_mov, INS_cmp, INS_call];
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo(expected));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(descriptors[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(emitAttr.EA_4BYTE));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(tail ? Mask(REG_RCX) : default));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FastTailCallsConsumeTargetsButDeferTransfersAndPendingLabels(bool memory)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var call = Call(TYP_VOID);
            call._callType = CT_INDIRECT;
            call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            var label = new BasicBlock(null, null);
            PendingLabel(codeGen) = label;
            var baseNode = Physical(memory ? TYP_REF : TYP_I_IMPL, REG_R10);
            var index = Physical(TYP_BYREF, REG_R11);
            call.ControlExpr = memory
                ? new GenTreeIndir(GT_IND, TYP_I_IMPL,
                    new GenTreeAddrMode(TYP_BYREF, baseNode, index, 1, 8) { IsContained = true })
                    { IsContained = true }
                : baseNode;
            if (memory)
            {
                codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_REF);
                codeGen.GCInfo.gcMarkRegPtrVal(REG_R11, TYP_BYREF);
#if DEBUG
                index.UseNum = 1;
#endif
            }

            codeGen.genCall(call);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(PendingLabel(codeGen), Is.SameAs(label));
            Assert.That(label.bbEmitCookie, Is.Null);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(memory ? Mask(REG_R10) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(memory ? Mask(REG_R11) : default));
#if DEBUG
            Assert.That(baseNode._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
#endif
            codeGen.genCallInstruction(call);
            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_tail_i_jmp));
        });
    }

    [Test]
    public static void PInvokeClearsLazyEmitterRootsAtThePreCallLabel()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var before = emitter.emitAddLabel(VarSetOps.MakeEmpty(compiler), Mask(REG_RAX), default);
            emitter.emitIns(INS_nop);
            var call = Call(TYP_VOID);
            call.Flags |= GTF_CALL_UNMANAGED;
            call._callMoreFlags |= GTF_CALL_M_PINVOKE;

            codeGen.genCall(call);

            Assert.That(emitter.emitCurIG, Is.Not.SameAs(before));
            Assert.That(before.igNext, Is.SameAs(emitter.emitCurIG));
            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_call));
            Assert.That(EmitterRefs(emitter), Is.EqualTo(SRBM_NONE));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void WideVectorMethodsClearUpperLanesOnlyForCallsThatNeedIt(bool wide, bool pinvoke)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.Contains256BitOrMoreAvxInstruction = wide;
            var call = Call(TYP_VOID);
            if (pinvoke)
            {
                call._callMoreFlags |= GTF_CALL_M_PINVOKE;
            }

            codeGen.genCall(call);

            instruction[] expected = wide && pinvoke ? [INS_vzeroupper, INS_call] : [INS_call];
            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo(expected));
        });
    }

    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL)]
    [TestCase(CORINFO_HELP_VIRTUAL_FUNC_PTR)]
    [TestCase(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT)]
    [TestCase(CORINFO_HELP_MEMSET)]
    [TestCase(CORINFO_HELP_MEMCPY)]
    public static void InterveningHelpersKeepThePendingLabelForTheRealCall(CorInfoHelpFunc helper)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var label = new BasicBlock(null, null);
            PendingLabel(codeGen) = label;
            var helperCall = Call(TYP_VOID);
            helperCall._callType = CT_HELPER;
            helperCall._callMethHnd = Compiler.eeFindHelper(helper);

            codeGen.genCall(helperCall);

            Assert.That(PendingLabel(codeGen), Is.SameAs(label));
            Assert.That(label.bbEmitCookie, Is.Null);
            codeGen.genCall(Call(TYP_VOID));
            Assert.That(PendingLabel(codeGen), Is.Null);
            Assert.That(label.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PendingLabelsFollowCallsButPrecedeReturnMoves(bool helper)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var label = new BasicBlock(null, null);
            PendingLabel(codeGen) = label;
            var call = Call(TYP_INT, REG_RCX);
            if (helper)
            {
                call._callType = CT_HELPER;
                call._callMethHnd = Compiler.eeFindHelper(CORINFO_HELP_STOP_FOR_GC);
            }
            var before = codeGen.Emitter.emitCurIG;

            codeGen.genCall(call);

            Assert.That(PendingLabel(codeGen), Is.Null);
            Assert.That(before?.igData?.Single().idIns(), Is.EqualTo(INS_call));
            Assert.That(label.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_mov));
        });
    }

    [Test]
    public static void Simd12UpperClearPreservesTheLowerThreeElements()
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            codeGen.genSimd12UpperClear(REG_XMM1);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_insertps));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)(sbyte)-8));
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsCallAndUpdatesCallState()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = Call(TYP_REF, REG_RAX);
            var label = new BasicBlock(null, null);
            PendingLabel(codeGen) = label;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.runWithSPMIErrorTrap = &InstructionRecordingTestSupport.UnavailableMethodMetadata;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.opts.dspCode = true;
            var first = codeGen.Emitter.emitCurIG;

            var diagnostic = InstructionRecordingTestSupport.Capture(() => codeGen.genCall(call));
            Assert.That(CodeGenLocalHeapTests.AllDescriptors(first, codeGen).Exists(id => id.idIns() == INS_call), Is.True);
            Assert.That(diagnostic, Does.Contain("call"));
            Assert.That(diagnostic, Does.Contain("<unknown method>"));
        });
    }
#endif

    private static GenTreeCall Call(var_types type, regNumber reg = REG_NA) => new(type)
    {
        _callType = CT_USER_FUNC,
        _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x4000,
        _directCallAddress = (void*)0x1234,
        _returnType = type,
        RegNum = reg,
    };

    private static GenTreePhysReg Physical(var_types type, regNumber reg) => new(reg, type) { RegNum = reg };

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genPendingCallLabel")]
    private static extern ref BasicBlock? PendingLabel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask EmitterRefs(Emitter emitter);
}
