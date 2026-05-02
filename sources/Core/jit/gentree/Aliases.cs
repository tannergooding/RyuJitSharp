// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using AssertionIndex = ushort;

global using unsafe VtablePtr = void*;

global using unsafe GenTreeUseEdgeIterator_AdvanceFn = delegate* unmanaged[Cdecl]<void>;

global using MultiRegSpillFlags = byte;
