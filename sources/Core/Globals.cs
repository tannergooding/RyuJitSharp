// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.Globals;
using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public static partial class Globals
{
    //
    // Specific Architecture Targets
    //

    public static readonly bool TARGET_ARM = RuntimeInformation.ProcessArchitecture is Architecture.Arm;

    public static readonly bool TARGET_ARM64 = RuntimeInformation.ProcessArchitecture is Architecture.Arm64;

    public static readonly bool TARGET_LOONGARCH64 = RuntimeInformation.ProcessArchitecture is Architecture.LoongArch64;

    // TODO-NET9: RuntimeInformation.OSArchitecture is Architecture.RiscV64;
    public static readonly bool TARGET_RISCV64;

    public static readonly bool TARGET_X64 = RuntimeInformation.ProcessArchitecture is Architecture.X64;

    public static readonly bool TARGET_X86 = RuntimeInformation.ProcessArchitecture is Architecture.X86;

    //
    // General Architecture Targets
    //

    public static readonly bool TARGET_64BIT = TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_X64;

    public static readonly bool TARGET_ARMARCH = TARGET_ARM || TARGET_ARM64;

    public static readonly bool TARGET_XARCH = TARGET_X86 || TARGET_X64;

    //
    // Operating System Targets
    //

    public static readonly bool TARGET_UNIX = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    //
    // ABI Checks
    //

    public static readonly bool UNIX_X86_ABI = TARGET_UNIX && TARGET_X86;

    //
    // Target Information
    //

    public static readonly int TARGET_POINTER_SIZE = TARGET_64BIT ? 8 : 4;
}
