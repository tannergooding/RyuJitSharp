// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
global using simd_t = RyuJitSharp.simd64_t;
#elif TARGET_ARM64
global using simd_t = RyuJitSharp.simd32_t;
#else
global using simd_t = RyuJitSharp.simd16_t;
#endif
