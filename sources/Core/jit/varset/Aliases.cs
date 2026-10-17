// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using VARSET_TP = nint[];

global using VarSetOps = RyuJitSharp.BitSetOpsWithCounter<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

global using ALLVARSET_TP = nint[];

global using AllVarSetOps = RyuJitSharp.BitSetOpsWithCounter<RyuJitSharp.Compiler, RyuJitSharp.AllVarBitSetTraits>;
