// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64ReturnExitTests
{
    [TestCase(TYP_REF, REG_RBX, INS_mov)]
    [TestCase(TYP_BYREF, REG_RBX, INS_mov)]
    [TestCase(TYP_FLOAT, REG_XMM1, INS_movaps)]
    [TestCase(TYP_INT, REG_RBX, INS_mov)]
    public static void ScalarReturnsMoveAndMarkOnlyTheirAbiRoots(var_types type, regNumber sourceReg,
        instruction instruction)
    {
        WithReturn(type, (compiler, codeGen) =>
        {
            var source = compiler.gtNewIconNode(TYP_INT, 1);
            source.RegNum = sourceReg;
            codeGen.GCInfo.gcMarkRegPtrVal(sourceReg, type);
            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, type, source));

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[instruction]));
            Assert.That(ids[0].idReg1(), Is.EqualTo(type == TYP_FLOAT ? REG_XMM0 : REG_RAX));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_RAX : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? RBM_RAX : RBM_NONE));
        });
    }

    [Test]
    public static void FieldListReturnsUseTwoIndependentAbiRegisterFiles()
    {
        WithReturn(TYP_STRUCT, (compiler, codeGen) =>
        {
            SetReturnRegisters(compiler, TYP_REF, TYP_DOUBLE);
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            first.RegNum = REG_RBX;
            var second = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            second.RegNum = REG_XMM2;
            var fields = new GenTreeFieldList();
            fields.AddField(compiler, first, 0, TYP_REF);
            fields.AddField(compiler, second, 8, TYP_DOUBLE);
            var tree = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, fields);

            Assert.That(codeGen.isStructReturn(tree), Is.True);
            codeGen.genReturn(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_RAX, REG_XMM0]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
        });
    }

    [Test]
    public static void NonEnregisteredStructLoadsEachReturnEightbyteFromItsLocal()
    {
        WithReturn(TYP_STRUCT, (compiler, codeGen) =>
        {
            SetReturnRegisters(compiler, TYP_LONG, TYP_DOUBLE);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = new ClassLayout(16), lvIsMultiRegRet = true,
                lvOnFrame = true, StackOffset = -32,
                RegNum = REG_STK,
            };
            var local = new GenTreeLclVar(TYP_STRUCT, 0) { IsContained = true };
            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_STRUCT, local));

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_RAX, REG_XMM0]));
            Assert.That(ids.Select(id => id.idAddr().iiaLclVar.lvaOffset()), Is.EqualTo((uint[])[0, 8]));
        });
    }

    [TestCase(REG_XMM0)]
    [TestCase(REG_XMM1)]
    [TestCase(REG_XMM2)]
    public static void VectorReturnPreservesBothLanesForEachSourceRegister(regNumber sourceReg)
    {
        WithReturn(TYP_STRUCT, (compiler, codeGen) =>
        {
            SetReturnRegisters(compiler, TYP_DOUBLE, TYP_DOUBLE);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_SIMD16, lvLRACandidate = true, RegNum = sourceReg,
            };
            var local = new GenTreeLclVar(TYP_SIMD16, 0) { RegNum = sourceReg };
            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_STRUCT, local));

            var ids = Descriptors(codeGen);
            if (sourceReg != REG_XMM0)
            {
                Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_movaps, INS_movhlps]));
                Assert.That(ids[0].idReg1(), Is.EqualTo(REG_XMM0));
                Assert.That(ids[0].idReg2(), Is.EqualTo(sourceReg));
            }
            else
            {
                Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_movhlps]));
            }

            Assert.That(ids[^1].idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(ids[^1].idReg2(), Is.EqualTo(sourceReg));
        });
    }

    [Test]
    public static void MultiRegisterCopyMovesOnlyAssignedFieldsBeforeAbiReturnMoves()
    {
        WithReturn(TYP_STRUCT, (compiler, codeGen) =>
        {
            SetReturnRegisters(compiler, TYP_LONG, TYP_DOUBLE);
            var call = new GenTreeCall(TYP_STRUCT);
            CallReturnDescriptor(call) = compiler.compRetTypeDesc;
            call.ClearOtherRegs();
            call.SetRegNumByIdx(REG_RCX, 0);
            call.SetRegNumByIdx(REG_XMM2, 1);
            var copy = new GenTreeCopyOrReload(GT_COPY, TYP_STRUCT, call);
            copy.SetRegNumByIdx(REG_RDX, 0);
            copy.SetRegNumByIdx(REG_NA, 1);

            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_STRUCT, copy));

            Assert.That(Descriptors(codeGen).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_RDX, REG_RAX, REG_XMM0]));
        });
    }

    [Test]
    public static void SpilledMultiregFieldLoadsFromPromotedStackHome()
    {
        WithReturn(TYP_STRUCT, (compiler, codeGen) =>
        {
            SetReturnRegisters(compiler, TYP_LONG, TYP_LONG);
            compiler.lvaCount = 3;
            compiler.lvaEnregMultiRegVars = true;
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16),
                    lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2, RegNum = REG_STK },
                new LclVarDsc { Type = TYP_LONG, lvIsStructField = true, lvLRACandidate = true,
                    lvOnFrame = true, StackOffset = -16, RegNum = REG_RCX },
                new LclVarDsc { Type = TYP_LONG, lvIsStructField = true, lvLRACandidate = true,
                    lvOnFrame = true, StackOffset = -8, RegNum = REG_RDX },
            ];
            var local = new GenTreeLclVar(TYP_STRUCT, 0);
            local.SetMultiReg();
            local.SetRegNumByIdx(REG_RCX, 0);
            local.SetRegNumByIdx(REG_NA, 1);

            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_STRUCT, local));

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_mov]));
            Assert.That(Descriptors(codeGen)[1].idAddr().iiaLclVar.lvaOffset(), Is.Zero);
        });
    }

    [Test]
    public static void FilterReturnsDoNotMarkOrdinaryReturnRoots()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            var source = compiler.gtNewIconNode(TYP_INT, 1);
            source.RegNum = REG_RBX;
            codeGen.genReturn(new GenTreeUnOp(GT_RETFILT, TYP_INT, source));

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void AsyncReturnClearsAndMarksContinuationAfterValue()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ASYNC);
            var source = compiler.gtNewIconNode(TYP_INT, 1);
            source.RegNum = REG_RBX;
            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_REF, source));

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_xor]));
            Assert.That(Descriptors(codeGen)[^1].idReg1(), Is.EqualTo(REG_ASYNC_CONTINUATION_RET));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(RBM_RAX | new regMaskTP(REG_ASYNC_CONTINUATION_RET.SingleTypeMask)));
        });
    }

    [Test]
    public static void SuspensionReturnsClearReferenceReturnRegistersAndMarkContinuation()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            var source = compiler.gtNewIconNode(TYP_REF, 1);
            source.RegNum = REG_RBX;
            codeGen.genReturnSuspend(new GenTreeUnOp(GT_RETURN_SUSPEND, TYP_VOID, source));

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_xor]));
            Assert.That(Descriptors(codeGen)[0].idReg1(), Is.EqualTo(REG_ASYNC_CONTINUATION_RET));
            Assert.That(Descriptors(codeGen)[1].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(RBM_RAX | new regMaskTP(REG_ASYNC_CONTINUATION_RET.SingleTypeMask)));
        });
    }

    [Test]
    public static void AsyncContinuationTransfersTheReturnRegisterAndItsGcState()
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            codeGen.GCInfo.gcMarkRegPtrVal(REG_ASYNC_CONTINUATION_RET, TYP_REF);
            var continuation = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF) { RegNum = REG_RBX };

            codeGen.genCodeForAsyncContinuation(continuation);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov]));
            Assert.That(Descriptors(codeGen)[0].idReg1(), Is.EqualTo(REG_RBX));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(RBM_RBX | new regMaskTP(REG_ASYNC_CONTINUATION_RET.SingleTypeMask)));
        });
    }

    [Test]
    public static void ProfilerLeaveObservesRestoredReturnRootBeforeCallback()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            using var callbacks = new SysVX64HelperCodeGenTests.HelperCallbacks(compiler, indirect: false);
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHnd = (void*)0x1234;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            codeGen.IsFramePointerUsed = true;
            var source = compiler.gtNewIconNode(TYP_REF, 1);
            source.RegNum = REG_RBX;

            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_REF, source));

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_mov, INS_lea, INS_call]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
            Assert.That(compiler.info.compProfilerCallback, Is.True);
            Assert.That(SysVX64HelperCodeGenTests.HelperCallbacks.Assertions, Is.Zero);
        });
    }

#if SWIFT_SUPPORT
    [Test]
    public static void SwiftErrorReturnsMoveErrorBeforeReturningOrdinaryValue()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            var error = compiler.gtNewIconNode(TYP_BYREF, 1);
            error.RegNum = REG_RBX;
            var value = compiler.gtNewIconNode(TYP_REF, 1);
            value.RegNum = REG_RCX;
            codeGen.genSwiftErrorReturn(new GenTreeOp(GT_SWIFT_ERROR_RET, TYP_REF, error, value));

            Assert.That(Descriptors(codeGen).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_SWIFT_ERROR, REG_RAX]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
        });
    }

    [Test]
    public static void SwiftStructReturnLoadsLoweredFieldsAtTheirDeclaredOffsets()
    {
        WithReturn(TYP_STRUCT, (compiler, codeGen) =>
        {
            compiler.info.compCallConv = CorInfoCallConvExtension.Swift;
            SetReturnRegisters(compiler, TYP_LONG, TYP_INT);
            CORINFO_METHOD_INFO methodInfo = default;
            var retClass = (CORINFO_CLASS_STRUCT_*)0x1234;
            methodInfo.args.retTypeClass = retClass;
            compiler.info.compMethodInfo = &methodInfo;
            var lowering = new CORINFO_SWIFT_LOWERING { numLoweredElements = 2 };
            lowering.offsets[0] = 0;
            lowering.offsets[1] = 12;
            compiler._swiftLoweringCache = [];
            compiler._swiftLoweringCache[new Pointer<CORINFO_CLASS_STRUCT_>(retClass)] =
                new StrongBox<CORINFO_SWIFT_LOWERING>(lowering);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = new ClassLayout(16), lvIsMultiRegRet = true,
                lvOnFrame = true, StackOffset = -32, RegNum = REG_STK,
            };
            var local = new GenTreeLclVar(TYP_STRUCT, 0) { IsContained = true };

            codeGen.genReturn(new GenTreeUnOp(GT_RETURN, TYP_STRUCT, local));

            Assert.That(Descriptors(codeGen).Select(id => id.idAddr().iiaLclVar.lvaOffset()),
                Is.EqualTo((uint[])[0, 12]));
            Assert.That(Descriptors(codeGen).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_RAX, REG_RDX]));
        });
    }
#endif

    [TestCase(TYP_VOID)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void ExitReservesEpilogAfterRecordingReturnRoots(var_types type)
    {
        WithReturn(type, (compiler, codeGen) =>
        {
            var block = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            compiler.compCurBB = block;
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];
            if (type != TYP_VOID)
            {
                codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, type);
            }

            codeGen.genExitCode(block);

            Assert.That(compiler.genIPmappings.Single().ipmdKind, Is.EqualTo(IPmappingDscKind.Epilog));
            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
            Assert.That(PlaceholderHead(codeGen.Emitter)?.igFlags & InsGroupFlags.Epilog,
                Is.EqualTo(InsGroupFlags.Epilog));
        });
    }

    [Test]
    public static void ExitChecksCookieBeforeReservingEpilog()
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            using var callbacks = new SysVX64HelperCodeGenTests.HelperCallbacks(compiler, indirect: false);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_LONG, lvOnFrame = true, StackOffset = -8, RegNum = REG_STK,
            };
            compiler.lvaGSSecurityCookie = 0;
            compiler.gsGlobalSecurityCookieVal = 1;
            compiler.NeedsGSSecurityCookie = true;
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];
            var block = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            compiler.compCurBB = block;
            var first = codeGen.Emitter.emitCurIG;

            codeGen.genExitCode(block);

            Assert.That(AllDescriptors(first, codeGen).Select(id => id.idIns()).Take(3),
                Is.EqualTo((instruction[])[INS_cmp, INS_je, INS_call]));
            Assert.That(PlaceholderHead(codeGen.Emitter)?.igFlags & InsGroupFlags.Epilog,
                Is.EqualTo(InsGroupFlags.Epilog));
            Assert.That(SysVX64HelperCodeGenTests.HelperCallbacks.Assertions, Is.Zero);
        });
    }

    [Test]
    public static void TailJumpExitSkipsReturnRootValidation()
    {
        WithReturn(TYP_REF, (compiler, codeGen) =>
        {
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];
            var block = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            block.SetFlags(BBF_HAS_JMP);
            compiler.compCurBB = block;

            codeGen.genExitCode(block);

            Assert.That(PlaceholderHead(codeGen.Emitter)?.igFlags & InsGroupFlags.Epilog,
                Is.EqualTo(InsGroupFlags.Epilog));
        });
    }

    [TestCase(1L, false)]
    [TestCase(2147483648L, true)]
    public static void CookieCheckUsesSignedImmediateOrTemporaryRegister(long cookie, bool load)
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            using var callbacks = new SysVX64HelperCodeGenTests.HelperCallbacks(compiler, indirect: false);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_LONG, lvOnFrame = true, StackOffset = -8, RegNum = REG_STK,
            };
            compiler.lvaGSSecurityCookie = 0;
            compiler.gsGlobalSecurityCookieVal = (nint)cookie;

            var first = codeGen.Emitter.emitCurIG;
            codeGen.genEmitGSCookieCheck(false);

            Assert.That(AllDescriptors(first, codeGen).Select(id => id.idIns()), Is.EqualTo(load
                ? (instruction[])[INS_mov, INS_cmp, INS_je, INS_call]
                : [INS_cmp, INS_je, INS_call]));
            Assert.That(SysVX64HelperCodeGenTests.HelperCallbacks.Assertions, Is.Zero);
        });
    }

#if DEBUG
    [Test]
    public static void StackPointerCheckEmitsConditionalTrapAtReturn()
    {
        WithReturn(TYP_VOID, (compiler, codeGen) =>
        {
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_LONG, lvOnFrame = true, lvDoNotEnregister = true,
                StackOffset = -8, RegNum = REG_STK,
            };
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genStackPointerCheck(true, 0);

            Assert.That(AllDescriptors(first, codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_cmp, INS_je, INS_int3]));
        });
    }
#endif

    private static void WithReturn(var_types type, Action<Compiler, CodeGen> action)
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags &= ~(InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead);
            compiler.compHndBBtab = [];
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.info.compRetNativeType = type;
            compiler.compRetTypeDesc = new ReturnTypeDesc();
            if (type != TYP_STRUCT)
            {
                compiler.compRetTypeDesc.InitializeReturnType(compiler, type, NO_CLASS_HANDLE,
                    compiler.info.compCallConv);
            }
            action(compiler, codeGen);
        });
    }

    private static void SetReturnRegisters(Compiler compiler, var_types first, var_types second)
    {
        compiler.compRetTypeDesc.InitializeReturnType(compiler, first, NO_CLASS_HANDLE,
            compiler.info.compCallConv);
        RegisterTypes(ref compiler.compRetTypeDesc)[1] = second;
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter)
            ?? throw new AssertionException("Missing instruction descriptors.");
    }

    private static List<Emitter.instrDesc> AllDescriptors(insGroup? first, CodeGen codeGen)
    {
        var result = new List<Emitter.instrDesc>();
        var group = first;
        while (group != codeGen.Emitter.emitCurIG)
        {
            if (group is null)
            {
                throw new AssertionException("Missing current instruction group.");
            }
            if (group.igData is not null)
            {
                result.AddRange(group.igData);
            }
            group = group.igNext;
        }
        result.AddRange(Descriptors(codeGen));
        return result;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regType")]
    private static extern ref InlineArrayMaxRetRegCount<var_types> RegisterTypes(ref ReturnTypeDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_returnTypeDesc")]
    private static extern ref ReturnTypeDesc CallReturnDescriptor(GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderList")]
    private static extern ref insGroup? PlaceholderHead(Emitter emitter);
}
#endif
