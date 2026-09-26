// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GSSecurityPhaseTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ShadowCandidateFollowsTargetParameterAbi(bool isParam, bool isRegArg)
    {
        var descriptor = new LclVarDsc
        {
            lvIsParam = isParam,
            lvIsRegArg = isRegArg,
        };

#if WINDOWS_AMD64_ABI
        Assert.That(Compiler.ShadowParamVarInfo.MayNeedShadowCopy(in descriptor), Is.EqualTo(isParam));
#else
        Assert.That(Compiler.ShadowParamVarInfo.MayNeedShadowCopy(in descriptor), Is.EqualTo(isParam && !isRegArg));
#endif
    }

    [Test]
    public static void PhaseWithoutCookieDoesNotCreateLocal()
    {
        WithCompiler(1, compiler => {
            Assert.That(compiler.gsPhase(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(compiler.gsShadowVarInfo, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CookiePhaseUsesEEValueOrAddressAndAllocatesImplicitLocal(bool indirect)
    {
        WithCompiler(1, compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getGSCookie = &GetGSCookie;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            s_indirect = indirect;
            compiler.NeedsGSSecurityCookie = true;
            compiler.info.compIsVarArgs = true;

            Assert.That(compiler.gsPhase(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaGSSecurityCookie, Is.EqualTo(1));
            Assert.That(compiler.lvaGetDesc(1).Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(compiler.lvaGetDesc(1).lvImplicitlyReferenced, Is.True);
            Assert.That(compiler.lvaGetDesc(1).IsAddressExposed, Is.True);
            Assert.That(compiler.gsGlobalSecurityCookieVal, Is.EqualTo((nint)0x12345678));
            Assert.That((nint)compiler.gsGlobalSecurityCookieAddr, Is.EqualTo(indirect ? (nint)0x4321 : 0));
            Assert.That(compiler.gsShadowVarInfo, Is.Null);
        });
    }

    [Test]
    public static void DependentLocalsExcludeIndirectionsCallsAndSelectConditions()
    {
        WithCompiler(5, compiler => {
            var condition = compiler.gtNewLclvNode(TYP_INT, 0);
            var trueValue = compiler.gtNewLclvNode(TYP_INT, 1);
            var address = compiler.gtNewLclvNode(TYP_BYREF, 2);
            var read = compiler.gtNewIndir(TYP_INT, address);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, trueValue, read);
            var visited = new System.Collections.Generic.List<int>();

            compiler.gsVisitDependentLocals(select, visited.Add);

            Assert.That(visited, Has.Count.EqualTo(1));
            Assert.That(visited[0], Is.EqualTo(1));
        });
    }

    [Test]
    public static void AssignmentDependenciesPropagatePointerToParameterAndKeepOtherInputsSeparate()
    {
        WithCompiler(4, compiler => {
            compiler.lvaGetDesc(0).lvIsParam = true;
            compiler.lvaGetDesc(0).Type = TYP_BYREF;
            compiler.lvaGetDesc(1).Type = TYP_BYREF;
            compiler.lvaGetDesc(2).Type = TYP_BYREF;
            var block = Block(compiler);
            block.InsertAtEnd(compiler.gtNewLclvNode(TYP_BYREF, 0));
            var alias1 = compiler.gtNewStoreLclVarNode(1, block.LastNode!);
            block.InsertAtEnd(alias1);
            var alias2Source = compiler.gtNewLclvNode(TYP_BYREF, 1);
            block.InsertAtEnd(alias2Source);
            block.InsertAtEnd(compiler.gtNewStoreLclVarNode(2, alias2Source));
            var address = compiler.gtNewLclvNode(TYP_BYREF, 2);
            block.InsertAtEnd(address);
            block.InsertAtEnd(new GenTreeIndir(GT_IND, TYP_INT, address));
            compiler.gsShadowVarInfoCount = compiler.lvaCount;
            compiler.gsShadowVarInfo = MakeShadowInfo(compiler.lvaCount);

            Assert.That(compiler.gsFindVulnerableParams(), Is.True);
            Assert.That(compiler.lvaGetDesc(0).lvIsPtr, Is.True);
            Assert.That(compiler.lvaGetDesc(1).lvIsPtr, Is.True);
            Assert.That(compiler.lvaGetDesc(2).lvIsPtr, Is.True);
            Assert.That(compiler.lvaGetDesc(3).lvIsPtr, Is.False);
        });
    }

    [TestCase(TYP_BYTE, TYP_INT)]
    [TestCase(TYP_BYREF, TYP_BYREF)]
    public static void VulnerableParameterIsRewrittenAndCopiedBeforeExistingLir(var_types type, var_types shadowType)
    {
        WithCompiler(2, compiler => {
            ref var descriptor = ref compiler.lvaGetDesc(0);
            descriptor.lvIsParam = true;
            descriptor.lvIsPtr = true;
            descriptor.Type = type;
            var block = Block(compiler);
            var original = compiler.gtNewLclvNode(type, 0);
            block.InsertAtEnd(original);
            compiler.gsShadowVarInfoCount = compiler.lvaCount;
            compiler.gsShadowVarInfo = MakeShadowInfo(compiler.lvaCount);

            compiler.gsParamsToShadows();

            var shadow = compiler.gsShadowVarInfo[0].ShadowCopy;
            Assert.That(shadow, Is.EqualTo(2));
            Assert.That(compiler.lvaGetDesc(shadow).Type, Is.EqualTo(shadowType));
            Assert.That(original.LclNum, Is.EqualTo(shadow));
            Assert.That(original.Type, Is.EqualTo(shadowType));
            Assert.That(block.FirstNode!.AsLclVar().LclNum, Is.Zero);
            Assert.That(block.FirstNode.Next!.AsLclVar().LclNum, Is.EqualTo(shadow));
            Assert.That(block.LastNode, Is.SameAs(original));
        });
    }

    [Test]
    public static void NoVulnerableLocalClearsShadowTable()
    {
        WithCompiler(1, compiler => {
            _ = Block(compiler);
            compiler.gsCopyShadowParams();

            Assert.That(compiler.gsShadowVarInfo, Is.Null);
            Assert.That(compiler.gsShadowVarInfoCount, Is.Zero);
        });
    }

    [Test]
    public static void ShadowCopiesRestoreOriginalArgumentsBeforeEveryJmp()
    {
        WithCompiler(2, compiler => {
            compiler.lvaGetDesc(0).lvIsParam = true;
            compiler.lvaGetDesc(0).lvIsPtr = true;
            compiler.lvaGetDesc(1).lvIsParam = true;
            compiler.lvaGetDesc(1).lvIsUnsafeBuffer = true;
            compiler.info.compArgsCount = 2;
            compiler.compJmpOpUsed = true;
            var first = Block(compiler);
            var second = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            second.MakeLir(null, null);
            first.Next = second;
            second.Prev = first;
            compiler.fgLastBB = second;
            var firstJump = new GenTreeVal(GT_JMP, TYP_VOID, 0);
            var secondJump = new GenTreeVal(GT_JMP, TYP_VOID, 0);
            first.InsertAtEnd(firstJump);
            second.InsertAtEnd(secondJump);
            compiler.gsShadowVarInfoCount = compiler.lvaCount;
            compiler.gsShadowVarInfo = MakeShadowInfo(compiler.lvaCount);

            compiler.gsParamsToShadows();

            GenTreeVal[] jumps = [firstJump, secondJump];
            foreach (var jump in jumps)
            {
                var firstStore = jump.Prev!.AsLclVar();
                var secondStore = firstStore.Data;
                Assert.That(firstStore.LclNum, Is.EqualTo(1));
                Assert.That(secondStore.AsLclVar().LclNum, Is.EqualTo(compiler.gsShadowVarInfo[1].ShadowCopy));
                var priorStore = firstStore.Prev!.Prev!.AsLclVar();
                Assert.That(priorStore.LclNum, Is.EqualTo(0));
                Assert.That(priorStore.Data.AsLclVar().LclNum, Is.EqualTo(compiler.gsShadowVarInfo[0].ShadowCopy));
            }
        });
    }

    [Test]
    public static void StructParameterShadowRetainsLayoutAndDescriptorFlags()
    {
        WithCompiler(1, compiler => {
            ref var original = ref compiler.lvaGetDesc(0);
            original.Type = TYP_STRUCT;
            original.Layout = new ClassLayout(24);
            original.lvIsParam = true;
            original.lvIsUnsafeBuffer = true;
            original.lvDoNotEnregister = true;
            original.IsNeverNegative = true;
            _ = Block(compiler);
            compiler.gsShadowVarInfoCount = 1;
            compiler.gsShadowVarInfo = MakeShadowInfo(1);

            Assert.That(compiler.gsCreateShadowingLocals(), Is.True);

            ref var shadow = ref compiler.lvaGetDesc(compiler.gsShadowVarInfo[0].ShadowCopy);
            Assert.That(shadow.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(shadow.Layout, Is.SameAs(compiler.lvaGetDesc(0).Layout));
            Assert.That(shadow.lvIsUnsafeBuffer, Is.True);
            Assert.That(shadow.lvDoNotEnregister, Is.True);
            Assert.That(shadow.IsNeverNegative, Is.True);
        });
    }

    private static bool s_indirect;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetGSCookie(ICorJitInfo* _, nint* value, nint** address)
    {
        *value = 0x12345678;
        *address = s_indirect ? (nint*)0x4321 : null;
    }

    private static Compiler.ShadowParamVarInfo[] MakeShadowInfo(int count)
    {
        var result = new Compiler.ShadowParamVarInfo[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = new Compiler.ShadowParamVarInfo();
        }
        return result;
    }

    private static BasicBlock Block(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
        block.MakeLir(null, null);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        return block;
    }

    private static void WithCompiler(int count, Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(count, compiler => {
            compiler.fgNodeThreading = NodeThreading.LIR;
            action(compiler);
        });
    }
}
