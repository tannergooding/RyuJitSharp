// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CallFoldingTests
{
    [Test]
    public static void BoxRemovalPreservesStatementIdentityAndMorphState(
        [Values(false, true)] bool morphed, [Values(false, true)] bool sourceEffects)
    {
        WithCompiler("Enum", "HasFlag", (compiler, _) =>
        {
            compiler.fgGlobalMorph = morphed;
            var address = compiler.gtNewIconNode(TYP_BYREF, 16);
            GenTree value = sourceEffects
                ? compiler.gtNewIndir(TYP_INT, address) : compiler.gtNewIconNode(TYP_INT, 42);
            var allocation = compiler.gtNewStoreLclVarNode(0, compiler.gtNewNull());
            var copy = compiler.gtNewStoreIndNode(TYP_INT, address, value);
            var allocationStatement = compiler.gtNewStmt(allocation);
            var copyStatement = compiler.gtNewStmt(copy);
            var box = new GenTreeBox(TYP_REF, compiler.gtNewLclvNode(TYP_REF, 0),
                allocationStatement, copyStatement);
            box.Flags |= GTF_BOX_VALUE;
            allocation._vnPair.SetBoth(42);
            copy._vnPair.SetBoth(43);
            if (morphed)
            {
                allocation.SetMorphed(compiler);
                copy.SetMorphed(compiler);
                value.SetMorphed(compiler);
            }

            var result = compiler.gtTryRemoveBoxUpstreamEffects(box);

            Assert.That(result, Is.SameAs(value));
            Assert.That(allocationStatement.RootNode, Is.SameAs(allocation));
            Assert.That(allocation.Oper, Is.EqualTo(GT_NOP));
            Assert.That(allocation.Flags & GTF_ALL_EFFECT, Is.EqualTo(GTF_EMPTY));
            Assert.That(allocation._vnPair.BothDefined, Is.False);
            Assert.That(copyStatement.RootNode, Is.SameAs(sourceEffects ? value : copy));
            if (!sourceEffects)
            {
                Assert.That(copy.Oper, Is.EqualTo(GT_NOP));
                Assert.That(copy.Flags & GTF_ALL_EFFECT, Is.EqualTo(GTF_EMPTY));
                Assert.That(copy._vnPair.BothDefined, Is.False);
            }
#if DEBUG
            Assert.That(allocation.WasMorphed, Is.EqualTo(morphed));
            Assert.That(copyStatement.RootNode.WasMorphed, Is.EqualTo(morphed));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BoxRemovalKeepsTheLiveOwnerOfAStructSource(bool narrow)
    {
        WithCompiler("Enum", "HasFlag", (compiler, _) =>
        {
            compiler.fgGlobalMorph = true;
            var layout = new ClassLayout(16);
            var address = compiler.gtNewIconNode(TYP_BYREF, 16);
            var source = compiler.gtNewBlkIndir(address, layout);
            source.Flags |= GTF_IND_VOLATILE | GTF_IND_UNALIGNED;
            source._vnPair.SetBoth(42);
            source.SetMorphed(compiler);
            var allocationStatement = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewNull()));
            var copyStatement = compiler.gtNewStmt(compiler.gtNewStoreBlkNode(address, source, layout));
            var box = new GenTreeBox(TYP_REF, compiler.gtNewLclvNode(TYP_REF, 0),
                allocationStatement, copyStatement);
            box.Flags |= GTF_BOX_VALUE;
            var options = narrow ? Compiler.BoxRemovalOptions.BR_REMOVE_AND_NARROW
                : Compiler.BoxRemovalOptions.BR_REMOVE_BUT_NOT_NARROW;

            var result = compiler.gtTryRemoveBoxUpstreamEffects(box, options)
                ?? throw new AssertionException("Box removal failed.");

            Assert.That(copyStatement.RootNode, Is.SameAs(result));
            Assert.That(result.Oper, Is.EqualTo(narrow ? GT_IND : GT_BLK));
            Assert.That(result.Type, Is.EqualTo(narrow ? TYP_BYTE : TYP_STRUCT));
            Assert.That(result.AsIndir().Addr, Is.SameAs(address));
            Assert.That(result.Flags, Is.EqualTo(source.Flags));
            Assert.That(result._vnPair.BothDefined, Is.EqualTo(!narrow));
#if DEBUG
            Assert.That(result.TreeId, Is.EqualTo(source.TreeId));
            Assert.That(result.WasMorphed, Is.True);
            Assert.That(result._morphCount, Is.EqualTo(source._morphCount));
#endif
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void IneligibleCallsDoNotQueryMetadata(bool special, bool minOpts)
    {
        WithCompiler("Type", "Other", (compiler, call) => {
            if (!special)
            {
                call._callMoreFlags &= ~GTF_CALL_M_SPECIAL_INTRINSIC;
            }

            Assert.That(compiler.gtFoldExprCall(call), Is.SameAs(call));
            Assert.That(((MethodMetadata*)call._callMethHnd)->Lookups, Is.Zero);
        }, minOpts);
    }

    [TestCase("op_Equality", GT_EQ)]
    [TestCase("op_Inequality", GT_NE)]
    public static void TypeEqualityUsesUserArgumentsAndReturnsANewComparison(string method, genTreeOps oper)
    {
        WithCompiler("Type", method, (compiler, call) => {
            var left = compiler.gtNewZeroConNode(TYP_REF);
            var right = compiler.gtNewLclvNode(TYP_REF, 0);
            right.Flags |= GTF_ORDER_SIDEEFF;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 123))
                .WithWellKnownArg(WellKnownArg.InstParam));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(left));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(right));

            var result = compiler.gtFoldExprCall(call);
            Assert.That(result, Is.Not.SameAs(call));
            Assert.That(result.Oper, Is.EqualTo(oper));
            Assert.That(result.Type, Is.EqualTo(TYP_INT));
            Assert.That(result.AsOp().Op1, Is.SameAs(left));
            Assert.That(result.AsOp().Op2, Is.SameAs(right));
            Assert.That(result.Flags & GTF_ORDER_SIDEEFF, Is.EqualTo(GTF_ORDER_SIDEEFF));
            Assert.That(call.Oper, Is.EqualTo(GT_CALL));
        });
    }

    [TestCase("Type", "op_Equality")]
    [TestCase("Type", "op_Inequality")]
    [TestCase("Enum", "HasFlag")]
    [TestCase("Type", "Other")]
    public static void UnsupportedOperandsOrIntrinsicsKeepTheCall(string className, string method)
    {
        WithCompiler(className, method, (compiler, call) => {
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0)));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 1)));
            Assert.That(compiler.gtFoldExprCall(call), Is.SameAs(call));
            Assert.That(((MethodMetadata*)call._callMethHnd)->Lookups, Is.EqualTo(1));
        });
    }

    [TestCase(CorInfoType.CORINFO_TYPE_BYTE, TYP_BYTE)]
    [TestCase(CorInfoType.CORINFO_TYPE_UBYTE, TYP_UBYTE)]
    [TestCase(CorInfoType.CORINFO_TYPE_INT, TYP_INT)]
    [TestCase(CorInfoType.CORINFO_TYPE_LONG, TYP_LONG)]
    public static void EnumEqualityUnboxesExactIntegralOperands(CorInfoType underlying, var_types expectedType)
    {
        WithCompiler("Enum", "Equals", (compiler, call) => {
            var enumType = underlying;
            compiler.lvaTable[0].lvClassHnd = (CORINFO_CLASS_STRUCT_*)&enumType;
            compiler.lvaTable[0].lvClassIsExact = true;
            compiler.lvaTable[1] = compiler.lvaTable[0];
            var left = compiler.gtNewLclvNode(TYP_REF, 0);
            var right = NewBoxedLocal(compiler, 1);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(left).WithWellKnownArg(WellKnownArg.ThisPointer));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(right));

            var result = compiler.gtFoldExprCall(call);
            Assert.That(result, Is.Not.SameAs(call));
            Assert.That(result.Oper, Is.EqualTo(GT_EQ));
            Assert.That(result.Type, Is.EqualTo(TYP_INT));
            var firstLoad = result.AsOp().Op1.AsIndir();
            var secondLoad = result.AsOp().Op2.AsIndir();
            Assert.That(firstLoad.Type, Is.EqualTo(expectedType));
            Assert.That(secondLoad.Type, Is.EqualTo(expectedType));
            Assert.That(firstLoad.Addr.AsOp().Op1, Is.SameAs(left));
            Assert.That(secondLoad.Addr.AsOp().Op1, Is.SameAs(right));
            Assert.That(firstLoad.Addr.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(secondLoad.Addr.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(firstLoad.Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)TARGET_POINTER_SIZE));
            Assert.That(secondLoad.Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)TARGET_POINTER_SIZE));
            Assert.That(firstLoad.Addr.AsOp().Op2, Is.Not.SameAs(secondLoad.Addr.AsOp().Op2));
            Assert.That(result.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EXCEPT));
            Assert.That(call.Oper, Is.EqualTo(GT_CALL));
        });
    }

    [TestCase(CorInfoType.CORINFO_TYPE_UNDEF, true, true)]
    [TestCase(CorInfoType.CORINFO_TYPE_FLOAT, true, true)]
    [TestCase(CorInfoType.CORINFO_TYPE_DOUBLE, true, true)]
    [TestCase(CorInfoType.CORINFO_TYPE_INT, false, true)]
    [TestCase(CorInfoType.CORINFO_TYPE_INT, true, false)]
    public static void EnumEqualityRejectsUnsupportedOrUnprovenTypes(CorInfoType underlying, bool sameClass, bool exact)
    {
        WithCompiler("Enum", "Equals", (compiler, call) => {
            var firstType = underlying;
            var secondType = underlying;
            compiler.lvaTable[0].lvClassHnd = (CORINFO_CLASS_STRUCT_*)&firstType;
            compiler.lvaTable[0].lvClassIsExact = exact;
            compiler.lvaTable[1].lvClassHnd = (CORINFO_CLASS_STRUCT_*)(sameClass ? &firstType : &secondType);
            compiler.lvaTable[1].lvClassIsExact = exact;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0)));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(NewBoxedLocal(compiler, 1)));
            Assert.That(compiler.gtFoldExprCall(call), Is.SameAs(call));
            Assert.That(((MethodMetadata*)call._callMethHnd)->Lookups, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EnumEqualityPreservesNullableArguments(bool nullConstant)
    {
        WithCompiler("Enum", "Equals", (compiler, call) =>
        {
            var enumType = CorInfoType.CORINFO_TYPE_INT;
            compiler.lvaTable[0].lvClassHnd = (CORINFO_CLASS_STRUCT_*)&enumType;
            compiler.lvaTable[0].lvClassIsExact = true;
            compiler.lvaTable[1] = compiler.lvaTable[0];
            var receiver = compiler.gtNewLclvNode(TYP_REF, 0);
            GenTree argument = nullConstant ? compiler.gtNewNull() : compiler.gtNewLclvNode(TYP_REF, 1);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(receiver).WithWellKnownArg(WellKnownArg.ThisPointer));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(argument));

            Assert.That(compiler.gtFoldExprCall(call), Is.SameAs(call));
            Assert.That(call.Args.GetUserArgByIndex(1)?.Node, Is.SameAs(argument));
        });
    }

    private static GenTreeBox NewBoxedLocal(Compiler compiler, int local)
    {
        return new GenTreeBox(TYP_REF, compiler.gtNewLclvNode(TYP_REF, local),
            compiler.gtNewStmt(compiler.gtNewNothingNode()), compiler.gtNewStmt(compiler.gtNewNothingNode()));
    }

    private static void WithCompiler(string className, string methodName, Action<Compiler, GenTreeCall> action, bool minOpts = false)
    {
        fixed (byte* classPointer = Encoding.UTF8.GetBytes(className + '\0'))
        fixed (byte* methodPointer = Encoding.UTF8.GetBytes(methodName + '\0'))
        fixed (byte* namespacePointer = "System\0"u8)
        {
            MethodMetadata metadata = new() { Class = classPointer, Method = methodPointer, Namespace = namespacePointer };
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getMethodNameFromMetadata = &GetMethodName;
            vtable.Base.Base.getTypeForPrimitiveValueClass = &GetUnderlyingType;
            vtable.Base.Base.isEnum = &IsEnum;
            vtable.Base.Base.getExactClasses = &GetExactClasses;
            vtable.Base.Base.isExactType =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, byte>)&IsExactType;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
            using var tls = new JitTls(&jitInfo);
#endif
            var previous = JitTls.Compiler;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compRetBuffArg = BAD_VAR_NUM };
            compiler.lvaTable = new LclVarDsc[2];
            compiler.lvaCount = 2;
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[1].Type = TYP_REF;
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            compiler.opts.SetMinOpts(minOpts);
            JitTls.Compiler = compiler;

            try
            {
                var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)&metadata);
                call._callMoreFlags |= GTF_CALL_M_SPECIAL_INTRINSIC;
                action(compiler, call);
            }
            finally
            {
                JitTls.Compiler = previous;
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetMethodName(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, byte** className,
        byte** namespaceName, byte** enclosingClasses, nint maxEnclosingClasses)
    {
        var metadata = (MethodMetadata*)method;
        metadata->Lookups++;
        *className = metadata->Class;
        *namespaceName = metadata->Namespace;

        return metadata->Method;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetUnderlyingType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => *(CorInfoType*)type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState IsEnum(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, CORINFO_CLASS_STRUCT_** underlying)
        => TypeCompareState.Must;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetExactClasses(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int count, CORINFO_CLASS_STRUCT_** classes)
        => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsExactType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 0;

    private struct MethodMetadata
    {
        public byte* Class;
        public byte* Method;
        public byte* Namespace;
        public int Lookups;
    }
}
