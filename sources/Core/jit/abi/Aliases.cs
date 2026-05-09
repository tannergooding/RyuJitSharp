// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
global using PlatformClassifier = RyuJitSharp.X86Classifier;
#elif WINDOWS_AMD64_ABI
global using PlatformClassifier = RyuJitSharp.WinX64Classifier;
#elif UNIX_AMD64_ABI
global using PlatformClassifier = RyuJitSharp.SysVX64Classifier;
#elif TARGET_ARM64
global using PlatformClassifier = RyuJitSharp.Arm64Classifier;
#elif TARGET_ARM
global using PlatformClassifier = RyuJitSharp.Arm32Classifier;
#elif TARGET_RISCV64
global using PlatformClassifier = RyuJitSharp.RiscV64Classifier;
#elif TARGET_LOONGARCH64
global using PlatformClassifier = RyuJitSharp.LoongArch64Classifier;
#elif TARGET_WASM
global using PlatformClassifier = RyuJitSharp.WasmClassifier;
#endif
