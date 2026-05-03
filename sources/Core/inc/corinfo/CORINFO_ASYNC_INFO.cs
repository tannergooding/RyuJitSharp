// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct CORINFO_ASYNC_INFO
{
    /// <summary>Class handle for System.Runtime.CompilerServices.Continuation</summary>
    public unsafe CORINFO_CLASS_HANDLE continuationClsHnd;

    /// <summary>'Next' field</summary>
    public unsafe CORINFO_FIELD_HANDLE continuationNextFldHnd;

    /// <summary>'ResumeInfo' field</summary>
    public unsafe CORINFO_FIELD_HANDLE continuationResumeInfoFldHnd;

    /// <summary>'State' field</summary>
    public unsafe CORINFO_FIELD_HANDLE continuationStateFldHnd;

    /// <summary>'Flags' field</summary>
    public unsafe CORINFO_FIELD_HANDLE continuationFlagsFldHnd;

    /// <summary>Method handle for AsyncHelpers.CaptureExecutionContext, used during suspension</summary>
    public unsafe CORINFO_METHOD_HANDLE captureExecutionContextMethHnd;

    /// <summary>Method handle for AsyncHelpers.RestoreExecutionContext, used during resumption</summary>
    public unsafe CORINFO_METHOD_HANDLE restoreExecutionContextMethHnd;

    /// <summary>Method handle for AsyncHelpers.CaptureContinuationContext, used during suspension</summary>
    public unsafe CORINFO_METHOD_HANDLE captureContinuationContextMethHnd;

    /// <summary>Method handle for AsyncHelpers.CaptureContexts, used at the beginning of async methods</summary>
    public unsafe CORINFO_METHOD_HANDLE captureContextsMethHnd;

    /// <summary>Method handle for AsyncHelpers.RestoreContexts, used before normal returns from async methods</summary>
    public unsafe CORINFO_METHOD_HANDLE restoreContextsMethHnd;

    /// <summary>Method handle for AsyncHelpers.RestoreContextsOnSuspension, used before suspending in async methods</summary>
    public unsafe CORINFO_METHOD_HANDLE restoreContextsOnSuspensionMethHnd;

    /// <summary>Finish suspension without saving continuation context (i.e. custom awaiter or ConfigureAwait(false))</summary>
    public unsafe CORINFO_METHOD_HANDLE finishSuspensionNoContinuationContextMethHnd;

    /// <summary>Finish suspension with saving continuation context (i.e. normal task await)</summary>
    public unsafe CORINFO_METHOD_HANDLE finishSuspensionWithContinuationContextMethHnd;
}
