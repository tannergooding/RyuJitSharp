// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumPointerExtensionTests
{
    [TestCase(false, -12)]
    [TestCase(false, 8)]
    [TestCase(true, -12)]
    [TestCase(true, 8)]
    public static void ExtensionPreservesPointerIdentityAndLiberalExceptions(bool isStatic, int offset)
    {
        WithStore((compiler, store) =>
        {
            var reference = store.VNForExpr(null, TYP_REF);
            var field = compiler.FieldSeqStore.Create((CORINFO_FIELD_STRUCT_*)0x1000, 0,
                FieldSeq.FieldKind.SimpleStaticKnownAddress);
            var sequence = store.VNForFieldSeq(field);
            var oldOffset = store.VNForIntPtrCon(20);
            var pointer = isStatic
                ? store.VNForFunc(TYP_BYREF, VNF_PtrToStatic, reference, sequence, oldOffset)
                : store.VNForFunc(TYP_BYREF, VNF_PtrToArrElem, store.VNForIntCon(1), reference,
                    store.VNForIntCon(2), oldOffset);
            var exceptionSet = store.VNExcSetSingleton(store.VNForExpr(null, TYP_REF));
            var node = compiler.gtNewLclvNode(TYP_BYREF, 0);
            node._vnPair = new ValueNumPair(store.VNWithExc(pointer, exceptionSet),
                store.VNForExpr(null, TYP_BYREF));
            var extension = compiler.gtNewIconNode(TYP_I_IMPL, offset);

            var result = store.ExtendPtrVN(node, extension);

            Assert.That(store.VNExceptionSet(result), Is.EqualTo(exceptionSet));
            VNFuncApp original = default;
            VNFuncApp extended = default;
            Assert.That(store.GetVNFunc(pointer, ref original), Is.True);
            Assert.That(store.GetVNFunc(store.VNNormalValue(result), ref extended), Is.True);
            Assert.That(extended.Func, Is.EqualTo(original.Func));
            var offsetArg = isStatic ? 2 : 3;
            for (var index = 0; index < offsetArg; index++)
            {
                Assert.That(extended.GetArg(index), Is.EqualTo(original.GetArg(index)));
            }
            Assert.That(store.ConstantValue<nint>(extended.GetArg(offsetArg)), Is.EqualTo((nint)(20 + offset)));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void UnrecognizedPointersOrNonconstantOffsetsReturnNoVN(int shape)
    {
        WithStore((compiler, store) =>
        {
            var node = compiler.gtNewLclvNode(TYP_BYREF, 0);
            node._vnPair.SetBoth(shape == 0 ? store.VNZeroForType(TYP_BYREF) : store.VNForExpr(null, TYP_BYREF));
            GenTree extension = shape == 2
                ? compiler.gtNewLclvNode(TYP_I_IMPL, 0)
                : compiler.gtNewIconNode(TYP_I_IMPL, 4);
            Assert.That(store.ExtendPtrVN(node, extension), Is.EqualTo(ValueNumStore.NoVN));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsFieldStatic(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* handle) => 1;

    private static void WithStore(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaCount = 1;
        compiler.lvaTable = new LclVarDsc[1];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isFieldStatic = &IsFieldStatic;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
        JitTls.Compiler = compiler;
        try
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
