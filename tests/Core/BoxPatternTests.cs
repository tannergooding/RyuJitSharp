// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using BoxPatterns = RyuJitSharp.Compiler.BoxPatterns;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BoxPatternTests
{
    [TestCase(OPCODE.CEE_NOP, 1)]
    [TestCase(OPCODE.CEE_UNBOX_ANY, 0)]
    [TestCase(OPCODE.CEE_UNBOX_ANY, 4)]
    [TestCase(OPCODE.CEE_BRTRUE_S, 1)]
    [TestCase(OPCODE.CEE_BRFALSE, 4)]
    [TestCase(OPCODE.CEE_ISINST, 5)]
    public static void UnmatchedPatternsDoNotConsumeOrChangeTheStack(OPCODE opcode, int length)
    {
        WithCompiler((compiler, state) => {
            var bytes = stackalloc byte[10];
            bytes[0] = (byte)opcode;
            var original = compiler.impStackTop().val;
            CORINFO_RESOLVED_TOKEN token = new() { hClass = (CORINFO_CLASS_STRUCT_*)1 };

            var consumed = compiler.impBoxPatternMatch(token, bytes, bytes + length, BoxPatterns.None);

            Assert.That(consumed, Is.EqualTo(-1));
            Assert.That(compiler.impStackTop().val, Is.SameAs(original));
            Assert.That(compiler.stackState.esStackDepth, Is.EqualTo(1));
            Assert.That(state->Resolutions, Is.Zero);
        });
    }

    [TestCase(TypeCompareState.Must, CorInfoType.CORINFO_TYPE_UNDEF, CorInfoType.CORINFO_TYPE_UNDEF, 5)]
    [TestCase(TypeCompareState.May, CorInfoType.CORINFO_TYPE_INT, CorInfoType.CORINFO_TYPE_INT, -1)]
    [TestCase(TypeCompareState.MustNot, CorInfoType.CORINFO_TYPE_INT, CorInfoType.CORINFO_TYPE_INT, 5)]
    [TestCase(TypeCompareState.MustNot, CorInfoType.CORINFO_TYPE_INT, CorInfoType.CORINFO_TYPE_LONG, -1)]
    public static void UnboxPreservesValueOnlyForCompatibleTypes(TypeCompareState comparison, CorInfoType source, CorInfoType target, int expected)
    {
        WithCompiler((compiler, state) => {
            state->Comparison = comparison;
            state->Source = source;
            state->Target = target;
            var bytes = stackalloc byte[] { (byte)OPCODE.CEE_UNBOX_ANY, 2, 0, 0, 0 };
            CORINFO_RESOLVED_TOKEN token = new() { hClass = (CORINFO_CLASS_STRUCT_*)1 };
            var original = compiler.impStackTop().val;

            var consumed = compiler.impBoxPatternMatch(token, bytes, bytes + 5, BoxPatterns.None);

            Assert.That(consumed, Is.EqualTo(expected));
            Assert.That(compiler.impStackTop().val, Is.SameAs(original));
            Assert.That(state->Resolutions, Is.EqualTo(1));
        });
    }

    [TestCase(OPCODE.CEE_BRTRUE, 5, BoxPatterns.None)]
    [TestCase(OPCODE.CEE_BRFALSE, 5, BoxPatterns.None)]
    [TestCase(OPCODE.CEE_BRTRUE_S, 2, BoxPatterns.IsByRefLike)]
    [TestCase(OPCODE.CEE_BRFALSE_S, 2, BoxPatterns.IsByRefLike)]
    public static void BranchPatternReplacesBoxButLeavesBranch(OPCODE opcode, int length, BoxPatterns options)
    {
        WithCompiler((compiler, state) => {
            var bytes = stackalloc byte[5];
            bytes[0] = (byte)opcode;
            CORINFO_RESOLVED_TOKEN token = new() { hClass = (CORINFO_CLASS_STRUCT_*)1 };

            var consumed = compiler.impBoxPatternMatch(token, bytes, bytes + length, options);

            Assert.That(consumed, Is.Zero);
            Assert.That(compiler.impStackTop().val.AsIntCon().IconValue, Is.EqualTo((nint)1));
            Assert.That(state->Resolutions, Is.Zero);
        });
    }

    [TestCase(TypeCompareState.MustNot, false, 5, var_types.TYP_REF, 0)]
    [TestCase(TypeCompareState.Must, false, 5, var_types.TYP_INT, 1)]
    [TestCase(TypeCompareState.Must, true, 8, var_types.TYP_INT, 1)]
    [TestCase(TypeCompareState.May, false, -1, var_types.TYP_INT, 42)]
    public static void IsInstUsesKnownCastAndConsumesOnlyTheFold(TypeCompareState comparison, bool comparisonForm, int expected, var_types type, int value)
    {
        WithCompiler((compiler, state) => {
            state->Comparison = comparison;
            byte[] bytes = comparisonForm
                ? [(byte)OPCODE.CEE_ISINST, 2, 0, 0, 0, (byte)OPCODE.CEE_LDNULL, (byte)OPCODE.CEE_PREFIX1, 3]
                : [(byte)OPCODE.CEE_ISINST, 2, 0, 0, 0, (byte)OPCODE.CEE_BRTRUE_S, 0];
            CORINFO_RESOLVED_TOKEN token = new() { hClass = (CORINFO_CLASS_STRUCT_*)1 };

            fixed (byte* code = bytes)
            {
                var consumed = compiler.impBoxPatternMatch(token, code, code + bytes.Length, BoxPatterns.None);
                Assert.That(consumed, Is.EqualTo(expected));
                Assert.That(compiler.impStackTop().val.Type, Is.EqualTo(type));
                Assert.That(compiler.impStackTop().val.AsIntCon().IconValue, Is.EqualTo((nint)value));
            }
        });
    }

    [TestCase(TypeCompareState.Must, 10)]
    [TestCase(TypeCompareState.May, -1)]
    public static void IsInstUnboxRequiresExactTypes(TypeCompareState comparison, int expected)
    {
        WithCompiler((compiler, state) => {
            state->Comparison = comparison;
            var bytes = stackalloc byte[] { (byte)OPCODE.CEE_ISINST, 2, 0, 0, 0, (byte)OPCODE.CEE_UNBOX_ANY, 2, 0, 0, 0 };
            CORINFO_RESOLVED_TOKEN token = new() { hClass = (CORINFO_CLASS_STRUCT_*)1 };
            var original = compiler.impStackTop().val;

            Assert.That(compiler.impBoxPatternMatch(token, bytes, bytes + 10, BoxPatterns.None), Is.EqualTo(expected));
            Assert.That(compiler.impStackTop().val, Is.SameAs(original));
        });
    }

    private delegate void CompilerAction(Compiler compiler, TestEE* state);

    private static void WithCompiler(CompilerAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.resolveToken = &ResolveToken;
        vtable.Base.Base.compareTypesForEquality = &Compare;
        vtable.Base.Base.compareTypesForCast = &Compare;
        vtable.Base.Base.getTypeForPrimitiveValueClass = &GetPrimitive;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.isNullableType = &IsNullable;
        vtable.Base.Base.getBoxHelper = &GetBoxHelper;
        TestEE state = new() { Info = new() { lpVtbl = &vtable } };
#if DEBUG
        using var tls = new JitTls(&state.Info);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info { compCompHnd = &state.Info, compMaxStack = 1 };
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            compiler.stackState.esStack = [new() { val = compiler.gtNewIconNode(var_types.TYP_INT, 42) }];
            compiler.stackState.esStackDepth = 1;
            action(compiler, &state);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ResolveToken(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token)
    {
        ((TestEE*)self)->Resolutions++;
        token->hClass = (CORINFO_CLASS_STRUCT_*)token->token;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState Compare(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* source, CORINFO_CLASS_STRUCT_* target)
        => ((TestEE*)self)->Comparison;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetPrimitive(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => type == (CORINFO_CLASS_STRUCT_*)1 ? ((TestEE*)self)->Source : ((TestEE*)self)->Target;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState IsNullable(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => TypeCompareState.MustNot;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc GetBoxHelper(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => CorInfoHelpFunc.CORINFO_HELP_BOX;

    private struct TestEE
    {
        public ICorJitInfo Info;
        public TypeCompareState Comparison;
        public CorInfoType Source;
        public CorInfoType Target;
        public int Resolutions;
    }
}
