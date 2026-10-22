// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    public string emitRegName(regNumber reg, bool varName = true) =>
        emitRegName(reg, EA_PTRSIZE, varName);

    public string emitRegName(regNumber reg, emitAttr attr, bool varName = true)
    {
        if (Configuration.TARGET_XARCH)
        {
            var name = codeGen.Compiler.compRegVarName(reg, varName);
            if (reg.IsMskReg)
            {
                return name;
            }

            if (Configuration.TARGET_X86)
            {
                assert(name.Length >= 3);
            }

            if (Configuration.TARGET_AMD64 && reg.IsIntReg && (reg > REG_X64_RDI) &&
                (Globals.EA_SIZE(attr) is EA_1BYTE or EA_2BYTE or EA_4BYTE))
            {
                assert(name.Length is 2 or 3);
            }

            switch (Globals.EA_SIZE(attr))
            {
                case EA_64BYTE:
                {
                    if (IsXMMReg(reg))
                    {
                        return emitZMMregName(reg);
                    }
                    break;
                }

                case EA_32BYTE:
                {
                    if (IsXMMReg(reg))
                    {
                        return emitYMMregName(reg);
                    }
                    break;
                }

                case EA_16BYTE:
                case EA_8BYTE:
                {
                    if (IsXMMReg(reg))
                    {
                        return emitXMMregName(reg);
                    }
                    break;
                }

                case EA_4BYTE:
                {
                    if (IsXMMReg(reg))
                    {
                        return emitXMMregName(reg);
                    }
                    assert(reg.IsIntReg);
                    if (Configuration.TARGET_AMD64)
                    {
                        name = reg > REG_X64_RDI ? name + "d" : string.Concat("e", name.AsSpan(1, 2));
                    }
                    break;
                }

                case EA_2BYTE:
                {
                    if (IsXMMReg(reg))
                    {
                        return emitXMMregName(reg);
                    }
                    if (Configuration.TARGET_AMD64 && reg > REG_X64_RDI)
                    {
                        name += "w";
                        break;
                    }
                    name = name[1..];
                    break;
                }

                case EA_1BYTE:
                {
                    if (Configuration.TARGET_AMD64)
                    {
                        name = reg > REG_X64_RDI ? name + "b" :
                            (int)reg < 4 ? string.Concat(name.AsSpan(1, 1), "l") :
                            string.Concat(name.AsSpan(1, 2), "l");
                    }
                    else
                    {
                        name = string.Concat(name.AsSpan(1, 1), "l", name.AsSpan(3));
                    }
                    break;
                }
            }

            return name;
        }

        if (Configuration.TARGET_ARM)
        {
            assert((uint)reg < (uint)REG_COUNT);
            var compiler = _compiler;
            if (compiler is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterDisplayRequiresAnActiveCompiler);
                return default!;
            }
            var name = compiler.compRegVarName(reg, varName, false);
            assert(name.Length >= 1);
            return name;
        }

        if (Configuration.TARGET_ARM64)
        {
            assert((uint)reg < (uint)REG_COUNT);
            var size = EA_SIZE(attr);

            if (reg == TargetRuntimeMetadata.StackPointerBaseRegister)
            {
                switch (size)
                {
                    case EA_8BYTE:
                    {
                        return "sp";
                    }

                    case EA_4BYTE:
                    {
                        return "wsp";
                    }

                    default:
                    {
                        FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InvalidARM64StackRegisterSizeArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? size : default)
    );
                        return default!;
                    }
                }
            }

            if ((reg >= TargetRuntimeMetadata.Register0) && (reg <= TargetRuntimeMetadata.ZeroRegister))
            {
                if (reg == TargetRuntimeMetadata.ZeroRegister)
                {
                    switch (size)
                    {
                        case EA_8BYTE:
                        {
                            return "xzr";
                        }

                        case EA_4BYTE:
                        {
                            return "wzr";
                        }

                        default:
                        {
                            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InvalidARM64ZeroRegisterSizeArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? size : default)
    );
                            return default!;
                        }
                    }
                }

                var index = ((int)reg).ToString(CultureInfo.InvariantCulture);
                switch (size)
                {
                    case EA_8BYTE:
                    {
                        return string.Concat("x", index);
                    }

                    case EA_4BYTE:
                    {
                        return string.Concat("w", index);
                    }

                    default:
                    {
                        FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InvalidARM64GeneralRegisterSizeArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? size : default)
    );
                        return default!;
                    }
                }
            }

            if (!isVectorRegister(reg))
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InvalidARM64RegisterArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? reg : default)
    );
                return default!;
            }

            var vectorIndex = ((int)reg - (int)REG_ARM64_V0).ToString(CultureInfo.InvariantCulture);
            switch (size)
            {
                case EA_8BYTE:
                {
                    return string.Concat("d", vectorIndex);
                }

                case EA_4BYTE:
                {
                    return string.Concat("s", vectorIndex);
                }

                case EA_16BYTE:
                {
                    return string.Concat("q", vectorIndex);
                }

                case EA_2BYTE:
                {
                    return string.Concat("h", vectorIndex);
                }

                case EA_1BYTE:
                {
                    return string.Concat("b", vectorIndex);
                }

                case EA_SCALABLE_ARM64:
                {
                    return string.Concat("z", vectorIndex);
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InvalidARM64VectorRegisterSizeArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? size : default)
    );
                    return default!;
                }
            }
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (Configuration.DEBUG)
            {
                return emitRegNameRiscV64(reg, attr, varName);
            }

            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.EmitterRegisterNamesOutsideXarchAreNotImplemented
    );
            return default!;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            assert((uint)reg < (uint)REG_COUNT);
            return reg.Name;
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.EmitterRegisterNamesOutsideXarchAreNotImplemented
    );
        return default!;
    }

    public string emitVectorRegName(regNumber reg)
    {
        if (!Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.VectorRegisterNamesAreOnlySupportedOnARM64);
            return default!;
        }

        assert((reg >= REG_ARM64_V0) && (reg <= REG_ARM64_V31));
        var index = ((int)reg - (int)REG_ARM64_V0).ToString(CultureInfo.InvariantCulture);
        return string.Concat("v", index);
    }

    private string emitFloatRegName(regNumber reg, bool varName = true) =>
        emitFloatRegName(reg, EA_PTRSIZE, varName);

    private string emitFloatRegName(regNumber reg, emitAttr attr, bool varName = true)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.FloatingRegisterNamesAreOnlySupportedOnARM32);
            return default!;
        }

        assert((uint)reg < (uint)REG_COUNT);
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.FloatingRegisterDisplayRequiresAnActiveCompiler);
            return default!;
        }
        var name = compiler.compRegVarName(reg, varName, true);
        assert(name.Length >= 1);
        return name;
    }

    public static string emitXMMregName(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.XMMRegisterNamesAreOnlySupportedOnXarch);
            return default!;
        }

        assert(reg < REG_COUNT);
        return "x" + reg.Name;
    }

    public static string emitYMMregName(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.YMMRegisterNamesAreOnlySupportedOnXarch);
            return default!;
        }

        assert(reg < REG_COUNT);
        return "y" + reg.Name;
    }

    public static string emitZMMregName(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.ZMMRegisterNamesAreOnlySupportedOnXarch);
            return default!;
        }

        assert(reg < REG_COUNT);
        return "z" + reg.Name;
    }
}
