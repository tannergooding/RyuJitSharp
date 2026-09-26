// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
#if DEBUG
using System;
#endif
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if DEBUG
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenAsyncDebugInfoPublicationTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void DisabledOrAbsentAsyncInfoDoesNotAllocate(bool debugInfo, bool hasPoints)
    {
        WithPublication((compiler, codeGen, context) =>
        {
            compiler.opts.compDbgInfo = debugInfo;
            compiler.compSuspensionPoints = hasPoints ? [] : null;
            codeGen.genReportAsyncDebugInfo();

            Assert.That(context->Allocations, Is.Zero);
            Assert.That(context->Publications, Is.Zero);
        });
    }

    [Test]
    public static void EmptyCollectionsStillTransferBothHostArrays()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            compiler.compSuspensionPoints = [];
            compiler.compAsyncVars = [];
            codeGen.genReportAsyncDebugInfo();

            Assert.That(context->Allocations, Is.EqualTo(2));
            Assert.That(context->PointBytes, Is.EqualTo((nint)0));
            Assert.That(context->VarBytes, Is.EqualTo((nint)0));
            Assert.That(context->Publications, Is.EqualTo(1));
            Assert.That(context->PointCount, Is.Zero);
            Assert.That(context->VarCount, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PublicationResolvesLocationsAndDoesNotReadTransferredBuffers(bool hasResumeTable)
    {
        WithPublication((compiler, codeGen, context) =>
        {
            compiler.compSuspensionPoints =
            [
                new() { DiagnosticNativeOffset = 111, NumContinuationVars = 1 },
                new() { DiagnosticNativeOffset = 222, NumContinuationVars = 1 },
            ];
            compiler.compAsyncVars =
            [
                new() { VarNumber = -1, Offset = 0x1234 },
                new() { VarNumber = 2, Offset = -16 },
            ];
            if (hasResumeTable)
            {
                _ = codeGen.genEmitAsyncResumeInfoTable(out var table);
                var group = new insGroup { igOffs = 0xFFFFFFF0 };
#if DEBUG
                group.igSelf = group;
#endif
                table.Locations[0] = new emitLocation(group);
            }
#if DEBUG
            Verbose(codeGen) = true;
#endif
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                codeGen.genReportAsyncDebugInfo();
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            Assert.That(context->Allocations, Is.EqualTo(2));
            Assert.That(context->Publications, Is.EqualTo(1));
            Assert.That(context->PointBytes, Is.EqualTo((nint)(2 * sizeof(ICorDebugInfo.AsyncSuspensionPoint))));
            Assert.That(context->VarBytes, Is.EqualTo((nint)(2 * sizeof(ICorDebugInfo.AsyncContinuationVarInfo))));
            Assert.That(context->PointCount, Is.EqualTo(2));
            Assert.That(context->VarCount, Is.EqualTo(2));
            Assert.That(context->FirstPoint.DiagnosticNativeOffset, Is.EqualTo(hasResumeTable ? -16 : 0));
            Assert.That(context->SecondPoint.DiagnosticNativeOffset, Is.Zero);
            Assert.That(context->FirstPoint.NumContinuationVars, Is.EqualTo(1));
            Assert.That(context->FirstVar.VarNumber, Is.EqualTo(-1));
            Assert.That(context->FirstVar.Offset, Is.EqualTo(0x1234));
            Assert.That(context->SecondVar.VarNumber, Is.EqualTo(2));
            Assert.That(context->SecondVar.Offset, Is.EqualTo(-16));
#if DEBUG
            var expected = "Reported async suspension points:\n  [0] NumAsyncVars = 1\n  [1] NumAsyncVars = 1\n" +
                "Reported async vars:\n  [0] VarNumber = 4294967295, Offset = 1234\n  [1] VarNumber = 2, Offset = fffffff0\n";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                Is.EqualTo(expected.Replace("\n", "\r\n", StringComparison.Ordinal)));
#else
            Assert.That(stream.Length, Is.Zero);
#endif
        });
    }

#if DEBUG
    [Test]
    public static void RawHexPreservesByteOrderWithoutSeparatorsOrNewlines()
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        hexDump(writer, null, 0);
        var bytes = stackalloc byte[] { 0x00, 0x10, 0xAA, 0xFF };
        hexDump(writer, bytes, 4);
        writer.Flush();

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo("0010AAFF"));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_verbose")]
    private static extern ref bool Verbose(CodeGen codeGen);
#endif

    private static void WithPublication(PublicationAction action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getAsyncResumptionStub = &GetAsyncResumptionStub;
            vtable.Base.Base.allocateArray = &Allocate;
            vtable.Base.Base.reportAsyncDebugInfo = &Report;
            var storage = stackalloc byte[128];
            var context = new Context
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Storage = storage,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            codeGen.Emitter.emitCmpHandle = &context.JitInfo;
            compiler.opts.compDbgInfo = true;
            action(compiler, codeGen, &context);
        });
    }

    private delegate void PublicationAction(Compiler compiler, CodeGen codeGen, Context* context);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* GetAsyncResumptionStub(ICorJitInfo* jitInfo, void** entryPoint)
    {
        *entryPoint = (void*)(delegate* unmanaged<void>)&ResumeStub;

        return (CORINFO_METHOD_STRUCT_*)jitInfo;
    }

    [UnmanagedCallersOnly]
    private static void ResumeStub()
    {
    }

    private struct Context
    {
        public ICorJitInfo JitInfo;
        public byte* Storage;
        public int Allocations;
        public int Publications;
        public nint PointBytes;
        public nint VarBytes;
        public int PointCount;
        public int VarCount;
        public ICorDebugInfo.AsyncSuspensionPoint FirstPoint;
        public ICorDebugInfo.AsyncSuspensionPoint SecondPoint;
        public ICorDebugInfo.AsyncContinuationVarInfo FirstVar;
        public ICorDebugInfo.AsyncContinuationVarInfo SecondVar;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Allocate(ICorJitInfo* jitInfo, nint bytes)
    {
        var context = (Context*)jitInfo;
        var result = context->Storage + (context->Allocations * 64);
        if (context->Allocations++ == 0)
        {
            context->PointBytes = bytes;
        }
        else
        {
            context->VarBytes = bytes;
        }
        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Report(ICorJitInfo* jitInfo, ICorDebugInfo.AsyncInfo* info,
        ICorDebugInfo.AsyncSuspensionPoint* points, ICorDebugInfo.AsyncContinuationVarInfo* vars, int count)
    {
        var context = (Context*)jitInfo;
        context->Publications++;
        context->PointCount = info->NumSuspensionPoints;
        context->VarCount = count;
        if (info->NumSuspensionPoints == 2)
        {
            context->FirstPoint = points[0];
            context->SecondPoint = points[1];
        }
        if (count == 2)
        {
            context->FirstVar = vars[0];
            context->SecondVar = vars[1];
        }

        NativeMemory.Fill(points, (nuint)context->PointBytes, 0xCC);
        NativeMemory.Fill(vars, (nuint)context->VarBytes, 0xCC);
    }
}
#endif
