// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    public struct AsyncSuspensionPoint
    {
        /// <summary>
        ///     <para>Offset of IP stored in ResumeInfo.DiagnosticIP.</para>
        ///     <para>This offset maps to the IL call that resulted in the suspension point through an ASYNC mapping.</para>
        ///     <para>Also used as a unique key for debug information about the suspension point.</para>
        ///     <para>See ResumeInfo.DiagnosticIP in SPC for more info.</para>
        /// </summary>
        public uint DiagnosticNativeOffset;

        /// <summary>Count of AsyncContinuationVarInfo in array of locals starting where the previous suspension point's locals end.</summary>
        public uint NumContinuationVars;
    }
}
