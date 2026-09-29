// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class XarchStaticOutputTests
{
    [TestCase(false, "64488B0510000000")]
    [TestCase(true, "65488B052510000000")]
    public static void SegmentStaticLoadsRetainNativePrefixesAndDisplacements(bool gs, string hex)
    {
        SysVX64EmitterCallTests.WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_C(INS_mov, EA_8BYTE, REG_RAX, gs ? FLD_GLOBAL_GS : FLD_GLOBAL_FS, 16);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No static load was recorded.");
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputCV(buffer, id, (ulong)insCodeRM(INS_mov) | 0x0500, null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
        });
    }

    [TestCase(-1, "48830510000000FF")]
    [TestCase(128, "4881051000000080000000")]
    public static void StaticImmediatesRetainSignedByteAndFullWidthForms(int immediate, string hex)
    {
        SysVX64EmitterCallTests.WithEmitter((_, emitter) =>
        {
            emitter.emitIns_C_I(INS_add, EA_8BYTE, FLD_GLOBAL_DS, 16, immediate);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No static immediate was recorded.");
            var constant = new Emitter.CnsVal { cnsVal = immediate };
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputCV(buffer, id, (ulong)insCodeMI(INS_add) | 0x0500, &constant);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
        });
    }

    [Test]
    public static void StaticDataLoadsRetainRipRelativeRelocationTargetAndWidth()
    {
        SysVX64EmitterCallTests.WithEmitter((_, emitter) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            emitter.emitCmpHandle = &context.JitInfo;

            var chunk = stackalloc AllocMemChunk[1];
            var offsets = stackalloc int[1];
            var data = stackalloc byte[64];
            chunk[0] = new AllocMemChunk { size = 64, block = data };
            offsets[0] = 0;
            emitter.emitConsDsc.dsdOffs = 64;
            emitter.emitDataChunks = chunk;
            emitter.emitDataChunkOffsets = offsets;
            emitter.emitNumDataChunks = 1;
            emitter.emitIns_R_C(INS_mov, EA_8BYTE, REG_RAX, Compiler.eeFindJitDataOffs(32), 8);

            var id = LastInstruction(emitter) ?? throw new AssertionException("No static data load was recorded.");
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputCV(buffer, id, (ulong)insCodeRM(INS_mov) | 0x0500, null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString("488B0500000000")));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
            Assert.That(context.Calls, Is.EqualTo(1));
            Assert.That(context.Kind, Is.EqualTo(CorInfoReloc.RELATIVE32));
            Assert.That(context.Delta, Is.Zero);
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(buffer + 3)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)(data + 40)));
        });
    }

    private struct RelocationContext
    {
        public ICorJitInfo JitInfo;
        public void* Location;
        public void* Target;
        public CorInfoReloc Kind;
        public int Delta;
        public int Calls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* info, void* location, void* locationRW,
        void* target, CorInfoReloc kind, int delta)
    {
        var context = (RelocationContext*)info;
        context->Location = location;
        context->Target = target;
        context->Kind = kind;
        context->Delta = delta;
        context->Calls++;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);
}
#endif
