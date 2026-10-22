// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class UnwindInfo
{
    private sealed class DebugData
    {
        private static ConditionalWeakTable<UnwindInfo, DebugData>? s_instances;

        internal bool AddingNOP;
        internal bool Initialized;

        internal static DebugData Get(UnwindInfo unwindInfo)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instances);
            return instances.GetValue(unwindInfo, static _ => new DebugData());
        }
    }

    public bool uwiAddingNOP
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnwindNOPDiagnosticsAreOnlyAvailableInDebugBuilds);
            }

            return DebugData.Get(this).AddingNOP;
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
                DebugData.Get(this).AddingNOP = false;
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

            return DebugData.Get(this).Initialized;
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
                DebugData.Get(this).Initialized = false;
            }
        }
    }

    private DebugData GetDebugData() => DebugData.Get(this);
}
