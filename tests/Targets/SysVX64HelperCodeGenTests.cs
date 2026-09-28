// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64HelperCodeGenTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ReturnTrapBranchesAroundHelperWithAssignedTarget(bool indirect)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, emitter) =>
        {
            using var callbacks = new HelperCallbacks(compiler, indirect);
            var codeGen = ActiveCodeGen(compiler);
            var data = compiler.gtNewIconNode(TYP_INT, 1);
            data.RegNum = REG_RAX;
            var trap = new GenTreeUnOp(GT_RETURNTRAP, TYP_VOID, data);
            codeGen.InternalRegisters.Add(trap, RBM_R11);

            codeGen.genCodeForReturnTrap(trap);

            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_STOP_FOR_GC));
            var group = emitter.emitCurIG?.igPrev ?? throw new AssertionException("Missing trap group.");
            var ins = group.igData?.Select(id => id.idIns()).ToArray();
            Assert.That(ins, Is.EqualTo(indirect
                ? (instruction[])[INS_cmp, INS_je, INS_mov, INS_call]
                : [INS_cmp, INS_je, INS_call]));
        });
    }

    [TestCase(false, CORINFO_HELP_CHECKED_ASSIGN_REF)]
    [TestCase(true, CORINFO_HELP_ASSIGN_REF)]
    public static void WriteBarrierGeneratesMatchingHelperWithoutStackArguments(
        bool uncheckedBarrier, CorInfoHelpFunc helper)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, emitter) =>
        {
            using var callbacks = new HelperCallbacks(compiler, indirect: false);
            ActiveCodeGen(compiler).genGCWriteBarrier(uncheckedBarrier
                ? GCInfo.WriteBarrierForm.WBF_BarrierUnchecked
                : GCInfo.WriteBarrierForm.WBF_BarrierChecked);

            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(HelperCallbacks.Helper, Is.EqualTo(helper));
            var id = Last(emitter);
            Assert.That(id.idIns(), Is.EqualTo(INS_call));
            Assert.That(id.idIsNoGC(), Is.True);
        });
    }

    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, false)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, true)]
    public static void LeaveAndTailcallCallbacksPreserveSysVReturnRegisters(
        CorInfoHelpFunc helper, bool indirectHandle)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, emitter) =>
        {
            using var callbacks = new HelperCallbacks(compiler, indirect: false);
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHndIndirected = indirectHandle;
            compiler.compProfilerMethHnd = (void*)0x1234;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
            var codeGen = ActiveCodeGen(compiler);
            codeGen.IsFramePointerUsed = true;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RDX, TYP_BYREF);

            codeGen.genProfilingLeaveCallback(helper);

            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(HelperCallbacks.Helper, Is.EqualTo(helper));
            Assert.That(Instructions(emitter), Is.EqualTo((instruction[])[INS_mov, INS_lea, INS_call]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(new regMaskTP(SRBM_RAX)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(new regMaskTP(SRBM_RDX)));
            Assert.That(compiler.info.compProfilerCallback, Is.True);
        });
    }

    [TestCase(REG_RAX, false)]
    [TestCase(REG_RDI, true)]
    [TestCase(REG_RSI, true)]
    [TestCase(REG_R11, false)]
    [TestCase(REG_R14, false)]
    [TestCase(REG_R15, false)]
    [TestCase(REG_RDX, true)]
    [TestCase(REG_RBX, true)]
    public static void EnterCallbackPreservesSysVArgumentsWithoutWindowsHoming(
        regNumber initReg, bool remainsZero)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, emitter) =>
        {
            using var callbacks = new HelperCallbacks(compiler, indirect: false);
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHnd = (void*)0x1234;
            compiler.lvaOutgoingArgSpaceVar = 0;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            var codeGen = ActiveCodeGen(compiler);
            codeGen.IsFramePointerUsed = true;
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing prolog group.");
            group.igFlags |= InsGroupFlags.Prolog;
            var zeroed = true;

            codeGen.genProfilingEnterCallback(initReg, ref zeroed);

            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_PROF_FCN_ENTER));
            Assert.That(Instructions(emitter), Is.EqualTo((instruction[])[INS_mov, INS_lea, INS_call]));
            var descriptors = CurrentDescriptors(emitter) ?? throw new AssertionException("Missing instructions.");
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R14));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R15));
            Assert.That(zeroed, Is.EqualTo(remainsZero));
        });
    }

    [Test]
    public static void IndirectEnterCallbackLoadsProfilerHandleIntoDedicatedRegister()
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, emitter) =>
        {
            using var callbacks = new HelperCallbacks(compiler, indirect: false);
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHndIndirected = true;
            compiler.compProfilerMethHnd = (void*)0x1234;
            compiler.lvaOutgoingArgSpaceVar = 0;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            var codeGen = ActiveCodeGen(compiler);
            codeGen.IsFramePointerUsed = true;
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing prolog group.");
            group.igFlags |= InsGroupFlags.Prolog;
            var zeroed = true;

            codeGen.genProfilingEnterCallback(REG_RDI, ref zeroed);

            Assert.That(HelperCallbacks.Assertions, Is.Zero);
            Assert.That(HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_PROF_FCN_ENTER));
            Assert.That(Instructions(emitter), Is.EqualTo((instruction[])[INS_mov, INS_lea, INS_call]));
            var descriptors = CurrentDescriptors(emitter) ?? throw new AssertionException("Missing instructions.");
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R14));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R15));
            Assert.That(zeroed, Is.True);
        });
    }

    private static CodeGen ActiveCodeGen(Compiler compiler)
    {
        return compiler.codeGen as CodeGen ?? throw new AssertionException("Missing code generator.");
    }

    private static instruction[] Instructions(Emitter emitter)
    {
        return CurrentDescriptors(emitter)?.Select(id => id.idIns()).ToArray()
            ?? throw new AssertionException("Missing instructions.");
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return CurrentDescriptors(emitter)?.LastOrDefault() ?? throw new AssertionException("Missing instruction.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);

    internal sealed class HelperCallbacks : IDisposable
    {
        private readonly ICorJitInfo.Vtbl<ICorJitInfo>* _vtbl;
        private readonly ICorJitInfo* _jitInfo;
        public static CorInfoHelpFunc Helper => s_helper;
        public static int Assertions => s_assertions;

        public HelperCallbacks(Compiler compiler, bool indirect)
        {
            s_indirect = indirect;
            s_helper = CORINFO_HELP_UNDEF;
            s_assertions = 0;
            _vtbl = (ICorJitInfo.Vtbl<ICorJitInfo>*)NativeMemory.AllocZeroed(
                (nuint)sizeof(ICorJitInfo.Vtbl<ICorJitInfo>));
            _jitInfo = (ICorJitInfo*)NativeMemory.AllocZeroed((nuint)sizeof(ICorJitInfo));
            _vtbl->Base.getHelperFtn = &GetHelperFtn;
            _vtbl->getRelocTypeHint = &GetRelocTypeHint;
            _vtbl->doAssert = &RecordAssertion;
            _jitInfo->lpVtbl = _vtbl;
            compiler.info.compCompHnd = _jitInfo;
            compiler.info.compMatchedVM = true;
        }

        public void Dispose()
        {
            NativeMemory.Free(_jitInfo);
            NativeMemory.Free(_vtbl);
        }
    }

    private static bool s_indirect;
    private static CorInfoHelpFunc s_helper;
    private static int s_assertions;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        s_helper = helper;
        lookup->accessType = s_indirect ? IAT_PVALUE : IAT_VALUE;
        lookup->addr = s_indirect ? (void*)0x100000000 : (void*)0x1234;
        return lookup->addr;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* self, void* address)
    {
        return CorInfoReloc.NONE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        return 0;
    }
}
#endif
