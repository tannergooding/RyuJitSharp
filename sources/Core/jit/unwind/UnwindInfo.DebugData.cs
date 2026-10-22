// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class UnwindInfo
{
    public bool uwiAddingNOP
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnwindNOPDiagnosticsAreOnlyAvailableInDebugBuilds);
            }

            return GetDebugData().AddingNOP;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnwindNOPDiagnosticsAreOnlyAvailableInDebugBuilds);
            }

            if (value)
            {
                GetDebugData().AddingNOP = true;
            }
            else
            {
                GetDebugData().AddingNOP = false;
            }
        }
    }

    private bool uwiInitialized
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnwindInitializationStateIsOnlyAvailableInDebugBuilds);
            }

            return GetDebugData().Initialized;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnwindInitializationStateIsOnlyAvailableInDebugBuilds);
            }

            if (value)
            {
                GetDebugData().Initialized = true;
            }
            else
            {
                GetDebugData().Initialized = false;
            }
        }
    }

    private Compiler.DebugData.UnwindInfoDebugData GetDebugData()
    {
        return Compiler.DebugData.Get(this);
    }
}
