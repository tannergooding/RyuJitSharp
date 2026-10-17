// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class AsyncResumeCodeGenTestSupport
{
    internal static void WithCodeGen(ResumeAction action)
    {
        void RecordResume(Compiler compiler, CodeGen codeGen)
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getAsyncResumptionStub = &GetAsyncResumptionStub;
            CORINFO_METHOD_STRUCT_ method = default;
            var context = new ResumeContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Method = &method,
                EntryPoint = (void*)(delegate* unmanaged<void>)&ResumeStub,
            };

            compiler.info.compCompHnd = &context.JitInfo;
            compiler.compSuspensionPoints =
            [
                new ICorDebugInfo.AsyncSuspensionPoint(),
                new ICorDebugInfo.AsyncSuspensionPoint(),
            ];
            compiler.compCurBB = new BasicBlock(null, null);
            _ = codeGen.Emitter.emitDataConst([0, 0, 0, 0], 4, var_types.TYP_INT);
            codeGen.Emitter.emitCmpHandle = &context.JitInfo;

            action(compiler, codeGen, &context);
        }

        if (Configuration.TARGET_ARM)
        {
            ArmCalleeSavedRegisterTests.WithCodeGen(RecordResume);
        }
        else if (Configuration.TARGET_ARM64)
        {
            Arm64CodeGenLocalVariableTests.WithCodeGen(RecordResume);
        }
        else
        {
            throw new System.PlatformNotSupportedException("Async resume recording is only supported on ARM targets.");
        }
    }

    internal static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    internal delegate void ResumeAction(Compiler compiler, CodeGen codeGen, ResumeContext* context);

    internal struct ResumeContext
    {
        public ICorJitInfo JitInfo;
        public CORINFO_METHOD_STRUCT_* Method;
        public void* EntryPoint;
        public int Queries;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* GetAsyncResumptionStub(ICorJitInfo* self, void** entryPoint)
    {
        var context = (ResumeContext*)self;
        context->Queries++;
        *entryPoint = context->EntryPoint;

        return context->Method;
    }

    [UnmanagedCallersOnly]
    private static void ResumeStub()
    {
        throw new System.InvalidOperationException("The recording fixture must not execute the resume stub.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
