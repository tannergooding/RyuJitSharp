// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenTestSupport
{
    internal static void WithCodeGen(var_types type, regNumber reg, Action<Compiler, CodeGen> action,
        bool minopts = true)
    {
        WithCompiler(type, reg, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            compiler.lvaTable[0].lvLRACandidate = false;
            codeGen.RegSet.ClearMaskVars();
            action(compiler, codeGen);
        }, minopts);
    }

    internal static void WithCompiler(var_types type, regNumber reg, Action<Compiler, CodeGen, GenTree> action,
        bool minopts = true)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
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
            codeGen.Emitter.emitBegFN(true);
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.UseEvexEncodings = true;
            var tree = compiler.gtNewLclvNode(type, 0);
            tree.RegNum = reg;
            tree.Flags |= GenTreeFlags.GTF_SPILL;
            action(compiler, codeGen, tree);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
