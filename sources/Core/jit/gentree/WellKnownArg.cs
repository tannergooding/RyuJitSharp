// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum WellKnownArg : byte
{
    None,
    ThisPointer,
    VarArgsCookie,
    InstParam,
    AsyncContinuation,
    RetBuffer,
    PInvokeFrame,
    WrapperDelegateCell,
    ShiftLow,
    ShiftHigh,
    VirtualStubCell,
    PInvokeCookie,
    PInvokeTarget,
    R2RIndirectionCell,
    ValidateIndirectCallTarget,
    DispatchIndirectCallTarget,
    SwiftError,
    SwiftSelf,
    X86TailCallSpecialArg,
    StackArrayLocal,
    RuntimeMethodHandle,
    AsyncExecutionContext,
    AsyncSynchronizationContext,
    WasmShadowStackPointer,
    WasmPortableEntryPoint,
    COUNT,
}
