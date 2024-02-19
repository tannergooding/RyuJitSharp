// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using JITINTERFACE_HRESULT = int;

// Cookie types consumed by the code generator (these are opaque values
// not inspected by the code generator):

global using unsafe CORINFO_ASSEMBLY_HANDLE = RyuJitSharp.CORINFO_ASSEMBLY_STRUCT_*;
global using unsafe CORINFO_MODULE_HANDLE = RyuJitSharp.CORINFO_MODULE_STRUCT_*;
global using unsafe CORINFO_DEPENDENCY_HANDLE = RyuJitSharp.CORINFO_DEPENDENCY*;
global using unsafe CORINFO_CLASS_HANDLE = RyuJitSharp.CORINFO_CLASS_STRUCT_*;
global using unsafe CORINFO_METHOD_HANDLE = RyuJitSharp.CORINFO_METHOD_STRUCT_*;
global using unsafe CORINFO_FIELD_HANDLE = RyuJitSharp.CORINFO_FIELD_STRUCT_*;
global using unsafe CORINFO_OBJECT_HANDLE = RyuJitSharp.CORINFO_OBJECT_STRUCT_*;

// represents a list of argument types
global using unsafe CORINFO_ARG_LIST_HANDLE = RyuJitSharp.CORINFO_ARG_LIST_STRUCT_*;

global using unsafe CORINFO_JUST_MY_CODE_HANDLE = RyuJitSharp.CORINFO_JUST_MY_CODE_HANDLE_*;

// a handle guaranteed to be unique per process
global using unsafe CORINFO_PROFILING_HANDLE = RyuJitSharp.CORINFO_PROFILING_STRUCT_*;

// a generic handle (could be any of the above)
global using unsafe CORINFO_GENERIC_HANDLE = RyuJitSharp.CORINFO_GENERIC_STRUCT_*;

// what is actually passed on the varargs call
global using unsafe CORINFO_VARARGS_HANDLE = RyuJitSharp.CORINFO_VarArgInfo*;

// Generic tokens are resolved with respect to a context, which is usually the method
// being compiled. The CORINFO_CONTEXT_HANDLE indicates which exact instantiation
// (or the open instantiation) is being referred to.
global using unsafe CORINFO_CONTEXT_HANDLE = RyuJitSharp.CORINFO_CONTEXT_STRUCT_*;

global using unsafe CORINFO_MethodPtr = void*; // a generic method pointer

// Guard-stack cookie for preventing against stack buffer overruns
global using GSCookie = nuint;

// Runs the given function under an error trap. This allows the JIT to make calls
// to interface functions that may throw exceptions without needing to be aware of
// the EH ABI, exception types, etc. Returns true if the given function completed
// successfully and false otherwise.
global using unsafe errorTrapFunction = delegate* unmanaged[Cdecl]<void*, void>;
