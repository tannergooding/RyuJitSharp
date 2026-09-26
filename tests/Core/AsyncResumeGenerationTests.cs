// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
#endif
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class AsyncResumeGenerationTests
{
    [Test]
    public static void WindowsAmd64EntriesHaveTwoNativePointerFields()
    {
        Assert.That(sizeof(CORINFO_AsyncResumeInfo), Is.EqualTo(16));
        Assert.That(Marshal.OffsetOf<CORINFO_AsyncResumeInfo>(nameof(CORINFO_AsyncResumeInfo.Resume)),
            Is.EqualTo((nint)0));
        Assert.That(Marshal.OffsetOf<CORINFO_AsyncResumeInfo>(nameof(CORINFO_AsyncResumeInfo.DiagnosticIP)),
            Is.EqualTo((nint)8));
    }

    [TestCase(0u, 0u)]
    [TestCase(1u, 0u)]
    [TestCase(3u, 0u)]
    [TestCase(0u, 4u)]
    [TestCase(1u, 4u)]
    [TestCase(3u, 4u)]
    public static void TablesRetainEntryCountPointerAlignmentAndAnEmitterWideStubCache(uint count, uint prefix)
    {
        WithResumeEmitter(0, (compiler, codeGen, context) =>
        {
            var emitter = codeGen.Emitter;
            if (prefix != 0)
            {
                _ = emitter.emitDataConst(new byte[prefix], 4, TYP_INT);
            }
            var previous = emitter.emitConsDsc.dsdLast;
            var current = emitter.emitDataSecCur;

            emitter.emitAsyncResumeTable(count, out var offset, out var table);

            Assert.That(offset, Is.EqualTo(prefix == 0 ? 0u : 8u));
            Assert.That(table.dsOffset, Is.EqualTo(offset));
            Assert.That(table.dsSize, Is.EqualTo(count * 16u));
            Assert.That(table.dsAlignment, Is.EqualTo(8u));
            Assert.That(table.dsDataType, Is.EqualTo(TYP_UNKNOWN));
            Assert.That(table.dsType, Is.EqualTo(Emitter.dataSection.sectionType.asyncResumeInfo));
            Assert.That(table.Locations, Has.Length.EqualTo(count));
            Assert.That(table.Locations.All(location => !location.Valid()), Is.True);
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(offset + (count * 16u)));
            Assert.That(emitter.emitDataSecCur, Is.SameAs(current));
            Assert.That(previous is null ? emitter.emitConsDsc.dsdList : previous.dsNext, Is.SameAs(table));
            Assert.That(table.dsNext, Is.Null);
            Assert.That(context->Queries, Is.EqualTo(1));
            Assert.That((nuint)StubHandle(emitter), Is.EqualTo((nuint)context->Method));
            Assert.That((nuint)StubEntryPoint(emitter), Is.EqualTo((nuint)context->EntryPoint));

            emitter.emitAsyncResumeTable(1, out var secondOffset, out var second);

            Assert.That(secondOffset, Is.EqualTo(offset + (count * 16u)));
            Assert.That(table.dsNext, Is.SameAs(second));
            Assert.That(emitter.emitConsDsc.dsdLast, Is.SameAs(second));
            Assert.That(second.Locations, Is.Not.SameAs(table.Locations));
            Assert.That(context->Queries, Is.EqualTo(1));
        });
    }

    [Test]
    public static void RegisteringATableDoesNotReplaceAnOpenBinaryDataSection()
    {
        WithResumeEmitter(1, (compiler, codeGen, context) =>
        {
            var emitter = codeGen.Emitter;
            _ = emitter.emitDataGenBeg(4, 4, TYP_INT);
            var binary = emitter.emitDataSecCur ?? throw new AssertionException("Missing binary section.");

            emitter.emitAsyncResumeTable(1, out var offset, out var table);
            emitter.emitDataGenData(0, [1, 2, 3, 4]);
            emitter.emitDataGenEnd();
            var afterOffset = emitter.emitDataConst([5, 6, 7, 8], 4, TYP_INT);

            Assert.That(offset, Is.EqualTo(8u));
            Assert.That(afterOffset, Is.EqualTo(24u));
            Assert.That(binary.Data, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(binary.dsNext, Is.SameAs(table));
            Assert.That(table.dsNext, Is.SameAs(emitter.emitConsDsc.dsdLast));
            Assert.That(context->Queries, Is.EqualTo(1));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public static void CodeGenRegistersOneTableAndEncodesEachStateAsADataOffset(int count)
    {
        WithResumeEmitter(count, (compiler, codeGen, context) =>
        {
            var emitter = codeGen.Emitter;
            _ = emitter.emitDataConst(new byte[4], 4, TYP_INT);

            var offset = codeGen.genEmitAsyncResumeInfoTable(out var table);
            var repeatedOffset = codeGen.genEmitAsyncResumeInfoTable(out var repeated);

            Assert.That(offset, Is.EqualTo(8u));
            Assert.That(repeatedOffset, Is.EqualTo(offset));
            Assert.That(repeated, Is.SameAs(table));
            Assert.That(table.Locations, Has.Length.EqualTo(count));
            Assert.That(table.dsSize, Is.EqualTo(count * 16));
            for (var state = 0; state < count; state++)
            {
                var handle = codeGen.genEmitAsyncResumeInfo((uint)state);
                Assert.That(Compiler.eeIsJitDataOffs(handle), Is.True);
                Assert.That(Compiler.eeGetJitDataOffs(handle), Is.EqualTo(8 + (state * 16)));
            }
            Assert.That(context->Queries, Is.EqualTo(1));
            Assert.That(table.dsNext, Is.Null);
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(8 + (count * 16)));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [Test]
    public static void RecordingCapturesInstructionPositionsAndLeavesRemovedStatesInvalid()
    {
        WithResumeEmitter(4, (compiler, codeGen, context) =>
        {
            var emitter = codeGen.Emitter;
            codeGen.genRecordAsyncResume(new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 2));
            _ = codeGen.genEmitAsyncResumeInfoTable(out var table);
            var firstGroup = emitter.emitCurIG ?? throw new AssertionException("Missing initial group.");
            emitter.emitIns(INS_nop);
            codeGen.genRecordAsyncResume(new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 0));
            emitter.emitIns(INS_nop);
            _ = emitter.emitAddLabel(codeGen.GCInfo.gcVarPtrSetCur,
                codeGen.GCInfo.gcRegGCrefSetCur, codeGen.GCInfo.gcRegByrefSetCur);
            emitter.emitIns(INS_nop);
            codeGen.genRecordAsyncResume(new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 3));

            Assert.That(table.Locations[2], Is.EqualTo(new emitLocation(firstGroup)));
            Assert.That(table.Locations[0].GetIG(), Is.SameAs(firstGroup));
            Assert.That(table.Locations[0].GetInsNum(), Is.EqualTo(1));
            Assert.That(table.Locations[0].GetInsOffset(), Is.EqualTo(1));
            Assert.That(table.Locations[3], Is.EqualTo(new emitLocation(emitter)));
            Assert.That(table.Locations[1].Valid(), Is.False);
            Assert.That(compiler.compSuspensionPoints!.All(point => point.DiagnosticNativeOffset == 123), Is.True);

            // Final encoding can change sizes; retain the group/index cookie,
            // not the originally estimated diagnostic byte offset.
            var saved = firstGroup.igData ?? throw new AssertionException("Missing saved instructions.");
            saved[0].idCodeSize(3);
            firstGroup.igSize = 4;
            firstGroup.igOffs = 64;
            firstGroup.igFlags |= InsGroupFlags.UpdatedInstructionSize;
            Assert.That(table.Locations[0].CodeOffset(emitter), Is.EqualTo(67u));
            Assert.That(table.Locations[2].CodeOffset(emitter), Is.EqualTo(64u));

            codeGen.genRecordAsyncResume(new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 0));
            Assert.That(table.Locations[0], Is.EqualTo(table.Locations[3]));
            Assert.That(context->Queries, Is.EqualTo(1));
        });
    }

    [TestCase(1L, REG_RAX)]
    [TestCase(0x100000001L, REG_R11)]
    public static void AddressGenerationUsesNativeStateTruncationAndProducesTheDestinationRegister(
        long state, regNumber reg)
    {
        WithResumeEmitter(2, (compiler, codeGen, context) =>
        {
            var emitter = codeGen.Emitter;
            _ = emitter.emitDataConst(new byte[4], 4, TYP_INT);
            codeGen.GCInfo.gcMarkRegPtrVal(reg, TYP_REF);
            var tree = new GenTreeVal(GT_ASYNC_RESUME_INFO, TYP_I_IMPL, unchecked((nint)state))
            {
                RegNum = reg,
            };

            codeGen.genAsyncResumeInfo(tree);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_lea));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(Compiler.eeGetJitDataOffs(id.idAddr().iiaFieldHnd), Is.EqualTo(24));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(context->Queries, Is.EqualTo(1));
#if DEBUG
            Assert.That(tree._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED));
#endif
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void DspCodeRecordsAsyncResumeTablesAndInstructions(int operation)
    {
        WithResumeEmitter(1, (compiler, codeGen, context) =>
        {
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
            {
                switch (operation)
                {
                    case 0:
                    {
                        codeGen.Emitter.emitAsyncResumeTable(1, out _, out _);
                        break;
                    }

                    case 1:
                    {
                        _ = codeGen.genEmitAsyncResumeInfoTable(out _);
                        break;
                    }

                    case 2:
                    {
                        _ = codeGen.genEmitAsyncResumeInfo(0);
                        break;
                    }

                    case 3:
                    {
                        codeGen.genRecordAsyncResume(new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 0));
                        break;
                    }

                    case 4:
                    {
                        codeGen.genAsyncResumeInfo(new GenTreeVal(GT_ASYNC_RESUME_INFO, TYP_I_IMPL, 0)
                        {
                            RegNum = REG_RAX,
                        });
                        break;
                    }
                }
            });

            Assert.That(codeGen.Emitter.emitConsDsc.dsdOffs, Is.GreaterThan(0));
            Assert.That(context->Queries, Is.EqualTo(1));
            Assert.That(Descriptors(codeGen).Count > 0, Is.EqualTo(operation == 4));
            Assert.That(diagnostic.Contains("lea", StringComparison.Ordinal), Is.EqualTo(operation == 4));
        });
    }
#endif

    private static void WithResumeEmitter(int count, ResumeTest action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getAsyncResumptionStub = &GetAsyncResumptionStub;
            CORINFO_METHOD_STRUCT_ method = default;
            var context = new ResumeContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Method = &method,
                EntryPoint = (void*)(delegate* unmanaged<void>)&ResumeStub,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            codeGen.Emitter.emitCmpHandle = &context.JitInfo;
            compiler.compSuspensionPoints = [];
            for (var index = 0; index < count; index++)
            {
                compiler.compSuspensionPoints.Add(new ICorDebugInfo.AsyncSuspensionPoint
                {
                    DiagnosticNativeOffset = 123,
                });
            }

            action(compiler, codeGen, &context);
        });
    }

    private delegate void ResumeTest(Compiler compiler, CodeGen codeGen, ResumeContext* context);

    private struct ResumeContext
    {
        public ICorJitInfo JitInfo;
        public CORINFO_METHOD_STRUCT_* Method;
        public void* EntryPoint;
        public int Queries;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* GetAsyncResumptionStub(ICorJitInfo* self, void** entryPoint)
    {
        var context = (ResumeContext*)self;
        context->Queries++;
        *entryPoint = context->EntryPoint;

        return context->Method;
    }

    [UnmanagedCallersOnly]
    private static void ResumeStub()
    {
        Assert.Fail("The recording fixture must not execute the resume stub.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAsyncResumeStub")]
    private static extern ref CORINFO_METHOD_STRUCT_* StubHandle(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAsyncResumeStubEntryPoint")]
    private static extern ref void* StubEntryPoint(Emitter emitter);
}
