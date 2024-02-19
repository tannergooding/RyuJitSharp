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
    // Specific Architecture Info
    //

    public static readonly bool HOST_ARM32 = RuntimeInformation.ProcessArchitecture is Architecture.Arm;

    public static readonly bool HOST_ARM64 = RuntimeInformation.ProcessArchitecture is Architecture.Arm64;

    public static readonly bool HOST_LOONGARCH64 = RuntimeInformation.ProcessArchitecture is Architecture.LoongArch64;

    // TODO-NET9: RuntimeInformation.OSArchitecture is Architecture.RiscV64;
    public static readonly bool HOST_RISCV64;

    public static readonly bool HOST_X64 = RuntimeInformation.ProcessArchitecture is Architecture.X64;

    public static readonly bool HOST_X86 = RuntimeInformation.ProcessArchitecture is Architecture.X86;

    public static readonly bool TARGET_ARM32 = HOST_ARM32;

    public static readonly bool TARGET_ARM64 = HOST_ARM64;

    public static readonly bool TARGET_LOONGARCH64 = HOST_LOONGARCH64;

    public static readonly bool TARGET_RISCV64 = HOST_RISCV64;

    public static readonly bool TARGET_X64 = HOST_X64;

    public static readonly bool TARGET_X86 = HOST_X86;

    //
    // General Architecture Info
    //

    public static readonly bool HOST_64BIT = HOST_ARM64 || HOST_LOONGARCH64 || HOST_RISCV64 || HOST_X64;

    public static readonly bool HOST_ARMARCH = HOST_ARM32 || HOST_ARM64;

    public static readonly bool HOST_XARCH = HOST_X86 || HOST_X64;

    public static readonly bool TARGET_64BIT = TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_X64;

    public static readonly bool TARGET_ARMARCH = TARGET_ARM32 || TARGET_ARM64;

    public static readonly bool TARGET_XARCH = TARGET_X86 || TARGET_X64;

    //
    // Operating System Info
    //

    public static readonly bool HOST_LINUX = OperatingSystem.IsLinux();

    public static readonly bool HOST_OSX = OperatingSystem.IsMacOS();

    public static readonly bool HOST_UNIX = HOST_LINUX || HOST_OSX;

    public static readonly bool HOST_WINDOWS = OperatingSystem.IsWindows();

    public static readonly bool TARGET_LINUX = HOST_LINUX;

    public static readonly bool TARGET_OSX = HOST_OSX;

    public static readonly bool TARGET_UNIX = HOST_UNIX;

    public static readonly bool TARGET_UNIX_ANYOS;

    public static readonly bool TARGET_WINDOWS = HOST_WINDOWS;

    //
    // Other Info
    //

    public static readonly int TARGET_POINTER_SIZE = TARGET_64BIT ? 8 : 4;

    public static readonly bool TARGET_OS_RUNTIMEDETERMINED = (!TARGET_WINDOWS && !TARGET_UNIX) || TARGET_UNIX_ANYOS;

    public static readonly bool TARGET_UNIX_OS_RUNTIMEDETERMINED = TARGET_UNIX_ANYOS;

    public static readonly bool UNIX_X64_ABI = TARGET_UNIX && TARGET_X64;

    public static readonly bool UNIX_X86_ABI = TARGET_UNIX && TARGET_X86;
}
