// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterDescriptorSchemaTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeJumpLayout
    {
        public ulong packedDescriptor;
        public nint addressUnion;
        public nint next;
        public nint group;
        public nint patchAddress;
        public uint offsetAndFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeAlignLayout
    {
        public ulong packedDescriptor;
        public nint addressUnion;
        public nint next;
        public nint group;
        public nint loopHeadPredecessor;
#if DEBUG
        public bool placedAfterJump;
#endif
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDebugInfoLayout
    {
        public uint number;
        public nuint size;
        public uint firstVariableOffset;
        public uint secondVariableOffset;
        public nuint cookie;
        public GenTreeFlags flags;
        public byte finallyCall;
        public byte catchReturn;
        public nint callSignature;
        public nint targetBlock;
    }

    [Test]
    public static void NativeLogicalLayoutMatchesPinnedAmd64Fields()
    {
        Assert.That(IntPtr.Size, Is.EqualTo(8));
        Assert.That(Unsafe.SizeOf<NativeJumpLayout>(), Is.EqualTo(48));
        Assert.That(Marshal.OffsetOf<NativeJumpLayout>(nameof(NativeJumpLayout.next)), Is.EqualTo((nint)16));
        Assert.That(Marshal.OffsetOf<NativeJumpLayout>(nameof(NativeJumpLayout.group)), Is.EqualTo((nint)24));
        Assert.That(Marshal.OffsetOf<NativeJumpLayout>(nameof(NativeJumpLayout.patchAddress)), Is.EqualTo((nint)32));
        Assert.That(Marshal.OffsetOf<NativeJumpLayout>(nameof(NativeJumpLayout.offsetAndFlags)), Is.EqualTo((nint)40));
#if DEBUG
        Assert.That(Unsafe.SizeOf<NativeAlignLayout>(), Is.EqualTo(48));
#else
        Assert.That(Unsafe.SizeOf<NativeAlignLayout>(), Is.EqualTo(40));
#endif
        Assert.That(Marshal.OffsetOf<NativeAlignLayout>(nameof(NativeAlignLayout.next)), Is.EqualTo((nint)16));
        Assert.That(Marshal.OffsetOf<NativeAlignLayout>(nameof(NativeAlignLayout.group)), Is.EqualTo((nint)24));
        Assert.That(Marshal.OffsetOf<NativeAlignLayout>(nameof(NativeAlignLayout.loopHeadPredecessor)), Is.EqualTo((nint)32));
        Assert.That(Unsafe.SizeOf<NativeDebugInfoLayout>(), Is.EqualTo(56));
        Assert.That(Marshal.OffsetOf<NativeDebugInfoLayout>(nameof(NativeDebugInfoLayout.size)), Is.EqualTo((nint)8));
        Assert.That(Marshal.OffsetOf<NativeDebugInfoLayout>(nameof(NativeDebugInfoLayout.callSignature)), Is.EqualTo((nint)40));
        Assert.That(Marshal.OffsetOf<NativeDebugInfoLayout>(nameof(NativeDebugInfoLayout.targetBlock)), Is.EqualTo((nint)48));

        var sizes = typeof(Emitter).GetNestedType("DescriptorSizes", BindingFlags.NonPublic)!;
        Assert.That(sizes.GetField("DebugInfo", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetRawConstantValue(), Is.EqualTo(Unsafe.SizeOf<NativeDebugInfoLayout>()));
        Assert.That(sizes.GetField("DebugPrefix", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetRawConstantValue(), Is.EqualTo(IntPtr.Size));
    }

    [TestCase("instrDescBasic", 16)]
    [TestCase("instrDescJmp", 48)]
#if DEBUG
    [TestCase("instrDescAlign", 48)]
#else
    [TestCase("instrDescAlign", 40)]
#endif
    public static void DescriptorLogicalSizeMatchesNativeLayout(string typeName, int size)
    {
        var descriptor = Create(typeName);
        descriptor.idIns(typeName switch
        {
            "instrDescJmp" => INS_jmp,
            "instrDescAlign" => INS_align,
            _ => INS_nop,
        });
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(size));

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        Assert.That(DescriptorSize(emitter, descriptor), Is.EqualTo(size));
    }

    [Test]
    public static void SmallDescriptorAndDebugPrefixKeepTheirNativeSizes()
    {
        var descriptor = Create("instrDescBasic");
        descriptor.idIns(INS_nop);
        descriptor.idSetIsSmallDsc();
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(8));

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
#if !DEBUG
        Assert.That(DebugPrefixSize(emitter), Is.Zero);
        compiler.opts.disAsm = true;
        emitter.emitBegCG(compiler, default);
#endif
        Assert.That(DebugPrefixSize(emitter), Is.EqualTo(IntPtr.Size));

        var info = new Emitter.instrDescDebugInfo { idNum = 9, idSize = 8 };
        descriptor.idDebugOnlyInfo(info);
        Assert.That(descriptor.idDebugOnlyInfo(), Is.SameAs(info));
        Assert.That(info.idSize, Is.EqualTo((nuint)8));

        var signature = new StrongBox<CORINFO_SIG_INFO>(default);
        info.idCallSig = signature;
        Assert.That(info.idCallSig, Is.SameAs(signature));
    }

    [Test]
    public static void UnsupportedExtendedDescriptorCannotUseBaseSize()
    {
        var descriptor = Create("instrDescBasic");
        descriptor.idIns(INS_nop);
        descriptor.idSetIsLargeCns();

        Assert.That(() => descriptor.NativeLogicalSize, Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public static void UninitializedOrSpecialInstructionCannotUseBaseDescriptorSize()
    {
        var descriptor = Create("instrDescBasic");
        Assert.That(() => descriptor.NativeLogicalSize, Throws.TypeOf<InvalidOperationException>());

        descriptor.idIns(INS_jmp);
        Assert.That(() => descriptor.NativeLogicalSize, Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public static void JumpIdentityCodeSizeAndPreviousDescriptorSizeRetainNativeWidths()
    {
        var descriptor = Create("instrDescJmp");
        descriptor.idIns(INS_jmp);
        descriptor.idCodeSize(15);
        descriptor.idSetPrevSize(56);

        Assert.That(descriptor.idInsIs(INS_jmp), Is.True);
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(15u));
        Assert.That(descriptor.idPrevSize(), Is.EqualTo(56u));

        var offset = typeof(Emitter).GetNestedType("instrDescJmp", BindingFlags.NonPublic)!
            .GetProperty("idjOffs")!;
        offset.SetValue(descriptor, uint.MaxValue);
        Assert.That(offset.GetValue(descriptor), Is.EqualTo(0x0FFF_FFFFu));

        var type = typeof(Emitter).GetNestedType("instrDescJmp", BindingFlags.NonPublic)!;
        var group = new insGroup();
        type.GetField("idjIG")!.SetValue(descriptor, group);
        var address = (byte*)0x1_0000_0051;
        type.GetField("idjAddr")!.SetValue(descriptor, System.Reflection.Pointer.Box(address, typeof(byte*)));
        Assert.That(type.GetField("idjIG")!.GetValue(descriptor), Is.SameAs(group));
        Assert.That((nuint)System.Reflection.Pointer.Unbox(type.GetField("idjAddr")!.GetValue(descriptor)!),
            Is.EqualTo((nuint)address));
    }

    [TestCase(0u)]
    [TestCase(56u)]
    [TestCase(124u)]
    public static void PreviousDescriptorSizeUsesFiveScaledBits(uint previousSize)
    {
        var descriptor = Create("instrDescBasic");
        descriptor.idSetPrevSize(previousSize);

        Assert.That(descriptor.idPrevSize(), Is.EqualTo(previousSize));
    }

    [Test]
    public static void AlignDescriptorTracksOwnerPredecessorAndFlags()
    {
        var descriptor = Create("instrDescAlign");
        descriptor.idIns(INS_align);
        var owner = new insGroup { igFlags = InsGroupFlags.HasAlign };
        var predecessor = new insGroup();
        var head = new insGroup();
        predecessor.igNext = head;
        var next = Create("instrDescAlign");

        var type = typeof(Emitter).GetNestedType("instrDescAlign", BindingFlags.NonPublic)!;
        type.GetField("idaNext")!.SetValue(descriptor, next);
        type.GetField("idaIG")!.SetValue(descriptor, owner);
        type.GetField("idaLoopHeadPredIG")!.SetValue(descriptor, predecessor);
        Assert.That(type.GetField("idaNext")!.GetValue(descriptor), Is.SameAs(next));
        Assert.That(type.GetMethod("loopHeadIG")!.Invoke(descriptor, null), Is.SameAs(head));

        Assert.That(() => type.GetMethod("removeAlignFlags")!.Invoke(descriptor, null), Throws.Nothing);
        Assert.That(owner.igFlags, Is.EqualTo(InsGroupFlags.RemovedAlign));
    }

    private static Emitter.instrDesc Create(string name)
    {
        var type = typeof(Emitter).GetNestedType(name, BindingFlags.NonPublic)!;
        return (Emitter.instrDesc)Activator.CreateInstance(type, nonPublic: true)!;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int DebugPrefixSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSizeOfInsDsc")]
    private static extern int DescriptorSize(Emitter emitter, Emitter.instrDesc descriptor);
}
