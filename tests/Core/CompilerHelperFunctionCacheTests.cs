// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.InfoAccessType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerHelperFunctionCacheTests
{
    private static int s_getHelperFtnCalls;
    private static int s_methodHandleOutputRequests;

    [Test]
    public static void HelperLookupsCacheCompleteEntryPointsPerCompiler()
    {
        s_getHelperFtnCalls = 0;
        s_methodHandleOutputRequests = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.getHelperFtn = &GetHelperFtn;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;
        firstCompiler.info.compMatchedVM = true;

        var indirectLookup = firstCompiler.compGetHelperFtn(CORINFO_HELP_MEMZERO);
        Assert.That(indirectLookup.accessType, Is.EqualTo(IAT_PVALUE));
        Assert.That((nint)indirectLookup.addr, Is.EqualTo((nint)0x9000));

        var cachedIndirectLookup = firstCompiler.compGetHelperFtn(CORINFO_HELP_MEMZERO);
        Assert.That(cachedIndirectLookup.accessType, Is.EqualTo(IAT_PVALUE));
        Assert.That((nint)cachedIndirectLookup.addr, Is.EqualTo((nint)0x9000));

        var directLookup = firstCompiler.compGetHelperFtn(CORINFO_HELP_MEMCPY);
        Assert.That(directLookup.accessType, Is.EqualTo(IAT_VALUE));
        Assert.That((nint)directLookup.addr, Is.EqualTo((nint)0xA000));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        secondCompiler.info.compMatchedVM = true;
        Assert.That((nint)secondCompiler.compGetHelperFtn(CORINFO_HELP_MEMZERO).addr, Is.EqualTo((nint)0x9000));

        var mismatchedCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        mismatchedCompiler.info.compCompHnd = &jitInfo;
        mismatchedCompiler.info.compMatchedVM = false;
        var mismatchedLookup = mismatchedCompiler.compGetHelperFtn(CORINFO_HELP_MEMZERO);
        Assert.That(mismatchedLookup.accessType, Is.EqualTo(IAT_VALUE));
        Assert.That((nint)mismatchedLookup.addr, Is.EqualTo(unchecked((nint)0xCA11CA11)));

        Assert.Multiple(() => {
            Assert.That(s_getHelperFtnCalls, Is.EqualTo(3));
            Assert.That(s_methodHandleOutputRequests, Is.Zero);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetHelperFtn(
        ICorJitInfo* _, CorInfoHelpFunc helper, CORINFO_CONST_LOOKUP* nativeEntrypoint,
        CORINFO_METHOD_STRUCT_** methodHandle)
    {
        s_getHelperFtnCalls++;
        if (methodHandle is not null)
        {
            s_methodHandleOutputRequests++;
            *methodHandle = null;
        }

        nativeEntrypoint->accessType = helper is CORINFO_HELP_MEMZERO ? IAT_PVALUE : IAT_VALUE;
        nativeEntrypoint->addr = (helper is CORINFO_HELP_MEMZERO) ? (void*)0x9000 : (void*)0xA000;
    }
}
