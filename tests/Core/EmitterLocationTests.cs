// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterLocationTests
{
    [TestCase(0u, 0u)]
    [TestCase(1u, 2u)]
    [TestCase(255u, 65535u)]
    [TestCase(65535u, 65535u)]
    public static void CookiePreservesBothNativeSixteenBitFields(uint instructions, uint offset)
    {
        var cookie = Emitter.emitSpecifiedOffset(instructions, offset);
        var location = new emitLocation(new insGroup(), cookie);

        Assert.That(emitGetInsNumFromCodePos(cookie), Is.EqualTo(instructions));
        Assert.That(emitGetInsOfsFromCodePos(cookie), Is.EqualTo(offset));
        Assert.That(location.GetInsNum(), Is.EqualTo((int)instructions));
        Assert.That(location.GetInsOffset(), Is.EqualTo((int)offset));
    }

    [Test]
    public static void CapturingAndSettingLocationsPreservesGroupAndPosition()
    {
        var emitter = CreateEmitter();
        var start = new emitLocation(emitter);
        Assert.That(start.IsCurrentLocation(emitter), Is.True);
        Assert.That(start.IsPreviousInsNum(emitter), Is.False);
        _ = Append(emitter, 2);
        var first = new emitLocation(emitter);

        Assert.That(first.GetIG(), Is.SameAs(emitter.emitCurIG));
        Assert.That(first.GetInsNum(), Is.EqualTo(1));
        Assert.That(first.GetInsOffset(), Is.EqualTo(2));
        Assert.That(start.IsPreviousInsNum(emitter), Is.True);

        emitLocation copy = default;
        copy.SetLocation(first);
        Assert.That(copy.IsCurrentLocation(emitter), Is.True);
        _ = Append(emitter, 3);
        Assert.That(first.IsCurrentLocation(emitter), Is.False);
        Assert.That(first.IsPreviousInsNum(emitter), Is.True);

        copy.CaptureLocation(emitter);
        Assert.That(copy.GetInsNum(), Is.EqualTo(2));
        Assert.That(copy.GetInsOffset(), Is.EqualTo(5));
        Assert.That(copy.IsCurrentLocation(emitter), Is.True);
    }

    [Test]
    public static void FinalOffsetsUseActualDescriptorSizesWhenPredictionsChange()
    {
        var emitter = CreateEmitter();
        var start = new emitLocation(emitter);
        var first = Append(emitter, 2);
        var afterFirst = new emitLocation(emitter);
        var second = Append(emitter, 5);
        var afterSecond = new emitLocation(emitter);
        _ = Append(emitter, 3);
        var end = new emitLocation(emitter);
        var group = Save(emitter, false);
        group.igOffs = 100;

        Assert.That(start.CodeOffset(emitter), Is.EqualTo(100u));
        Assert.That(afterFirst.CodeOffset(emitter), Is.EqualTo(102u));
        Assert.That(afterSecond.CodeOffset(emitter), Is.EqualTo(107u));
        Assert.That(end.CodeOffset(emitter), Is.EqualTo(110u));

        first.idCodeSize(1);
        second.idCodeSize(2);
        group.igSize = 6;
        group.igFlags |= InsGroupFlags.UpdatedInstructionSize;

        Assert.That(start.CodeOffset(emitter), Is.EqualTo(100u));
        Assert.That(afterFirst.CodeOffset(emitter), Is.EqualTo(101u));
        Assert.That(afterSecond.CodeOffset(emitter), Is.EqualTo(103u));
        Assert.That(end.CodeOffset(emitter), Is.EqualTo(106u));
        Assert.That(afterFirst.GetInsOffset(), Is.EqualTo(2));
        Assert.That(afterSecond.GetInsOffset(), Is.EqualTo(7));
    }

    [Test]
    public static void BoundaryOffsetsUseGroupSizesAndUnsignedOffsetArithmetic()
    {
        var emitter = CreateEmitter();
        _ = Append(emitter, 5);
        var group = Save(emitter, false);
        group.igOffs = uint.MaxValue - 2;
        var beginning = new emitLocation(group, Emitter.emitSpecifiedOffset(0, 123));
        var end = new emitLocation(group, Emitter.emitSpecifiedOffset(1, 99));

        Assert.That(beginning.CodeOffset(emitter), Is.EqualTo(uint.MaxValue - 2));
        Assert.That(end.CodeOffset(emitter), Is.EqualTo(2u));
    }

    [Test]
    public static void PreviousInstructionChecksOnlyTheAdjacentGroup()
    {
        var emitter = CreateEmitter();
        var firstStart = new emitLocation(emitter);
        _ = Append(emitter, 2);
        var firstEnd = new emitLocation(emitter);
        NextGroup(emitter, true);
        Assert.That(firstEnd.IsPreviousInsNum(emitter), Is.False);

        _ = Append(emitter, 3);
        Assert.That(firstEnd.IsPreviousInsNum(emitter), Is.True);
        Assert.That(firstStart.IsPreviousInsNum(emitter), Is.False);
        _ = Append(emitter, 1);
        Assert.That(firstEnd.IsPreviousInsNum(emitter), Is.False);
        NextGroup(emitter, true);
        _ = Append(emitter, 1);
        Assert.That(firstEnd.IsPreviousInsNum(emitter), Is.False);
    }

    [Test]
    public static void EqualityUsesGroupIdentityAndTheEntireCookie()
    {
        var firstGroup = new insGroup();
        var secondGroup = new insGroup();
        var first = new emitLocation(firstGroup, Emitter.emitSpecifiedOffset(2, 5));
        var same = new emitLocation(firstGroup, Emitter.emitSpecifiedOffset(2, 5));
        var differentOffset = new emitLocation(firstGroup, Emitter.emitSpecifiedOffset(2, 6));
        var differentGroup = new emitLocation(secondGroup, Emitter.emitSpecifiedOffset(2, 5));

        Assert.That(first == same, Is.True);
        Assert.That(first.Equals((object)same), Is.True);
        Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        Assert.That(first != differentOffset, Is.True);
        Assert.That(first != differentGroup, Is.True);
    }

#if DEBUG
    [Test]
    public static void LocationPrintingPreservesTheUnsignedMethodNumber()
    {
        var emitter = CreateEmitter();
        var location = new emitLocation(emitter.emitCurIG, Emitter.emitSpecifiedOffset(2, 5));
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;

        try
        {
            s_jitstdout = writer;
            location.Print(-1);
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
            Is.EqualTo("(G_M4294967295_IG02,ins#2,ofs#5)"));
    }
#endif

    private static Emitter CreateEmitter()
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

        return emitter;
    }

    private static Emitter.instrDesc Append(Emitter emitter, uint size)
    {
        var allocator = typeof(Emitter).GetMethod("emitNewInstr", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var descriptor = (Emitter.instrDesc)allocator.Invoke(emitter, [EA_1BYTE])!;
        descriptor.idIns(INS_nop);
        descriptor.idCodeSize(size);
        CodeSize(emitter) += (int)size;

        return descriptor;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CodeSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup Save(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNxtIG")]
    private static extern void NextGroup(Emitter emitter, bool extend);
}
