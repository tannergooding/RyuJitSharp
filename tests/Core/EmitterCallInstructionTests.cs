// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
#if LATE_DISASM
using System.Collections.Generic;
#endif
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterCallInstructionTests
{
    [Test]
    public static void ParametersRetainNativeDefaults()
    {
        var parameters = new EmitCallParams();
        Assert.That(parameters.callType, Is.EqualTo(EC_COUNT));
        Assert.That((nuint)parameters.methHnd, Is.EqualTo((nuint)0));
        Assert.That((nuint)parameters.addr, Is.EqualTo((nuint)0));
        Assert.That(parameters.argSize, Is.EqualTo((nint)0));
        Assert.That(parameters.retSize, Is.EqualTo(EA_PTRSIZE));
        Assert.That(parameters.secondRetSize, Is.EqualTo(EA_UNKNOWN));
        Assert.That(parameters.hasAsyncRet, Is.False);
        Assert.That(parameters.ptrVars, Is.Empty);
        Assert.That(parameters.gcrefRegs.IsEmpty && parameters.byrefRegs.IsEmpty, Is.True);
        Assert.That(parameters.ireg, Is.EqualTo(REG_NA));
        Assert.That(parameters.xreg, Is.EqualTo(REG_NA));
        Assert.That(parameters.xmul, Is.Zero);
        Assert.That(parameters.disp, Is.EqualTo((nint)0));
        Assert.That(parameters.isJump || parameters.noSafePoint, Is.False);
        Assert.That(parameters.returnValueCall, Is.Null);
#if DEBUG
        Assert.That(parameters.sigInfo, Is.Null);
#endif
    }

    [TestCase(EC_FUNC_TOKEN, false, IF_METHOD, INS_call, 5u)]
    [TestCase(EC_FUNC_TOKEN, true, IF_METHOD, INS_l_jmp, 5u)]
    [TestCase(EC_FUNC_TOKEN_INDIR, false, IF_METHPTR, INS_call, 6u)]
    [TestCase(EC_FUNC_TOKEN_INDIR, true, IF_METHPTR, INS_tail_i_jmp, 6u)]
    [TestCase(EC_INDIR_R, false, IF_ARD, INS_call, 2u)]
    [TestCase(EC_INDIR_R, true, IF_ARD, INS_tail_i_jmp, 3u)]
    [TestCase(EC_INDIR_ARD, false, IF_ARD, INS_call, 2u)]
    [TestCase(EC_INDIR_ARD, true, IF_ARD, INS_tail_i_jmp, 3u)]
    public static void CallKindsRetainInstructionFormatsAndTailJumpPrefixes(
        EmitCallType type, bool tail, Emitter.insFormat format, instruction ins, uint size)
    {
        WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            var emitter = codeGen.Emitter;
            var parameters = Parameters(compiler, type);
            parameters.isJump = tail;
            emitter.emitIns_Call(parameters);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idIsCall(), Is.True);
            Assert.That(id.idIsLargeCall(), Is.False);
            Assert.That(id.NativeLogicalSize, Is.EqualTo(16));
            Assert.That(id.idIsNoGC(), Is.EqualTo(tail));
            Assert.That(id.idIsCallRegPtr(), Is.EqualTo(type == EC_INDIR_R));
            Assert.That(id.idIsDspReloc(), Is.EqualTo(type is EC_FUNC_TOKEN or EC_FUNC_TOKEN_INDIR));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(StackLevel(codeGen), Is.EqualTo(4096u));
        });
    }

    [TestCase(REG_RBP, false, 2u)]
    [TestCase(REG_R12, false, 3u)]
    [TestCase(REG_R13, false, 3u)]
    [TestCase(REG_R20, false, 4u)]
    [TestCase(REG_R21, false, 4u)]
    [TestCase(REG_RBP, true, 3u)]
    [TestCase(REG_R12, true, 3u)]
    [TestCase(REG_R20, true, 4u)]
    public static void RegisterCallsDoNotAcquireMemorySibBytesOrDisplacements(
        regNumber reg, bool tail, uint size)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, EC_INDIR_R);
            parameters.ireg = reg;
            parameters.isJump = tail;
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(reg));
            Assert.That(CallDisplacement(codeGen.Emitter, id), Is.EqualTo((nint)0));
        });
    }

    [TestCase(REG_R12, REG_NA, 0u, 0, 4u, false)]
    [TestCase(REG_RBP, REG_NA, 0u, 0, 3u, false)]
    [TestCase(REG_RAX, REG_RCX, 4u, 128, 7u, false)]
    [TestCase(REG_RAX, REG_NA, 0u, 8191, 6u, false)]
    [TestCase(REG_RAX, REG_NA, 0u, 8192, 6u, true)]
    [TestCase(REG_RAX, REG_NA, 0u, -8191, 6u, false)]
    [TestCase(REG_RAX, REG_NA, 0u, -8192, 6u, true)]
    [TestCase(REG_RAX, REG_NA, 0u, -8193, 6u, true)]
    public static void AddressCallsPreserveIndexScaleAndSmallOrLargeDisplacements(
        regNumber baseReg, regNumber indexReg, uint scale, int displacement, uint size, bool large)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, EC_INDIR_ARD);
            parameters.ireg = baseReg;
            parameters.xreg = indexReg;
            parameters.xmul = scale;
            parameters.disp = displacement;
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(baseReg));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(indexReg));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(scale == 4 ? 2u : 0u));
            Assert.That(CallDisplacement(codeGen.Emitter, id), Is.EqualTo((nint)displacement));
            Assert.That(id.idIsLargeCall(), Is.EqualTo(large));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(large ? 72 : 16));
        });
    }

    [TestCase(EC_FUNC_TOKEN_INDIR, false, 1234L, false, 7u)]
    [TestCase(EC_FUNC_TOKEN_INDIR, false, 2147483648L, true, 6u)]
    [TestCase(EC_FUNC_TOKEN_INDIR, false, -1234L, false, 7u)]
    [TestCase(EC_FUNC_TOKEN_INDIR, true, 1234L, true, 6u)]
    [TestCase(EC_INDIR_ARD, false, 1234L, false, 8u)]
    [TestCase(EC_INDIR_ARD, false, -1234L, false, 8u)]
    [TestCase(EC_INDIR_ARD, false, 2147483648L, true, 7u)]
    [TestCase(EC_INDIR_ARD, true, 1234L, true, 7u)]
    public static void AbsoluteIndirectCallsPreserveNativeSizeEstimatesAndRelocationChoice(
        EmitCallType type, bool reloc, long address, bool displacementRelocation, uint size)
    {
        WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.compReloc = reloc;
            var parameters = Parameters(compiler, type);
            if (type == EC_INDIR_ARD)
            {
                parameters.ireg = REG_NA;
                parameters.disp = (nint)address;
            }
            else
            {
                parameters.addr = unchecked((void*)(nint)address);
            }
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            // Native emitIns_Call applies absolute-call relocation after emitInsSizeAM:
            // its conservative address-mode estimate includes the initial SIB byte.
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idIsDspReloc(), Is.EqualTo(displacementRelocation));
            if (type == EC_FUNC_TOKEN_INDIR)
            {
                Assert.That(unchecked((nint)id.idAddr().iiaAddr), Is.EqualTo((nint)address));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void DirectCallsKeepTlsRelocationAndNativeAotNullTargets(bool tls, bool nativeAot)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);
            compiler.opts.compReloc = false;
            if (nativeAot)
            {
                compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
                parameters.addr = null;
            }
            if (tls)
            {
                parameters.methHnd = (CORINFO_METHOD_STRUCT_*)1;
            }
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            Assert.That(id.idCodeSize(), Is.EqualTo(tls ? 6u : 5u));
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(id.idIsTlsGD(), Is.EqualTo(tls));
            Assert.That((nuint)id.idAddr().iiaAddr, Is.EqualTo((nuint)parameters.addr));
        });
    }

    [TestCase(EC_FUNC_TOKEN, 0, false, false)]
    [TestCase(EC_FUNC_TOKEN, 15, false, false)]
    [TestCase(EC_FUNC_TOKEN, 16, false, true)]
    [TestCase(EC_FUNC_TOKEN, -1, false, true)]
    [TestCase(EC_FUNC_TOKEN, 0, true, true)]
    [TestCase(EC_INDIR_R, 15, false, false)]
    [TestCase(EC_INDIR_R, 16, false, true)]
    [TestCase(EC_INDIR_R, -1, false, true)]
    [TestCase(EC_INDIR_R, 0, true, true)]
    public static void ArgumentCountsAndAsyncReturnsSelectNativeLogicalDescriptors(
        EmitCallType type, int args, bool asyncReturn, bool large)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, type);
            parameters.argSize = args * sizeof(nint);
            parameters.hasAsyncRet = asyncReturn;
            parameters.retSize = EA_UNKNOWN;
            parameters.secondRetSize = EA_GCREF;
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            Assert.That(id.idIsLargeCall(), Is.EqualTo(large));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(large ? 72 : 16));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(large ? CallView.Arguments(id) : unchecked((uint)id.idSmallCns()),
                Is.EqualTo(unchecked((uint)args)));
            if (large)
            {
                Assert.That(CallView.AsyncReturn(id), Is.EqualTo(asyncReturn));
                Assert.That(CallView.Displacement(id), Is.EqualTo((nint)0));
            }
            Assert.That(ThisRefs(codeGen.Emitter), Is.EqualTo(regMask.SRBM_NONE));
        });
    }

    [TestCase(REG_RSI, 1u, 0u)]
    [TestCase(REG_RDI, 2u, 0u)]
    [TestCase(REG_RBX, 4u, 0u)]
    [TestCase(REG_RBP, 8u, 0u)]
    [TestCase(REG_R12, 0u, 1u)]
    [TestCase(REG_R13, 0u, 2u)]
    [TestCase(REG_R14, 0u, 4u)]
    [TestCase(REG_R15, 0u, 8u)]
    public static void SmallCallsEncodeAllWindowsCalleeSavedGcRegisters(regNumber reg, uint reg1, uint reg2)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);
            parameters.gcrefRegs = new(reg.SingleTypeMask | SRBM_RAX | SRBM_RCX);
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            Assert.That(id.idIsLargeCall(), Is.False);
            Assert.That((uint)id.idReg1(), Is.EqualTo(reg1));
            Assert.That((uint)id.idReg2(), Is.EqualTo(reg2));
            Assert.That(DecodeGcRegisters(null, id), Is.EqualTo((uint)reg.SingleTypeMask));
            Assert.That(ThisRefs(codeGen.Emitter), Is.EqualTo(reg.SingleTypeMask));
        });
    }

    [TestCase(EC_FUNC_TOKEN, EA_GCREF)]
    [TestCase(EC_FUNC_TOKEN, EA_BYREF)]
    [TestCase(EC_INDIR_ARD, EA_GCREF)]
    [TestCase(EC_INDIR_ARD, EA_BYREF)]
    public static void LargeCallsOwnGcSnapshotsAndApplyReturnLivenessAfterRecording(
        EmitCallType type, emitAttr returnType)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var parameters = Parameters(compiler, type);
            parameters.ptrVars = VarSetOps.MakeSingleton(compiler, 0);
            parameters.gcrefRegs = new(SRBM_RBX | SRBM_RCX);
            parameters.byrefRegs = new(SRBM_R12 | SRBM_RDX);
            parameters.retSize = returnType;
            emitter.emitIns_Call(parameters);
            var id = Last(emitter);

            Assert.That(id.NativeLogicalSize, Is.EqualTo(72));
            Assert.That(CallView.Refs(id), Is.EqualTo(new regMaskTP(SRBM_RBX)));
            Assert.That(CallView.Byrefs(id), Is.EqualTo(new regMaskTP(SRBM_R12)));
            Assert.That(ThisRefs(emitter), Is.EqualTo(SRBM_RBX | (returnType == EA_GCREF ? SRBM_RAX : 0)));
            Assert.That(ThisByrefs(emitter), Is.EqualTo(SRBM_R12 | (returnType == EA_BYREF ? SRBM_RAX : 0)));
            Assert.That(CallView.Variables(id), Is.Not.SameAs(parameters.ptrVars));
            Assert.That(ThisVars(emitter), Is.Not.SameAs(parameters.ptrVars).And.Not.SameAs(CallView.Variables(id)));
            VarSetOps.RemoveElemD(compiler, parameters.ptrVars, 0);
            Assert.That(VarSetOps.IsMember(compiler, CallView.Variables(id), 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, ThisVars(emitter), 0), Is.True);

            emitter.emitIns_Call(Parameters(compiler, type));
            Assert.That(VarSetOps.IsMember(compiler, CallView.Variables(id), 0), Is.True);
            Assert.That(VarSetOps.IsEmpty(compiler, ThisVars(emitter)), Is.True);
        });
    }

    [TestCase(EC_FUNC_TOKEN, false)]
    [TestCase(EC_FUNC_TOKEN, true)]
    [TestCase(EC_INDIR_ARD, false)]
    [TestCase(EC_INDIR_ARD, true)]
    public static void LiveStackVariablesAndByrefsIndependentlyRequireLargeDescriptors(EmitCallType type, bool byref)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, type);
            if (byref)
            {
                parameters.byrefRegs = new(SRBM_RBX);
            }
            else
            {
                parameters.ptrVars = VarSetOps.MakeSingleton(compiler, 0);
            }
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            Assert.That(id.idIsLargeCall(), Is.True);
            Assert.That(CallView.Byrefs(id), Is.EqualTo(parameters.byrefRegs));
            Assert.That(VarSetOps.IsEmpty(compiler, CallView.Variables(id)), Is.EqualTo(byref));
        });
    }

    [TestCase(CORINFO_HELP_LLSH, true)]
    [TestCase(CORINFO_HELP_LRSH, true)]
    [TestCase(CORINFO_HELP_LRSZ, true)]
    [TestCase(CORINFO_HELP_GET_GCSTATIC_BASE_NOCTOR, true)]
    [TestCase(CORINFO_HELP_GET_NONGCSTATIC_BASE_NOCTOR, true)]
    [TestCase(CORINFO_HELP_ASSIGN_REF, true)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF, true)]
    [TestCase(CORINFO_HELP_BULK_WRITEBARRIER_SMALL, true)]
    [TestCase(CORINFO_HELP_FAIL_FAST, true)]
    [TestCase(CORINFO_HELP_INIT_PINVOKE_FRAME, true)]
    [TestCase(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER, true)]
    [TestCase(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS, true)]
    [TestCase(CORINFO_HELP_TAILCALL, true)]
    [TestCase(CORINFO_HELP_STACK_PROBE, true)]
    [TestCase(CORINFO_HELP_CHECK_OBJ, true)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, true)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, true)]
    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, true)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, true)]
    [TestCase(CORINFO_HELP_UNDEF, false)]
    [TestCase(CORINFO_HELP_LMUL, false)]
    [TestCase(CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE_NOCTOR, false)]
    [TestCase(CORINFO_HELP_BULK_WRITEBARRIER, false)]
    [TestCase(CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT, false)]
    [TestCase(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT, false)]
    public static void HelperNoGcClassificationPreservesTheNativePropertyTable(CorInfoHelpFunc helper, bool noGc)
    {
        Assert.That(Emitter.emitNoGChelper(Compiler.eeFindHelper(helper)), Is.EqualTo(noGc));
    }

    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, REG_RCX, false, true)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, REG_RAX, true, true)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, REG_RAX, true, true)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, REG_RCX, true, true)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, REG_R10, true, true)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, REG_R11, false, true)]
    [TestCase(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT, REG_RCX, true, false)]
    [TestCase(CORINFO_HELP_ASSIGN_REF, REG_RCX, false, true)]
    [TestCase(CORINFO_HELP_ASSIGN_REF, REG_R20, true, true)]
    [TestCase(CORINFO_HELP_LLSH, REG_R20, false, true)]
    public static void SpecialHelperKillMasksPreserveOnlyTheNativeGcRegisters(
        CorInfoHelpFunc helper, regNumber reg, bool preserved, bool noGc)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);
            parameters.methHnd = Compiler.eeFindHelper(helper);
            parameters.gcrefRegs = new(reg.SingleTypeMask);
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);

            Assert.That(ThisRefs(codeGen.Emitter), Is.EqualTo(preserved ? reg.SingleTypeMask : 0));
            Assert.That(id.idIsNoGC(), Is.EqualTo(noGc));
            Assert.That(id.idIsLargeCall(), Is.EqualTo(preserved));
            if (preserved)
            {
                Assert.That(CallView.Refs(id), Is.EqualTo(parameters.gcrefRegs));
            }
        });
    }

    [Test]
    public static void NoSafePointAndDebugRecordsDoNotSuppressGcStateUpdates()
    {
        WithEmitter((compiler, codeGen) =>
        {
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);
            parameters.noSafePoint = true;
            parameters.byrefRegs = new(SRBM_RBX);
            parameters.retSize = EA_GCREF;
#if DEBUG
            parameters.sigInfo = new StrongBox<CORINFO_SIG_INFO>();
#endif
            codeGen.Emitter.emitIns_Call(parameters);
            var id = Last(codeGen.Emitter);
            Assert.That(id.idIsNoGC(), Is.True);
            Assert.That(ThisRefs(codeGen.Emitter), Is.EqualTo(SRBM_RAX));
            Assert.That(ThisByrefs(codeGen.Emitter), Is.EqualTo(SRBM_RBX));
#if DEBUG
            var info = id.idDebugOnlyInfo();
            assert(info is not null);
            Assert.That(info.idMemCookie, Is.EqualTo((nint)parameters.methHnd));
            Assert.That(info.idCallSig, Is.SameAs(parameters.sigInfo));
            Assert.That(info.idSize, Is.EqualTo((nuint)72));
#endif
        });
    }

    [Test]
    public static void LargeCallAllocationRollsOverWithoutReplacingPreviouslyRecordedDescriptors()
    {
        WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitIns_Nop(1);
            var first = Last(emitter);
            var previousGroup = emitter.emitCurIG;
            assert(previousGroup is not null);
            Capacity(emitter) = Used(emitter) + 16;
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);
            parameters.hasAsyncRet = true;
            emitter.emitIns_Call(parameters);

            Assert.That(emitter.emitCurIG, Is.Not.SameAs(previousGroup));
            Assert.That(previousGroup.igNext, Is.SameAs(emitter.emitCurIG));
            Assert.That(previousGroup.igLastIns, Is.SameAs(first));
            Assert.That(previousGroup.igSize, Is.EqualTo(1));
            Assert.That(Last(emitter).NativeLogicalSize, Is.EqualTo(72));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(CurrentSize(emitter), Is.EqualTo(5));
        });
    }

#if LATE_DISASM
    [TestCase(EC_FUNC_TOKEN, false, false)]
    [TestCase(EC_FUNC_TOKEN, false, true)]
    [TestCase(EC_FUNC_TOKEN, true, false)]
    [TestCase(EC_FUNC_TOKEN, true, true)]
    [TestCase(EC_FUNC_TOKEN_INDIR, false, false)]
    [TestCase(EC_FUNC_TOKEN_INDIR, false, true)]
    [TestCase(EC_FUNC_TOKEN_INDIR, true, false)]
    [TestCase(EC_FUNC_TOKEN_INDIR, true, true)]
    public static void LateDisassemblyRecordsTargetsInSeparateLazyMapsAndOverwritesEntries(
        EmitCallType type, bool helper, bool enabled)
    {
        WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.doLateDisasm = enabled;
            var parameters = Parameters(compiler, type);
            if (helper)
            {
                parameters.methHnd = Compiler.eeFindHelper(CORINFO_HELP_LLSH);
            }
            codeGen.Emitter.emitIns_Call(parameters);

            var map = helper ? HelperMap(ref codeGen.Disassembler) : MethodMap(ref codeGen.Disassembler);
            var otherMap = helper ? MethodMap(ref codeGen.Disassembler) : HelperMap(ref codeGen.Disassembler);
            Assert.That(otherMap, Is.Null);
            if (!enabled)
            {
                Assert.That(map, Is.Null);
                return;
            }

            assert(map is not null);
            Assert.That(map, Has.Count.EqualTo(1));
            Assert.That((nuint)map[(nuint)parameters.addr].Value, Is.EqualTo((nuint)parameters.methHnd));
            parameters.methHnd = helper
                ? Compiler.eeFindHelper(CORINFO_HELP_LRSH) : (CORINFO_METHOD_STRUCT_*)0x4000;
            codeGen.Emitter.emitIns_Call(parameters);
            Assert.That(map, Has.Count.EqualTo(1));
            Assert.That((nuint)map[(nuint)parameters.addr].Value, Is.EqualTo((nuint)parameters.methHnd));
        });
    }
#endif

#if DEBUG
    [Test]
    public static void DspCodeRecordsCallsAndGcState()
    {
        WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitIns_Nop(1);
            var first = Last(emitter);
            var used = Used(emitter);
            var vars = ThisVars(emitter);
            var parameters = Parameters(compiler, EC_FUNC_TOKEN);
            parameters.ptrVars = VarSetOps.MakeSingleton(compiler, 0);
            parameters.gcrefRegs = new(SRBM_RBX);
            parameters.retSize = EA_GCREF;
#if LATE_DISASM
            compiler.opts.doLateDisasm = true;
#endif
            compiler.opts.dspCode = true;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.runWithSPMIErrorTrap = &InstructionRecordingTestSupport.UnavailableMethodMetadata;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;

            var diagnostic = InstructionRecordingTestSupport.Capture(() => emitter.emitIns_Call(parameters));

            Assert.That(Last(emitter), Is.Not.SameAs(first));
            Assert.That(Used(emitter), Is.GreaterThan(used));
            Assert.That(CurrentCount(emitter), Is.EqualTo(2));
            Assert.That(CurrentSize(emitter), Is.GreaterThan(1));
            Assert.That(ThisVars(emitter), Is.SameAs(vars));
            Assert.That(VarSetOps.IsMember(compiler, ThisVars(emitter), 0), Is.True);
            Assert.That(ThisRefs(emitter), Is.Not.EqualTo(regMask.SRBM_NONE));
            Assert.That(ThisByrefs(emitter), Is.EqualTo(regMask.SRBM_NONE));
            Assert.That(diagnostic, Does.Contain("call"));
            Assert.That(diagnostic, Does.Contain("<unknown method>"));
#if LATE_DISASM
            Assert.That(MethodMap(ref codeGen.Disassembler), Is.Not.Null);
#endif
        });
    }
#endif

    private static EmitCallParams Parameters(Compiler compiler, EmitCallType type) => new()
    {
        callType = type,
        methHnd = (CORINFO_METHOD_STRUCT_*)0x2000,
        addr = type is EC_FUNC_TOKEN or EC_FUNC_TOKEN_INDIR ? (void*)0x1234 : null,
        ireg = type is EC_INDIR_R or EC_INDIR_ARD ? REG_RAX : REG_NA,
        ptrVars = VarSetOps.MakeEmpty(compiler),
    };

    internal static void WithEmitter(Action<Compiler, CodeGen> action, bool minopts = true)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = var_types.TYP_REF;
            AllInt(compiler) = SRBM_ALLINT_ALL;
            IntTrash(compiler) = SRBM_INT_CALLEE_TRASH_ALL;
            FloatTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
            MaskTrash(compiler) = SRBM_MSK_CALLEE_TRASH_EVEX;
            codeGen.CopyRegisterInfo();
            codeGen.Emitter.UseRex2Encodings = true;
            StackLevel(codeGen) = 4096;
            action(compiler, codeGen);
        }, minopts);
    }

    private static Emitter.instrDesc Last(Emitter emitter)
        => LastInstruction(emitter) ?? throw new AssertionException("Missing call descriptor.");

    internal abstract class CallView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static uint Arguments(instrDesc id) => ((instrDescCGCA)id).idcArgCnt;
        public static nint Displacement(instrDesc id) => ((instrDescCGCA)id).idcDisp;
        public static nint[] Variables(instrDesc id) => ((instrDescCGCA)id).idcGCvars;
        public static regMaskTP Refs(instrDesc id) => ((instrDescCGCA)id).idcGcrefRegs;
        public static regMaskTP Byrefs(instrDesc id) => ((instrDescCGCA)id).idcByrefRegs;
        public static bool AsyncReturn(instrDesc id) => ((instrDescCGCA)id).hasAsyncContinuationRet();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask AllInt(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask IntTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask FloatTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask MaskTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVars")]
    private static extern ref nint[] ThisVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCIdisp")]
    private static extern nint CallDisplacement(Emitter emitter, Emitter.instrDesc id);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitDecodeCallGCregs")]
    private static extern uint DecodeGcRegisters(Emitter? emitter, Emitter.instrDesc id);

#if LATE_DISASM
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_addressToMethodHandleMap")]
    private static extern ref Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>>? MethodMap(ref Disassembler disassembler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_helperAddressToMethodHandleMap")]
    private static extern ref Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>>? HelperMap(ref Disassembler disassembler);
#endif
}
