// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public ref struct PlatformClassifier
{
    private enum ClassifierKind : byte
    {
        Invalid,
        X86,
        WindowsAmd64,
        UnixAmd64,
        Arm64,
        Arm32,
        RiscV64,
        LoongArch64,
        Wasm,
    }

    private X86Classifier _x86;
    private WinX64Classifier _windowsAmd64;
    private SysVX64Classifier _unixAmd64;
    private Arm64Classifier _arm64;
    private Arm32Classifier _arm32;
    private RiscV64Classifier _riscV64;
    private LoongArch64Classifier _loongArch64;
    private WasmClassifier _wasm;
    private ClassifierKind _kind;

    public PlatformClassifier(in ClassifierInfo info)
    {
        this = default;

        if (Configuration.TARGET_XARCH && !Configuration.TARGET_64BIT)
        {
            _x86 = new X86Classifier(info);
            _kind = ClassifierKind.X86;
        }
        else if (Configuration.TARGET_AMD64 && Configuration.WINDOWS_AMD64_ABI)
        {
            _windowsAmd64 = new WinX64Classifier(info);
            _kind = ClassifierKind.WindowsAmd64;
        }
        else if (Configuration.UNIX_AMD64_ABI)
        {
            _unixAmd64 = new SysVX64Classifier(info);
            _kind = ClassifierKind.UnixAmd64;
        }
        else if (Configuration.TARGET_ARM64)
        {
            _arm64 = new Arm64Classifier(info);
            _kind = ClassifierKind.Arm64;
        }
        else if (Configuration.TARGET_ARM)
        {
            _arm32 = new Arm32Classifier(info);
            _kind = ClassifierKind.Arm32;
        }
        else if (Configuration.TARGET_RISCV64)
        {
            _riscV64 = new RiscV64Classifier(info);
            _kind = ClassifierKind.RiscV64;
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            _loongArch64 = new LoongArch64Classifier(info);
            _kind = ClassifierKind.LoongArch64;
        }
        else if (Configuration.TARGET_WASM)
        {
            _wasm = new WasmClassifier(info);
            _kind = ClassifierKind.Wasm;
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED, "No ABI classifier is available for this target.");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "An invalid ABI classifier kind indicates corrupt compiler state and must terminate compilation.")]
    public readonly int StackSize => _kind switch
    {
        ClassifierKind.X86 when Configuration.TARGET_XARCH && !Configuration.TARGET_64BIT => _x86.StackSize,
        ClassifierKind.WindowsAmd64 when Configuration.TARGET_AMD64 && Configuration.WINDOWS_AMD64_ABI =>
            _windowsAmd64.StackSize,
        ClassifierKind.UnixAmd64 when Configuration.UNIX_AMD64_ABI => _unixAmd64.StackSize,
        ClassifierKind.Arm64 when Configuration.TARGET_ARM64 => _arm64.StackSize,
        ClassifierKind.Arm32 when Configuration.TARGET_ARM => _arm32.StackSize,
        ClassifierKind.RiscV64 when Configuration.TARGET_RISCV64 => _riscV64.StackSize,
        ClassifierKind.LoongArch64 when Configuration.TARGET_LOONGARCH64 => _loongArch64.StackSize,
        ClassifierKind.Wasm when Configuration.TARGET_WASM => _wasm.StackSize,
        _ => throw new FatalJitException("Invalid ABI classifier kind."),
    };

    public unsafe AbiPassingInformation Classify(
        Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        switch (_kind)
        {
            case ClassifierKind.X86 when Configuration.TARGET_XARCH && !Configuration.TARGET_64BIT:
            {
                return _x86.Classify(comp, type, structLayout, wellKnownParam);
            }

            case ClassifierKind.WindowsAmd64 when Configuration.TARGET_AMD64 && Configuration.WINDOWS_AMD64_ABI:
            {
                return _windowsAmd64.Classify(comp, type, structLayout, wellKnownParam);
            }

            case ClassifierKind.UnixAmd64 when Configuration.UNIX_AMD64_ABI:
            {
                return _unixAmd64.Classify(comp, type, structLayout, wellKnownParam);
            }

            case ClassifierKind.Arm64 when Configuration.TARGET_ARM64:
            {
                return _arm64.Classify(comp, type, structLayout, wellKnownParam);
            }

            case ClassifierKind.Arm32 when Configuration.TARGET_ARM:
            {
                return _arm32.Classify(comp, type, structLayout, wellKnownParam);
            }

            case ClassifierKind.RiscV64 when Configuration.TARGET_RISCV64:
            {
                return _riscV64.Classify(comp, type, structLayout, wellKnownParam);
            }

            case ClassifierKind.LoongArch64 when Configuration.TARGET_LOONGARCH64:
            {
                return _loongArch64.Classify(comp, type, structLayout, wellKnownParam);
            }

            case ClassifierKind.Wasm when Configuration.TARGET_WASM:
            {
                return _wasm.Classify(comp, type, structLayout, wellKnownParam);
            }

            default:
            {
                throw new FatalJitException("Invalid ABI classifier kind.");
            }
        }
    }
}
