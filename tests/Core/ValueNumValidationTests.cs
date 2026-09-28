// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumValidationTests
{
    private static int s_assertions;
    private static string? s_lastAssertion;

    [TestCase(VNFunc.VNF_ADD, 2)]
    [TestCase(VNFunc.VNF_IND, 1)]
    [TestCase(VNFunc.VNF_MapSelect, 4)]
    [TestCase(VNFunc.VNF_PtrToLoc, 32)]
    public static void StaticValidationDetectsAttributeCorruption(VNFunc func, byte mask)
    {
        WithCompiler(_ =>
        {
            var field = typeof(VNFuncExtensions).GetField("s_attribs", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(nameof(VNFuncExtensions), "s_attribs");
            if (field.GetValue(null) is not ValueNumStore.VNFOpAttrib[] attributes)
            {
                throw new InvalidOperationException("Missing VN attribute table.");
            }

            ValueNumStore.ValidateValueNumStoreStatics();
            Assert.That(s_assertions, Is.Zero);
            var previous = attributes[(int)func];
            try
            {
                attributes[(int)func] ^= (ValueNumStore.VNFOpAttrib)mask;
                ValueNumStore.ValidateValueNumStoreStatics();
                Assert.That(s_assertions, Is.EqualTo(1));
            }
            finally
            {
                attributes[(int)func] = previous;
            }
        });
    }

    [TestCase(TYP_INT, 0)]
    [TestCase(TYP_LONG, 0)]
    [TestCase(TYP_FLOAT, 0)]
    [TestCase(TYP_DOUBLE, 0)]
    [TestCase(TYP_REF, 0)]
    [TestCase(TYP_SIMD16, 1)]
    public static void ScalarOneRejectsSimdTypes(var_types type, int expectedAssertions)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            var result = VNOneForType(store, type);
            Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
            Assert.That(result == ValueNumStore.NoVN, Is.EqualTo(type is TYP_REF or TYP_SIMD16));
        });
    }

    [Test]
    public static void ComponentTestsKeepNativeAssertionAndRunOnceWhenEnabled()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var previousSetting = ComponentTestSetting(ref JitConfig);
            var previousRun = DidComponentUnitTests(compiler);
            try
            {
                DidComponentUnitTests(compiler) = false;
                ComponentTestSetting(ref JitConfig) = 0;
                compiler.compDoComponentUnitTestsOnce();
                Assert.That(DidComponentUnitTests(compiler), Is.False);
                Assert.That(s_assertions, Is.Zero, s_lastAssertion);

                ComponentTestSetting(ref JitConfig) = 1;
                compiler.compDoComponentUnitTestsOnce();
                Assert.That(DidComponentUnitTests(compiler), Is.True);
                Assert.That(compiler.vnStore, Is.SameAs(store));
                Assert.That(s_assertions, Is.EqualTo(1), s_lastAssertion);
                Assert.That(s_lastAssertion, Does.Contain("application.GetArg(0) == one"));
                compiler.compDoComponentUnitTestsOnce();
                Assert.That(s_assertions, Is.EqualTo(1), s_lastAssertion);
            }
            finally
            {
                ComponentTestSetting(ref JitConfig) = previousSetting;
                DidComponentUnitTests(compiler) = previousRun;
            }
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.info.compFullName = nameof(ValueNumValidationTests);
        JitTls.Compiler = compiler;
        s_assertions = 0;
        s_lastAssertion = null;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        s_lastAssertion = Marshal.PtrToStringUTF8((nint)expression);
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "VNOneForType")]
    private static extern int VNOneForType(ValueNumStore store, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_runComponentUnitTests")]
    private static extern ref int ComponentTestSetting(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_didComponentUnitTests")]
    private static extern ref bool DidComponentUnitTests(Compiler compiler);
}
#endif
