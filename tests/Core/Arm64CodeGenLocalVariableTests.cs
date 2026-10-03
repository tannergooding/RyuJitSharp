// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections.Generic;
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

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
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

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
