// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 || TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SharedLocalVariableSpillTests
{
#if TARGET_AMD64
    private const regNumber IntegerRegister = REG_RAX;
    private const regNumber FloatRegister = REG_XMM0;
    private const regNumber UnrelatedRegister = REG_RDX;
    private const regNumber ReferenceRegister = REG_RCX;
    private const regNumber ByrefRegister = REG_R8;
#else
    private const regNumber IntegerRegister = REG_R0;
    private const regNumber FloatRegister = REG_V0;
    private const regNumber UnrelatedRegister = REG_R1;
    private const regNumber ReferenceRegister = REG_R2;
    private const regNumber ByrefRegister = REG_R3;
#endif

#if TARGET_AMD64
    private const instruction IntegerStore = INS_mov;
    private const instruction FloatStore = INS_movss;
    private const instruction DoubleStore = INS_movsd_simd;
    private const instruction VectorStore = INS_movaps;
    private const instruction StructVectorStore = INS_movups;
#else
    private const instruction IntegerStore = INS_str;
    private const instruction FloatStore = INS_str;
    private const instruction DoubleStore = INS_str;
    private const instruction VectorStore = INS_str;
    private const instruction StructVectorStore = INS_str;
#endif

    [TestCase(TYP_BYTE, IntegerRegister, IntegerStore, EA_4BYTE, 0u)]
    [TestCase(TYP_UBYTE, IntegerRegister, IntegerStore, EA_4BYTE, 0u)]
    [TestCase(TYP_SHORT, IntegerRegister, IntegerStore, EA_4BYTE, 0u)]
    [TestCase(TYP_USHORT, IntegerRegister, IntegerStore, EA_4BYTE, 0u)]
    [TestCase(TYP_INT, IntegerRegister, IntegerStore, EA_4BYTE, 0u)]
    [TestCase(TYP_LONG, IntegerRegister, IntegerStore, EA_8BYTE, 0u)]
    [TestCase(TYP_FLOAT, FloatRegister, FloatStore, EA_4BYTE, 0u)]
    [TestCase(TYP_DOUBLE, FloatRegister, DoubleStore, EA_8BYTE, 0u)]
    [TestCase(TYP_SIMD8, FloatRegister, DoubleStore, EA_8BYTE, 0u)]
    [TestCase(TYP_SIMD16, FloatRegister, VectorStore, EA_16BYTE, 0u)]
    [TestCase(TYP_STRUCT, IntegerRegister, IntegerStore, EA_4BYTE, 1u)]
    [TestCase(TYP_STRUCT, IntegerRegister, IntegerStore, EA_4BYTE, 2u)]
    [TestCase(TYP_STRUCT, IntegerRegister, IntegerStore, EA_4BYTE, 4u)]
    [TestCase(TYP_STRUCT, IntegerRegister, IntegerStore, EA_8BYTE, 8u)]
    [TestCase(TYP_STRUCT, FloatRegister, StructVectorStore, EA_16BYTE, 16u)]
    public static void SpillRecordsTheNormalizedStackHomeAndKillsOnlyTheLocalRegister(
        var_types type, regNumber reg, instruction ins, emitAttr size, uint layoutSize)
    {
        WithCompiler(type, reg, (compiler, codeGen, tree) =>
        {
            var flags = tree.Flags;

            codeGen.genSpillVar(tree);

            var descriptor = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("The local spill did not record a store.");
            Assert.That(CurrentCount(codeGen.Emitter), Is.EqualTo(1));
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            Assert.That(descriptor.idCodeSize(), Is.GreaterThan(0));
#if TARGET_ARM64
            Assert.That(CurrentSize(codeGen.Emitter), Is.EqualTo(4));
#endif
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(UnrelatedRegister)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.RegNum, Is.EqualTo(reg));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
        }, layoutSize);
    }

    [TestCase(TYP_REF, false)]
    [TestCase(TYP_REF, true)]
    [TestCase(TYP_BYREF, false)]
    [TestCase(TYP_BYREF, true)]
    public static void GcSpillPreservesUnrelatedPointersAndAddsOrRetainsTheTrackedStackHome(
        var_types type, bool alreadyLive)
    {
        WithCompiler(type, IntegerRegister, (compiler, codeGen, tree) =>
        {
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            if (alreadyLive)
            {
                VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0);
            }
            codeGen.GCInfo.gcMarkRegPtrVal(IntegerRegister, type);
            var flags = tree.Flags;

            codeGen.genSpillVar(tree);

            var descriptor = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("The GC local spill did not record a store.");
            Assert.That(CurrentCount(codeGen.Emitter), Is.EqualTo(1));
            Assert.That(descriptor.idIns(), Is.EqualTo(IntegerStore));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(descriptor.idGCref(), Is.EqualTo(
                type == TYP_REF ? GCInfo.GCtype.GCT_GCREF : GCInfo.GCtype.GCT_BYREF));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(UnrelatedRegister)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
        });
    }

    [TestCase(TYP_BYTE, EA_1BYTE)]
    [TestCase(TYP_SHORT, EA_2BYTE)]
    public static void OsrFieldsKeepTheirNarrowStackHomes(var_types type, emitAttr size)
    {
        WithCompiler(type, IntegerRegister, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].lvIsOSRLocal = true;
            compiler.lvaTable[0].lvIsStructField = true;

            codeGen.genSpillVar(tree);

            var descriptor = LastInstruction(codeGen.Emitter) ??
                throw new AssertionException("The OSR field spill did not record a store.");
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
#if TARGET_ARM64
            Assert.That(descriptor.idIns(), Is.EqualTo(type == TYP_BYTE ? INS_strb : INS_strh));
#else
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
#endif
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_INT, true)]
    [TestCase(TYP_REF, false)]
    [TestCase(TYP_REF, true)]
    [TestCase(TYP_BYREF, false)]
    [TestCase(TYP_BYREF, true)]
    public static void AlwaysMemoryUsesAvoidStoringButStillMoveRegisterLivenessToMemory(
        var_types type, bool singleDef)
    {
        WithCompiler(type, IntegerRegister, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].lvSpillAtSingleDef = singleDef;
            compiler.lvaTable[0]._lvLiveInOutOfHandler = !singleDef;
            var isGc = varTypeIsGC(type);
            if (isGc)
            {
                VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
                codeGen.GCInfo.gcMarkRegPtrVal(IntegerRegister, type);
            }
            var flags = tree.Flags;

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(UnrelatedRegister)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(isGc));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.RegNum, Is.EqualTo(IntegerRegister));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void DefinitionsOnlyClearSpillAndRetainTheHomeOnlyWhenAlreadySpilled(
        bool spilled, bool singleDef)
    {
        WithCompiler(TYP_REF, IntegerRegister, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].lvSpillAtSingleDef = singleDef;
            compiler.lvaTable[0]._lvLiveInOutOfHandler = spilled && !singleDef;
            tree.Flags |= GTF_VAR_DEF;
            if (spilled)
            {
                tree.Flags |= GTF_SPILLED;
            }
            compiler.opts.compDbgInfo = true;
            codeGen.initializeVariableLiveKeeper();
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            codeGen.GCInfo.gcMarkRegPtrVal(IntegerRegister, TYP_REF);
            var mask = codeGen.RegSet.GetMaskVars();
            var refs = codeGen.GCInfo.gcRegGCrefSetCur;
            var flags = tree.Flags;

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(mask));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(refs));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(spilled ? IntegerRegister : REG_STK));
            Assert.That(tree.RegNum, Is.EqualTo(IntegerRegister));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Is.Empty);
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void StackResidentLocalsDoNotStoreKillRegistersOrReportANewLiveRange(var_types type)
    {
        WithCompiler(type, IntegerRegister, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].RegNum = REG_STK;
            codeGen.RegSet.RemoveMaskVars(Mask(IntegerRegister));
            compiler.opts.compDbgInfo = true;
            codeGen.initializeVariableLiveKeeper();
            if (varTypeIsGC(type))
            {
                VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            }
            var flags = tree.Flags;

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(UnrelatedRegister)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Is.Empty);
        });
    }

#if TARGET_ARM64
#if FEATURE_SIMD
    [Test]
    public static void Simd12SpillRecordsTheStackStoreBeforeUpdatingSpillState()
    {
        WithCompiler(TYP_SIMD12, FloatRegister, (compiler, codeGen, tree) =>
        {
            compiler.opts.compDbgInfo = true;
            codeGen.initializeVariableLiveKeeper();
            var flags = tree.Flags;

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.EqualTo(4));
            Assert.That(CurrentSize(codeGen.Emitter), Is.EqualTo(16));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(UnrelatedRegister)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.RegNum, Is.EqualTo(FloatRegister));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AlwaysMemorySimd12DoesNotRequireTheUnportedStore(bool singleDef)
    {
        WithCompiler(TYP_SIMD12, FloatRegister, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].lvSpillAtSingleDef = singleDef;
            compiler.lvaTable[0]._lvLiveInOutOfHandler = !singleDef;
            var flags = tree.Flags;

            codeGen.genSpillVar(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(UnrelatedRegister)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
        });
    }
#endif

    [Test]
    public static void DebugLocationDependencyObservesTheAlreadyChangedStackHomeAndLiveness()
    {
        WithCompiler(TYP_REF, IntegerRegister, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0]._lvLiveInOutOfHandler = true;
            compiler.opts.compDbgInfo = true;
            codeGen.initializeVariableLiveKeeper();
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            codeGen.GCInfo.gcMarkRegPtrVal(IntegerRegister, TYP_REF);
            var flags = tree.Flags;

            var error = Assert.Throws<FatalJitException>(() => codeGen.genSpillVar(tree));

            Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(error, Has.Message.EqualTo("Variable locations outside AMD64 are not implemented."));
            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(UnrelatedRegister)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(ByrefRegister)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.Flags, Is.EqualTo(flags & ~GTF_SPILL));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Is.Empty);
        });
    }
#endif

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void WithCompiler(var_types type, regNumber reg, Action<Compiler, CodeGen, GenTree> action,
        uint layoutSize = 0)
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
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        compiler.info.compLocalsCount = 1;
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [0];
        var local = type == TYP_STRUCT
            ? new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(layoutSize) }
            : new LclVarDsc { Type = type };
        local.RegNum = reg;
        local.lvTracked = true;
        local.lvLRACandidate = true;
        local.lvOnFrame = true;
        local.lvFramePointerBased = true;
        local.StackOffset = -16;
        compiler.lvaTable = [local];
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler) { IsFramePointerUsed = true, IsFramePointerRequired = true };
            compiler.codeGen = codeGen;
            codeGen.initializeVariableLiveKeeper();
            codeGen.RegSet.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            codeGen.RegSet.SetMaskVars(codeGen.genGetRegMask(in compiler.lvaTable[0]) | Mask(UnrelatedRegister));
            codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            codeGen.GCInfo.gcMarkRegPtrVal(ReferenceRegister, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(ByrefRegister, TYP_BYREF);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(true
#if DEBUG
                , false
#endif
                );
#if TARGET_AMD64
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.UseEvexEncodings = true;
#endif
            var tree = compiler.gtNewLclvNode(type, 0);
            tree.RegNum = reg;
            tree.Flags |= GTF_SPILL | GTF_VAR_DEATH;
            action(compiler, codeGen, tree);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);
}
#endif
