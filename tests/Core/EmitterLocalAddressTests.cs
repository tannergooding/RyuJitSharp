// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterLocalAddressTests
{
    [TestCase(0, 0u, 0x00000000u)]
    [TestCase(32767, 32767u, 0x3FFFFFFFu)]
    [TestCase(0, 32768u, 0x40000000u)]
    [TestCase(32767, 65535u, 0x7FFFFFFFu)]
    [TestCase(-1, 0u, 0x80000001u)]
    [TestCase(-32767, 32767u, 0xBFFFFFFFu)]
    [TestCase(32768, 0u, 0xC0008000u)]
    [TestCase(32768, 255u, 0xFFC08000u)]
    [TestCase(4194303, 255u, 0xFFFFFFFFu)]
    public static void AddressTagsPreserveNativePackedBits(int variable, uint offset, uint expectedBits)
    {
        emitLclVarAddr address = default;
        address.initLclVarAddr(variable, offset);

        Assert.That(Unsafe.SizeOf<emitLclVarAddr>(), Is.EqualTo(4));
        Assert.That(Unsafe.BitCast<emitLclVarAddr, uint>(address), Is.EqualTo(expectedBits));
        Assert.That(address.lvaVarNum(), Is.EqualTo(variable));
        Assert.That(address.lvaOffset(), Is.EqualTo(offset));

        address.initLclVarAddr(3, 7);
        Assert.That(address.lvaVarNum(), Is.EqualTo(3));
        Assert.That(address.lvaOffset(), Is.EqualTo(7));
    }

    [TestCase(0, 65536u)]
    [TestCase(32767, uint.MaxValue)]
    [TestCase(-32768, 0u)]
    [TestCase(int.MinValue, 0u)]
    [TestCase(-1, 32768u)]
    [TestCase(32768, 256u)]
    [TestCase(4194304, 0u)]
    public static void UnencodableAddressesRetainTheNativeImplementationLimit(int variable, uint offset)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
#if DEBUG
        JitTls.LogEnv.Compiler = compiler;
#endif

        try
        {
            emitLclVarAddr address = default;
            address.initLclVarAddr(2, 3);
            var exception = Assert.Throws<FatalJitException>(() => address.initLclVarAddr(variable, offset));

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));
            Assert.That(address.lvaVarNum(), Is.EqualTo(2));
            Assert.That(address.lvaOffset(), Is.EqualTo(3u));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void DescriptorAddressReferenceSurvivesSavingItsGroup()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compCurBB = new BasicBlock(null, null);
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , false
#endif
            );
        var allocator = typeof(Emitter).GetMethod("emitNewInstr", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var descriptor = (Emitter.instrDesc)allocator.Invoke(emitter, [EA_8BYTE])!;
        descriptor.idIns(INS_mov);
        descriptor.idCodeSize(3);
        CurrentSize(emitter) = 3;
        ref var address = ref descriptor.idAddr().iiaLclVar;
        address.initLclVarAddr(32768, 255);

        var group = Save(emitter, false);
        var saved = group.igData ?? throw new AssertionException("The saved group has no instruction descriptors.");
        Assert.That(saved, Has.Length.EqualTo(1));
        Assert.That(saved[0], Is.SameAs(descriptor));
        Assert.That(saved[0].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(32768));
        Assert.That(saved[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(255u));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(16));

        address.initLclVarAddr(-1, 7);
        Assert.That(saved[0].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(-1));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup Save(Emitter emitter, bool extend);
}
