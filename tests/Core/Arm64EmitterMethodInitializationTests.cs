// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64EmitterMethodInitializationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void StartupCreatesPrologAndBodyGroups(bool hasFramePointer)
    {
        var emitter = CreateEmitter();
        Begin(emitter, hasFramePointer);

        var prolog = FirstGroup(emitter) ?? throw new AssertionException("No prolog group was created.");
        var body = emitter.emitCurIG ?? throw new AssertionException("No body group was created.");
        Assert.That(prolog.GetDisplayId(), Is.EqualTo(1u));
        Assert.That(body.GetDisplayId(), Is.EqualTo(2u));
        Assert.That(prolog.igFlags, Is.EqualTo(InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead));
        Assert.That(body.igFlags, Is.EqualTo(InsGroupFlags.None));
        Assert.That(prolog.igNext, Is.SameAs(body));
        Assert.That(LastGroup(emitter), Is.SameAs(body));
        Assert.That(emitter.emitHasFramePtr, Is.EqualTo(hasFramePointer));
        Assert.That(LastBarrier(emitter), Is.Null);
        Assert.That(Capacity(emitter), Is.EqualTo((nuint)(200 * (16 + DebugPrefix(emitter)))));
    }

    [Test]
    public static void StartupClearsPriorBarrierAndCreatesFreshGroups()
    {
        var emitter = CreateEmitter();
        Begin(emitter, false);
        var oldProlog = FirstGroup(emitter);
        LastBarrier(emitter) = TestEmitter.Barrier();

        emitter.Init();
        Begin(emitter, true);

        Assert.That(LastBarrier(emitter), Is.Null);
        Assert.That(FirstGroup(emitter), Is.Not.SameAs(oldProlog));
        var newProlog = FirstGroup(emitter) ?? throw new AssertionException("No replacement prolog group was created.");
        Assert.That(newProlog.GetDisplayId(), Is.EqualTo(1u));
        Assert.That(emitter.emitHasFramePtr, Is.True);
    }

    private static TestEmitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new TestEmitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        return emitter;
    }

    private sealed class TestEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Barrier()
        {
            return new instrDescBasic();
        }
    }

    private static void Begin(Emitter emitter, bool hasFramePointer)
    {
        emitter.emitBegFN(hasFramePointer
#if DEBUG
            , true
#endif
            );
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlast")]
    private static extern ref insGroup? LastGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastMemBarrier")]
    private static extern ref Emitter.instrDesc? LastBarrier(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int DebugPrefix(Emitter emitter);
}
#endif
