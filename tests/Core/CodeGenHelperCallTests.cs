// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenHelperCallTests
{
    [TestCase(false, 0x1234L, CorInfoReloc.NONE, false, REG_NA)]
    [TestCase(true, 0x1234L, CorInfoReloc.NONE, false, REG_NA)]
    [TestCase(true, 0x100000000L, CorInfoReloc.RELATIVE32, false, REG_NA)]
    [TestCase(true, 0x100000000L, CorInfoReloc.NONE, true, REG_NA)]
    [TestCase(true, 0x100000000L, CorInfoReloc.NONE, true, REG_R11)]
    [TestCase(false, -1234L, CorInfoReloc.NONE, false, REG_NA)]
    [TestCase(true, -1234L, CorInfoReloc.NONE, false, REG_NA)]
    [TestCase(true, long.MinValue, CorInfoReloc.NONE, true, REG_NA)]
    public static void HelperLookupPreservesDirectAndIndirectAddressChoices(
        bool indirect, long address, CorInfoReloc hint, bool registerTarget, regNumber target)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var context = new HelperContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Address = unchecked((void*)(nint)address),
                Access = indirect ? IAT_PVALUE : IAT_VALUE,
                Hint = hint,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;
            compiler.opts.compReloc = true;

            codeGen.genEmitHelperCall(CORINFO_HELP_ASSIGN_REF, 0, EA_PTRSIZE, target);

            var descriptors = Descriptors(codeGen);
            Assert.That(context.HelperQueries, Is.EqualTo(1));
            Assert.That(context.HintQueries, Is.EqualTo(registerTarget ? 2 : indirect ? 1 : 0));
            Assert.That(descriptors, Has.Count.EqualTo(registerTarget ? 2 : 1));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_call));
            Assert.That(descriptors[^1].idInsFmt(), Is.EqualTo(registerTarget ? Emitter.insFormat.IF_ARD :
                indirect ? Emitter.insFormat.IF_METHPTR : Emitter.insFormat.IF_METHOD));
            if (registerTarget)
            {
                var expectedTarget = target == REG_NA ? REG_DEFAULT_HELPER_CALL_TARGET : target;
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(expectedTarget));
                Assert.That(descriptors[0].idIsCnsReloc(), Is.True);
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)address));
                Assert.That(descriptors[^1].idAddr().iiaAddrMode.amBaseReg,
                    Is.EqualTo(expectedTarget));
            }
        });
    }

    [TestCase(0L, true)]
    [TestCase(2147483647L, true)]
    [TestCase(2147483648L, false)]
    [TestCase(-2147483648L, true)]
    [TestCase(-2147483649L, false)]
    [TestCase(-1L, true)]
    public static void IndirectZeroRelativeAddressesRetainSigned32BitBoundaries(long address, bool encodable)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var bits = unchecked((nuint)(nint)address);
            Assert.That(CodeGen.genCodeIndirAddrCanBeEncodedAsZeroRelOffset(bits), Is.EqualTo(encodable));
            compiler.opts.compReloc = false;
            Assert.That(codeGen.genCodeIndirAddrNeedsReloc(bits), Is.EqualTo(!encodable));
            Assert.That(codeGen.genCodeAddrNeedsReloc(bits), Is.True);
            compiler.opts.compReloc = true;
            Assert.That(codeGen.genCodeIndirAddrNeedsReloc(bits), Is.True);
        });
    }

    [Test]
    public static void CurrentGcStateIsCapturedWithoutReplacingTheCallParameters()
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RSI, TYP_BYREF);
            var parameters = Parameters();

            codeGen.genEmitCallWithCurrentGC(ref parameters);

            Assert.That(parameters.ptrVars, Is.SameAs(codeGen.GCInfo.gcVarPtrSetCur));
            Assert.That(parameters.gcrefRegs, Is.EqualTo(RBM_RBX));
            Assert.That(parameters.byrefRegs, Is.EqualTo(RBM_RSI));
            Assert.That(Descriptors(codeGen)[^1].idIns(), Is.EqualTo(INS_call));
        });
    }

    [TestCase(TYP_INT, REG_RAX)]
    [TestCase(TYP_DOUBLE, REG_XMM0)]
    [TestCase(TYP_SIMD16, REG_XMM0)]
    public static void ManagedReturnLocationsUseTheReturnRegisterAndPostCallLocation(
        var_types type, regNumber expectedRegister)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            codeGen.genInitialize();
            compiler.opts.compDbgInfo = true;
            compiler.opts.compScopeInfo = true;
            var call = new GenTreeCall(type) { _returnType = type };
            var root = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
            var inline = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
            inline._parent = root;
            inline._location = new ILLocation(37, ICorDebugInfo.CALL_INSTRUCTION);
            compiler.genCallSite2DebugInfoMap = new()
            {
                [call] = new DebugInfo(inline, new ILLocation(3, ICorDebugInfo.CALL_INSTRUCTION)),
            };
            var parameters = Parameters();
            parameters.returnValueCall = call;

            codeGen.genEmitCallWithCurrentGC(ref parameters);

            var records = ReturnInfo(codeGen);
            Assert.That(records, Has.Count.EqualTo(1));
            var record = records[0] ?? throw new AssertionException("Missing return record.");
            Assert.That(FieldValue(record, "callILOffset"), Is.EqualTo(37));
            var location = (CodeGen.siVarLoc)FieldValue(record, "returnValueLoc");
            CodeGen.siVarLoc expected = default;
            expected.storeVariableInRegisters(expectedRegister, REG_NA);
            Assert.That(location, Is.EqualTo(expected));
            Assert.That(FieldValue(record, "returnLocation"), Is.EqualTo(new emitLocation(codeGen.Emitter)));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void ReturnReportingHonorsNativeEligibilityGates(int mode)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            codeGen.genInitialize();
            compiler.opts.compDbgInfo = mode != 0;
            compiler.opts.compScopeInfo = mode != 1;
            var call = new GenTreeCall(TYP_INT) { _returnType = mode == 3 ? TYP_VOID : TYP_INT };
            compiler.genCallSite2DebugInfoMap = [];
            if (mode != 2)
            {
                compiler.genCallSite2DebugInfoMap.Add(call,
                    new DebugInfo(null, new ILLocation(7, ICorDebugInfo.CALL_INSTRUCTION)));
            }
            var parameters = Parameters();
            parameters.returnValueCall = call;
            parameters.isJump = mode == 4;

            codeGen.genEmitCallWithCurrentGC(ref parameters);

            Assert.That(ReturnInfo(codeGen), Is.Empty);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void ReturnBufferReportingRequiresALocalAddress(bool localAddress)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            codeGen.genInitialize();
            compiler.opts.compDbgInfo = true;
            compiler.opts.compScopeInfo = true;
            var call = new GenTreeCall(TYP_VOID) { _returnType = TYP_STRUCT };
            GenTree address = localAddress
                ? new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4)
                : Register(compiler, TYP_BYREF, REG_RCX);
            var putArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_BYREF, address);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(putArg).WithWellKnownArg(WellKnownArg.RetBuffer));
            compiler.genCallSite2DebugInfoMap = new()
            {
                [call] = new DebugInfo(null, new ILLocation(19, ICorDebugInfo.CALL_INSTRUCTION)),
            };
            var parameters = Parameters();
            parameters.returnValueCall = call;

            codeGen.genEmitCallWithCurrentGC(ref parameters);

            var records = ReturnInfo(codeGen);
            Assert.That(records, Has.Count.EqualTo(localAddress ? 1 : 0));
            if (localAddress)
            {
                var record = records[0] ?? throw new AssertionException("Missing return record.");
                Assert.That(FieldValue(record, "returnValueLoc"),
                    Is.EqualTo(codeGen.getSiVarLoc(in compiler.lvaTable[0], 4, 0)));
            }
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectsBeforeHelperQueriesAndGcCapture()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = true;
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() =>
                codeGen.genEmitHelperCall(CORINFO_HELP_ASSIGN_REF, 0, EA_PTRSIZE));
            var parameters = Parameters();
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            _ = Assert.Throws<FatalJitException>(() => codeGen.genEmitCallWithCurrentGC(ref parameters));
            Assert.That(parameters.gcrefRegs, Is.EqualTo(RBM_NONE));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }
#endif

    private static EmitCallParams Parameters()
    {
        return new EmitCallParams
        {
            callType = EC_FUNC_TOKEN,
            addr = (void*)0x1234,
            methHnd = Compiler.eeFindHelper(CORINFO_HELP_ASSIGN_REF),
            retSize = EA_PTRSIZE,
        };
    }

    private static IList ReturnInfo(CodeGen codeGen)
    {
        return typeof(CodeGen).GetField("emittedCallReturnInfo", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(codeGen) as IList ?? throw new AssertionException("Missing call-return storage.");
    }

    private static object FieldValue(object record, string name)
    {
        var recordType = typeof(CodeGen).GetNestedType("EmittedCallReturnInfo", BindingFlags.NonPublic) ??
            throw new AssertionException("Missing return record type.");
        return recordType.GetField(name)?.GetValue(record) ??
            throw new AssertionException($"Missing return field {name}.");
    }

    private struct HelperContext
    {
        public ICorJitInfo JitInfo;
        public void* Address;
        public InfoAccessType Access;
        public CorInfoReloc Hint;
        public int HelperQueries;
        public int HintQueries;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        var context = (HelperContext*)self;
        context->HelperQueries++;
        lookup->accessType = context->Access;
        lookup->addr = context->Address;

        return context->Address;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* self, void* address)
    {
        var context = (HelperContext*)self;
        context->HintQueries++;

        return context->Hint;
    }
}
