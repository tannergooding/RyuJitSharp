// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.instruction;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64EmitterLabelTests
{
    [Test]
    public static void EmptyLabelsOwnTheirGcSetsAndRetainTheCurrentGroup()
    {
        var (compiler, emitter) = CreateEmitter();
        var group = emitter.emitCurIG;
        var vars = VarSetOps.MakeSingleton(compiler, 0);

        var label = emitter.emitAddLabel(vars, new((regMask)1), new((regMask)2));

        Assert.That(label, Is.SameAs(group));
        Assert.That(ThisVars(emitter), Is.EqualTo(vars).And.Not.SameAs(vars));
        Assert.That(InitVars(emitter), Is.EqualTo(vars).And.Not.SameAs(vars).And.Not.SameAs(ThisVars(emitter)));
        Assert.That(ThisRefs(emitter), Is.EqualTo((regMask)1));
        Assert.That(ThisByrefs(emitter), Is.EqualTo((regMask)2));
        Assert.That(InitRefs(emitter), Is.EqualTo((regMask)1));
        Assert.That(InitByrefs(emitter), Is.EqualTo((regMask)2));
        VarSetOps.RemoveElemD(compiler, vars, 0);
        Assert.That(VarSetOps.IsMember(compiler, ThisVars(emitter), 0), Is.True);
        Assert.That(VarSetOps.IsMember(compiler, InitVars(emitter), 0), Is.True);
    }

    [Test]
    public static void EmptyInlineLabelsPreserveExistingGcState()
    {
        var (_, emitter) = CreateEmitter();
        var group = emitter.emitCurIG;
        var vars = ThisVars(emitter);
        var initialVars = InitVars(emitter);
        ThisRefs(emitter) = (regMask)1;
        ThisByrefs(emitter) = (regMask)2;

        Assert.That(emitter.emitAddInlineLabel(), Is.SameAs(group));
        Assert.That(ThisVars(emitter), Is.SameAs(vars));
        Assert.That(InitVars(emitter), Is.SameAs(initialVars));
        Assert.That(ThisRefs(emitter), Is.EqualTo((regMask)1));
        Assert.That(ThisByrefs(emitter), Is.EqualTo((regMask)2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LastCallGcClassificationTracksDescriptorContext(bool noGc)
    {
        var (_, emitter) = CreateEmitter();
        Assert.That(LastCallCanGc(emitter), Is.False);
        var descriptor = LabelEmitter.Basic(INS_bl);
        LastInstruction(emitter) = descriptor;
        Assert.That(LastCallCanGc(emitter), Is.False);
        descriptor.idSetIsCall();
        SetNoGc(descriptor, noGc);

        Assert.That(GetNoGc(descriptor), Is.EqualTo(noGc));
        Assert.That(LastCallCanGc(emitter), Is.EqualTo(!noGc));
        SetNoGc(descriptor, !noGc);
        Assert.That(GetNoGc(descriptor), Is.EqualTo(!noGc));
        Assert.That(LastCallCanGc(emitter), Is.EqualTo(noGc));
    }

    [TestCase(BBJ_ALWAYS, INS_nop)]
    [TestCase(BBJ_THROW, INS_brk)]
    public static void ChangedCallLivenessReachesTheRequiredPaddingDependency(BBKinds kind, instruction expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var (compiler, emitter) = CreateEmitter();
#if DEBUG
        JitTls.Compiler = compiler;
#endif
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.compCurBB.SetFlags(BBF_HAS_LABEL);
        var previous = new BasicBlock(null, null);
        if (kind is BBJ_ALWAYS)
        {
            previous.SetKindAndTargetEdge(kind, new FlowEdge(previous, compiler.compCurBB, null));
        }
        else
        {
            previous.Kind = kind;
        }

        var descriptor = LabelEmitter.Basic(INS_bl);
        descriptor.idSetIsCall();
        LastInstruction(emitter) = descriptor;
        LastInstructionGroup(emitter) = emitter.emitCurIG;

        var exception = Assert.Throws<FatalJitException>(() =>
            emitter.emitAddLabel(VarSetOps.MakeEmpty(compiler), new((regMask)1), default, previous));

        Assert.That(exception, Has.Message.EqualTo(
            $"Zero-operand instruction recording outside xarch is not ported ({expected})."));
    }

    private static (Compiler Compiler, LabelEmitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new LabelEmitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        var group = new insGroup { igFlags = InsGroupFlags.Prolog };
        Prepare(emitter, group);
        return (compiler, emitter);
    }

    private sealed class LabelEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Basic(instruction ins)
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(ins);
            return descriptor;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGenIG")]
    private static extern void Prepare(Emitter emitter, insGroup group);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitLastInsIsCallWithGC")]
    private static extern bool LastCallCanGc(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idIsNoGC")]
    private static extern bool GetNoGc(Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idSetIsNoGC")]
    private static extern void SetNoGc(Emitter.instrDesc descriptor, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsIG")]
    private static extern ref insGroup? LastInstructionGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVars")]
    private static extern ref nint[] ThisVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefVars")]
    private static extern ref nint[] InitVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefRegs")]
    private static extern ref regMask InitRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitByrefRegs")]
    private static extern ref regMask InitByrefs(Emitter emitter);
}
#endif
