// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenInitializationTests
{
    [TestCase(TYP_REF, true, false, false, false, true)]
    [TestCase(TYP_BYREF, true, false, false, false, true)]
    [TestCase(TYP_INT, true, false, false, false, false)]
    [TestCase(TYP_REF, false, false, false, false, false)]
    [TestCase(TYP_REF, true, true, false, false, false)]
    [TestCase(TYP_REF, true, false, true, false, false)]
    [TestCase(TYP_REF, true, false, true, true, true)]
    public static void PreparationClassifiesOnlyTrackedGcLocalsWithStackHomes(
        var_types type, bool tracked, bool fullRegister, bool parameter, bool registerArgument, bool expected)
    {
        CodeGenSpillVariableTests.WithCompiler(type, REG_RAX, (compiler, codeGen, _) =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.lvTracked = tracked;
            local.lvLRACandidate = tracked;
            local.lvRegister = fullRegister;
            local.lvIsParam = parameter;
            local.lvIsRegArg = registerArgument;
            compiler.fgBBcount = 7;
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            LastLiveMask(codeGen) = regMaskTP.CreateFromRegNum(REG_RAX, REG_RAX.SingleTypeMask);

            codeGen.genPrepForCompiler();

            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0), Is.EqualTo(expected));
            Assert.That(VarSetOps.IsEmpty(compiler, LastLiveSet(codeGen)), Is.True);
            Assert.That(LastLiveMask(codeGen).IsEmpty, Is.True);
            Assert.That(compiler.Metrics.BasicBlocksAtCodegen, Is.EqualTo(7));
        });
    }

    [TestCase(true, true, true, false, REG_RAX, true)]
    [TestCase(false, true, true, false, REG_RAX, false)]
    [TestCase(true, false, true, false, REG_RAX, false)]
    [TestCase(true, true, false, false, REG_RAX, false)]
    [TestCase(true, true, true, true, REG_RAX, false)]
    [TestCase(true, true, true, false, REG_XMM0, false)]
    public static void RegisterInitializationMarksOnlyEligibleLiveIntegerParameters(
        bool parameter, bool fullRegister, bool live, bool addressExposed, regNumber reg, bool expected)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, reg, (compiler, codeGen, _) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
#if SWIFT_SUPPORT
            compiler.lvaSwiftErrorArg = BAD_VAR_NUM;
#endif
            codeGen.RegSet.rsClearRegsModified();
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            ref var local = ref compiler.lvaTable[0];
            local.lvIsParam = parameter;
            local.lvRegister = fullRegister;
            local.SetAddressExposed(addressExposed, AddressExposedReason.NONE);
            if (live)
            {
                VarSetOps.AddElemD(compiler, compiler.fgFirstBB.bbLiveIn, 0);
            }

            codeGen.genInitializeRegisterState();

            var mask = expected ? regMaskTP.CreateFromRegNum(REG_RAX, REG_RAX.SingleTypeMask) : default;
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(mask));
        });
    }

    [TestCase(false, 1)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    public static void InitializationPreservesTheNativeScopePredicates(bool scopeInfo, int scopeCount)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.opts.compScopeInfo = scopeInfo;
            compiler.info.compVarScopesCount = scopeCount;
            compiler.compNextEnterScopeIndex = 3;
            compiler.compNextExitScopeIndex = 4;
            InFuncletRegion(codeGen) = true;
            LastEndOffset(codeGen) = 42;

            codeGen.genInitialize();

            Assert.That(InFuncletRegion(codeGen), Is.EqualTo(!scopeInfo || (scopeCount == 0)));
            Assert.That(LastEndOffset(codeGen), Is.EqualTo(scopeInfo ? 0 : 42));
            Assert.That(compiler.compNextEnterScopeIndex, Is.EqualTo(scopeInfo && (scopeCount != 0) ? 0 : 3));
            Assert.That(compiler.compNextExitScopeIndex, Is.EqualTo(scopeInfo && (scopeCount != 0) ? 0 : 4));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InitializationResetsPointerListsForTheSelectedReportingMode(bool fullMap)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            codeGen.IsFullPtrRegMapRequired = fullMap;
            object state = codeGen.GCInfo;
            var variable = new GCInfo.varPtrDsc();
            var register = new GCInfo.regPtrDsc();
            var callType = typeof(GCInfo).GetNestedType("CallDsc", BindingFlags.NonPublic) ??
                throw new AssertionException("Missing call descriptor type.");
            var call = Activator.CreateInstance(callType, nonPublic: true) ??
                throw new AssertionException("Could not create a call descriptor.");
            Field(typeof(GCInfo), "gcVarPtrList").SetValue(state, variable);
            Field(typeof(GCInfo), "gcVarPtrLast").SetValue(state, variable);
            Field(typeof(GCInfo), "gcRegPtrList").SetValue(state, register);
            Field(typeof(GCInfo), "gcRegPtrLast").SetValue(state, register);
            Field(typeof(GCInfo), "gcCallDescList").SetValue(state, call);
            Field(typeof(GCInfo), "gcCallDescLast").SetValue(state, call);
            codeGen.GCInfo = (GCInfo)state;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, TYP_BYREF);
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0);
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            var oldLife = codeGen.GCInfo.gcVarPtrSetCur;

            codeGen.genInitialize();
            state = codeGen.GCInfo;

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            Assert.That(VarSetOps.IsEmpty(compiler, codeGen.GCInfo.gcVarPtrSetCur), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, oldLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0), Is.True);
            Assert.That(Field(typeof(GCInfo), "gcVarPtrList").GetValue(state), Is.Null);
            Assert.That(Field(typeof(GCInfo), "gcVarPtrLast").GetValue(state), Is.Null);
            Assert.That(Field(typeof(GCInfo), "gcRegPtrList").GetValue(state), Is.SameAs(fullMap ? null : register));
            Assert.That(Field(typeof(GCInfo), "gcRegPtrLast").GetValue(state), Is.SameAs(fullMap ? null : register));
            Assert.That(Field(typeof(GCInfo), "gcCallDescList").GetValue(state), Is.SameAs(fullMap ? call : null));
            Assert.That(Field(typeof(GCInfo), "gcCallDescLast").GetValue(state), Is.SameAs(fullMap ? call : null));
        });
    }

    [Test]
    public static void PreparationAndInitializationWireTheRealTreeLifeEntryPoint()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.lvaTable[0].lvLRACandidate = false;
            compiler.lvaTable[0].RegNum = REG_STK;
            compiler.opts.compDbgInfo = true;
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
            var oldLife = compiler.compCurLife;
            StackLevel(codeGen) = 16;

            codeGen.genPrepForCompiler();
            codeGen.genInitialize();

            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, oldLife, 0), Is.True);
            Assert.That(codeGen.getCurrentStackLevel(), Is.Zero);
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree.Flags |= GTF_VAR_DEF;
            codeGen.genUpdateLife(tree);

            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.True);
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void InitializationReplacesCallReturnStorageAndPendingLabels()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (_, codeGen, _) =>
        {
            codeGen.genInitialize();
            var returnsField = Field(typeof(CodeGen), "emittedCallReturnInfo");
            var first = returnsField.GetValue(codeGen) as IList ??
                throw new AssertionException("Call-return storage was not initialized.");
            var elementType = typeof(CodeGen).GetNestedType("EmittedCallReturnInfo", BindingFlags.NonPublic) ??
                throw new AssertionException("Missing call-return descriptor type.");
            _ = first.Add(Activator.CreateInstance(elementType));
            PendingCallLabel(codeGen) = new BasicBlock(null, null);

            codeGen.genInitialize();

            var second = returnsField.GetValue(codeGen) as IList ??
                throw new AssertionException("Call-return storage was not reinitialized.");
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second, Is.Empty);
            Assert.That(PendingCallLabel(codeGen), Is.Null);
        });
    }

    private static FieldInfo Field([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        string name)
    {
        return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertionException($"Missing field {type.Name}.{name}.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genLastLiveSet")]
    private static extern ref nint[] LastLiveSet(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genLastLiveMask")]
    private static extern ref regMaskTP LastLiveMask(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "siInFuncletRegion")]
    private static extern ref bool InFuncletRegion(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "siLastEndOffs")]
    private static extern ref int LastEndOffset(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genPendingCallLabel")]
    private static extern ref BasicBlock? PendingCallLabel(CodeGen codeGen);
}
