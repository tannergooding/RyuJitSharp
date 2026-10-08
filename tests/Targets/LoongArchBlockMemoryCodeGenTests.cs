// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BarrierKind;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.Target.UnitTests;

internal static unsafe class LoongArchBlockMemoryCodeGenTests
{
    [Test]
    public static void LoopDispatchRecordsInstructions()
    {
        WithCodeGen((_, codeGen) => {
            var value = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, new GenTreeIntCon(TYP_INT, 0))
            {
                IsContained = true,
            };
            var node = new GenTreeBlk(TYP_STRUCT, Physical(REG_S0), value, new ClassLayout(8))
            {
                _kind = BlkOpKindLoop,
            };
            codeGen.InternalRegisters.Add(node, genRegMask(REG_S1));

            codeGen.genCodeForStoreBlk(node);

            Assert.That(CurrentGroupInstructionCount(codeGen.Emitter), Is.GreaterThan(0));
        });
    }

    [TestCase(1, false)]
    [TestCase(7, false)]
    [TestCase(16, false)]
    [TestCase(17, false)]
    [TestCase(16, true)]
    public static void CopyDispatchRecordsInstructions(int size, bool isVolatile)
    {
        WithCodeGen((_, codeGen) => {
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, Physical(REG_S1))
            {
                IsContained = true,
            };
            var node = new GenTreeBlk(TYP_STRUCT, Physical(REG_S0), source, new ClassLayout((uint)size))
            {
                _kind = BlkOpKindUnroll,
            };
            if (isVolatile)
            {
                node.Flags |= GTF_IND_VOLATILE;
            }

            codeGen.InternalRegisters.Add(node, genRegMask(REG_S2));
            codeGen.genCodeForStoreBlk(node);

            Assert.That(CurrentGroupInstructionCount(codeGen.Emitter), Is.GreaterThan(0));
        });
    }

    [TestCase(BARRIER_FULL)]
    [TestCase(BARRIER_LOAD_ONLY)]
    [TestCase(BARRIER_STORE_ONLY)]
    public static void EveryBarrierKindRecordsAnInstruction(BarrierKind kind)
    {
        WithCodeGen((_, codeGen) => {
            codeGen.instGen_MemoryBarrier(kind);

            Assert.That(CurrentGroupInstructionCount(codeGen.Emitter), Is.GreaterThan(0));
        });
    }

    private static GenTreePhysReg Physical(regNumber reg)
    {
        return new GenTreePhysReg(reg, TYP_BYREF) { RegNum = reg };
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
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.GCInfo.gcRegPtrSetInit();
            codeGen.GCInfo.gcVarPtrSetInit();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentGroupInstructionCount(Emitter emitter);
}
#endif
