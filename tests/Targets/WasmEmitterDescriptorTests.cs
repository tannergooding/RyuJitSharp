// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root.

#if TARGET_WASM
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp.UnitTests;

internal static class WasmEmitterDescriptorTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDebugInfoLayout
    {
        public uint idNum;
        public nuint idSize;
        public uint idVarRefOffs;
        public uint idVarRefOffs2;
        public nuint idMemCookie;
        public GenTreeFlags idFlags;
        public byte idFinallyCall;
        public byte idCatchRet;
        public nint idCallSig;
        public nint idTargetBlock;
        public int lclBaseIndex;
        public uint idLclNum;
        public uint idLclOffset;
    }

    [Test]
    public static void DebugInfoSizeIncludesWasmLocalMetadata()
    {
        var expectedSize = IntPtr.Size == 8 ? 72 : 48;
        var firstLocalOffset = IntPtr.Size == 8 ? 56 : 36;
        Assert.That(Unsafe.SizeOf<NativeDebugInfoLayout>(), Is.EqualTo(expectedSize));
        Assert.That(Marshal.OffsetOf<NativeDebugInfoLayout>(nameof(NativeDebugInfoLayout.lclBaseIndex)),
            Is.EqualTo((nint)firstLocalOffset));
        Assert.That(Marshal.OffsetOf<NativeDebugInfoLayout>(nameof(NativeDebugInfoLayout.idLclNum)),
            Is.EqualTo((nint)(firstLocalOffset + 4)));
        Assert.That(Marshal.OffsetOf<NativeDebugInfoLayout>(nameof(NativeDebugInfoLayout.idLclOffset)),
            Is.EqualTo((nint)(firstLocalOffset + 8)));

        var sizes = typeof(Emitter).GetNestedType("DescriptorSizes", BindingFlags.NonPublic)!;
        var debugInfoSize = sizes.GetField("DebugInfo", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.That(debugInfoSize.GetRawConstantValue(), Is.EqualTo(expectedSize));
    }

    [Test]
    public static void SpecialDescriptorKindsPreserveTheirPayloads()
    {
        var localDeclaration = TestEmitter.CreateLocalDeclaration(3, WasmValueType.I64);
        Assert.That(localDeclaration.IsLocalDeclaration, Is.True);
        Assert.That(localDeclaration.Count, Is.EqualTo(3u));
        Assert.That(localDeclaration.Type, Is.EqualTo(WasmValueType.I64));

        var valueTypeImmediate = TestEmitter.CreateValueTypeImmediate(WasmValueType.ExnRef, 127);
        Assert.That(valueTypeImmediate.IsValueTypeImmediate, Is.True);
        Assert.That(valueTypeImmediate.Type, Is.EqualTo(WasmValueType.ExnRef));
        Assert.That(valueTypeImmediate.Immediate, Is.EqualTo(127u));

        var vectorBytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        (var isVectorImmediate, var actualVectorBytes) = TestEmitter.CreateVectorImmediate(vectorBytes);
        Assert.That(isVectorImmediate, Is.True);
        Assert.That(actualVectorBytes, Is.EqualTo(vectorBytes));

        Assert.That(TestEmitter.CreateMemargLaneImmediate(15), Is.EqualTo((byte)15));
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Exposes protected descriptor types for tests.")]
    private sealed class TestEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static (bool IsLocalDeclaration, uint Count, WasmValueType Type) CreateLocalDeclaration(
            uint count, WasmValueType type)
        {
            var descriptor = new instrDescLclVarDecl
            {
                lclCnt = count,
                lclType = type,
            };
            descriptor.idInsFmt(IF_LOCAL_DECL);
            return (descriptor.idIsLclVarDecl(), emitGetLclVarDeclCount(descriptor),
                emitGetLclVarDeclType(descriptor));
        }

        internal static (bool IsValueTypeImmediate, WasmValueType Type, uint Immediate) CreateValueTypeImmediate(
            WasmValueType type, uint immediate)
        {
            var descriptor = new instrDescValTypeImm
            {
                valType = type,
                imm = immediate,
            };
            descriptor.idInsFmt(IF_TRY_TABLE);
            return (descriptor.idIsValTypeImm(), emitGetValTypeImmType(descriptor),
                emitGetValTypeImmImm(descriptor));
        }

        internal static (bool IsVectorImmediate, byte[] Bytes) CreateVectorImmediate(ReadOnlySpan<byte> bytes)
        {
            var descriptor = new instrDescV128Imm();
            descriptor.idInsFmt(IF_V128);
            descriptor.idV128Const(bytes);
            return (descriptor.idIsV128Imm(), emitGetV128ImmValue(descriptor).ToArray());
        }

        internal static byte CreateMemargLaneImmediate(byte lane)
        {
            var descriptor = new instrDescMemargLane();
            descriptor.idInsFmt(IF_MEMARG_LANE);
            descriptor.idLaneIdx(lane);
            return emitGetLaneImmValue(descriptor);
        }
    }
}
#endif
