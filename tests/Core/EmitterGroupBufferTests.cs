// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterGroupBufferTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void PreparationRetainsTheBufferAndResetsGroupCounters(bool noGc)
    {
        var emitter = CreateEmitter();
        var group = new insGroup();
        NoGc(emitter) = noGc;
        emitter.emitCurStackLvl = 32;
        Prepare(emitter, group);
        var buffer = Buffer(emitter);
        var capacity = Capacity(emitter);
        Count(emitter) = 12;
        CodeSize(emitter) = 31;
        Used(emitter) = 1;
        LastInstructionSize(emitter) = 20;

        Prepare(emitter, group);

        Assert.That(emitter.emitCurIG, Is.SameAs(group));
        Assert.That(Buffer(emitter), Is.SameAs(buffer));
        Assert.That(Capacity(emitter), Is.EqualTo(capacity));
        Assert.That(Count(emitter), Is.Zero);
        Assert.That(CodeSize(emitter), Is.Zero);
        Assert.That(Used(emitter), Is.EqualTo((nuint)0));
        Assert.That(LastInstructionSize(emitter), Is.Zero);
        Assert.That((group.igFlags & InsGroupFlags.NoGCInterrupt) != 0, Is.EqualTo(noGc));
#if EMIT_TRACK_STACK_DEPTH
        Assert.That(group.igStkLvl, Is.EqualTo(32u));
#endif
    }

    [TestCase(1)]
    [TestCase(65)]
    public static void SavingCopiesGcSetsAndRetainsNativeHeaderWidths(int trackedCount)
    {
        var emitter = CreateEmitter(trackedCount);
        var group = new insGroup();
        Prepare(emitter, group);
        InitVars(emitter)[^1] = 1;
        InitRefs(emitter) = SRBM_RAX;
        InitByrefs(emitter) = SRBM_RCX;
        ThisVars(emitter)[^1] = 2;
        ThisRefs(emitter) = SRBM_RDX;
        ThisByrefs(emitter) = SRBM_RBX;

        Assert.That(Save(emitter, false), Is.SameAs(group));

        Assert.That(group.igFlags, Is.EqualTo(InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs));
        Assert.That(group.igGCvars()[^1], Is.EqualTo((nint)1));
        Assert.That(group.igByrefRegs(), Is.EqualTo((uint)SRBM_RCX));
        Assert.That(group.igGCregs, Is.EqualTo(SRBM_RAX));
        Assert.That(GroupDataOffset(group), Is.EqualTo((nuint)(nint.Size + sizeof(uint))));
        Assert.That(GroupStorageSize(group), Is.EqualTo(GroupDataOffset(group)));
        Assert.That(PrevVars(emitter)[^1], Is.EqualTo((nint)2));
        Assert.That(PrevRefs(emitter), Is.EqualTo(SRBM_RDX));
        Assert.That(PrevByrefs(emitter), Is.EqualTo(SRBM_RBX));

        InitVars(emitter)[^1] = 4;
        ThisVars(emitter)[^1] = 8;
        Assert.That(group.igGCvars()[^1], Is.EqualTo((nint)1));
        Assert.That(PrevVars(emitter)[^1], Is.EqualTo((nint)2));
        Assert.That(group.igData, Is.Empty);
        Assert.That(Used(emitter), Is.EqualTo((nuint)0));
    }

    [Test]
    public static void MatchingGcStateStillStoresByrefsButNotVariables()
    {
        var emitter = CreateEmitter();
        var group = new insGroup();
        Prepare(emitter, group);
        InitByrefs(emitter) = SRBM_R8;
        PrevByrefs(emitter) = SRBM_R8;

        _ = Save(emitter, false);

        Assert.That(group.igFlags, Is.EqualTo(InsGroupFlags.ByrefRegs));
        Assert.That(group.igByrefRegs(), Is.EqualTo((uint)SRBM_R8));
        Assert.That(GroupStorageSize(group), Is.EqualTo((nuint)sizeof(uint)));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ForcedGcStateClearsOnlyAfterALabelAndNotAtOverflow(bool overflow, bool addedLabel)
    {
        var emitter = CreateEmitter();
        var group = new insGroup();
        Prepare(emitter, group);
        ForceGcState(emitter) = true;
        AddedLabel(emitter) = addedLabel;
        ThisVars(emitter)[0] = 4;
        ThisRefs(emitter) = SRBM_RDX;
        ThisByrefs(emitter) = SRBM_RBX;

        _ = Save(emitter, overflow);

        Assert.That(group.igFlags & InsGroupFlags.GCVars, Is.EqualTo(InsGroupFlags.GCVars));
        Assert.That(ForceGcState(emitter), Is.EqualTo(overflow || !addedLabel));
        Assert.That(AddedLabel(emitter), Is.EqualTo(overflow && addedLabel));
        Assert.That(PrevVars(emitter)[0], Is.EqualTo(overflow ? (nint)0 : (nint)4));
        Assert.That(PrevRefs(emitter), Is.EqualTo(overflow ? SRBM_NONE : SRBM_RDX));
        Assert.That(PrevByrefs(emitter), Is.EqualTo(overflow ? SRBM_NONE : SRBM_RBX));
    }

    [Test]
    public static void ExtensionInheritsGcStateAndEmptyGroupRetainsPreviousLastInstruction()
    {
        var emitter = CreateEmitter();
        var previous = new insGroup();
        var instruction = DescriptorFactory.Basic();
        LastInstruction(emitter) = instruction;
        LastInstructionGroup(emitter) = previous;
        LastSavedNoGc(emitter) = true;
        var group = new insGroup { igFlags = InsGroupFlags.Extend };
        Prepare(emitter, group);
        InitVars(emitter)[0] = 1;
        ForceGcState(emitter) = true;
        InitRefs(emitter) = SRBM_RAX;
        InitByrefs(emitter) = SRBM_RCX;

        _ = Save(emitter, true);

        Assert.That(group.igFlags, Is.EqualTo(InsGroupFlags.Extend));
        Assert.That(group.igGCregs, Is.EqualTo(SRBM_NONE));
        Assert.That(GroupStorageSize(group), Is.EqualTo((nuint)0));
        Assert.That(group.igLastIns, Is.Null);
        Assert.That(LastInstruction(emitter), Is.SameAs(instruction));
        Assert.That(LastInstructionGroup(emitter), Is.SameAs(previous));
        Assert.That(LastSavedNoGc(emitter), Is.True);
    }

    [TestCase(256, 0)]
    [TestCase(-1, 0)]
    [TestCase(0, 65536)]
    [TestCase(0, -1)]
    public static void SavingRejectsCountsThatDoNotFitNativeFields(int instructionCount, int codeSize)
    {
        var emitter = CreateEmitter();
        Prepare(emitter, new insGroup());
        Count(emitter) = instructionCount;
        CodeSize(emitter) = codeSize;

        _ = Assert.Throws<FatalJitException>(() => Save(emitter, false));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExactNativeCapacityIncludesTheRuntimeDebugPrefix(bool disassembly)
    {
        var emitter = CreateEmitter(disassembly: disassembly);
        var group = new insGroup();
        Prepare(emitter, group);
#if DEBUG
        const int expectedCapacity = 1424;
        const int prefix = 8;
#else
        var expectedCapacity = disassembly ? 1424 : 912;
        var prefix = disassembly ? 8 : 0;
#endif
        Assert.That(Capacity(emitter), Is.EqualTo((nuint)expectedCapacity));

        for (var index = 0; index < 64; index++)
        {
            var descriptor = DescriptorFactory.Basic(small: index < 14);
            AddDescriptor(emitter, descriptor, 1);
        }

        Assert.That(Used(emitter), Is.EqualTo(Capacity(emitter)));
        _ = Save(emitter, false);

        Assert.That(group.igInsCnt, Is.EqualTo(64));
        Assert.That(group.igSize, Is.EqualTo(64));
        Assert.That(group.igData, Has.Length.EqualTo(64));
        Assert.That(group.igStorageSize, Is.EqualTo((nuint)(expectedCapacity + sizeof(uint))));
        Assert.That(group.igData![0].StorageOffset, Is.EqualTo((nuint)prefix));
        Assert.That(group.igData[14].StorageOffset, Is.EqualTo((nuint)((14 * (8 + prefix)) + prefix)));
        Assert.That(group.igLastIns, Is.SameAs(group.igData[^1]));
        Assert.That(CodeOffset(emitter), Is.EqualTo(64));
        Assert.That(Buffer(emitter), Is.Empty);
    }

    [Test]
    public static void SavingReversesAndAppendsJumpAndAlignmentListsWithoutLosingAliases()
    {
        var emitter = CreateEmitter();
        var firstGroup = new insGroup();
        Prepare(emitter, firstGroup);
        var firstJump = DescriptorFactory.Jump(emitter, 0);
        AddDescriptor(emitter, firstJump, 2);
        var firstAlign = DescriptorFactory.Align(emitter);
        AddDescriptor(emitter, firstAlign, 3);
        _ = Save(emitter, false);
        var firstData = firstGroup.igData!;

        var secondGroup = new insGroup();
        NoGc(emitter) = true;
        Prepare(emitter, secondGroup);
        var secondJump = DescriptorFactory.Jump(emitter, 0);
        AddDescriptor(emitter, secondJump, 2);
        var secondAlign = DescriptorFactory.Align(emitter);
        AddDescriptor(emitter, secondAlign, 3);
        var thirdAlign = DescriptorFactory.Align(emitter);
        AddDescriptor(emitter, thirdAlign, 3);
        var thirdJump = DescriptorFactory.Jump(emitter, 8);
        AddDescriptor(emitter, thirdJump, 2);
        _ = Save(emitter, false);

        Assert.That(firstGroup.igData, Is.SameAs(firstData));
        Assert.That(firstData, Is.EqualTo((Emitter.instrDesc[])[firstJump, firstAlign]));
        Assert.That(secondGroup.igData, Is.EqualTo((Emitter.instrDesc[])[secondJump, secondAlign, thirdAlign, thirdJump]));
        Assert.That(DescriptorFactory.FirstJump(emitter), Is.SameAs(firstJump));
        Assert.That(DescriptorFactory.NextJump(firstJump), Is.SameAs(secondJump));
        Assert.That(DescriptorFactory.NextJump(secondJump), Is.SameAs(thirdJump));
        Assert.That(DescriptorFactory.NextJump(thirdJump), Is.Null);
        Assert.That(DescriptorFactory.LastJump(emitter), Is.SameAs(thirdJump));
        Assert.That(DescriptorFactory.PendingJump(emitter), Is.Null);
        Assert.That(DescriptorFactory.FirstAlign(emitter), Is.SameAs(firstAlign));
        Assert.That(DescriptorFactory.NextAlign(firstAlign), Is.SameAs(secondAlign));
        Assert.That(DescriptorFactory.NextAlign(secondAlign), Is.SameAs(thirdAlign));
        Assert.That(DescriptorFactory.NextAlign(thirdAlign), Is.Null);
        Assert.That(DescriptorFactory.LastAlign(emitter), Is.SameAs(thirdAlign));
        Assert.That(DescriptorFactory.LastAlignGroup(emitter), Is.SameAs(secondAlign));
        Assert.That(DescriptorFactory.PendingAlign(emitter), Is.Null);
        Assert.That(secondGroup.igLastIns, Is.SameAs(thirdJump));
        Assert.That(LastInstruction(emitter), Is.SameAs(thirdJump));
        Assert.That(LastInstructionGroup(emitter), Is.SameAs(secondGroup));
        Assert.That(thirdJump.StorageGroup, Is.SameAs(secondGroup));
        Assert.That(thirdJump.StorageIndex, Is.EqualTo(3));
        Assert.That(secondGroup.igFlags & InsGroupFlags.HasRemovableJump, Is.EqualTo(InsGroupFlags.HasRemovableJump));
        Assert.That(LastSavedNoGc(emitter), Is.True);
        Assert.That(CodeOffset(emitter), Is.EqualTo(15));
    }

    [TestCase(InsGroupFlags.Prolog, true)]
    [TestCase(InsGroupFlags.Epilog, true)]
    [TestCase(InsGroupFlags.FuncletProlog, true)]
    [TestCase(InsGroupFlags.FuncletEpilog, true)]
    [TestCase(InsGroupFlags.OutOfOrderHead, false)]
    public static void OutOfOrderGroupsUseTheFixedSizeJumpList(InsGroupFlags flags, bool fixedSize)
    {
        var emitter = CreateEmitter();
        var group = new insGroup { igFlags = flags };
        Prepare(emitter, group);
        var first = DescriptorFactory.Jump(emitter, 0);
        AddDescriptor(emitter, first, 2);
        var second = DescriptorFactory.Jump(emitter, 2);
        AddDescriptor(emitter, second, 2);

        _ = Save(emitter, false);

        Assert.That(DescriptorFactory.FixedJumps(emitter), Is.SameAs(fixedSize ? first : null));
        Assert.That(DescriptorFactory.FirstJump(emitter), Is.SameAs(fixedSize ? null : first));
        Assert.That(DescriptorFactory.LastJump(emitter), Is.SameAs(fixedSize ? null : second));
        Assert.That(DescriptorFactory.NextJump(first), Is.SameAs(second));
        Assert.That(DescriptorFactory.NextJump(second), Is.Null);
    }

    [Test]
    public static void SavingAfterLastInstructionRemovalDoesNotInventALastInstruction()
    {
        var emitter = CreateEmitter();
        var group = new insGroup();
        Prepare(emitter, group);
        AddDescriptor(emitter, DescriptorFactory.Basic(), 1);
        LastInstruction(emitter) = null;
        LastInstructionGroup(emitter) = null;

        _ = Save(emitter, false);

        Assert.That(group.igInsCnt, Is.EqualTo(1));
        Assert.That(group.igLastIns, Is.Null);
        Assert.That(LastInstruction(emitter), Is.Null);
        Assert.That(group.igFlags & InsGroupFlags.HasRemovableJump, Is.EqualTo(InsGroupFlags.None));
    }

    private static void AddDescriptor(Emitter emitter, Emitter.instrDesc descriptor, uint codeSize)
    {
        descriptor.StorageGroup = emitter.emitCurIG;
        descriptor.StorageIndex = Buffer(emitter).Count;
        descriptor.StorageOffset = Used(emitter) + (nuint)DebugPrefix(emitter);
        descriptor.StorageSize = (nuint)(descriptor.NativeLogicalSize + DebugPrefix(emitter));
        descriptor.idCodeSize(codeSize);
        descriptor.idSetPrevSize((uint)LastInstructionSize(emitter));
        Buffer(emitter).Add(descriptor);
        Used(emitter) += descriptor.StorageSize;
        Count(emitter)++;
        CodeSize(emitter) += (int)codeSize;
        LastInstruction(emitter) = descriptor;
        LastInstructionGroup(emitter) = emitter.emitCurIG;
        LastInstructionSize(emitter) = (int)descriptor.StorageSize;
    }

    private static DescriptorFactory CreateEmitter(int trackedCount = 1, bool disassembly = false)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = trackedCount;
        compiler.lvaTrackedCountInSizeTUnits = (trackedCount + 63) / 64;
        compiler.opts.disAsm = disassembly;
        var emitter = new DescriptorFactory(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();

        return emitter;
    }

    private sealed class DescriptorFactory : Emitter
    {
        public DescriptorFactory(CodeGen codeGen) : base(codeGen)
        {
        }

        public static instrDesc Basic(bool small = false)
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(INS_nop);

            if (small)
            {
                descriptor.idSetIsSmallDsc();
            }

            return descriptor;
        }

        public static instrDesc Jump(Emitter emitter, uint offset)
        {
            var descriptor = new instrDescJmp
            {
                idjIG = emitter.emitCurIG,
                idjOffs = offset,
                idjShort = true,
                idjNext = PendingJumps(emitter),
            };
            descriptor.idIns(INS_jmp);
            PendingJumps(emitter) = descriptor;

            return descriptor;
        }

        public static instrDesc Align(Emitter emitter)
        {
            var descriptor = new instrDescAlign
            {
                idaIG = emitter.emitCurIG,
                idaNext = PendingAligns(emitter),
            };
            descriptor.idIns(INS_align);
            PendingAligns(emitter) = descriptor;

            return descriptor;
        }

        public static instrDesc? FirstJump(Emitter emitter) => Jumps(emitter);
        public static instrDesc? LastJump(Emitter emitter) => JumpLast(emitter);
        public static instrDesc? FixedJumps(Emitter emitter) => FixedSizeJumps(emitter);
        public static instrDesc? PendingJump(Emitter emitter) => PendingJumps(emitter);
        public static instrDesc? NextJump(instrDesc descriptor) => ((instrDescJmp)descriptor).idjNext;
        public static instrDesc? FirstAlign(Emitter emitter) => Aligns(emitter);
        public static instrDesc? LastAlign(Emitter emitter) => AlignLast(emitter);
        public static instrDesc? LastAlignGroup(Emitter emitter) => AlignLastGroup(emitter);
        public static instrDesc? PendingAlign(Emitter emitter) => PendingAligns(emitter);
        public static instrDesc? NextAlign(instrDesc descriptor) => ((instrDescAlign)descriptor).idaNext;

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGjmpList")]
        private static extern ref instrDescJmp? PendingJumps(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitJumpList")]
        private static extern ref instrDescJmp? Jumps(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitJumpLast")]
        private static extern ref instrDescJmp? JumpLast(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitFixedSizeJumpList")]
        private static extern ref instrDescJmp? FixedSizeJumps(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGAlignList")]
        private static extern ref instrDescAlign? PendingAligns(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAlignList")]
        private static extern ref instrDescAlign? Aligns(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAlignLast")]
        private static extern ref instrDescAlign? AlignLast(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAlignLastGroup")]
        private static extern ref instrDescAlign? AlignLastGroup(Emitter emitter);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int DebugPrefix(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurCodeOffset")]
    private static extern ref int CodeOffset(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGenIG")]
    private static extern void Prepare(Emitter emitter, insGroup group);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup Save(Emitter emitter, bool overflow);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc> Buffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int Count(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CodeSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCIG")]
    private static extern ref bool NoGc(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsFullSize")]
    private static extern ref int LastInstructionSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastSavedIGWasNoGC")]
    private static extern ref bool LastSavedNoGc(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsIG")]
    private static extern ref insGroup? LastInstructionGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrevGCrefVars")]
    private static extern ref nint[] PrevVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefVars")]
    private static extern ref nint[] InitVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVars")]
    private static extern ref nint[] ThisVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefRegs")]
    private static extern ref regMask InitRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitByrefRegs")]
    private static extern ref regMask InitByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrevGCrefRegs")]
    private static extern ref regMask PrevRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrevByrefRegs")]
    private static extern ref regMask PrevByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceStoreGCState")]
    private static extern ref bool ForceGcState(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAddedLabel")]
    private static extern ref bool AddedLabel(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "igStorageSize")]
    private static extern ref nuint GroupStorageSize(insGroup group);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "igDataOffset")]
    private static extern ref nuint GroupDataOffset(insGroup group);
}
