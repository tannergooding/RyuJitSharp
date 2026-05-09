// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using unsafe COMP_HANDLE = RyuJitSharp.ICorJitInfo*;

global using IL_OFFSET = int;

// Code can't be more than 2^31 in any direction.
// This is signed, so it should be used for anything that is relative to something else.
global using NATIVE_OFFSET = int;

// Type used for weights (e.g. block and edge weights)
global using weight_t = double ;
