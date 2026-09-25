// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RegisterTrackingTests
{
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void LocalRegisterLifeGivesDeathPriorityOverBirth(bool born, bool dying)
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            compiler.lvaTable = [new() { Type = TYP_INT, lvTracked = true, lvLRACandidate = true, RegNum = REG_RAX }];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
#if DEBUG
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
#endif
            codeGen.RegSet.SetMaskVars(dying ? RBM_RAX | RBM_RBX : RBM_RBX);

            codeGen.genUpdateRegLife(in compiler.lvaTable[0], born, dying
#if DEBUG
                , tree
#endif
            );

            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(dying ? RBM_RBX : RBM_RAX | RBM_RBX));
        });
    }

    [Test]
    public static void AlwaysAliveMemoryLocalMayAlreadyOccupyItsRegisterAtBirth()
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            compiler.lvaTable = [
                new() { Type = TYP_REF, lvTracked = true, lvLRACandidate = true, RegNum = REG_RAX, lvSpillAtSingleDef = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
#if DEBUG
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
#endif
            codeGen.RegSet.SetMaskVars(RBM_RAX);

            codeGen.genUpdateRegLife(in compiler.lvaTable[0], true, false
#if DEBUG
                , tree
#endif
            );

            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(RBM_RAX));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ScalarLocalLocationUsesTheDefinitionOrCopyRegister(bool copy)
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            compiler.lvaTable = [new() { Type = TYP_INT, lvLRACandidate = true, RegNum = REG_STK }];
            compiler.lvaCount = 1;
            GenTree tree = compiler.gtNewLclvNode(TYP_INT, 0);
            if (copy)
            {
                tree = new GenTreeCopyOrReload(GT_COPY, TYP_INT, tree);
            }
            tree.RegNum = REG_R8;

            codeGen.genUpdateVarReg(ref compiler.lvaTable[0], tree);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_R8));
            Assert.That(codeGen.genGetRegMask(in compiler.lvaTable[0]), Is.EqualTo(RBM_R8));
        });
    }

    [Test]
    public static void PromotedLocalMaskOnlyIncludesRegisterResidentFields()
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            compiler.lvaEnregMultiRegVars = true;
            compiler.lvaTable = [
                new() { Type = TYP_STRUCT, lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new() { Type = TYP_INT, lvIsStructField = true, lvLRACandidate = true, RegNum = REG_RAX },
                new() { Type = TYP_DOUBLE, lvIsStructField = true, lvLRACandidate = true, RegNum = REG_STK },
            ];
            compiler.lvaCount = 3;
            var tree = compiler.gtNewLclvNode(TYP_STRUCT, 0).AsLclVar();
            tree.Flags |= GTF_VAR_MULTIREG;
            tree.SetRegNumByIdx(REG_XMM1, 1);
            Assert.That(codeGen.genGetRegMask(tree), Is.EqualTo(RBM_RAX));

            codeGen.genUpdateVarReg(ref compiler.lvaTable[2], tree, 1);

            Assert.That(compiler.lvaTable[2].RegNum, Is.EqualTo(REG_XMM1));
            Assert.That(codeGen.genGetRegMask(tree), Is.EqualTo(RBM_RAX | RBM_XMM1));
        });
    }

    [Test]
    public static void GcRegisterStatePreservesLiveVariablesAndReclassifiesReferences()
    {
        WithCompiler(minOpts: true, (_, codeGen) => {
            ref var gc = ref codeGen.GCInfo;
            ref var registers = ref codeGen.RegSet;
            var pointers = new regMaskTP(SRBM_RAX | SRBM_RBX);
            gc.gcMarkRegSetGCref(pointers);
            gc.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            registers.SetMaskVars(pointers);
            gc.gcMarkRegSetNpt(pointers);
            Assert.That(gc.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
            Assert.That(gc.gcRegByrefSetCur, Is.EqualTo(RBM_RAX));

            registers.RemoveMaskVars(RBM_RAX);
            gc.gcMarkRegPtrVal(REG_RAX, TYP_INT);
            Assert.That(gc.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(gc.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));

            registers.ClearMaskVars();
            gc.gcMarkRegSetNpt(pointers);
            Assert.That(gc.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void GcBlockResetDoesNotDiscardTrackedStackPointerClassification()
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            compiler.lvaTrackedCount = 2;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            ref var gc = ref codeGen.GCInfo;
            gc.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
            VarSetOps.AddElemD(compiler, gc.gcTrkStkPtrLcls, 0);
            gc.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            VarSetOps.AddElemD(compiler, gc.gcVarPtrSetCur, 1);
            gc.gcRegGCrefSetCur = RBM_RAX;
            gc.gcRegByrefSetCur = RBM_RBX;

            gc.gcResetForBB();

            Assert.That(gc.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(gc.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(VarSetOps.IsMember(compiler, gc.gcTrkStkPtrLcls, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, gc.gcVarPtrSetCur, 1), Is.False);
        });
    }

    [TestCase(REG_RAX, EA_1BYTE, "al")]
    [TestCase(REG_RAX, EA_2BYTE, "ax")]
    [TestCase(REG_RAX, EA_4BYTE, "eax")]
    [TestCase(REG_RAX, EA_GCREF, "rax")]
    [TestCase(REG_RSI, EA_1BYTE, "sil")]
    [TestCase(REG_R8, EA_1BYTE, "r8b")]
    [TestCase(REG_R15, EA_2BYTE, "r15w")]
    [TestCase(REG_R16, EA_4BYTE, "r16d")]
    [TestCase(REG_XMM0, EA_8BYTE, "xmm0")]
    [TestCase(REG_XMM15, EA_32BYTE, "ymm15")]
    [TestCase(REG_XMM31, EA_64BYTE, "zmm31")]
    [TestCase(REG_K1, EA_64BYTE, "k1")]
    public static void EmitterRegisterNamesMatchNativeSizeAndRegisterClass(
        regNumber reg, emitAttr attr, string expected)
    {
        WithCompiler(minOpts: true, (_, codeGen) =>
            Assert.That(codeGen.Emitter.emitRegName(reg, attr, varName: false), Is.EqualTo(expected)));
    }

#if DEBUG
    [Test]
    public static void GcRegisterDiagnosticsPreserveTransitionsAndForcedUnchangedOutput()
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            compiler.verbose = true;
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                codeGen.GCInfo.gcMarkRegSetGCref(RBM_RAX);
                codeGen.GCInfo.gcMarkRegSetByref(RBM_RAX);
                codeGen.GCInfo.gcMarkRegSetByref(RBM_RAX, forceOutput: true);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            var indent = new string('\t', 7);
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(
                $"{indent}GC regs: 0000 {{}} => 0001 {{rax}}{Environment.NewLine}" +
                $"{indent}GC regs: 0001 {{rax}} => 0000 {{}}{Environment.NewLine}" +
                $"{indent}Byref regs: 0000 {{}} => 0001 {{rax}}{Environment.NewLine}" +
                $"{indent}Byref regs: (unchanged) 0001 {{rax}}{Environment.NewLine}"));
        });
    }
#endif

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
#if FEATURE_SIMD
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
#endif
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_MASK)]
#endif
    public static void TypeSelectsTheNativeRegisterMaskBankWithoutFiltering(var_types type)
    {
        var mask = new regMaskTP(SRBM_RAX | SRBM_XMM6);
#if FEATURE_MASKED_HW_INTRINSICS
        mask |= regMaskTP.CreateFromRegNum(REG_K1, genSingleTypeRegMask(REG_K1));
#endif
        var expected = varTypeIsMask(type) ? mask.MskRegSet : mask.Lower;

        Assert.That(mask.GetRegSetForType(type), Is.EqualTo(expected));
    }

    [Test]
    public static void ModifiedMaskAccumulatesAndRemovingRegistersPreservesOthers()
    {
        WithCompiler(minOpts: true, (_, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();

            var modified = new regMaskTP(SRBM_RAX | SRBM_RBX | SRBM_XMM6);
            registers.rsSetRegsModified(modified);
            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX), suppressDump: true);

            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(modified));
            Assert.That(registers.rsGetModifiedCalleeSavedRegsMask(),
                Is.EqualTo(new regMaskTP(SRBM_RBX | SRBM_XMM6)));
            Assert.That(registers.rsGetModifiedIntCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
            Assert.That(registers.rsGetModifiedOsrIntCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
            Assert.That(registers.rsGetModifiedFltCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_XMM6)));
            Assert.That(registers.rsRegsModified(new regMaskTP(SRBM_RBX)), Is.True);
            Assert.That(registers.rsRegsModified(new regMaskTP(SRBM_RCX)), Is.False);

            registers.rsRemoveRegsModified(new regMaskTP(SRBM_RBX | SRBM_RCX));
            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RAX | SRBM_XMM6)));

            registers.rsClearRegsModified();
            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void VerifyRegisterUsedAddsRegisterToModifiedMask()
    {
        WithCompiler(minOpts: true, (_, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();

            registers.verifyRegUsed(REG_RBX);

            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void VerifyRegistersUsedSkipsMinOptsButMarksOptimizedRegisters(bool minOpts)
    {
        WithCompiler(minOpts, (_, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();

            registers.verifyRegistersUsed(RBM_NONE);
            registers.verifyRegistersUsed(new regMaskTP(SRBM_RBX | SRBM_RAX));

            Assert.That(registers.rsGetModifiedRegsMask(),
                Is.EqualTo(minOpts ? RBM_NONE : new regMaskTP(SRBM_RBX | SRBM_RAX)));
        });
    }

    [Test]
    public static void FinalFrameLayoutAllowsAlreadySavedAndCallerSavedRegisters()
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();
            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX));
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;

            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX | SRBM_RAX));
            registers.rsRemoveRegsModified(new regMaskTP(SRBM_RAX));

            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
        });
    }

    [TestCase(InsGroupFlags.Prolog)]
    [TestCase(InsGroupFlags.FuncletProlog)]
    [TestCase(InsGroupFlags.Epilog)]
    [TestCase(InsGroupFlags.FuncletEpilog)]
    public static void FinalFrameLayoutAllowsCalleeSavedChangesInPrologAndEpilogGroups(InsGroupFlags phase)
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            codeGen.Emitter.emitCurIG = new insGroup { igFlags = phase };

            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX));
            Assert.That(registers.rsGetModifiedCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));

            registers.rsRemoveRegsModified(new regMaskTP(SRBM_RBX));
            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(RBM_NONE));
        });
    }

    private static void WithCompiler(bool minOpts, Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
#if DEBUG
        JitTls.Compiler = compiler;
#endif
        try
        {
            action(compiler, codeGen);
        }
        finally
        {
#if DEBUG
            JitTls.Compiler = previous;
#endif
        }
    }
}
