// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    [Flags]
    public enum BoundaryTypes
    {
        /// <summary>No implicit boundaries.</summary>
        NO_BOUNDARIES = 0x00,

        /// <summary>Boundary whenever the IL evaluation stack is empty.</summary>
        STACK_EMPTY_BOUNDARIES = 0x01,

        /// <summary>Before every CEE_NOP instruction.</summary>
        NOP_BOUNDARIES = 0x02,

        /// <summary>Before every CEE_CALL, CEE_CALLVIRT, etc instruction.</summary>
        CALL_SITE_BOUNDARIES = 0x04,

        /// <summary>Set of boundaries that debugger should always reasonably ask the JIT for.</summary>
        DEFAULT_BOUNDARIES = STACK_EMPTY_BOUNDARIES | NOP_BOUNDARIES | CALL_SITE_BOUNDARIES
    }
}
