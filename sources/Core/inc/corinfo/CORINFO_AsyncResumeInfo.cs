// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// Note: Keep synchronized with AsyncHelpers.ResumeInfo
// Any changes to this are an R2R breaking change. Update the R2R verion as needed
public struct CORINFO_AsyncResumeInfo
{
    /// <summary>delegate*&lt;Continuation, ref byte, Continuation&gt;</summary>
    public TARGET_SIZE_T Resume;

    /// <summary>Pointer in main code for diagnostics.</summary>
    /// <remarks>See comments on ICorDebugInfo::AsyncSuspensionPoint::DiagnosticNativeOffset and ResumeInfo.DiagnosticIP in SPC.</remarks>
    public TARGET_SIZE_T DiagnosticIP;
}
