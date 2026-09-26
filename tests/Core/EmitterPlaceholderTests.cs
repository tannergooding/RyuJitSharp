// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insGroupPlaceholderType;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class EmitterPlaceholderTests
{
    [TestCase(IGPT_PROLOG, false, InsGroupFlags.None)]
    [TestCase(IGPT_EPILOG, false, InsGroupFlags.Epilog)]
    [TestCase(IGPT_EPILOG, true, InsGroupFlags.Epilog)]
    [TestCase(IGPT_FUNCLET_PROLOG, false, InsGroupFlags.FuncletProlog)]
    [TestCase(IGPT_FUNCLET_EPILOG, false, InsGroupFlags.FuncletEpilog)]
    [TestCase(IGPT_FUNCLET_EPILOG, true, InsGroupFlags.FuncletEpilog)]
    public static void ReservationKindsPreserveNativeSnapshotsAndContinuation(
        insGroupPlaceholderType kind, bool last, InsGroupFlags flag)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            var vars = VarSetOps.MakeSingleton(compiler, 0);
            var placeholder = emitter.emitAddLabel(vars, new(SRBM_RAX), new(SRBM_RDX));
            var initialOffset = placeholder.igOffs;
            compiler.compCurrFuncIdx = 3;
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];
            codeGen.GCInfo.gcVarPtrSetCur = vars;
            codeGen.GCInfo.gcRegGCrefSetCur = new(SRBM_EXCEPTION_OBJECT);
            codeGen.GCInfo.gcRegByrefSetCur = default;
            NoGcRequests(emitter) = 2;
            NoGcGroup(emitter) = true;
            placeholder.igFlags |= InsGroupFlags.NoGCInterrupt;
            var block = new BasicBlock(null, null)
            {
                Next = last ? null : new BasicBlock(null, null),
            };

            switch (kind)
            {
                case IGPT_PROLOG:
                {
                    codeGen.genReserveProlog(block);
                    break;
                }

                case IGPT_EPILOG:
                {
                    codeGen.genReserveEpilog(block);
                    break;
                }

                case IGPT_FUNCLET_PROLOG:
                {
                    codeGen.genReserveFuncletProlog(block);
                    break;
                }

                case IGPT_FUNCLET_EPILOG:
                {
                    codeGen.genReserveFuncletEpilog(block);
                    break;
                }
            }

            var data = placeholder.igPhData;
            assert(data is not null);
            var extend = kind is IGPT_EPILOG or IGPT_FUNCLET_EPILOG;
            var funclet = kind is IGPT_FUNCLET_PROLOG or IGPT_FUNCLET_EPILOG;
            var expectedRefs = kind == IGPT_PROLOG ? SRBM_NONE : extend ? SRBM_RAX : SRBM_EXCEPTION_OBJECT;
            Assert.That(FirstPlaceholder(emitter), Is.SameAs(placeholder));
            Assert.That(LastPlaceholder(emitter), Is.SameAs(placeholder));
            Assert.That(placeholder.igFuncIdx, Is.EqualTo(3));
            Assert.That(placeholder.igFlags, Is.EqualTo(
                flag | InsGroupFlags.Placeholder | InsGroupFlags.OutOfOrderHead | InsGroupFlags.NoGCInterrupt));
            Assert.That(data.igPhType, Is.EqualTo(kind));
            Assert.That(data.igPhBB, Is.SameAs(block));
            Assert.That(data.igPhNext, Is.Null);
            Assert.That(data.igPhInitGCrefRegs, Is.EqualTo(new regMaskTP(expectedRefs)));
            Assert.That(data.igPhInitByrefRegs, Is.EqualTo(new regMaskTP(extend ? SRBM_RDX : SRBM_NONE)));
            Assert.That(VarSetOps.IsMember(compiler, data.igPhInitGCrefVars, 0), Is.EqualTo(kind != IGPT_PROLOG));
            Assert.That(data.igPhInitGCrefVars, Is.Not.SameAs(vars));
            Assert.That(data.igPhInitGCrefVars, Is.Not.SameAs(data.igPhPrevGCrefVars));
            Assert.That(CodeOffset(emitter), Is.EqualTo(initialOffset + 256));
            Assert.That(LastInstruction(emitter), Is.Null);
            Assert.That(LastInstructionGroup(emitter), Is.Null);
            Assert.That(NoGcRequests(emitter), Is.EqualTo(extend && !last ? 0 : 2));
            Assert.That(NoGcGroup(emitter), Is.EqualTo(!extend || last));
            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(funclet ? 1 : 0));

            if (funclet)
            {
                var mapping = compiler.genIPmappings.First;
                assert(mapping is not null);
                Assert.That(mapping.Value.ipmdKind, Is.EqualTo(
                    kind == IGPT_FUNCLET_PROLOG ? IPmappingDscKind.Prolog : IPmappingDscKind.Epilog));
                Assert.That(mapping.Value.ipmdNativeLoc.GetIG(), Is.SameAs(placeholder));
                Assert.That(mapping.Value.ipmdNativeLoc.GetInsOffset(), Is.EqualTo(256));
                Assert.That(mapping.Value.ipmdIsLabel, Is.True);
            }

            if (last)
            {
                Assert.That(emitter.emitCurIG, Is.Null);
            }
            else
            {
                var next = emitter.emitCurIG;
                assert(next is not null);
                Assert.That(placeholder.igNext, Is.SameAs(next));
                Assert.That(next.igOffs, Is.EqualTo(initialOffset + 256));
                Assert.That(next.igFlags, Is.EqualTo(extend ? InsGroupFlags.None : InsGroupFlags.NoGCInterrupt));
                Assert.That(ForceGcState(emitter), Is.True);
            }

            VarSetOps.RemoveElemD(compiler, vars, 0);
            Assert.That(VarSetOps.IsMember(compiler, data.igPhInitGCrefVars, 0), Is.EqualTo(kind != IGPT_PROLOG));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void EpilogsSavePrecedingCodeAndPadBothGcAndNoGcCalls(int callKind)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, tree) =>
        {
            var emitter = codeGen.Emitter;
            var vars = VarSetOps.MakeSingleton(compiler, 0);
            _ = emitter.emitAddLabel(vars, new(SRBM_R8), new(SRBM_R9));
            emitter.emitIns(INS_nop);
            var body = emitter.emitAddLabel(VarSetOps.MakeEmpty(compiler), new(SRBM_RAX), new(SRBM_RDX));
            if (callKind == 0)
            {
                emitter.emitIns(INS_nop);
            }
            else
            {
                _ = EmitterLabelTests.RecordCallDescriptor(emitter, noGc: callKind == 2);
            }
            codeGen.genReserveEpilog(new BasicBlock(null, null));

            var placeholder = LastPlaceholder(emitter);
            assert(placeholder is not null);
            var data = placeholder.igPhData;
            assert(data is not null);
            Assert.That(body.igSize, Is.EqualTo(callKind == 0 ? 1 : 6));
            Assert.That(body.igInsCnt, Is.EqualTo(callKind == 0 ? 1 : 2));
            assert(body.igData is not null);
            Assert.That(body.igData[^1].idIns(), Is.EqualTo(INS_nop));
            Assert.That(body.igNext, Is.SameAs(placeholder));
            Assert.That(placeholder.igFlags & InsGroupFlags.Extend, Is.EqualTo(InsGroupFlags.Extend));
            Assert.That(data.igPhPrevGCrefRegs, Is.EqualTo(new regMaskTP(SRBM_R8)));
            Assert.That(data.igPhPrevByrefRegs, Is.EqualTo(new regMaskTP(SRBM_R9)));
            Assert.That(data.igPhInitGCrefRegs, Is.EqualTo(new regMaskTP(SRBM_RAX)));
            Assert.That(data.igPhInitByrefRegs, Is.EqualTo(new regMaskTP(SRBM_RDX)));
            Assert.That(data.igPhPrevGCrefVars, Is.Not.SameAs(data.igPhInitGCrefVars));
            VarSetOps.RemoveElemD(compiler, vars, 0);
            Assert.That(VarSetOps.IsMember(compiler, data.igPhPrevGCrefVars, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, data.igPhInitGCrefVars, 0), Is.False);
        });
    }

    [Test]
    public static void MultipleReservationsLinkInOrderAndInvalidateLastInstruction()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            codeGen.genReserveProlog(new BasicBlock(null, null));
            var root = LastPlaceholder(emitter);
            assert(root is not null);
            compiler.compCurrFuncIdx = 1;
            codeGen.genReserveFuncletProlog(new BasicBlock(null, null));
            var prolog = LastPlaceholder(emitter);
            assert(prolog is not null);
            emitter.emitIns(INS_nop);
            codeGen.genReserveFuncletEpilog(new BasicBlock(null, null));
            var epilog = LastPlaceholder(emitter);
            assert(epilog is not null);
            assert(root.igPhData is not null);
            assert(prolog.igPhData is not null);
            assert(epilog.igPhData is not null);

            Assert.That(root.igPhData.igPhNext, Is.SameAs(prolog));
            Assert.That(prolog.igPhData.igPhNext, Is.SameAs(epilog));
            Assert.That(epilog.igPhData.igPhNext, Is.Null);
            Assert.That(CodeOffset(emitter), Is.EqualTo(769));
            Assert.That(LastInstruction(emitter), Is.Null);
            Assert.That(LastInstructionGroup(emitter), Is.Null);
        });
    }

#if DEBUG
    [Test]
    public static void RequiredCallPaddingSupportsDisassemblyDuringEpilogReservation()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            var group = emitter.emitCurIG;
            var call = EmitterLabelTests.RecordCallDescriptor(emitter, noGc: false);
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(
                () => codeGen.genReserveEpilog(new BasicBlock(null, null)));
            Assert.That(FirstPlaceholder(emitter), Is.Not.Null);
            Assert.That(emitter.emitCurIG, Is.Not.SameAs(group));
            Assert.That(call.idIns(), Is.EqualTo(INS_call));
            Assert.That(diagnostic, Does.Contain("nop"));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderList")]
    private static extern ref insGroup? FirstPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderLast")]
    private static extern ref insGroup? LastPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCIG")]
    private static extern ref bool NoGcGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurCodeOffset")]
    private static extern ref int CodeOffset(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceStoreGCState")]
    private static extern ref bool ForceGcState(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsIG")]
    private static extern ref insGroup? LastInstructionGroup(Emitter emitter);
}
