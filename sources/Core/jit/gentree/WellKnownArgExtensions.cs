// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public static class WellKnownArgExtensions
{
#if DEBUG
    private static readonly string[] s_names = [
        "None",                             // WellKnownArg.None
        "ThisPointer",                      // WellKnownArg.ThisPointer
        "VarArgsCookie",                    // WellKnownArg.VarArgsCookie
        "InstParam",                        // WellKnownArg.InstParam
        "AsyncContinuation",                // WellKnownArg.AsyncContinuation
        "RetBuffer",                        // WellKnownArg.RetBuffer
        "PInvokeFrame",                     // WellKnownArg.PInvokeFrame
        "WrapperDelegateCell",              // WellKnownArg.WrapperDelegateCell
        "ShiftLow",                         // WellKnownArg.ShiftLow
        "ShiftHigh",                        // WellKnownArg.ShiftHigh
        "VirtualStubCell",                  // WellKnownArg.VirtualStubCell
        "PInvokeCookie",                    // WellKnownArg.PInvokeCookie
        "PInvokeTarget",                    // WellKnownArg.PInvokeTarget
        "R2RIndirectionCell",               // WellKnownArg.R2RIndirectionCell
        "ValidateIndirectCallTarget",       // WellKnownArg.ValidateIndirectCallTarget
        "DispatchIndirectCallTarget",       // WellKnownArg.DispatchIndirectCallTarget
        "SwiftError",                       // WellKnownArg.SwiftError
        "SwiftSelf",                        // WellKnownArg.SwiftSelf
        "X86TailCallSpecialArg",            // WellKnownArg.X86TailCallSpecialArg
        "StackArrayLocal",                  // WellKnownArg.StackArrayLocal
        "RuntimeMethodHandle",              // WellKnownArg.RuntimeMethodHandle
        "AsyncExecutionContext",            // WellKnownArg.AsyncExecutionContext
        "AsyncSynchronizationContext",      // WellKnownArg.AsyncSynchronizationContext
        "WasmShadowStackPointer",           // WellKnownArg.WasmShadowStackPointer
        "WasmPortableEntryPoint",           // WellKnownArg.WasmPortableEntryPoint
    ];
#endif

    extension(WellKnownArg wellKnownArg)
    {
#if DEBUG
        public string Name
        {
            get
            {
                assert(s_names.Length == (int)(WellKnownArg.COUNT));
                assert(wellKnownArg < WellKnownArg.COUNT);
                return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(s_names), (int)(wellKnownArg));
            }
        }
#endif
    }
}
