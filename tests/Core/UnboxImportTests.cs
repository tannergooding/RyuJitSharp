// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class UnboxImportTests
{
    [TestCase(OPCODE.CEE_UNBOX, false, false)]
    [TestCase(OPCODE.CEE_UNBOX, true, false)]
    [TestCase(OPCODE.CEE_UNBOX, false, true)]
    [TestCase(OPCODE.CEE_UNBOX_ANY, false, false)]
    [TestCase(OPCODE.CEE_UNBOX_ANY, true, false)]
    public static void KnownTypeUnboxBuildsAddressAfterCloning(OPCODE opcode, bool effectful, bool nonNull)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.resolveToken = &ResolveToken;
        vtable.Base.embedGenericHandle =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, byte, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)&EmbedHandle;
        vtable.Base.Base.canAccessClass = &CanAccessClass;
        vtable.Base.Base.isValueClass = &IsValueClass;
        vtable.Base.Base.getUnBoxHelper = &GetUnboxHelper;
        vtable.Base.Base.compareTypesForEquality = &Compare;
        vtable.Base.Base.getObjectType = &GetObjectType;
        vtable.Base.Base.asCorInfoType = &AsCorInfoType;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.printClassName = &PrintClassName;
        vtable.Base.Base.runWithSPMIErrorTrap =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, byte>)&RunWithErrorTrap;
        TestEE ee = new() { Info = new() { lpVtbl = &vtable } };
#if DEBUG
        using var tls = new JitTls(&ee.Info);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info = new Compiler.Info {
            compCompHnd = &ee.Info,
            compMaxStack = 1,
            compRetBuffArg = Globals.BAD_VAR_NUM,
        };
        compiler.lvaTable = [
            new LclVarDsc { Type = var_types.TYP_REF, lvClassHnd = (CORINFO_CLASS_STRUCT_*)1 },
            new LclVarDsc { Type = var_types.TYP_REF },
        ];
        compiler.lvaCount = 2;
        compiler.compCurBB = new BasicBlock(null, null) { bbCodeOffs = 0, bbCodeOffsEnd = 5, bbWeight = 1 };
        compiler.fgFirstBB = compiler.compCurBB;
        compiler.stackState.esStack = new StackEntry[1];
        JitTls.Compiler = compiler;

        try
        {
            var value = nonNull
                ? (GenTree)compiler.gtNewIconNode(var_types.TYP_REF, 0x1234)
                : compiler.gtNewLclvNode(var_types.TYP_REF, 0);
            if (nonNull)
            {
                value.Flags |= Globals.GTF_ICON_OBJ_HDL;
            }
            var operand = effectful
                ? compiler.gtNewCommaNode(var_types.TYP_REF,
                    compiler.gtNewStoreLclVarNode(1, compiler.gtNewNull()), value)
                : value;
            var originalAssignment = effectful ? operand.AsOp().Op1 : null;
            Assert.That(compiler.opts.OptimizationEnabled, Is.True);
            Assert.That(compiler.compCurBB.isRunRarely, Is.False);
            Assert.That((nint)compiler.gtGetClassHandle(operand, out _, out _), Is.EqualTo((nint)1));
            compiler.impPushOnStack(operand, new typeInfo());
            byte[] il = [(byte)opcode, 1, 0, 0, 0];
            fixed (byte* code = il)
            {
                compiler.info.compCode = code;
                compiler.info.compILCodeSize = il.Length;
                compiler.impImportBlockCode(compiler.compCurBB);
            }

            Assert.That(ee.Comparisons, Is.EqualTo(1));
            var result = compiler.impStackTop().val;
            Assert.That(result.Oper, Is.EqualTo(opcode == OPCODE.CEE_UNBOX_ANY
                ? genTreeOps.GT_IND
                : nonNull ? genTreeOps.GT_ADD : genTreeOps.GT_COMMA));
            var addressTree = opcode == OPCODE.CEE_UNBOX
                ? nonNull ? result : result.AsOp().Op2
                : result.AsIndir().Addr;
            Assert.That(addressTree.Oper, Is.EqualTo(genTreeOps.GT_ADD));
            var address = addressTree.AsOp();

            Assert.Multiple(() => {
                Assert.That(address.Op2.AsIntCon().IconValue, Is.EqualTo((nint)IntPtr.Size));
                Assert.That(address.Flags & GenTreeFlags.GTF_ASG,
                    Is.EqualTo(opcode == OPCODE.CEE_UNBOX_ANY && effectful ? GenTreeFlags.GTF_ASG : GenTreeFlags.GTF_EMPTY));
                Assert.That(address.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF,
                    Is.EqualTo(opcode == OPCODE.CEE_UNBOX && !nonNull ? GenTreeFlags.GTF_ORDER_SIDEEFF : GenTreeFlags.GTF_EMPTY));
            });
            if (opcode == OPCODE.CEE_UNBOX && !nonNull)
            {
                var nullcheck = result.AsOp().Op1;
                Assert.That(nullcheck.Oper, Is.EqualTo(genTreeOps.GT_NULLCHECK));
                Assert.That(nullcheck.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF,
                    Is.EqualTo(GenTreeFlags.GTF_ORDER_SIDEEFF));
                if (effectful)
                {
                    Assert.That(address.Op1.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
                    compiler.impEndTreeList(compiler.compCurBB);
                    var spill = compiler.compCurBB.FirstStmt ?? throw new AssertionException("Missing unbox spill.");
                    Assert.That(spill.RootNode.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_VAR));
                    Assert.Multiple(() => {
                        Assert.That(spill.NextStmt, Is.Null);
                        Assert.That(spill.RootNode.AsLclVar().Data, Is.SameAs(operand));
                        Assert.That(spill.RootNode.AsLclVar().Data.AsOp().Op1, Is.SameAs(originalAssignment));
                        Assert.That(spill.RootNode.AsLclVar().LclNum, Is.EqualTo(address.Op1.AsLclVar().LclNum));
                        Assert.That(nullcheck.AsIndir().Addr.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
                        Assert.That(nullcheck.AsIndir().Addr.AsLclVar().LclNum, Is.EqualTo(address.Op1.AsLclVar().LclNum));
                    });
                }
            }
            else if (opcode == OPCODE.CEE_UNBOX_ANY)
            {
                Assert.That(result.Flags & GenTreeFlags.GTF_EXCEPT, Is.EqualTo(GenTreeFlags.GTF_EXCEPT));
            }
#if DEBUG
            if (opcode == OPCODE.CEE_UNBOX && !effectful)
            {
                Assert.That(address.Op2.TreeId, Is.EqualTo(address.Op1.TreeId + 1));
                Assert.That(address.TreeId, Is.EqualTo(address.Op2.TreeId + 1));
            }
#endif
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct TestEE
    {
        public ICorJitInfo Info;
        public int Comparisons;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ResolveToken(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token)
        => token->hClass = (CORINFO_CLASS_STRUCT_*)1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void EmbedHandle(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token, byte parent,
        CORINFO_METHOD_STRUCT_* context, CORINFO_GENERICHANDLE_RESULT* result)
    {
        *result = default;
        result->handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS;
        result->lookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
        result->lookup.constLookup.handle = (CORINFO_GENERIC_STRUCT_*)1;
        result->compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoIsAccessAllowedResult CanAccessClass(ICorJitInfo* self,
        CORINFO_RESOLVED_TOKEN* token, CORINFO_METHOD_STRUCT_* method, CORINFO_HELPER_DESC* helper)
        => CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsValueClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc GetUnboxHelper(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls)
        => CorInfoHelpFunc.CORINFO_HELP_UNBOX;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState Compare(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* left, CORINFO_CLASS_STRUCT_* right)
    {
        ((TestEE*)self)->Comparisons++;
        return TypeCompareState.Must;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetObjectType(ICorJitInfo* self, CORINFO_OBJECT_STRUCT_* obj)
        => (CORINFO_CLASS_STRUCT_*)1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType AsCorInfoType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls)
        => CorInfoType.CORINFO_TYPE_INT;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls, int index) => null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls, byte* buffer, nint length, nint* required)
    {
        var name = "Box"u8;
        *required = name.Length + 1;
        var written = (int)Math.Min(Math.Max(length - 1, 0), name.Length);
        name[..written].CopyTo(new Span<byte>(buffer, written));

        if (length > 0)
        {
            buffer[written] = 0;
        }
        return written;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithErrorTrap(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        callback(state);
        return 1;
    }
}
