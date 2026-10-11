// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Target
{
    public static string TgtPlatformName => TargetOS.IsWindows ? "Windows" : "Unix";

    public static string TgtCpuName
    {
        get
        {
            if (TargetArchitecture.IsX86)
            {
                return "x86";
            }
            else if (TargetArchitecture.IsX64)
            {
                return "x64";
            }
            else if (TargetArchitecture.IsArm32)
            {
                return "arm";
            }
            else if (TargetArchitecture.IsArm64)
            {
                return "arm64";
            }
            else if (TargetArchitecture.IsLoongArch64)
            {
                return "loongarch64";
            }
            else if (TargetArchitecture.IsRiscV64)
            {
                return "riscv64";
            }
            else if (TargetArchitecture.IsWasm)
            {
                if (!TargetArchitecture.Is64Bit)
                {
                    return "wasm32";
                }

                throw new NotSupportedException("NYI: WASM64");
            }

            throw new NotSupportedException("Unsupported or unset target architecture.");
        }
    }

    public static ArgOrder TgtArgOrder => TargetArchitecture.IsX86 ? ARG_ORDER_L2R : ARG_ORDER_R2L;

    public static ArgOrder TgtUnmanagedArgOrder => ARG_ORDER_R2L;
}
