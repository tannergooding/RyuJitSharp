// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerSignatureArgumentCacheTests
{
    private static int s_argTypeCalls;
    private static int s_argClassCalls;

    [Test]
    public static void ArgTypeCacheUsesCopiedSignatureValuesAndPreservesOutputShape()
    {
        s_argTypeCalls = 0;
        s_argClassCalls = 0;

        WithCompiler(jitInfo => {
            var firstSignatureBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
            var copiedSignatureBytes = (byte[])firstSignatureBytes.Clone();

            fixed (byte* firstSignature = firstSignatureBytes)
            fixed (byte* copiedSignature = copiedSignatureBytes)
            {
                var firstSigInfo = CreateSignatureInfo(firstSignature, firstSignatureBytes.Length);
                var copiedSigInfo = CreateSignatureInfo(copiedSignature, copiedSignatureBytes.Length);
                var firstArgs = (CORINFO_ARG_LIST_STRUCT_*)(firstSignature + 3);
                var copiedArgs = (CORINFO_ARG_LIST_STRUCT_*)(copiedSignature + 3);

                CORINFO_CLASS_STRUCT_* firstClass = default;
                var firstType = jitInfo->getArgType(&firstSigInfo, firstArgs, &firstClass);

                CORINFO_CLASS_STRUCT_* copiedClass = default;
                var copiedType = jitInfo->getArgType(&copiedSigInfo, copiedArgs, &copiedClass);

                Assert.That(s_argTypeCalls, Is.EqualTo(1));
                Assert.That(copiedType, Is.EqualTo(firstType));
                Assert.That((nint)copiedClass, Is.EqualTo((nint)firstClass));

                var noOutputType = jitInfo->getArgType(&firstSigInfo, firstArgs, null);
                var repeatedNoOutputType = jitInfo->getArgType(&copiedSigInfo, copiedArgs, null);

                Assert.That(s_argTypeCalls, Is.EqualTo(2));
                Assert.That(repeatedNoOutputType, Is.EqualTo(noOutputType));

                CORINFO_CLASS_STRUCT_* repeatedClass = default;
                var repeatedType = jitInfo->getArgType(&firstSigInfo, firstArgs, &repeatedClass);

                Assert.That(s_argTypeCalls, Is.EqualTo(2));
                Assert.That(repeatedType, Is.EqualTo(firstType));
                Assert.That((nint)repeatedClass, Is.EqualTo((nint)firstClass));

                var secondArgument = (CORINFO_ARG_LIST_STRUCT_*)(firstSignature + 4);
                _ = jitInfo->getArgType(&firstSigInfo, secondArgument, null);

                Assert.That(s_argTypeCalls, Is.EqualTo(3));
            }
        });
    }

    [Test]
    public static void ArgTypeCacheDistinguishesSignatureContextAndSkipsMethodSignatureHandles()
    {
        s_argTypeCalls = 0;
        s_argClassCalls = 0;

        WithCompiler(jitInfo => {
            var signatureBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
            var instantiation = stackalloc CORINFO_CLASS_STRUCT_*[1];
            var copiedInstantiation = stackalloc CORINFO_CLASS_STRUCT_*[1];
            instantiation[0] = (CORINFO_CLASS_STRUCT_*)(nint)0x3001;
            copiedInstantiation[0] = instantiation[0];

            fixed (byte* signature = signatureBytes)
            {
                var sigInfo = CreateSignatureInfo(signature, signatureBytes.Length);
                var args = (CORINFO_ARG_LIST_STRUCT_*)(signature + 3);
                CORINFO_CLASS_STRUCT_* valueClass = default;

                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(1));

                sigInfo.scope = (CORINFO_MODULE_STRUCT_*)(nint)0x202;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(2));

                sigInfo.token++;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(3));

                sigInfo.sigInst.classInstCount = 1;
                sigInfo.sigInst.classInst = instantiation;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(4));

                sigInfo.sigInst.classInst = copiedInstantiation;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(4));

                instantiation[0] = (CORINFO_CLASS_STRUCT_*)(nint)0x3002;
                sigInfo.sigInst.classInst = instantiation;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(5));

                signatureBytes[0] = 0x10;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(6));

                sigInfo.methodSignature = (MethodSignatureInfo*)0x4001;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(8));
            }
        });
    }

    [Test]
    public static void ArgTypeCacheIsScopedToCompilerAndJitInfo()
    {
        s_argTypeCalls = 0;
        s_argClassCalls = 0;

        WithCompiler(jitInfo => {
            var firstCompiler = JitTls.Compiler!;
            var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            secondCompiler.info.compCompHnd = jitInfo;

            var signatureBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

            fixed (byte* signature = signatureBytes)
            {
                var sigInfo = CreateSignatureInfo(signature, signatureBytes.Length);
                var args = (CORINFO_ARG_LIST_STRUCT_*)(signature + 2);
                CORINFO_CLASS_STRUCT_* valueClass = default;

                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(1));

                try
                {
                    JitTls.Compiler = secondCompiler;
                    _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                    Assert.That(s_argTypeCalls, Is.EqualTo(2));
                }
                finally
                {
                    JitTls.Compiler = firstCompiler;
                }

                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(2));

                var copiedJitInfo = *jitInfo;
                firstCompiler.info.compCompHnd = &copiedJitInfo;
                _ = jitInfo->getArgType(&sigInfo, args, &valueClass);
                Assert.That(s_argTypeCalls, Is.EqualTo(3));
                firstCompiler.info.compCompHnd = jitInfo;
            }
        });
    }

    [Test]
    public static void ArgClassCacheUsesCopiedSignatureValues()
    {
        s_argTypeCalls = 0;
        s_argClassCalls = 0;

        WithCompiler(jitInfo => {
            var firstSignatureBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var copiedSignatureBytes = (byte[])firstSignatureBytes.Clone();

            fixed (byte* firstSignature = firstSignatureBytes)
            fixed (byte* copiedSignature = copiedSignatureBytes)
            {
                var firstSigInfo = CreateSignatureInfo(firstSignature, firstSignatureBytes.Length);
                var copiedSigInfo = CreateSignatureInfo(copiedSignature, copiedSignatureBytes.Length);
                var firstArgs = (CORINFO_ARG_LIST_STRUCT_*)(firstSignature + 2);
                var copiedArgs = (CORINFO_ARG_LIST_STRUCT_*)(copiedSignature + 2);

                var firstClass = jitInfo->getArgClass(&firstSigInfo, firstArgs);
                var copiedClass = jitInfo->getArgClass(&copiedSigInfo, copiedArgs);

                Assert.That(s_argClassCalls, Is.EqualTo(1));
                Assert.That((nint)copiedClass, Is.EqualTo((nint)firstClass));

                copiedSigInfo.token++;
                _ = jitInfo->getArgClass(&copiedSigInfo, copiedArgs);

                Assert.That(s_argClassCalls, Is.EqualTo(2));
            }
        });
    }

    private static CORINFO_SIG_INFO CreateSignatureInfo(byte* signature, int signatureLength)
    {
        var sigInfo = default(CORINFO_SIG_INFO);
        sigInfo.retType = CORINFO_TYPE_VOID;
        sigInfo.numArgs = 2;
        sigInfo.args = (CORINFO_ARG_LIST_STRUCT_*)(signature + 2);
        sigInfo.pSig = signature;
        sigInfo.cbSig = signatureLength;
        sigInfo.scope = (CORINFO_MODULE_STRUCT_*)(nint)0x201;
        sigInfo.token = 0x06000001;
        return sigInfo;
    }

    private static void WithCompiler(CompilerTest action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArgType = &GetArgType;
        vtable.Base.Base.getArgClass = &GetArgClass;

        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;

        var previousCompiler = JitTls.Compiler;
        using var tls = TestConfiguration.CreateJitTls(&jitInfo);

        try
        {
            JitTls.Compiler = compiler;
            action(&jitInfo);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetArgType(
        ICorJitInfo* _, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* args, CORINFO_CLASS_STRUCT_** valueClass)
    {
        s_argTypeCalls++;

        if (valueClass != null)
        {
            *valueClass = (CORINFO_CLASS_STRUCT_*)(nint)(0x1000 + s_argTypeCalls);
        }

        return (CorInfoTypeWithMod)CORINFO_TYPE_CLASS;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetArgClass(
        ICorJitInfo* _, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* args)
    {
        s_argClassCalls++;
        return (CORINFO_CLASS_STRUCT_*)(nint)(0x2000 + s_argClassCalls);
    }

    private unsafe delegate void CompilerTest(ICorJitInfo* jitInfo);
}
