// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenReturnTests
{
    [TestCase(TYP_BYTE, INS_movsx)]
    [TestCase(TYP_UBYTE, INS_movzx)]
    [TestCase(TYP_SHORT, INS_movsx)]
    [TestCase(TYP_USHORT, INS_movzx)]
    [TestCase(TYP_INT, INS_mov)]
    [TestCase(TYP_LONG, INS_mov)]
    [TestCase(TYP_REF, INS_mov)]
    [TestCase(TYP_BYREF, INS_mov)]
    [TestCase(TYP_FLOAT, INS_movaps)]
    [TestCase(TYP_DOUBLE, INS_movaps)]
    public static void ReturnsMoveToTheAbiRegisterAndRestoreGcRoots(var_types type, instruction expected)
    {
        WithReturn(type, (compiler, codeGen) =>
        {
            var sourceReg = varTypeUsesFloatReg(type) ? REG_XMM1 : REG_RCX;
            GenTree source = varTypeUsesFloatReg(type)
                ? compiler.gtNewDconNode(type, -0.0)
                : Register(compiler, type, sourceReg);
            source.RegNum = sourceReg;
            codeGen.GCInfo.gcMarkRegPtrVal(sourceReg, type);
            var tree = new GenTreeUnOp(GT_RETURN, type, source);

            codeGen.genReturn(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(varTypeUsesFloatReg(type) ? REG_XMM0 : REG_RAX));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(sourceReg));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_RAX : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? RBM_RAX : RBM_NONE));
        });
    }

    [TestCase(GT_RETURN, TYP_VOID)]
    [TestCase(GT_RETFILT, TYP_VOID)]
    [TestCase(GT_RETFILT, TYP_INT)]
    public static void VoidAndFilterReturnsPreserveTheirNativeEffects(genTreeOps oper, var_types type)
    {
        WithReturn(oper == GT_RETURN ? type : TYP_REF, (compiler, codeGen) =>
        {
            var source = type == TYP_VOID ? null : Register(compiler, type, REG_RCX);
            codeGen.genReturn(new GenTreeUnOp(oper, type, source));

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(type == TYP_VOID ? 0 : 1));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_REF)]
    public static void FieldListReturnsUseDescriptorTypesAndRegisters(var_types type)
    {
        WithReturn(type, (compiler, codeGen) =>
        {
            GenTree source = type == TYP_FLOAT
                ? compiler.gtNewDconNode(type, 1.0)
                : Register(compiler, type, REG_RCX);
            source.RegNum = type == TYP_FLOAT ? REG_XMM1 : REG_RCX;
            var fields = new GenTreeFieldList();
            fields.AddField(compiler, source, 0, type);
            var tree = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, fields);
            Assert.That(CodeGen.isStructReturn(tree), Is.True);

            codeGen.genReturn(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idReg1(), Is.EqualTo(type == TYP_FLOAT ? REG_XMM0 : REG_RAX));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_RAX : RBM_NONE));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AsyncReturnsClearAndReportTheContinuationAfterReturningTheValue(bool profiler)
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ASYNC);
            if (profiler)
            {
                ProfilerHookNeeded(compiler) = true;
                PrepareProfiler(compiler, codeGen, finalLayout: true, framePointer: true, indirect: false);
            }
            var source = Register(compiler, TYP_REF, REG_RCX);

            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_REF, source));

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo(profiler
                ? (instruction[])[INS_mov, INS_mov, INS_lea, INS_call, INS_xor]
                : [INS_mov, INS_xor]));
            Assert.That(Descriptors(codeGen)[^1].idReg1(), Is.EqualTo(REG_ASYNC_CONTINUATION_RET));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(RBM_RAX | new regMaskTP(REG_ASYNC_CONTINUATION_RET.SingleTypeMask)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReturnBufferAddressesRemainByrefsForTheProfiler(bool alias)
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            ProfilerHookNeeded(compiler) = true;
            compiler.info.compRetBuffArg = 0;
            PrepareProfiler(compiler, codeGen, finalLayout: true, framePointer: true, indirect: false);
            var source = Register(compiler, TYP_BYREF, alias ? REG_RAX : REG_RBX);

            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_BYREF, source));

            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_RAX));
            Assert.That(Descriptors(codeGen)[^1].idIns(), Is.EqualTo(INS_call));
            Assert.That(compiler.info.compProfilerCallback, Is.True);
        });
    }

    [Test]
    public static void ProfilerCallbacksRetainHandleAndFrameAddressForms(
        [Values(false, true)] bool finalLayout, [Values(false, true)] bool framePointer,
        [Values(false, true)] bool indirect,
        [Values(CORINFO_HELP_PROF_FCN_LEAVE, CORINFO_HELP_PROF_FCN_TAILCALL)] CorInfoHelpFunc helper)
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            ProfilerHookNeeded(compiler) = true;
            PrepareProfiler(compiler, codeGen, finalLayout, framePointer, indirect);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);

            codeGen.genProfilingLeaveCallback(helper);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov, INS_lea, INS_call]));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(descriptors[0].idIsDspReloc(), Is.EqualTo(indirect));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_RDX));
            if (finalLayout)
            {
                Assert.That(descriptors[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(framePointer ? REG_RBP : REG_RSP));
                Assert.That(descriptors[1].idAddr().iiaAddrMode.amDisp, Is.EqualTo(framePointer ? 16 : 88));
            }
            else
            {
                Assert.That(descriptors[1].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_SRD));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
            Assert.That(descriptors[^1].idIsNoGC(), Is.True);
        });
    }

    [Test]
    public static void AliasedReturnsAndDisabledProfilerCallbacksDoNotAddInstructions()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            var source = Register(compiler, TYP_REF, REG_RAX);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);

            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_REF, source));
            codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
        });
    }

    [TestCase(false, 0, 528, -16)]
    [TestCase(true, 0, 0, -544)]
    [TestCase(false, 32, 32, -512)]
    [TestCase(true, 512, 240, -304)]
    public static void FrameDeltasPreserveNativeSignsAndEncLocallocPrecedence(
        bool enc, int outgoingSize, int spToFp, int callerToFp)
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = true;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 512;
            compiler.opts.compDbgEnC = enc;
            compiler.compLocallocUsed = outgoingSize != 0;
            compiler.lvaOutgoingArgSpaceSize.Value = outgoingSize;

            Assert.That(codeGen.genSPtoFPdelta, Is.EqualTo(spToFp));
            Assert.That(codeGen.genCallerSPtoInitialSPdelta, Is.EqualTo(-544));
            Assert.That(codeGen.genCallerSPtoFPdelta, Is.EqualTo(callerToFp));
            Assert.That(compiler.lvaToCallerSPRelativeOffset(24, isFpBased: true), Is.EqualTo(callerToFp + 24));
            Assert.That(compiler.lvaToCallerSPRelativeOffset(24, isFpBased: false), Is.EqualTo(-520));

            PatchpointInfo patchpoint = default;
            patchpoint.Initialize(0, 96);
            compiler.info.compPatchpointInfo = &patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            Assert.That(compiler.lvaToCallerSPRelativeOffset(24, true, forRootFrame: false), Is.EqualTo(callerToFp + 24));
            Assert.That(compiler.lvaToCallerSPRelativeOffset(24, true), Is.EqualTo(callerToFp + 24 - 104));
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(32)]
    public static void StackChecksBranchAroundTheBreakpointAndDefineTheContinuation(int offset)
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            compiler.lvaSetVarDoNotEnregister(0, DoNotEnregisterReason.ReturnSpCheck);
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genStackPointerCheck(true, 0, offset, REG_R11);

            var saved = firstGroup?.igData ?? throw new AssertionException("Missing stack check group.");
            Assert.That(saved.Select(id => id.idIns()), Is.EqualTo(offset == 0
                ? (instruction[])[INS_cmp, INS_je, INS_int3]
                : [INS_mov, INS_sub, INS_cmp, INS_je, INS_int3]));
            var target = EmitterJumpInstructionTests.JumpView.Target(saved[^2]);
            Assert.That(target?.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReturnStackChecksOnlyRunInTheRootFunction(bool funclet)
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            compiler.opts.compStackCheckOnRet = true;
            compiler.lvaReturnSpCheck = 0;
            compiler.lvaSetVarDoNotEnregister(0, DoNotEnregisterReason.ReturnSpCheck);
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            compiler.funCurrentFunc().funKind = funclet ? FuncKind.FUNC_HANDLER : FuncKind.FUNC_ROOT;
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genReturn(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null));

            Assert.That(firstGroup?.igData is not null, Is.EqualTo(!funclet));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [Test]
    public static void DisassemblyRejectsBeforeConsumingTheReturnOrSettingProfilerState()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            var source = Register(compiler, TYP_REF, REG_RCX);
            var tree = new GenTreeUnOp(GT_RETURN, TYP_REF, source);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, TYP_REF);
            compiler.opts.dspCode = true;

            _ = Assert.Throws<FatalJitException>(() => codeGen.genReturn(tree));
            _ = Assert.Throws<FatalJitException>(() => codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RCX));
            Assert.That(compiler.info.compProfilerCallback, Is.False);
        });
    }
#endif

    private static void PrepareProfiler(Compiler compiler, CodeGen codeGen, bool finalLayout, bool framePointer, bool indirect)
    {
        compiler.compCalleeRegsPushed = 2;
        compiler.compLclFrameSize = 64;
        codeGen.IsFramePointerUsed = framePointer;
        compiler.lvaOutgoingArgSpaceVar = compiler.lvaGrabTemp(false, "Outgoing profiler arguments");
        compiler.lvaOutgoingArgSpaceSize.Value = 32;
        compiler.lvaDoneFrameLayout = finalLayout ? Compiler.FINAL_FRAME_LAYOUT : Compiler.INITIAL_FRAME_LAYOUT;
        compiler.lvaTable[0].lvIsParam = true;
        compiler.compProfilerMethHnd = unchecked((void*)(nint)(indirect ? 0x1234 : -1));
        compiler.compProfilerMethHndIndirected = indirect;
    }

    internal static void WithReturn(var_types type, Action<Compiler, CodeGen> action)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.compHndBBtab = [];
            _ = compiler.fgCreateFunclets();
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.info.compRetNativeType = type;
            compiler.compRetTypeDesc = new ReturnTypeDesc();
            compiler.compRetTypeDesc.InitializeReturnType(compiler, type, NO_CLASS_HANDLE, compiler.info.compCallConv);
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
}
