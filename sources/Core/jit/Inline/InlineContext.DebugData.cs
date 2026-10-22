// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class InlineContext
{
    private Compiler.DebugData.InlineContextDebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlineContextDiagnosticStateIsOnlyAvailableInDebugBuilds);
        }

        return Compiler.DebugData.Get(this);
    }
}
