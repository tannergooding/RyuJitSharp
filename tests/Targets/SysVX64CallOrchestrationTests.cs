// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64CallOrchestrationTests
{
    [TestCase(TYP_REF, REG_RAX, REG_RAX)]
    [TestCase(TYP_REF, REG_RCX, REG_RAX)]
    [TestCase(TYP_BYREF, REG_RDX, REG_RAX)]
    [TestCase(TYP_DOUBLE, REG_XMM2, REG_XMM0)]
    public static void CallsTransferAbiResultsAndPreserveAllocatedGcRoots(
        var_types type, regNumber allocated, regNumber abi)
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(type, allocated);
            call.Next = new GenTree(GT_NOP, TYP_VOID);

            codeGen.genCall(call);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(allocated == abi
                ? (instruction[])[INS_call]
                : [INS_call, type == TYP_DOUBLE ? INS_movaps : INS_mov]));
            if (allocated != abi)
            {
                Assert.That(ids[1].idReg1(), Is.EqualTo(allocated));
                Assert.That(ids[1].idReg2(), Is.EqualTo(abi));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(type == TYP_REF ? Mask(allocated) : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(type == TYP_BYREF ? Mask(allocated) : RBM_NONE));
        });
    }

    [Test]
    public static void MultiregCallsTransferIntegerAndFloatingReturnsInAbiOrder()
    {
        WithCall((compiler, codeGen) =>
        {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeReturnType(compiler, TYP_LONG, NO_CLASS_HANDLE,
                compiler.info.compCallConv);
            RegisterTypes(ref descriptor)[1] = TYP_DOUBLE;
            var call = DirectCall(TYP_STRUCT);
            CallReturnDescriptor(call) = descriptor;
            call.ClearOtherRegs();
            call.SetRegNumByIdx(REG_RCX, 0);
            call.SetRegNumByIdx(REG_XMM2, 1);

            codeGen.genCall(call);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_call, INS_mov, INS_movaps]));
            Assert.That(ids.Skip(1).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_RCX, REG_XMM2]));
            Assert.That(ids.Skip(1).Select(id => id.idReg2()),
                Is.EqualTo((regNumber[])[REG_RAX, REG_XMM0]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnmanagedVector3ReturnsClearTheSecondAbiRegisterAfterCopies(bool moveResults)
    {
        WithCall((compiler, codeGen) =>
        {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeReturnType(compiler, TYP_DOUBLE, NO_CLASS_HANDLE,
                compiler.info.compCallConv);
            RegisterTypes(ref descriptor)[0] = TYP_SIMD8;
            RegisterTypes(ref descriptor)[1] = TYP_FLOAT;
            var call = DirectCall(TYP_SIMD12);
            call.Flags |= GTF_CALL_UNMANAGED;
            CallReturnDescriptor(call) = descriptor;
            call.ClearOtherRegs();
            call.SetRegNumByIdx(moveResults ? REG_XMM2 : REG_XMM0, 0);
            call.SetRegNumByIdx(moveResults ? REG_XMM3 : REG_XMM1, 1);

            codeGen.genCall(call);

            var ids = Descriptors(codeGen);
            Assert.That(ids.First().idIns(), Is.EqualTo(INS_call));
            Assert.That(ids, Has.Count.EqualTo(moveResults ? 4 : 2));
            if (moveResults)
            {
                Assert.That(ids[1].idReg1(), Is.EqualTo(REG_XMM2));
                Assert.That(ids[1].idReg2(), Is.EqualTo(REG_XMM0));
                Assert.That(ids[2].idReg1(), Is.EqualTo(REG_XMM3));
                Assert.That(ids[2].idReg2(), Is.EqualTo(REG_XMM1));
            }
            Assert.That(ids.Last().idIns(), Is.EqualTo(INS_insertps));
            Assert.That(ids.Last().idReg1(), Is.EqualTo(REG_XMM1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NullCheckUsesSysVThisRegisterAfterArgumentPlacement(bool tail)
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            call.Flags |= GTF_CALL_NULLCHECK;
            if (tail)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }
            AddLateArgument(compiler, call, PutArg(TYP_REF, REG_R10), REG_RDI);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_REF);

            codeGen.genCall(call);

            var ids = Descriptors(codeGen);
            instruction[] expected = tail ? [INS_mov, INS_cmp] : [INS_mov, INS_cmp, INS_call];
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(expected));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RDI));
            Assert.That(ids[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RDI));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(tail ? Mask(REG_RDI) : RBM_NONE));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiregFieldListsPlaceEachSysVEightbyteAndKeepTailRoots(bool tail)
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            if (tail)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }

            var first = PutArg(TYP_REF, REG_R10);
            var second = PutArg(TYP_DOUBLE, REG_XMM2);
            var fields = new GenTreeFieldList();
            fields.AddField(compiler, first, 0, TYP_REF);
            fields.AddField(compiler, second, 8, TYP_DOUBLE);
            var arg = call.Args.PushBack(NewCallArg.CreateForStruct(fields, TYP_STRUCT, new ClassLayout(16)));
            var firstSeg = AbiPassingSegment.InRegister(REG_RDI, 0, 8);
            var secondSeg = AbiPassingSegment.InRegister(REG_XMM0, 8, 8);
            arg.AbiInfo = AbiPassingInformation.FromSegments(compiler, in firstSeg, in secondSeg);
            arg.EarlyNode = null;
            arg.LateNode = fields;
            call.Args.PushLateBack(arg);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_REF);

            codeGen.genCallPlaceRegArgs(call);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov, INS_movaps]));
            Assert.That(ids.Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_RDI, REG_XMM0]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(tail ? Mask(REG_RDI) : RBM_NONE));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FieldListSkipsStackSegmentsWithoutAdvancingTheRegisterField(bool registerFirst)
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            var fields = new GenTreeFieldList();
            var fieldType = registerFirst ? TYP_REF : TYP_DOUBLE;
            var register = registerFirst ? REG_RDI : REG_XMM0;
            fields.AddField(compiler, PutArg(fieldType, registerFirst ? REG_R10 : REG_XMM2),
                (ushort)(registerFirst ? 0 : 8), fieldType);
            var arg = call.Args.PushBack(NewCallArg.CreateForStruct(fields, TYP_STRUCT, new ClassLayout(16)));
            var regSegment = AbiPassingSegment.InRegister(register, registerFirst ? 0 : 8, 8);
            var stackSegment = AbiPassingSegment.OnStack(0, registerFirst ? 8 : 0, 8);
            arg.AbiInfo = registerFirst
                ? AbiPassingInformation.FromSegments(compiler, in regSegment, in stackSegment)
                : AbiPassingInformation.FromSegments(compiler, in stackSegment, in regSegment);
            arg.EarlyNode = null;
            arg.LateNode = fields;
            call.Args.PushLateBack(arg);

            codeGen.genCallPlaceRegArgs(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(registerFirst ? INS_mov : INS_movaps));
            Assert.That(id.idReg1(), Is.EqualTo(register));
        });
    }

    [Test]
    public static void PlacementLeavesEarlyAndStackOnlyArgumentsAlreadyMaterialized()
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            var early = call.Args.PushBack(NewCallArg.CreateForPrimitive(Physical(TYP_INT, REG_RSI)));
            early.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(REG_RDI, 0, 8));
            var stack = call.Args.PushBack(NewCallArg.CreateForPrimitive(Physical(TYP_INT, REG_R10)));
            stack.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.OnStack(0, 0, 8));
            stack.EarlyNode = null;
            stack.LateNode = Physical(TYP_INT, REG_R10);
            call.Args.PushLateBack(stack);
            AddLateArgument(compiler, call, PutArg(TYP_INT, REG_R11), REG_RDX);

            codeGen.genCallPlaceRegArgs(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(id.idReg2(), Is.EqualTo(REG_R11));
        });
    }

    [Test]
    public static void PutArgRegProducesTheSysVArgumentBeforeCallPlacement()
    {
        WithCall((compiler, codeGen) =>
        {
            var argument = PutArg(TYP_REF, REG_R10, REG_RDI);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_REF);

            codeGen.genPutArgReg(argument);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_mov));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RDI));
            Assert.That(id.idReg2(), Is.EqualTo(REG_R10));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_RDI)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FastTailCallKeepsIndirectTargetRootsThroughTheEpilog(bool memory)
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            call._callType = CT_INDIRECT;
            call._callMoreFlags |= GTF_CALL_M_TAILCALL;
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
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(memory ? Mask(REG_R10) : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(memory ? Mask(REG_R11) : RBM_NONE));
            codeGen.genCallInstruction(call);
            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_tail_i_jmp));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndirectionCellUsesTheAllocatedSysVRegister(bool r2r)
    {
        WithCall((compiler, codeGen) =>
        {
            compiler.virtualStubParamInfo = new Compiler.VirtualStubParamInfo();
            var call = DirectCall(TYP_VOID);
            var register = r2r ? REG_R2R_INDIRECT_PARAM : compiler.virtualStubParamInfo.Reg;
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
                PutArg(TYP_I_IMPL, register)).WithWellKnownArg(kind));
            argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(register, 0, 8));

            Assert.That(codeGen.getCallIndirectionCellReg(call), Is.EqualTo(register));
            codeGen.genCallInstruction(call);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(r2r ? INS_tail_i_jmp : INS_call));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(register));
        });
    }

    [Test]
    public static void UnsupportedSysVVarargsRejectBeforeArgumentConsumption()
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            call.Args.IsVarArgs = true;
            var argument = PutArg(TYP_REF, REG_R10);
            AddLateArgument(compiler, call, argument, REG_RDI);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_REF);

            var error = Assert.Throws<FatalJitException>(() => codeGen.genCall(call));

            Assert.That(error?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_R10)));
#if DEBUG
            Assert.That(argument._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
#endif
        });
    }

    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL)]
    [TestCase(CORINFO_HELP_VIRTUAL_FUNC_PTR)]
    [TestCase(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT)]
    [TestCase(CORINFO_HELP_MEMSET)]
    [TestCase(CORINFO_HELP_MEMCPY)]
    public static void InterveningHelpersDoNotConsumeThePendingCallLabel(CorInfoHelpFunc helper)
    {
        WithCall((_, codeGen) =>
        {
            var label = new BasicBlock(null, null);
            PendingLabel(codeGen) = label;
            var helperCall = DirectCall(TYP_VOID);
            helperCall._callType = CT_HELPER;
            helperCall._callMethHnd = Compiler.eeFindHelper(helper);

            codeGen.genCall(helperCall);

            Assert.That(PendingLabel(codeGen), Is.SameAs(label));
            Assert.That(label.bbEmitCookie, Is.Null);
            codeGen.genCall(DirectCall(TYP_VOID));
            Assert.That(PendingLabel(codeGen), Is.Null);
            Assert.That(label.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
        });
    }

    [Test]
    public static void PendingCallLabelPrecedesTheReturnRegisterMove()
    {
        WithCall((_, codeGen) =>
        {
            var label = new BasicBlock(null, null);
            PendingLabel(codeGen) = label;
            var before = codeGen.Emitter.emitCurIG;

            codeGen.genCall(DirectCall(TYP_INT, REG_RCX));

            Assert.That(PendingLabel(codeGen), Is.Null);
            Assert.That(before?.igData?.Single().idIns(), Is.EqualTo(INS_call));
            Assert.That(label.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_mov));
        });
    }

    [Test]
    public static void UnmanagedCallKillsLazyRootsAtTheGcBoundary()
    {
        WithCall((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var before = emitter.emitAddLabel(VarSetOps.MakeEmpty(compiler), Mask(REG_RAX), RBM_NONE);
            emitter.emitIns(INS_nop);
            var call = DirectCall(TYP_VOID);
            call.Flags |= GTF_CALL_UNMANAGED;
            call._callMoreFlags |= GTF_CALL_M_PINVOKE;

            codeGen.genCall(call);

            Assert.That(emitter.emitCurIG, Is.Not.SameAs(before));
            Assert.That(before.igNext, Is.SameAs(emitter.emitCurIG));
            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_call));
        });
    }

#if DEBUG
    [Test]
    public static void TailcallEpilogCheckKeepsSysVArgumentRegistersOutsideSavedAndCookieMasks()
    {
        WithCall((compiler, codeGen) =>
        {
            var call = DirectCall(TYP_VOID);
            call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            compiler.NeedsGSSecurityCookie = true;
            AddLateArgument(compiler, call, PutArg(TYP_REF, REG_RDI), REG_RDI);
            AddLateArgument(compiler, call, PutArg(TYP_DOUBLE, REG_XMM0), REG_XMM0);

            codeGen.genCheckTailCallEpilogRegisters(call);
        });
    }
#endif

    private static void WithCall(Action<Compiler, CodeGen> action)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, _) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing code generator.");
            action(compiler, codeGen);
        });
    }

    private static GenTreeCall DirectCall(var_types type, regNumber register = REG_NA) => new(type)
    {
        _callType = CT_USER_FUNC,
        _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x4000,
        _directCallAddress = (void*)0x1234,
        _returnType = type,
        RegNum = register,
    };

    private static GenTreePhysReg Physical(var_types type, regNumber register)
        => new(register, type) { RegNum = register };

    private static GenTreeUnOp PutArg(var_types type, regNumber source, regNumber? target = null)
        => new(GT_PUTARG_REG, type, Physical(type, source)) { RegNum = target ?? source };

    private static void AddLateArgument(Compiler compiler, GenTreeCall call, GenTree argument,
        regNumber register)
    {
        var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(argument));
        arg.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
            AbiPassingSegment.InRegister(register, 0, 8));
        arg.EarlyNode = null;
        arg.LateNode = argument;
        call.Args.PushLateBack(arg);
    }

    private static regMaskTP Mask(regNumber register)
        => regMaskTP.CreateFromRegNum(register, register.SingleTypeMask);

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
        => CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing instruction descriptors.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genPendingCallLabel")]
    private static extern ref BasicBlock? PendingLabel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regType")]
    private static extern ref InlineArrayMaxRetRegCount<var_types> RegisterTypes(ref ReturnTypeDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_returnTypeDesc")]
    private static extern ref ReturnTypeDesc CallReturnDescriptor(GenTreeCall call);
}
#endif
