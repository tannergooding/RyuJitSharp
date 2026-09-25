// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.ICorDebugInfo.VarLocType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenSpillVariableTests
{
    [TestCase(TYP_BYTE, REG_RAX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_UBYTE, REG_RAX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_SHORT, REG_RAX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_USHORT, REG_RAX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_INT, REG_RAX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_LONG, REG_RAX, INS_mov, EA_8BYTE)]
    [TestCase(TYP_FLOAT, REG_XMM0, INS_movss, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, REG_XMM0, INS_movsd_simd, EA_8BYTE)]
    [TestCase(TYP_SIMD16, REG_XMM0, INS_movaps, EA_16BYTE)]
    [TestCase(TYP_SIMD32, REG_XMM0, INS_movups, EA_32BYTE)]
    [TestCase(TYP_SIMD64, REG_XMM0, INS_movups, EA_64BYTE)]
    [TestCase(TYP_MASK, REG_K1, INS_kmovq_msk, EA_8BYTE)]
    public static void SpillingUsesStackHomeWidthAndKillsOnlyTheVariablesRegister(
        var_types type, regNumber reg, instruction ins, emitAttr size)
    {
        WithCompiler(type, reg, (compiler, codeGen, tree) =>
        {
            var unrelated = regMaskTP.CreateFromRegNum(REG_RDX, REG_RDX.SingleTypeMask);
            var gcTemporary = regMaskTP.CreateFromRegNum(REG_RCX, REG_RCX.SingleTypeMask);
            codeGen.RegSet.AddMaskVars(unrelated);
            codeGen.GCInfo.gcMarkRegSetGCref(gcTemporary);

            codeGen.genSpillVar(tree);
            var descriptor = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("The spill did not record a store.");

            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idCodeSize(), Is.GreaterThan(0));
            Assert.That(CurrentCount(codeGen.Emitter), Is.EqualTo(1));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(unrelated));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(gcTemporary));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.Flags & GTF_SPILL, Is.EqualTo(GTF_EMPTY));
        });
    }

    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void SpillMovesGcLivenessFromTheRegisterToTheTrackedStackHome(var_types type)
    {
        WithCompiler(type, REG_RAX, (compiler, codeGen, tree) =>
        {
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, type);

            codeGen.genSpillVar(tree);

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.True);
            var descriptor = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("The GC spill did not record a store.");
            Assert.That(descriptor.idGCref(), Is.EqualTo(type == TYP_REF ? GCInfo.GCtype.GCT_GCREF : GCInfo.GCtype.GCT_BYREF));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AlwaysAliveMemoryLocalsAvoidTheStoreButStillDieInTheirRegister(bool writeThrough)
    {
        WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0]._lvLiveInOutOfHandler = writeThrough;
            compiler.lvaTable[0].lvSpillAtSingleDef = !writeThrough;

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars().IsEmpty, Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.Flags & GTF_SPILL, Is.EqualTo(GTF_EMPTY));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DefinitionsDoNotStoreAndOnlySpilledWriteThroughDefinitionsRetainTheirRegister(bool spilled)
    {
        WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, tree) =>
        {
            tree.Flags |= GTF_VAR_DEF;
            if (spilled)
            {
                tree.Flags |= GTF_SPILLED;
                compiler.lvaTable[0]._lvLiveInOutOfHandler = true;
            }
            var before = codeGen.RegSet.GetMaskVars();

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(before));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(spilled ? REG_RAX : REG_STK));
            Assert.That(tree.Flags & GTF_SPILL, Is.EqualTo(GTF_EMPTY));
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.EqualTo(spilled));
        });
    }

    [TestCase(TYP_BYTE, EA_1BYTE)]
    [TestCase(TYP_SHORT, EA_2BYTE)]
    public static void OsrStructFieldsRetainTheirNarrowStackHome(var_types type, emitAttr size)
    {
        WithCompiler(type, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].lvIsOSRLocal = true;
            compiler.lvaTable[0].lvIsStructField = true;

            codeGen.genSpillVar(tree);
            var descriptor = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("The OSR field spill did not record a store.");

            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
        });
    }

    [Test]
    public static void AnAlreadyStackResidentLocalDoesNotRecordAnotherSpill()
    {
        WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].RegNum = REG_STK;
            codeGen.RegSet.ClearMaskVars();

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.Flags & GTF_SPILL, Is.EqualTo(GTF_EMPTY));
        });
    }

    [Test]
    public static void SpillUpdatesTheLiveRangeAfterRecordingTheStoreAndChangingTheHome()
    {
        WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.opts.compDbgInfo = true;
            codeGen.initializeVariableLiveKeeper();
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);

            codeGen.genSpillVar(tree);
            var ranges = keeper.getLiveRangesForVarForBody(0);

            Assert.That(ranges, Has.Count.EqualTo(2));
            Assert.That(ranges[0].m_VarLocation.vlType, Is.EqualTo(VLT_REG));
            Assert.That(ranges[1].m_VarLocation.vlType, Is.EqualTo(VLT_STK));
            Assert.That(ranges[1].m_VarLocation.vlStk.vlsOffset, Is.EqualTo(-16));
            Assert.That(ranges[0].m_EndEmitLocation.Valid(), Is.True);
            Assert.That(ranges[1].m_EndEmitLocation.Valid(), Is.False);
            Assert.That(ranges[1].m_StartEmitLocation, Is.EqualTo(ranges[0].m_EndEmitLocation));
            Assert.That(ranges[1].m_StartEmitLocation, Is.Not.EqualTo(ranges[0].m_StartEmitLocation));
        });
    }

    [Test]
    public static void Simd12SpillsRecordBothNativeStores()
    {
        WithCompiler(TYP_SIMD12, REG_XMM0, (_, codeGen, tree) =>
        {
            codeGen.genSpillVar(tree);
            var descriptor = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("The SIMD12 spill did not record its upper store.");

            Assert.That(CurrentCount(codeGen.Emitter), Is.EqualTo(2));
            Assert.That(CurrentSize(codeGen.Emitter), Is.EqualTo(12));
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_extractps));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(2));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
            var descriptors = CurrentDescriptors(codeGen.Emitter) ??
                throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movsd_simd));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM0));
        });
    }

    private static void WithCompiler(var_types type, regNumber reg, Action<Compiler, CodeGen, GenTree> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.info.compTypeCtxtArg = BAD_VAR_NUM;
        compiler.lvaAsyncContinuationArg = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.info.compLocalsCount = 1;
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [0];
        compiler.lvaTable = [new LclVarDsc
        {
            Type = type,
            RegNum = reg,
            lvTracked = true,
            lvLRACandidate = true,
            lvOnFrame = true,
            lvFramePointerBased = true,
            StackOffset = -16,
        }];
        compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler) { IsFramePointerUsed = true };
            compiler.codeGen = codeGen;
            codeGen.initializeVariableLiveKeeper();
            codeGen.RegSet.SetMaskVars(codeGen.genGetRegMask(in compiler.lvaTable[0]));
            codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(true
#if DEBUG
                , false
#endif
                );
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.UseEvexEncodings = true;
            var tree = compiler.gtNewLclvNode(type, 0);
            tree.RegNum = reg;
            tree.Flags |= GTF_SPILL;
            action(compiler, codeGen, tree);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);
}
