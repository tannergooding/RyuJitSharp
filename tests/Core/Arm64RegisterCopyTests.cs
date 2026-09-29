// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64RegisterCopyTests
{
    [TestCase(TYP_INT, INS_mov)]
    [TestCase(TYP_REF, INS_mov)]
    [TestCase(TYP_BYREF, INS_mov)]
    [TestCase(TYP_FLOAT, INS_fmov)]
    [TestCase(TYP_DOUBLE, INS_fmov)]
    [TestCase(TYP_SIMD8, INS_mov)]
    [TestCase(TYP_SIMD12, INS_mov)]
    [TestCase(TYP_SIMD16, INS_mov)]
    [TestCase(TYP_MASK, INS_sve_mov)]
    public static void DestinationTypeSelectsTheNativeCopyInstruction(var_types type, instruction expected)
    {
        Assert.That(CreateCodeGen().ins_Copy(type), Is.EqualTo(expected));
    }

    [TestCase(REG_R1, TYP_INT, INS_mov)]
    [TestCase(REG_V1, TYP_INT, INS_mov)]
    [TestCase(REG_R1, TYP_FLOAT, INS_fmov)]
    [TestCase(REG_V1, TYP_FLOAT, INS_fmov)]
    [TestCase(REG_R1, TYP_DOUBLE, INS_fmov)]
    [TestCase(REG_V1, TYP_DOUBLE, INS_fmov)]
    [TestCase(REG_V1, TYP_SIMD16, INS_mov)]
    [TestCase(REG_R1, TYP_MASK, INS_sve_mov)]
    [TestCase(REG_P1, TYP_MASK, INS_sve_mov)]
    public static void SourceClassSelectsTheNativeCopyInstruction(
        regNumber source, var_types type, instruction expected)
    {
        Assert.That(CreateCodeGen().ins_Copy(source, type), Is.EqualTo(expected));
    }

    [TestCase(TYP_INT, REG_R0, REG_R1, EA_UNKNOWN, EA_4BYTE, INS_mov)]
    [TestCase(TYP_LONG, REG_R0, REG_R1, EA_UNKNOWN, EA_8BYTE, INS_mov)]
    [TestCase(TYP_LONG, REG_R0, REG_R1, EA_4BYTE, EA_4BYTE, INS_mov)]
    [TestCase(TYP_FLOAT, REG_V0, REG_V1, EA_UNKNOWN, EA_4BYTE, INS_fmov)]
    [TestCase(TYP_DOUBLE, REG_V0, REG_V1, EA_UNKNOWN, EA_8BYTE, INS_fmov)]
    [TestCase(TYP_SIMD16, REG_V0, REG_V1, EA_UNKNOWN, EA_16BYTE, INS_mov)]
    public static void MoveRecordsTheSelectedInstructionAndExplicitOrDefaultWidth(
        var_types type, regNumber destination, regNumber source,
        emitAttr requested, emitAttr expectedSize, instruction expected)
    {
        var codeGen = CreateCodeGen();

        codeGen.inst_Mov(type, destination, source, canSkip: false, requested);

        var descriptor = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing register move.");
        Assert.That(descriptor.idIns(), Is.EqualTo(expected));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(expectedSize));
        Assert.That(descriptor.idReg1(), Is.EqualTo(destination));
        Assert.That(descriptor.idReg2(), Is.EqualTo(source));
        Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
    }

    [TestCase(false, 4)]
    [TestCase(true, 0)]
    public static void MoveForwardsExplicitElisionPermission(bool canSkip, int expectedSize)
    {
        var codeGen = CreateCodeGen();

        codeGen.inst_Mov(TYP_LONG, REG_R0, REG_R0, canSkip);

        Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(expectedSize));
    }

    [TestCase(0)]
    [TestCase(2)]
    public static void VariableRangeKeeperStartsWithEmptyBodyAndPrologRanges(int localCount)
    {
        var keeper = new CodeGen.VariableLiveKeeper(localCount, localCount, CreateCodeGen());
        for (var index = 0; index < localCount; index++)
        {
            Assert.That(keeper.getLiveRangesForVarForBody(index), Is.Empty);
            Assert.That(keeper.getLiveRangesForVarForProlog(index), Is.Empty);
        }

        keeper.siEndAllVariableLiveRange();
        keeper.psiClosePrologVariableRanges();
    }

    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_INT)]
    public static void ProductionMarksTheDestinationGcKind(var_types type)
    {
        WithRegisterLife((compiler, codeGen) =>
        {
            var value = compiler.gtNewIconNode(type, 0);
            value.RegNum = REG_R0;

            codeGen.genProduceReg(value);

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? Mask(REG_R0) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? Mask(REG_R0) : default));
        });
    }

    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_INT)]
    public static void ScalarCopyConsumesItsSourceAndProducesTheDestination(var_types type)
    {
        WithRegisterLife((compiler, codeGen) =>
        {
            var value = compiler.gtNewIconNode(type, 0);
            value.RegNum = REG_R0;
            var copy = new GenTreeCopyOrReload(GT_COPY, type, value) { RegNum = REG_R1 };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R0, type);

            codeGen.genRegCopy(copy);

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? Mask(REG_R1) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? Mask(REG_R1) : default));
            var descriptor = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing scalar copy.");
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndexedCopyConsumesOneResultAndCopiesOnlyWhenAssigned(bool retarget)
    {
        WithRegisterLife((compiler, codeGen) =>
        {
            var call = new GenTreeCall(TYP_STRUCT);
            call._returnTypeDesc.InitializeReturnType(compiler, TYP_REF, null, CorInfoCallConvExtension.Managed);
            ReturnTypes(ref call._returnTypeDesc)[1] = TYP_BYREF;
            call.SetRegNumByIdx(REG_R0, 0);
            call.SetRegNumByIdx(REG_R1, 1);
            var copy = new GenTreeCopyOrReload(GT_COPY, TYP_STRUCT, call) { RegNum = REG_R3 };
            if (retarget)
            {
                copy.SetRegNumByIdx(REG_R2, 1);
            }
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R1, TYP_BYREF);

            Assert.That(codeGen.genRegCopy(copy, 1), Is.EqualTo(retarget ? REG_R2 : REG_R1));

            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(retarget ? Mask(REG_R2) : default));
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(retarget ? 4 : 0));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalReloadPreservesRespillAndLastUseLocationRules(bool reSpill, bool lastUse)
    {
        WithRegisterLife((compiler, codeGen) =>
        {
            var local = PrepareLocal(compiler, codeGen, TYP_REF);
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0);

            codeGen.genUnspillLocal(0, TYP_REF, local, REG_R0, reSpill, lastUse);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(reSpill ? REG_STK : REG_R0));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(reSpill ? default : Mask(REG_R0)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_R0)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(reSpill));
            var descriptor = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing local reload.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
        });
    }

    [TestCase(false, false, 4)]
    [TestCase(false, true, 0)]
    [TestCase(true, false, 4)]
    [TestCase(true, true, 4)]
    public static void LocalSpillPreservesWriteThroughRules(bool definition, bool writeThrough, int expectedSize)
    {
        WithRegisterLife((compiler, codeGen) =>
        {
            var local = PrepareLocal(compiler, codeGen, TYP_REF);
            compiler.lvaTable[0].RegNum = REG_R0;
            compiler.lvaTable[0].lvSpillAtSingleDef = writeThrough;
            local.RegNum = REG_R0;
            local.Flags = GTF_SPILL | (definition ? GTF_VAR_DEF : GTF_EMPTY);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R0, TYP_REF);

            codeGen.genSpillLocal(0, TYP_REF, local, REG_R0);

            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(expectedSize));
            if (expectedSize != 0)
            {
                var descriptor = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing local spill.");
                Assert.That(descriptor.idIns(), Is.EqualTo(INS_str));
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
                Assert.That(descriptor.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            }
        });
    }

    [TestCase(TYP_INT, REG_R0, EA_4BYTE)]
    [TestCase(TYP_REF, REG_R0, EA_8BYTE)]
    [TestCase(TYP_BYREF, REG_R0, EA_8BYTE)]
    [TestCase(TYP_FLOAT, REG_V0, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, REG_V0, EA_8BYTE)]
    public static void TreeSpillAndReloadPreserveTypeAndRecycleTheTemporary(
        var_types type, regNumber reg, emitAttr size)
    {
        WithRegisterLife((compiler, codeGen) =>
        {
            _ = PrepareLocal(compiler, codeGen, TYP_INT);
            codeGen.RegSet.tmpBeginPreAllocateTemps();
            codeGen.RegSet.tmpPreAllocateTemps(type, 1);
            var temp = codeGen.RegSet.tmpGetTemp(type);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            GenTree value = varTypeIsFloating(type)
                ? compiler.gtNewDconNode(type, 0)
                : compiler.gtNewIconNode(type, 0);
            value.RegNum = reg;
            value.Flags |= GTF_SPILL;

            codeGen.RegSet.rsSpillTree(reg, value);

            Assert.That(value.Flags & (GTF_SPILL | GTF_SPILLED), Is.EqualTo(GTF_SPILLED));
            var store = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing temporary spill.");
            Assert.That(store.idIns(), Is.EqualTo(INS_str));
            Assert.That(store.idOpSize(), Is.EqualTo(size));
            Assert.That(store.idReg1(), Is.EqualTo(reg));
            var spilled = codeGen.RegSet.rsUnspillInPlace(value, reg);
            Assert.That(spilled, Is.SameAs(temp));

            codeGen.reloadReg(type, spilled, reg);

            var load = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing temporary reload.");
            Assert.That(load.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(load.idOpSize(), Is.EqualTo(size));
            Assert.That(load.idReg1(), Is.EqualTo(reg));
            Assert.That(value.Flags & GTF_SPILLED, Is.EqualTo(GTF_EMPTY));
            codeGen.RegSet.tmpRlsTemp(spilled);
            var recycled = codeGen.RegSet.tmpGetTemp(type);
            Assert.That(recycled, Is.SameAs(temp));
            codeGen.RegSet.tmpRlsTemp(recycled);
            codeGen.RegSet.rsSpillBeg();
        });
    }

    private static GenTreeLclVar PrepareLocal(Compiler compiler, CodeGen codeGen, var_types type)
    {
        compiler.lvaCount = 1;
        compiler.lvaTrackedToVarNum = [0];
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
        compiler.lvaTable = [new LclVarDsc
        {
            Type = type,
            RegNum = REG_STK,
            lvTracked = true,
            lvLRACandidate = true,
            lvOnFrame = true,
            lvFramePointerBased = true,
            StackOffset = -16,
        }];
        codeGen.IsFramePointerRequired = true;
        codeGen.IsFramePointerUsed = true;
        codeGen.initializeVariableLiveKeeper();

        return compiler.gtNewLclvNode(type, 0).AsLclVar();
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void WithRegisterLife(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var codeGen = CreateCodeGen();
        var compiler = codeGen.Compiler;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.codeGen = codeGen;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
        LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static CodeGen CreateCodeGen()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.opts.compMinOptsIsSet = true;
        compiler.opts.compMinOpts = true;
        compiler.opts.canUseAllOpts = false;
        var codeGen = new CodeGen(compiler);
        codeGen.RegSet.rsClearRegsModified();
        var emitter = codeGen.Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );
        return codeGen;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regType")]
    private static extern ref InlineArrayMaxRetRegCount<var_types> ReturnTypes(ref ReturnTypeDesc descriptor);
}
#endif
