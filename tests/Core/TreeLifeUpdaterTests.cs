// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.ICorDebugInfo.VarLocType;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class TreeLifeUpdaterTests
{
    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void ScalarUpdatesPrioritizeDeathAndDoNotInventRanges(bool born, bool dying, bool live)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree.Flags |= (born ? GTF_VAR_DEF : GTF_EMPTY) | (dying ? GTF_VAR_DEATH : GTF_EMPTY);
            var updater = new TreeLifeUpdater(compiler, forCodeGen: true);

            updater.UpdateLife(tree);

            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.EqualTo(live));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(live));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Has.Count.EqualTo(live ? 1 : 0));
            Assert.That(compiler.compCurLifeTree, Is.SameAs(tree));
        });
    }

    [Test]
    public static void RepeatedTreesAreIgnoredAndDistinctDeathsCloseTheExistingRange()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree.Flags |= GTF_VAR_DEF;
            var updater = new TreeLifeUpdater(compiler, forCodeGen: true);
            updater.UpdateLife(tree);
            tree.Flags = (tree.Flags & ~GTF_VAR_DEF) | GTF_VAR_DEATH;
            updater.UpdateLife(tree);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);

            var dying = compiler.gtNewLclvNode(TYP_REF, 0);
            dying.Flags |= GTF_VAR_DEATH;
            updater.UpdateLife(dying);

            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.False);
            var ranges = codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0);
            Assert.That(ranges, Has.Count.EqualTo(1));
            Assert.That(ranges[0].m_EndEmitLocation.Valid(), Is.True);
        });
    }

    [Test]
    public static void PartialDefinitionsDoNotCreateBirths()
    {
        WithCompiler((compiler, _) =>
        {
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree.Flags |= GTF_VAR_DEF | GTF_VAR_USEASG;
            new TreeLifeUpdater(compiler, forCodeGen: true).UpdateLife(tree);

            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
        });
    }

    [Test]
    public static void RegisterBirthChangesTheHomeBeforeOpeningTheRange()
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvLRACandidate = true;
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree.RegNum = REG_RCX;
            tree.Flags |= GTF_VAR_DEF;

            new TreeLifeUpdater(compiler, forCodeGen: true).UpdateLife(tree);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RCX));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(regMaskTP.CreateFromRegNum(REG_RCX, REG_RCX.SingleTypeMask)));
            Assert.That(VarSetOps.IsEmpty(compiler, codeGen.GCInfo.gcVarPtrSetCur), Is.True);
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0)[0].m_VarLocation.vlType,
                Is.EqualTo(VLT_REG));
        });
    }

    [Test]
    public static void CodegenLifeUpdatesPerformRealSpillsAndPreserveTheLocalsLiveness()
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.lvaTable[0].RegNum = REG_RAX;
            codeGen.RegSet.SetMaskVars(codeGen.genGetRegMask(in compiler.lvaTable[0]));
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
            codeGen.getVariableLiveKeeper().siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree.RegNum = REG_RAX;
            tree.Flags |= GTF_SPILL;

            new TreeLifeUpdater(compiler, forCodeGen: true).UpdateLife(tree);

            Assert.That(CurrentCount(codeGen.Emitter), Is.EqualTo(1));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.True);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(tree.Flags & GTF_SPILL, Is.EqualTo(GTF_EMPTY));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0)[1].m_VarLocation.vlType,
                Is.EqualTo(VLT_STK));
        });
    }

    [TestCase(GT_IND, false)]
    [TestCase(GT_IND, true)]
    [TestCase(GT_LOCKADD, false)]
    [TestCase(GT_LOCKADD, true)]
    [TestCase(GT_XADD, false)]
    [TestCase(GT_XADD, true)]
    [TestCase(GT_XCHG, false)]
    [TestCase(GT_XCHG, true)]
    [TestCase(GT_XAND, false)]
    [TestCase(GT_XAND, true)]
    [TestCase(GT_XORR, false)]
    [TestCase(GT_XORR, true)]
    public static void LocalAddressModesChooseTheNativeDefinitionNodeWithoutCodegenState(genTreeOps oper, bool general)
    {
        WithCompiler((compiler, _) =>
        {
            compiler.codeGen = null;
            var updater = new TreeLifeUpdater(compiler, forCodeGen: false);
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            address.Flags |= GTF_VAR_DEF;
            updater.UpdateLife(address, general);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.EqualTo(general));
            GenTree indir = oper == GT_IND
                ? new GenTreeIndir(oper, TYP_REF, address)
                : new GenTreeOp(oper, TYP_INT, address, compiler.gtNewIconNode(TYP_INT, 1));
            updater.UpdateLife(indir, general);

            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(compiler.compCurLifeTree, Is.SameAs(general ? address : indir));
        });
    }

    [Test]
    public static void CallsUpdateBothPhysicalLocalDefinitionsBeforeDuplicateSuppression()
    {
        WithCompiler((compiler, codeGen) =>
        {
            ConfigurePromotedFields(compiler, codeGen);
            compiler.lvaTable[1].RegNum = REG_STK;
            compiler.lvaTable[2].RegNum = REG_STK;
            compiler.lvaTable[1].lvLRACandidate = false;
            compiler.lvaTable[2].lvLRACandidate = false;
#if DEBUG
            compiler.lvaTable[1].IsDefinedViaAddress = true;
            compiler.lvaTable[2].IsDefinedViaAddress = true;
#endif
            var resumed = compiler.gtNewLclAddrNode(TYP_BYREF, 1, 0);
            var retbuf = compiler.gtNewLclAddrNode(TYP_BYREF, 2, 0);
            resumed.Flags |= GTF_VAR_DEF;
            retbuf.Flags |= GTF_VAR_DEF;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.SetIsAsync(default);
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(resumed).WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(retbuf).WithWellKnownArg(WellKnownArg.RetBuffer));

            new TreeLifeUpdater(compiler, forCodeGen: true).UpdateLife(call);

            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.True);
            Assert.That(compiler.compCurLifeTree, Is.SameAs(call));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PromotedLocalDeathsUpdateTheSelectedMemoryOrRegisterFields(bool multiReg)
    {
        WithCompiler((compiler, codeGen) =>
        {
            ConfigurePromotedFields(compiler, codeGen);
            if (!multiReg)
            {
                compiler.lvaTable[1].RegNum = REG_STK;
                compiler.lvaTable[2].RegNum = REG_STK;
            }
            var tree = compiler.gtNewLclvNode(TYP_STRUCT, 0).AsLclVar();
            tree.Flags |= GTF_VAR_DEF;
            if (multiReg)
            {
                tree.SetMultiReg();
                tree.SetRegNumByIdx(REG_RAX, 0);
                tree.SetRegNumByIdx(REG_RCX, 1);
            }
            var updater = new TreeLifeUpdater(compiler, forCodeGen: true);
            updater.UpdateLife(tree);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.True);

            compiler.compCurLifeTree = null;
            tree.Flags &= ~GTF_VAR_DEF;
            tree.SetLastUse(0, true);
            if (multiReg)
            {
                tree.SetLastUse(1, true);
            }
            updater.UpdateLife(tree);

            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.EqualTo(!multiReg));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(1)[0].m_EndEmitLocation.Valid(), Is.True);
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(2)[0].m_EndEmitLocation.Valid(),
                Is.EqualTo(multiReg));
        });
    }

    [Test]
    public static void FieldUpdatesSignalOnlyTheSelectedSpillAndRetainOtherFields()
    {
        WithCompiler((compiler, codeGen) =>
        {
            ConfigurePromotedFields(compiler, codeGen);
            var tree = compiler.gtNewLclvNode(TYP_STRUCT, 0).AsLclVar();
            tree.SetMultiReg();
            tree.SetRegNumByIdx(REG_RAX, 0);
            tree.SetRegNumByIdx(REG_RCX, 1);
            tree.Flags |= GTF_VAR_DEF;
            var updater = new TreeLifeUpdater(compiler, forCodeGen: true);
            Assert.That(updater.UpdateLifeFieldVar(tree, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.False);
            Assert.That(updater.UpdateLifeFieldVar(tree, 1), Is.False);

            tree.Flags &= ~GTF_VAR_DEF;
            tree.SetLastUse(0, true);
            Assert.That(updater.UpdateLifeFieldVar(tree, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.True);

            tree.SetLastUse(0, false);
            tree.Flags |= GTF_SPILL;
            tree.SetRegSpillFlagByIdx(GTF_SPILL, 1);
            Assert.That(updater.UpdateLifeFieldVar(tree, 0), Is.False);
            Assert.That(updater.UpdateLifeFieldVar(tree, 1), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 1), Is.True);
            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
        });
    }

#if DEBUG
    [Test]
    public static void DiagnosticsReportIndependentSnapshotsOfLocalAndGcLiveness()
    {
        WithCompiler((compiler, _) =>
        {
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree.Flags |= GTF_VAR_DEF;
            compiler.verbose = true;
            compiler.opts.compDbgInfo = false;
            var updater = new TreeLifeUpdater(compiler, forCodeGen: true);
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                updater.UpdateLife(tree);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }
            var indent = new string('\t', 7);
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(
                $"{indent}Live vars after [{tree.TreeId:D6}]: {{}} +{{V00}} => {{V00}}{Environment.NewLine}" +
                $"{indent}GC vars after [{tree.TreeId:D6}]: {{}} => {{V00}}{Environment.NewLine}"));
        });
    }
#endif

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.opts.compDbgInfo = true;
            compiler.lvaTable[0].RegNum = REG_STK;
            compiler.lvaTable[0].lvLRACandidate = false;
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            codeGen.RegSet.ClearMaskVars();
            codeGen.initializeVariableLiveKeeper();
            action(compiler, codeGen);
        });
    }

    private static void ConfigurePromotedFields(Compiler compiler, CodeGen codeGen)
    {
        compiler.lvaCount = 3;
        compiler.info.compLocalsCount = 3;
        compiler.lvaTrackedCount = 2;
        compiler.lvaTrackedToVarNum = [1, 2];
        compiler.lvaEnregMultiRegVars = true;
        compiler.lvaTable = [
            new() { Type = TYP_STRUCT, lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
            new()
            {
                Type = TYP_INT, lvIsStructField = true, lvTracked = true, lvLRACandidate = true,
                RegNum = REG_RAX, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -16, _varIndex = 0,
            },
            new()
            {
                Type = TYP_REF, lvIsStructField = true, lvTracked = true, lvLRACandidate = true,
                RegNum = REG_RCX, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -8, _varIndex = 1,
            },
        ];
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
        VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 1);
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        codeGen.initializeVariableLiveKeeper();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);
}
