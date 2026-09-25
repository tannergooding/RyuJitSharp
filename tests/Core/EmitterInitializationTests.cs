// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInitializationTests
{
    [Test]
    public static void InitRequiresTheMethodCompiler()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;

        Assert.That(emitter.Init, Throws.TypeOf<FatalJitException>());
    }

    [Test]
    public static void InitAllocatesSeparateEmptyTrackedSetsAndClearsTheCurrentGroup()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();

        Assert.That(PrevVars(emitter), Has.Length.EqualTo(1));
        Assert.That(InitVars(emitter), Has.Length.EqualTo(1));
        Assert.That(ThisVars(emitter), Has.Length.EqualTo(1));
        Assert.That(ReferenceEquals(PrevVars(emitter), InitVars(emitter)), Is.False);
        Assert.That(ReferenceEquals(PrevVars(emitter), ThisVars(emitter)), Is.False);
        Assert.That(ReferenceEquals(InitVars(emitter), ThisVars(emitter)), Is.False);

        PrevVars(emitter)[0] = 1;
        InitVars(emitter)[0] = 2;
        ThisVars(emitter)[0] = 4;
        emitter.emitCurIG = new insGroup();
#if DEBUG
        DebugPrevVars(emitter)[0] = 8;
        DebugThisVars(emitter)[0] = 16;
        DebugPrevRegPtr(emitter) = new GCInfo.regPtrDsc();
        DebugPrevGCrefRegs(emitter) = new regMaskTP(SRBM_RAX);
        DebugPrevByrefRegs(emitter) = new regMaskTP(SRBM_RBX);
#endif

        emitter.Init();

        Assert.That(PrevVars(emitter), Is.EqualTo(new nint[] { 0 }));
        Assert.That(InitVars(emitter), Is.EqualTo(new nint[] { 0 }));
        Assert.That(ThisVars(emitter), Is.EqualTo(new nint[] { 0 }));
        Assert.That(emitter.emitCurIG, Is.Null);
#if DEBUG
        Assert.That(DebugPrevVars(emitter), Is.EqualTo(new nint[] { 0 }));
        Assert.That(DebugThisVars(emitter), Is.EqualTo(new nint[] { 0 }));
        Assert.That(DebugPrevRegPtr(emitter), Is.Null);
        Assert.That(DebugPrevGCrefRegs(emitter).IsEmpty, Is.True);
        Assert.That(DebugPrevByrefRegs(emitter).IsEmpty, Is.True);
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrevGCrefVars")]
    private static extern ref nint[] PrevVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefVars")]
    private static extern ref nint[] InitVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVars")]
    private static extern ref nint[] ThisVars(Emitter emitter);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "debugPrevGCrefVars")]
    private static extern ref nint[] DebugPrevVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "debugThisGCrefVars")]
    private static extern ref nint[] DebugThisVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "debugPrevRegPtrDsc")]
    private static extern ref GCInfo.regPtrDsc? DebugPrevRegPtr(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "debugPrevGCrefRegs")]
    private static extern ref regMaskTP DebugPrevGCrefRegs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "debugPrevByrefRegs")]
    private static extern ref regMaskTP DebugPrevByrefRegs(Emitter emitter);
#endif
}
