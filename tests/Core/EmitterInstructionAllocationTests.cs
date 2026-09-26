// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
#endif
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using GCtype = RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInstructionAllocationTests
{
    [TestCase(EA_1BYTE)]
    [TestCase(EA_2BYTE)]
    [TestCase(EA_4BYTE)]
    [TestCase(EA_8BYTE)]
    [TestCase(EA_16BYTE)]
    [TestCase(EA_32BYTE)]
    [TestCase(EA_64BYTE)]
    public static void AllocationEncodesOperandSizesAndInitializesStorage(emitAttr size)
    {
        var emitter = CreateEmitter(out _);
        var first = Allocate(emitter, size);
        var second = Allocate(emitter, size, small: true);
        var fullSize = (nuint)(16 + Prefix(emitter));

        Assert.That(first.idOpSize(), Is.EqualTo(size));
        Assert.That(first.idGCref(), Is.EqualTo(GCtype.GCT_NONE));
        Assert.That(first.NativeLogicalSize, Is.EqualTo(16));
        Assert.That(second.NativeLogicalSize, Is.EqualTo(8));
        Assert.That(first.StorageGroup, Is.SameAs(emitter.emitCurIG));
        Assert.That(first.StorageIndex, Is.Zero);
        Assert.That(second.StorageIndex, Is.EqualTo(1));
        Assert.That(first.StorageOffset, Is.EqualTo((nuint)Prefix(emitter)));
        Assert.That(second.StorageOffset, Is.EqualTo(fullSize + (nuint)Prefix(emitter)));
        Assert.That(first.idPrevSize(), Is.Zero);
        Assert.That(second.idPrevSize(), Is.EqualTo((uint)fullSize));
        Assert.That(LastInstruction(emitter), Is.SameAs(second));
        Assert.That(emitter.emitCurIG!.igLastIns, Is.SameAs(second));
        Assert.That(CurrentCount(emitter), Is.EqualTo(2));
#if DEBUG
        Assert.That(first.idDebugOnlyInfo()?.idNum, Is.EqualTo(1u));
        Assert.That(first.idDebugOnlyInfo()?.idSize, Is.EqualTo((nuint)16));
        Assert.That(second.idDebugOnlyInfo()?.idNum, Is.EqualTo(2u));
        Assert.That(second.idDebugOnlyInfo()?.idSize, Is.EqualTo((nuint)8));
#else
        Assert.That(first.idDebugOnlyInfo(), Is.Null);
#endif
    }

    [TestCase(EA_GCREF, GCtype.GCT_GCREF)]
    [TestCase(EA_BYREF, GCtype.GCT_BYREF)]
    [TestCase(EA_GCREF_FLG | EA_BYREF_FLG | EA_4BYTE, GCtype.GCT_GCREF)]
    public static void PointerAttributesOverrideOperandWidth(emitAttr attr, GCtype type)
    {
        var emitter = CreateEmitter(out _);
        var descriptor = Allocate(emitter, attr);

        Assert.That(descriptor.idGCref(), Is.EqualTo(type));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
    }

    [TestCase("emitNewInstrJmp", INS_jmp, 48)]
#if DEBUG
    [TestCase("emitNewInstrAlign", INS_align, 48)]
#else
    [TestCase("emitNewInstrAlign", INS_align, 40)]
#endif
    public static void SpecialWrappersAllocateTheirNativeLayout(string method, instruction instruction, int size)
    {
        var emitter = CreateEmitter(out _);
        var allocator = typeof(Emitter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var descriptor = (Emitter.instrDesc)allocator.Invoke(emitter, null)!;
        if (instruction != INS_align)
        {
            descriptor.idIns(instruction);
        }

        Assert.That(descriptor.idIns(), Is.EqualTo(instruction));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(size));
        Assert.That(descriptor.StorageSize, Is.EqualTo((nuint)(size + Prefix(emitter))));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_1BYTE));
        Assert.That(descriptor.idGCref(), Is.EqualTo(GCtype.GCT_NONE));
    }

    [TestCase(false, EA_4BYTE, false, false)]
    [TestCase(false, EA_4BYTE | EA_DSP_RELOC_FLG, true, false)]
    [TestCase(false, EA_4BYTE | EA_CNS_RELOC_FLG, false, false)]
    [TestCase(true, EA_4BYTE | EA_CNS_RELOC_FLG, false, true)]
    [TestCase(true, EA_4BYTE | EA_DSP_RELOC_FLG | EA_CNS_RELOC_FLG, true, true)]
    public static void Amd64DisplacementRelocationDoesNotRequireRelocatableCode(
        bool relocatable, emitAttr attr, bool displacementReloc, bool constantReloc)
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.opts.compReloc = relocatable;
        var descriptor = Allocate(emitter, attr);

        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
        Assert.That(descriptor.idIsDspReloc(), Is.EqualTo(displacementReloc));
        Assert.That(descriptor.idIsCnsReloc(), Is.EqualTo(constantReloc));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AllocationExtendsAtExactlyTheNativeBufferEnd(bool disassembly)
    {
        var emitter = CreateEmitter(out _, disassembly);
        var original = emitter.emitCurIG!;
        var fullSize = (nuint)(8 + Prefix(emitter));
        var countBeforeExtension = (int)(Capacity(emitter) / fullSize) - 1;
        Emitter.instrDesc? last = null;

        for (var index = 0; index < countBeforeExtension; index++)
        {
            last = Allocate(emitter, EA_1BYTE, small: true);
            Assert.That(emitter.emitCurIG, Is.SameAs(original));
        }

        Assert.That(Used(emitter) + fullSize, Is.EqualTo(Capacity(emitter)));
        var next = Allocate(emitter, EA_1BYTE, small: true);

        Assert.That(emitter.emitCurIG, Is.Not.SameAs(original));
        Assert.That(original.igNext, Is.SameAs(emitter.emitCurIG));
        Assert.That(original.igInsCnt, Is.EqualTo(countBeforeExtension));
        Assert.That(original.igData, Has.Length.EqualTo(countBeforeExtension));
        Assert.That(original.igLastIns, Is.SameAs(last));
        Assert.That(emitter.emitCurIG!.igFlags, Is.EqualTo(InsGroupFlags.Extend));
        Assert.That(next.StorageGroup, Is.SameAs(emitter.emitCurIG));
        Assert.That(next.StorageIndex, Is.Zero);
        Assert.That(next.StorageOffset, Is.EqualTo((nuint)Prefix(emitter)));
        Assert.That(next.idPrevSize(), Is.Zero);
        Assert.That(CurrentCount(emitter), Is.EqualTo(1));

        if (disassembly)
        {
            Assert.That(next.idDebugOnlyInfo()?.idNum, Is.EqualTo((uint)(countBeforeExtension + 1)));
        }
    }

    [TestCase(254, false)]
    [TestCase(255, true)]
    public static void AllocationExtendsBeforeTheInstructionCountWouldOverflow(int count, bool extends)
    {
        var emitter = CreateEmitter(out _);
        var original = emitter.emitCurIG!;
        // Isolate the count limit from the smaller normal AMD64 byte-buffer limit.
        Capacity(emitter) = 8192;

        for (var index = 0; index < count; index++)
        {
            _ = Allocate(emitter, EA_1BYTE, small: true);
        }

        Assert.That(emitter.emitCurIG, Is.SameAs(original));
        _ = Allocate(emitter, EA_1BYTE, small: true);

        Assert.That(emitter.emitCurIG == original, Is.EqualTo(!extends));
        Assert.That(CurrentCount(emitter), Is.EqualTo(extends ? 1 : 255));

        if (extends)
        {
            Assert.That(original.igInsCnt, Is.EqualTo(255));
            Assert.That(original.igData, Has.Length.EqualTo(255));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ForcedEmptyGroupUpdatesNoGcStateWithoutAllocatingAnotherGroup(bool noGc)
    {
        var emitter = CreateEmitter(out _);
        var group = emitter.emitCurIG!;
        group.igFlags = noGc ? InsGroupFlags.None : InsGroupFlags.NoGCInterrupt;
        NoGc(emitter) = noGc;
        ForceNewGroup(emitter) = true;

        _ = Allocate(emitter, EA_1BYTE);

        Assert.That(emitter.emitCurIG, Is.SameAs(group));
        Assert.That((group.igFlags & InsGroupFlags.NoGCInterrupt) != 0, Is.EqualTo(noGc));
        Assert.That(ForceNewGroup(emitter), Is.True);

        _ = Allocate(emitter, EA_1BYTE);

        Assert.That(emitter.emitCurIG, Is.Not.SameAs(group));
        Assert.That(ForceNewGroup(emitter), Is.False);
        Assert.That((emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt) != 0, Is.EqualTo(noGc));
        Assert.That(emitter.emitCurIG.igFlags & InsGroupFlags.Extend, Is.EqualTo(InsGroupFlags.Extend));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NextGroupCopiesEntryGcStateOnlyForNonExtensions(bool extend)
    {
        var emitter = CreateEmitter(out _);
        _ = Allocate(emitter, EA_1BYTE);
        ThisVars(emitter)[0] = 1;
        ThisRefs(emitter) = SRBM_RAX;
        ThisByrefs(emitter) = SRBM_RCX;

        NextGroup(emitter, extend);

        Assert.That(InitVars(emitter)[0], Is.EqualTo(extend ? (nint)0 : (nint)1));
        Assert.That(InitRefs(emitter), Is.EqualTo(extend ? SRBM_NONE : SRBM_RAX));
        Assert.That(InitByrefs(emitter), Is.EqualTo(extend ? SRBM_NONE : SRBM_RCX));
        ThisVars(emitter)[0] = 2;
        Assert.That(InitVars(emitter)[0], Is.EqualTo(extend ? (nint)0 : (nint)1));
    }

#if DEBUG
    [Test]
    [NonParallelizable]
    public static void BlockMappingDiagnosticPrintsBlockNumberWithoutDebugIdentifier()
    {
        var emitter = CreateEmitter(out var compiler);
        var block = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
        block.bbNum = 1;
        compiler.verbose = true;

        var output = CodeGenLifeTransitionTests.Capture(() => _ = Allocate(emitter, EA_1BYTE));

        var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
        Assert.That(output, Is.EqualTo($"Mapped BB01 to {emitter.emitLabelString(group)}{Environment.NewLine}"));
        Assert.That(group.igBlocks, Is.EqualTo((BasicBlock?[])[block]));
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public static void EmitterStressSplitsUnlessTheGroupEndsWithAlignment(bool endsWithAlignment)
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.compAllowStress = true;
        compiler.info.compMethodName = "Method";
        compiler.info.compFullName = "EmitterStress:Method";
        var original = emitter.emitCurIG!;
        var savedNames = StressNames(ref JitConfig);

        fixed (byte* names = "STRESS_EMITTER\0"u8)
        {
            try
            {
                StressNames(ref JitConfig) = names;

                if (endsWithAlignment)
                {
                    var allocator = typeof(Emitter).GetMethod("emitNewInstrAlign",
                        BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var alignment = (Emitter.instrDesc)allocator.Invoke(emitter, null)!;
                    alignment.idIns(INS_align);
                    original.igFlags |= InsGroupFlags.HasAlign;
                }
                else
                {
                    _ = Allocate(emitter, EA_1BYTE);
                }

                _ = Allocate(emitter, EA_1BYTE);

                Assert.That(emitter.emitCurIG == original, Is.EqualTo(endsWithAlignment));
                Assert.That(CurrentCount(emitter), Is.EqualTo(endsWithAlignment ? 2 : 1));
                Assert.That(compiler.compActiveStressModes[(int)Compiler.compStressArea.STRESS_EMITTER], Is.EqualTo(1));
            }
            finally
            {
                StressNames(ref JitConfig) = savedNames;
            }
        }
    }

    [Test]
    public static void AllocationMapsBlockTransitionsInOrder()
    {
        var emitter = CreateEmitter(out var compiler);
        var first = compiler.compCurBB;
        _ = Allocate(emitter, EA_1BYTE);
        _ = Allocate(emitter, EA_1BYTE);
        var second = new BasicBlock(null, null);
        compiler.compCurBB = second;
        _ = Allocate(emitter, EA_1BYTE);
        compiler.compCurBB = first;
        _ = Allocate(emitter, EA_1BYTE);

        Assert.That(emitter.emitCurIG!.igBlocks, Is.EqualTo((BasicBlock?[])[first, second, first]));
        Assert.That(emitter.emitCurIG.lastGeneratedBlock, Is.SameAs(first));
        NextGroup(emitter, true);
        Assert.That(emitter.emitCurIG!.lastGeneratedBlock, Is.Null);
        _ = Allocate(emitter, EA_1BYTE);
        Assert.That(emitter.emitCurIG.igBlocks, Is.EqualTo((BasicBlock?[])[first]));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressModeNames")]
    private static extern ref byte* StressNames(ref JitConfigValues config);
#endif

    internal static Emitter.instrDesc Allocate(Emitter emitter, emitAttr attr, bool small = false)
    {
        var allocator = typeof(Emitter).GetMethod(small ? "emitNewInstrSmall" : "emitNewInstr",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var descriptor = (Emitter.instrDesc)allocator.Invoke(emitter, [attr])!;
        descriptor.idIns(INS_nop);

        return descriptor;
    }

    private static Emitter CreateEmitter(out Compiler compiler, bool disassembly = false)
    {
        compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.opts.disAsm = disassembly;
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNxtIG")]
    private static extern void NextGroup(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int Prefix(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCIG")]
    private static extern ref bool NoGc(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);

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
