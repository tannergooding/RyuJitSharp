// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64CodeGenLocalVariableTests
{
    [TestCase(TYP_INT, EA_4BYTE)]
    [TestCase(TYP_LONG, EA_8BYTE)]
    public static void StackLocalLoadsUseTheNodeTypeAndLocalFrameAddress(var_types type, emitAttr size)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = type;
            var tree = new GenTreeLclVar(type, 0)
            {
                RegNum = REG_R3,
            };

            codeGen.genCodeForLclVar(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var descriptor = descriptors[0];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_FPBASE));
            Assert.That(descriptor.idIsLclVar(), Is.True);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
        });
    }

    [TestCase(true, GTF_EMPTY)]
    [TestCase(false, GTF_SPILLED)]
    [TestCase(false, GTF_VAR_MULTIREG)]
    public static void AllocatorManagedAndDeferredLocalsAreNotLoadedAgain(bool isRegCandidate, GenTreeFlags flags)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvLRACandidate = isRegCandidate;
            var tree = new GenTreeLclVar(TYP_INT, 0)
            {
                RegNum = REG_R3,
                Flags = flags,
            };

            codeGen.genCodeForLclVar(tree);

            Assert.That(Descriptors(codeGen.Emitter), Is.Empty);
        });
    }

    [Test]
    public static void StackFieldStoresUseTheSourceRegisterAndFieldOffset()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_LONG;
            var source = compiler.gtNewIconNode(TYP_INT, 7);
            source.RegNum = REG_R3;
            var tree = compiler.gtNewStoreLclFldNode(TYP_INT, 0, 4, source);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclFld(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var descriptor = descriptors[0];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_FPBASE));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void ContainedZeroFieldStoresUseTheZeroRegister()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_LONG;
            var source = compiler.gtNewIconNode(TYP_INT, 0);
            source.IsContained = true;
            var tree = compiler.gtNewStoreLclFldNode(TYP_INT, 0, 4, source);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclFld(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ZR));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));
        });
    }

    [Test]
    public static void ContainedBitcastFieldStoresUseTheUncontainedSourceRegister()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            var source = compiler.gtNewIconNode(TYP_INT, 7);
            source.RegNum = REG_R3;
            var bitcast = new GenTreeUnOp(GT_BITCAST, TYP_FLOAT, source)
            {
                IsContained = true,
            };
            var tree = compiler.gtNewStoreLclFldNode(TYP_FLOAT, 0, 4, bitcast);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclFld(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));
        });
    }

    [Test]
    public static void StackLocalStoresUseTheSourceRegisterAndUpdateTheLocalHome()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            var source = compiler.gtNewIconNode(TYP_INT, 7);
            source.RegNum = REG_R3;
            var tree = compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclVar(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void RegisterLocalStoresUseTheAssignedRegisterAndExtendIntegerValues()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.lvaTable[0].RegNum = REG_R4;
            var source = compiler.gtNewIconNode(TYP_INT, 7);
            source.RegNum = REG_R3;
            var tree = compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = REG_R4;

            codeGen.genCodeForStoreLclVar(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R4));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R3));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_R4));
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void ContainedSimd16LocalStoresUsePairOfZeroRegisters()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var source = new GenTreeVecCon(TYP_SIMD16)
            {
                IsContained = true,
            };
            var tree = compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclVar(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_stp));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ZR));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_FP));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void ContainedSimdRegisterLocalStoresUseMoveImmediate()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.lvaTable[0].RegNum = REG_V0;
            var source = new GenTreeVecCon(TYP_SIMD16)
            {
                IsContained = true,
            };
            var tree = compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = REG_V0;

            codeGen.genCodeForStoreLclVar(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movi));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V0));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_V0));
        });
    }

    [Test]
    public static void ContainedSimd12FieldStoresWriteTwoZeroChunks()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var source = new GenTreeVecCon(TYP_SIMD12)
            {
                IsContained = true,
            };
            var tree = compiler.gtNewStoreLclFldNode(TYP_SIMD12, 0, 4, source);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclFld(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ZR));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_ZR));
            Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void Simd12FieldRegisterStoresCopyToTheAssignedRegister()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var source = new GenTreeVecCon(TYP_SIMD12)
            {
                RegNum = REG_V1,
            };
            source.SimdVal.u32[0] = 1;
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.lvaTable[0].RegNum = REG_V0;
            var tree = compiler.gtNewStoreLclFldNode(TYP_SIMD12, 0, 4, source);
            tree.RegNum = REG_V0;

            codeGen.genCodeForStoreLclFld(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_V1));
        });
    }

    [Test]
    public static void Simd12FieldStackStoresRotateWhenNoTemporaryRegisterIsAvailable()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var source = new GenTreeVecCon(TYP_SIMD12)
            {
                RegNum = REG_V1,
            };
            source.SimdVal.u32[0] = 1;
            var tree = compiler.gtNewStoreLclFldNode(TYP_SIMD12, 0, 4, source);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclFld(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(4));

            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));

            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_ext));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[1].idReg3(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[1].idSmallCns(), Is.EqualTo(8));

            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[2].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[2].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));

            Assert.That(descriptors[3].idIns(), Is.EqualTo(INS_ext));
            Assert.That(descriptors[3].idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(descriptors[3].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[3].idReg2(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[3].idReg3(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[3].idSmallCns(), Is.EqualTo(8));
        });
    }

    [Test]
    public static void Simd12FieldStackStoresUseTemporaryRegisterWhenAssigned()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var source = new GenTreeVecCon(TYP_SIMD12)
            {
                RegNum = REG_V1,
            };
            source.SimdVal.u32[0] = 1;
            var tree = compiler.gtNewStoreLclFldNode(TYP_SIMD12, 0, 4, source);
            tree.RegNum = REG_NA;
            codeGen.InternalRegisters.Add(tree, RBM_R10);

            codeGen.genCodeForStoreLclFld(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(3));

            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));

            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R10));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[1].idSmallCns(), Is.EqualTo(2));

            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[2].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_R10));
            Assert.That(descriptors[2].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
            Assert.That(codeGen.InternalRegisters.Count(tree), Is.Zero);
        });
    }
#endif

    internal static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.eeInfoInitialized = true;
        compiler.lvaCount = 1;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_INT,
                lvOnFrame = true,
                lvFramePointerBased = true,
                StackOffset = -16,
            },
        ];
        compiler.lvaDoneFrameLayout = Compiler.REGALLOC_FRAME_LAYOUT;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.IsFramePointerRequired = true;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    internal static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    internal static List<Emitter.instrDesc> AllDescriptors(CodeGen codeGen)
    {
        var emitter = codeGen.Emitter;
        var descriptors = new List<Emitter.instrDesc>();
        var currentGroup = emitter.emitCurIG;

        for (var group = FirstGroup(emitter); group is not null; group = group.igNext)
        {
            if (group == currentGroup)
            {
                descriptors.AddRange(Descriptors(emitter));
            }
            else if (group.igInsCnt > 0)
            {
                descriptors.AddRange(group.igData
                    ?? throw new AssertionException("Missing saved descriptor buffer."));
            }
        }

        return descriptors;
    }

    internal static void SaveCurrentGroup(Emitter emitter)
    {
        SaveGroup(emitter, extend: false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNxtIG")]
    private static extern void SaveGroup(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
