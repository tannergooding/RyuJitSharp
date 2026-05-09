// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.EHHandlerType;

namespace RyuJitSharp;

public enum EHHandlerType
{
    EH_HANDLER_CATCH = 0x1, // Don't use zero (to aid debugging uninitialized memory)
    EH_HANDLER_FILTER,
    EH_HANDLER_FAULT,
    EH_HANDLER_FINALLY,
    EH_HANDLER_FAULT_WAS_FINALLY
}
