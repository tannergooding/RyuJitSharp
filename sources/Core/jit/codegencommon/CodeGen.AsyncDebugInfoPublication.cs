// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genReportAsyncDebugInfo()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Async debug-info publication requires Windows AMD64.");
#else
        if (!_compiler.opts.compDbgInfo)
        {
            return;
        }

        var suspensionPoints = _compiler.compSuspensionPoints;
        if (suspensionPoints is null)
        {
            return;
        }

        for (var index = 0; index < suspensionPoints.Count; index++)
        {
            var diagnosticNativeOffset = 0u;
            if (genAsyncResumeInfoTable is not null)
            {
                var location = genAsyncResumeInfoTable.Locations[index];
                if (location.Valid())
                {
                    diagnosticNativeOffset = location.CodeOffset(Emitter);
                }
            }

            var point = suspensionPoints[index];
            point.DiagnosticNativeOffset = unchecked((int)diagnosticNativeOffset);
            suspensionPoints[index] = point;
        }

        var asyncInfo = new ICorDebugInfo.AsyncInfo { NumSuspensionPoints = suspensionPoints.Count };
        var jitInfo = _compiler.info.compCompHnd;
        var hostSuspensionPoints = (ICorDebugInfo.AsyncSuspensionPoint*)jitInfo->allocateArray(
            unchecked((nint)((nuint)suspensionPoints.Count * (nuint)sizeof(ICorDebugInfo.AsyncSuspensionPoint))));
        for (var index = 0; index < suspensionPoints.Count; index++)
        {
            hostSuspensionPoints[index] = suspensionPoints[index];
        }

        var asyncVars = _compiler.compAsyncVars;
        assert(asyncVars is not null);
        var hostVars = (ICorDebugInfo.AsyncContinuationVarInfo*)jitInfo->allocateArray(
            unchecked((nint)((nuint)asyncVars.Count * (nuint)sizeof(ICorDebugInfo.AsyncContinuationVarInfo))));
        for (var index = 0; index < asyncVars.Count; index++)
        {
            hostVars[index] = asyncVars[index];
        }

        jitInfo->reportAsyncDebugInfo(&asyncInfo, hostSuspensionPoints, hostVars, asyncVars.Count);
#if DEBUG
        if (_verbose)
        {
            // The EE owns the submitted buffers now; retain native diagnostic
            // ordering without reading transferred storage.
            jitprintf("Reported async suspension points:\n");
            for (var index = 0; index < suspensionPoints.Count; index++)
            {
                jitprintf($"  [{index}] NumAsyncVars = {unchecked((uint)suspensionPoints[index].NumContinuationVars)}\n");
            }
            jitprintf("Reported async vars:\n");
            for (var index = 0; index < asyncVars.Count; index++)
            {
                jitprintf($"  [{index}] VarNumber = {unchecked((uint)asyncVars[index].VarNumber)}, Offset = {unchecked((uint)asyncVars[index].Offset):x}\n");
            }
        }
#endif
#endif
    }
}
