// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using GCtype = RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterStackStoreTests
{
    private static readonly JitFlags[] s_jitFlags = GC.AllocateArray<JitFlags>(1, pinned: true);

    [TestCase(INS_mov, EA_8BYTE, REG_RAX, 4u, IF_SWR_RRD)]
    [TestCase(INS_mov, EA_4BYTE, REG_R8, 4u, IF_SWR_RRD)]
    [TestCase(INS_mov, EA_2BYTE, REG_RAX, 4u, IF_SWR_RRD)]
    [TestCase(INS_mov, EA_1BYTE, REG_RSI, 4u, IF_SWR_RRD)]
    [TestCase(INS_xchg, EA_8BYTE, REG_RAX, 4u, IF_SRW_RRW)]
    [TestCase(INS_movups, EA_16BYTE, REG_XMM0, 5u, IF_SWR_RRD)]
    public static void StackStoresRecordOperandsNativeCodeSizeAndDescriptorAccounting(
        instruction ins, emitAttr size, regNumber reg, uint expectedSize, Emitter.insFormat format)
    {
        var emitter = CreateEmitter(out _);
        emitter.emitIns_S_R(ins, size, reg, 0, 7);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");

        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
        Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
        Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(7u));
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(expectedSize));
        Assert.That(CurrentSize(emitter), Is.EqualTo((int)expectedSize));
        Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(16));
#if DEBUG
        var debugInfo = descriptor.idDebugOnlyInfo() ?? throw new AssertionException("Missing descriptor debug information.");
        Assert.That(debugInfo.idVarRefOffs, Is.EqualTo(emitVarRefOffset));
#endif
    }

    [TestCase(false, 2)]
    [TestCase(true, 1)]
    public static void OptimizedIdenticalStoresAreElidedButMinoptsStoresAreRetained(bool optimized, int count)
    {
        var emitter = CreateEmitter(out _, minopts: !optimized);
        emitter.emitIns_S_R(INS_mov, EA_8BYTE, REG_RAX, 0, 0);
        var first = LastInstruction(emitter);
        emitter.emitIns_S_R(INS_mov, EA_8BYTE, REG_RAX, 0, 0);

        Assert.That(CurrentCount(emitter), Is.EqualTo(count));
        Assert.That(CurrentSize(emitter), Is.EqualTo(4 * count));
        Assert.That(ReferenceEquals(first, LastInstruction(emitter)), Is.EqualTo(optimized));
    }

    [TestCase(EA_GCREF, GCtype.GCT_GCREF)]
    [TestCase(EA_BYREF, GCtype.GCT_BYREF)]
    public static void GcBirthStoresKeepTheirPointerClassificationAndAreNeverElided(emitAttr attr, GCtype type)
    {
        var emitter = CreateEmitter(out _, minopts: false);
        emitter.emitIns_S_R(INS_mov, attr, REG_RAX, 0, 0);
        emitter.emitIns_S_R(INS_mov, attr, REG_RAX, 0, 0);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No pointer store was recorded.");

        Assert.That(CurrentCount(emitter), Is.EqualTo(2));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_8BYTE));
        Assert.That(descriptor.idGCref(), Is.EqualTo(type));
    }

    [TestCase(INS_OPTS_EVEX_em_k1, 1u, false)]
    [TestCase(INS_OPTS_EVEX_em_k7, 7u, false)]
    [TestCase(INS_OPTS_EVEX_em_k3 | INS_OPTS_EVEX_em_zero, 3u, true)]
    public static void EmbeddedMaskOptionsRetainAllMaskBitsAndZeroing(insOpts options, uint mask, bool zero)
    {
        var emitter = CreateEmitter(out _);
        emitter.emitIns_S_R(INS_movups, EA_16BYTE, REG_XMM0, 0, 0, options);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No masked store was recorded.");

        Assert.That(descriptor.idGetEvexAaaContext(), Is.EqualTo(mask));
        Assert.That(descriptor.idIsEvexZContextSet(), Is.EqualTo(zero));
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(7u));
    }

    [Test]
    public static void SimdExtractStoreRecordsItsImmediateAndActualSize()
    {
        var emitter = CreateEmitter(out _);
        emitter.emitIns_S_R_I(INS_extractps, EA_4BYTE, 0, 8, REG_XMM0, 2);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No extract store was recorded.");

        Assert.That(descriptor.idIns(), Is.EqualTo(INS_extractps));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_SWR_RRD_CNS));
        Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
        Assert.That(descriptor.idSmallCns(), Is.EqualTo(2));
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(7u));
        Assert.That(CurrentSize(emitter), Is.EqualTo(7));
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void InstructionDisplayRecordsStackStoresAndTheirNativeSizes(bool immediate)
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.opts.dspCode = true;
        var before = Used(emitter);
        var diagnostic = InstructionRecordingTestSupport.Capture(() =>
        {
            if (immediate)
            {
                emitter.emitIns_S_R_I(INS_extractps, EA_4BYTE, 0, 0, REG_XMM0, 2);
            }
            else
            {
                emitter.emitIns_S_R(INS_mov, EA_8BYTE, REG_RAX, 0, 0);
            }
        });

        Assert.That(Used(emitter), Is.GreaterThan(before));
        Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        Assert.That(CurrentSize(emitter), Is.EqualTo(immediate ? 7 : 4));
        Assert.That(LastInstruction(emitter)?.idIns(), Is.EqualTo(immediate ? INS_extractps : INS_mov));
        Assert.That(diagnostic, Does.Contain(immediate ? "extractps" : "mov"));
    }
#endif

#if DEBUG
    private const int emitVarRefOffset = 11;
#endif

    private static Emitter CreateEmitter(out Compiler compiler, bool minopts = true)
    {
        compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.codeGen = new CodeGen(compiler) { IsFramePointerUsed = true };
        compiler.opts.jitFlags = (JitFlags*)Unsafe.AsPointer(ref s_jitFlags[0]);
        compiler.opts.SetMinOpts(minopts);
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc
        {
            Type = TYP_LONG,
            lvOnFrame = true,
            lvFramePointerBased = true,
            StackOffset = -16,
        }];
        compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.compCalleeRegsPushed = 0;
        var emitter = compiler.codeGen.Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , false
#endif
            );
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
#if DEBUG
        VarRefOffset(emitter) = emitVarRefOffset;
#endif
        return emitter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitVarRefOffs")]
    private static extern ref int VarRefOffset(Emitter emitter);
#endif
}
