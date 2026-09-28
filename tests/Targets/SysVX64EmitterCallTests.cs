// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64EmitterCallTests
{
    [TestCase(EmitCallType.EC_FUNC_TOKEN, EA_GCREF, GCT_GCREF)]
    [TestCase(EmitCallType.EC_FUNC_TOKEN, EA_BYREF, GCT_BYREF)]
    [TestCase(EmitCallType.EC_INDIR_R, EA_GCREF, GCT_GCREF)]
    [TestCase(EmitCallType.EC_INDIR_R, EA_BYREF, GCT_BYREF)]
    public static void SecondGcReturnForcesLargeDescriptorAndPreservesBothReturnRegisters(
        EmitCallType type, emitAttr secondReturn, GCInfo.GCtype gcType)
    {
        WithEmitter((compiler, emitter) =>
        {
            var parameters = Parameters(compiler);
            parameters.callType = type;
            if (type == EmitCallType.EC_INDIR_R)
            {
                parameters.addr = null;
                parameters.ireg = REG_R11;
            }
            parameters.retSize = EA_GCREF;
            parameters.secondRetSize = secondReturn;
            emitter.emitIns_Call(parameters);

            var id = Last(emitter);
            Assert.That(id.idIsLargeCall(), Is.True);
            Assert.That(id.NativeLogicalSize, Is.EqualTo(72));
            Assert.That(CallView.SecondReturn(id), Is.EqualTo(gcType));
            Assert.That(ThisRefs(emitter), Is.EqualTo(SRBM_RAX | (gcType == GCT_GCREF ? SRBM_RDX : 0)));
            Assert.That(ThisByrefs(emitter), Is.EqualTo(gcType == GCT_BYREF ? SRBM_RDX : 0));
        });
    }

    [TestCase(EA_GCREF, GCT_GCREF)]
    [TestCase(EA_BYREF, GCT_BYREF)]
    public static void IssuedCallBirthsBothGcReturnRegisters(emitAttr secondReturn, GCInfo.GCtype gcType)
    {
        WithEmitter((compiler, emitter) =>
        {
            var parameters = Parameters(compiler);
            parameters.callType = EmitCallType.EC_INDIR_R;
            parameters.addr = null;
            parameters.ireg = REG_R11;
            parameters.retSize = EA_GCREF;
            parameters.secondRetSize = secondReturn;
            parameters.noSafePoint = true;
#if DEBUG
            parameters.sigInfo = new StrongBox<CORINFO_SIG_INFO>();
#endif
            emitter.emitIns_Call(parameters);
            var id = Last(emitter);
            var buffer = stackalloc byte[32];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
            emitter.emitSimpleStkUsed = true;
            ThisRefs(emitter) = SRBM_NONE;
            ThisByrefs(emitter) = SRBM_NONE;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            SyncThisRegister(emitter) = REG_NA;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordCallSite = &RecordCallSite;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            emitter.emitCmpHandle = &jitInfo;

            var end = buffer;
            var size = emitter.emitOutputInstr(emitter.emitCurIG!, id, &end);

            Assert.That(size, Is.EqualTo((nuint)72));
            Assert.That(end - buffer, Is.EqualTo(3));
            Assert.That(ThisRefs(emitter), Is.EqualTo(SRBM_RAX | (gcType == GCT_GCREF ? SRBM_RDX : 0)));
            Assert.That(ThisByrefs(emitter), Is.EqualTo(gcType == GCT_BYREF ? SRBM_RDX : 0));
        });
    }

    [TestCase(EA_8BYTE, false)]
    [TestCase(EA_8BYTE, true)]
    public static void NonGcSecondReturnKeepsNativeSmallOrAsyncDescriptor(
        emitAttr secondReturn, bool asyncReturn)
    {
        WithEmitter((compiler, emitter) =>
        {
            var parameters = Parameters(compiler);
            parameters.secondRetSize = secondReturn;
            parameters.hasAsyncRet = asyncReturn;
            emitter.emitIns_Call(parameters);

            var id = Last(emitter);
            Assert.That(id.idIsLargeCall(), Is.EqualTo(asyncReturn));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(asyncReturn ? 72 : 16));
            if (asyncReturn)
            {
                Assert.That(CallView.SecondReturn(id), Is.EqualTo(GCT_NONE));
            }
        });
    }

    [Test]
    public static void CodeGenerationPassesCurrentGcStateThroughSysVCallRecording()
    {
        WithEmitter((compiler, emitter) =>
        {
            if (compiler.codeGen is not CodeGen codeGen)
            {
                throw new AssertionException("Missing active code generator.");
            }
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            codeGen.GCInfo.gcRegGCrefSetCur = new regMaskTP(SRBM_RBX);
            codeGen.GCInfo.gcRegByrefSetCur = new regMaskTP(SRBM_R12);
            var parameters = Parameters(compiler);
            parameters.retSize = EA_GCREF;
            parameters.secondRetSize = EA_BYREF;

            codeGen.genEmitCallWithCurrentGC(ref parameters);

            var id = Last(emitter);
            Assert.That(id.idIsLargeCall(), Is.True);
            Assert.That(CallView.Refs(id), Is.EqualTo(new regMaskTP(SRBM_RBX)));
            Assert.That(CallView.Byrefs(id), Is.EqualTo(new regMaskTP(SRBM_R12)));
            Assert.That(ThisRefs(emitter), Is.EqualTo(SRBM_RBX | SRBM_RAX));
            Assert.That(ThisByrefs(emitter), Is.EqualTo(SRBM_R12 | SRBM_RDX));
        });
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_PROF_FCN_ENTER, SRBM_RDI, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_PROF_FCN_ENTER, SRBM_RAX, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_PROF_FCN_LEAVE, SRBM_RDX, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_PROF_FCN_LEAVE, SRBM_RAX, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF, SRBM_RDI, false)]
    public static void HelperCallsPreserveSysVArgumentAndReturnRegisters(
        CorInfoHelpFunc helper, regMask register, bool survives)
    {
        WithEmitter((compiler, emitter) =>
        {
            var parameters = Parameters(compiler);
            parameters.methHnd = Compiler.eeFindHelper(helper);
            parameters.gcrefRegs = new regMaskTP(register);
            emitter.emitIns_Call(parameters);

            Assert.That(ThisRefs(emitter), Is.EqualTo(survives ? register : SRBM_NONE));
            Assert.That(Last(emitter).idIsLargeCall(), Is.EqualTo(survives));
        });
    }

    private static EmitCallParams Parameters(Compiler compiler) => new()
    {
        callType = EmitCallType.EC_FUNC_TOKEN,
        methHnd = (CORINFO_METHOD_STRUCT_*)0x2000,
        addr = (void*)0x1234,
        ptrVars = VarSetOps.MakeEmpty(compiler),
    };

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("Missing call descriptor.");
    }

    private abstract class CallView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static GCInfo.GCtype SecondReturn(instrDesc id)
        {
            return ((instrDescCGCA)id).idSecondGCref();
        }

        public static regMaskTP Refs(instrDesc id)
        {
            return ((instrDescCGCA)id).idcGcrefRegs;
        }

        public static regMaskTP Byrefs(instrDesc id)
        {
            return ((instrDescCGCA)id).idcByrefRegs;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisRegister(Emitter emitter);

    [System.Runtime.InteropServices.UnmanagedCallersOnly(
        CallConvs = [typeof(System.Runtime.CompilerServices.CallConvMemberFunction)])]
    private static void RecordCallSite(ICorJitInfo* info, int offset,
        CORINFO_SIG_INFO* signature, CORINFO_METHOD_STRUCT_* method)
    {
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask AllInt(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask IntTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask FloatTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask MaskTrash(Compiler compiler);

    internal static void WithEmitter(Action<Compiler, Emitter> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        compiler.info.compMatchedVM = true;
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [0];
        compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(true
#if DEBUG
                , false
#endif
                );
            AllInt(compiler) = SRBM_ALLINT_ALL;
            IntTrash(compiler) = SRBM_INT_CALLEE_TRASH_ALL;
            FloatTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
            MaskTrash(compiler) = SRBM_MSK_CALLEE_TRASH_EVEX;
            codeGen.CopyRegisterInfo();
            StackLevel(codeGen) = 4096;
            action(compiler, codeGen.Emitter);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
#endif
