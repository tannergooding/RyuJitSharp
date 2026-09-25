// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanCallBuildingTests
{
    [TestCase(TYP_VOID, SRBM_NONE)]
    [TestCase(TYP_INT, SRBM_INTRET)]
    [TestCase(TYP_LONG, SRBM_LNGRET)]
    [TestCase(TYP_DOUBLE, SRBM_FLOATRET)]
    [TestCase(TYP_SIMD16, SRBM_FLOATRET)]
    public static void CallKillsBeforeDefiningItsAbiReturn(var_types type, regMask returnRegister)
    {
        WithAllocator((compiler, allocator) => {
            var call = compiler.gtNewCallNode(type, CT_USER_FUNC, null);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCall(allocator, call), Is.Zero);
            var kill = allocator.refPositions.Find(reference => reference.refType is RefType.RefTypeKill)
                ?? throw new AssertionException("The call did not kill its caller-saved registers.");
            Assert.That(kill.nodeLocation, Is.EqualTo(5));
            var callDefs = allocator.refPositions.FindAll(reference =>
                (reference.treeNode == call) && reference.refType is RefType.RefTypeDef);
            Assert.That(callDefs, Has.Count.EqualTo(type is TYP_VOID ? 0 : 1));
            if (type is not TYP_VOID)
            {
                Assert.That(callDefs[0].registerAssignment, Is.EqualTo(returnRegister));
                Assert.That(allocator.refPositions.IndexOf(kill),
                    Is.LessThan(allocator.refPositions.IndexOf(callDefs[0])));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FastTailCallTargetAvoidsGsCookieTemporary(bool cookie, bool secretStub)
    {
        WithAllocator((compiler, allocator) => {
            if (cookie)
            {
                compiler.NeedsGSSecurityCookie = true;
            }
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            ReferenceBuildLocation(allocator) = 2;
            var targetDef = BuildDef(allocator, target, SRBM_NONE, 0);
            var call = compiler.gtNewCallNode(TYP_VOID, CT_INDIRECT, null);
            call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            call.ControlExpr = target;
            if (secretStub)
            {
                var secret = compiler.gtNewIconNode(TYP_I_IMPL, 7);
                var putArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_I_IMPL, secret) { RegNum = REG_R10 };
                _ = BuildDef(allocator, putArg, SRBM_R10, 0);
                var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(secret).WithWellKnownArg(
                    WellKnownArg.SecretStubParam));
                arg.EarlyNode = null;
                arg.LateNode = putArg;
                call.Args.PushLateBack(arg);
            }

            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildCall(allocator, call), Is.EqualTo(secretStub ? 2 : 1));
            var excluded = !cookie ? SRBM_NONE : secretStub ? SRBM_R11 : SRBM_R10;
            Assert.That(targetDef.nextRefPosition?.registerAssignment,
                Is.EqualTo(SRBM_INT_CALLEE_TRASH_INIT & ~excluded));
        });
    }

    [TestCase(REG_RCX)]
    [TestCase(REG_XMM2)]
    public static void RegisterArgumentsUseAndDefineTheirAbiRegister(regNumber register)
    {
        WithAllocator((compiler, allocator) => {
            var type = register is REG_XMM2 ? TYP_DOUBLE : TYP_INT;
            GenTree value = type is TYP_DOUBLE
                ? compiler.gtNewDconNode(TYP_DOUBLE, 2.5)
                : compiler.gtNewIconNode(TYP_INT, 5);
            ReferenceBuildLocation(allocator) = 2;
            var valueDef = BuildDef(allocator, value, SRBM_NONE, 0);
            var arg = new GenTreeUnOp(GT_PUTARG_REG, type, value) { RegNum = register };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildPutArgReg(allocator, arg), Is.EqualTo(1));
            Assert.That(valueDef.nextRefPosition?.registerAssignment,
                Is.EqualTo(genSingleTypeRegMask(register)));
            Assert.That(allocator.refPositions[^1].registerAssignment,
                Is.EqualTo(genSingleTypeRegMask(register)));
            Assert.That(PlacedArgumentRegisters(allocator).IsSet(register), Is.True);

            var argumentDefinition = allocator.refPositions[^1];
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var callArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            callArg.EarlyNode = null;
            callArg.LateNode = arg;
            call.Args.PushLateBack(callArg);
            ReferenceBuildLocation(allocator) = 6;

            Assert.That(BuildCall(allocator, call), Is.EqualTo(1));
            Assert.That(argumentDefinition.nextRefPosition?.registerAssignment,
                Is.EqualTo(genSingleTypeRegMask(register)));
            Assert.That(PlacedArgumentRegisters(allocator).IsEmpty, Is.True);
            Assert.That(PlacedArgumentLocalCount(allocator), Is.Zero);
        });
    }

    [Test]
    public static void NonLastUseLocalArgumentRetainsItsRegisterRelationship()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [
                new() { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            var localInterval = new Interval(TYP_INT, SRBM_ALLINT_INIT);
            allocator.localVarIntervals = [localInterval];
            localInterval.setLocalNumber(compiler, 0, allocator);
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            var arg = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, source) { RegNum = REG_RCX };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildPutArgReg(allocator, arg), Is.EqualTo(1));
            var definition = allocator.refPositions[^1];
            Assert.That(definition.getInterval().isSpecialPutArg, Is.True);
            Assert.That(definition.getInterval().relatedInterval, Is.SameAs(localInterval));
            Assert.That(PlacedArgumentLocalCount(allocator), Is.EqualTo(1));
        });
    }

    [Test]
    public static void WriteBarrierUsesFixedArgumentRegistersThenKillsHelperRegisters()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [
                new() { Type = TYP_BYREF },
                new() { Type = TYP_REF },
            ];
            compiler.lvaCount = 2;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var source = compiler.gtNewLclvNode(TYP_REF, 1);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var sourceDef = BuildDef(allocator, source, SRBM_NONE, 0);
            var store = new GenTreeStoreInd(TYP_REF, address, source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildGCWriteBarrier(allocator, store), Is.EqualTo(2));
            Assert.That(addressDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_WRITE_BARRIER_DST));
            Assert.That(sourceDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_WRITE_BARRIER_SRC));
            Assert.That(allocator.refPositions.Exists(reference =>
                reference.refType is RefType.RefTypeKill && reference.nodeLocation == 5), Is.True);
        });
    }

    [Test]
    public static void AsyncContinuationKillIsDelayedBeforeTheCallKill()
    {
        WithAllocator((compiler, allocator) => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            call._callMoreFlags |= GTF_CALL_M_ASYNC;
            call.Next = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF);
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ASYNC);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCall(allocator, call), Is.Zero);
            var continuationKill = allocator.refPositions.Find(reference =>
                reference.refType is RefType.RefTypeKill &&
                reference.registerAssignment == genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET))
                ?? throw new AssertionException("The continuation register was not reserved.");
            Assert.That(continuationKill.delayRegFree, Is.True);
            Assert.That(continuationKill.nodeLocation, Is.EqualTo(5));
            var callKill = allocator.refPositions.FindLast(reference => reference.refType is RefType.RefTypeKill)
                ?? throw new AssertionException("The call did not kill registers.");
            Assert.That(allocator.refPositions.IndexOf(continuationKill),
                Is.LessThan(allocator.refPositions.IndexOf(callKill)));
        });
    }

    [TestCase(REG_XMM0, SRBM_RCX)]
    [TestCase(REG_XMM1, SRBM_RDX)]
    [TestCase(REG_XMM2, SRBM_R8)]
    [TestCase(REG_XMM3, SRBM_R9)]
    public static void WindowsVarargsFloatsReserveTheCorrespondingIntegerRegister(
        regNumber floatRegister, regMask integerRegister)
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewDconNode(TYP_DOUBLE, 2.5);
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var putArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_DOUBLE, value) { RegNum = floatRegister };
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, putArg, genSingleTypeRegMask(floatRegister), 0);
            var targetDef = BuildDef(allocator, target, SRBM_NONE, 0);
            var call = compiler.gtNewCallNode(TYP_VOID, CT_INDIRECT, null);
            call.Args.IsVarArgs = true;
            call.ControlExpr = target;
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            arg.EarlyNode = null;
            arg.LateNode = putArg;
            arg.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(floatRegister, 0, 8);
            call.Args.PushLateBack(arg);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCall(allocator, call), Is.EqualTo(2));
            var internalDef = allocator.refPositions.Find(reference =>
                (reference.treeNode == call) && reference.refType is RefType.RefTypeDef &&
                reference.getInterval().isInternal)
                ?? throw new AssertionException("Varargs did not reserve the matching integer register.");
            Assert.That(internalDef.registerAssignment, Is.EqualTo(integerRegister));
            Assert.That(internalDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(targetDef.nextRefPosition?.registerAssignment,
                Is.EqualTo(AvailableIntRegs(allocator) & ~SRBM_ARG_REGS));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallsRecordVzeroupperOnlyWhenTheCalleeNeedsIt(bool pinvoke)
    {
        WithAllocator((compiler, allocator) => {
            EnableInstructionSet(compiler, InstructionSet_AVX);
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            if (pinvoke)
            {
                call._callMoreFlags |= GTF_CALL_M_PINVOKE;
            }

            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildCall(allocator, call), Is.Zero);
            Assert.That(compiler.codeGen?.Emitter.ContainsCallNeedingVzeroupper, Is.EqualTo(pinvoke));
        });
    }

#if SWIFT_SUPPORT
    [Test]
    public static void SwiftErrorArgumentKeepsItsRegisterBusyThroughTheCall()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            var putArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_I_IMPL, source) { RegNum = REG_SWIFT_ERROR };
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, putArg, SRBM_SWIFT_ERROR, 0);
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            call.Flags |= GTF_CALL_UNMANAGED;
            call._unmgdCallConv = CorInfoCallConvExtension.Swift;
            call.Next = new GenTree(GT_SWIFT_ERROR, TYP_I_IMPL);
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(source).WithWellKnownArg(
                WellKnownArg.SwiftError));
            arg.EarlyNode = null;
            arg.LateNode = putArg;
            call.Args.PushLateBack(arg);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCall(allocator, call), Is.EqualTo(1));
            var fixedUse = allocator.physRegs[(int)REG_SWIFT_ERROR].lastRefPosition
                ?? throw new AssertionException("The Swift error register has no fixed argument use.");
            Assert.That(fixedUse.refType, Is.EqualTo(RefType.RefTypeFixedReg));
            Assert.That(fixedUse.nodeLocation, Is.EqualTo(4));
            Assert.That(fixedUse.delayRegFree, Is.True);
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCall")]
    private static extern int BuildCall(LinearScan allocator, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPutArgReg")]
    private static extern int BuildPutArgReg(LinearScan allocator, GenTreeUnOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildGCWriteBarrier")]
    private static extern int BuildGCWriteBarrier(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_placedArgumentRegisters")]
    private static extern ref regMaskTP PlacedArgumentRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_placedArgumentLocalCount")]
    private static extern ref int PlacedArgumentLocalCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask CompilerIntCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask CompilerMaskCalleeTrash(Compiler compiler);

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        CompilerIntCalleeTrash(compiler) = SRBM_INT_CALLEE_TRASH_INIT;
        CompilerFloatCalleeTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
        CompilerMaskCalleeTrash(compiler) = SRBM_MSK_CALLEE_TRASH_INIT;
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        codeGen.RegSet.rsClearRegsModified();
        try
        {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void EnableInstructionSet(Compiler compiler, CORINFO_InstructionSet isa)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
    }
}
