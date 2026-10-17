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
            var compiler = _compiler ?? throw new FatalJitException("Register display requires an active compiler.");
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
                return size switch
                {
                    EA_8BYTE => "sp",
                    EA_4BYTE => "wsp",
                    _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Invalid ARM64 stack register size {size}."),
                };
            }

            if ((reg >= TargetRuntimeMetadata.Register0) && (reg <= TargetRuntimeMetadata.ZeroRegister))
            {
                if (reg == TargetRuntimeMetadata.ZeroRegister)
                {
                    return size switch
                    {
                        EA_8BYTE => "xzr",
                        EA_4BYTE => "wzr",
                        _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Invalid ARM64 zero register size {size}."),
                    };
                }

                var index = ((int)reg).ToString(CultureInfo.InvariantCulture);
                return size switch
                {
                    EA_8BYTE => string.Concat("x", index),
                    EA_4BYTE => string.Concat("w", index),
                    _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Invalid ARM64 general register size {size}."),
                };
            }

            if (!isVectorRegister(reg))
            {
                throw new FatalJitException(CORJIT_INTERNALERROR, $"Invalid ARM64 register {reg}.");
            }

            var vectorIndex = ((int)reg - (int)REG_ARM64_V0).ToString(CultureInfo.InvariantCulture);
            return size switch
            {
                EA_8BYTE => string.Concat("d", vectorIndex),
                EA_4BYTE => string.Concat("s", vectorIndex),
                EA_16BYTE => string.Concat("q", vectorIndex),
                EA_2BYTE => string.Concat("h", vectorIndex),
                EA_1BYTE => string.Concat("b", vectorIndex),
                EA_SCALABLE_ARM64 => string.Concat("z", vectorIndex),
                _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Invalid ARM64 vector register size {size}."),
            };
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (Configuration.DEBUG)
            {
                return emitRegNameRiscV64(reg, attr, varName);
            }

            throw new FatalJitException(CORJIT_SKIPPED, "Emitter register names outside xarch are not implemented.");
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            assert((uint)reg < (uint)REG_COUNT);
            return reg.Name;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Emitter register names outside xarch are not implemented.");
    }

    public string emitVectorRegName(regNumber reg)
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Vector register names are only supported on ARM64.");
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
            throw new FatalJitException(CORJIT_SKIPPED, "Floating register names are only supported on ARM32.");
        }

        assert((uint)reg < (uint)REG_COUNT);
        var compiler = _compiler ?? throw new FatalJitException("Floating register display requires an active compiler.");
        var name = compiler.compRegVarName(reg, varName, true);
        assert(name.Length >= 1);
        return name;
    }

    public static string emitXMMregName(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "XMM register names are only supported on xarch.");
        }

        assert(reg < REG_COUNT);
        return "x" + reg.Name;
    }

    public static string emitYMMregName(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "YMM register names are only supported on xarch.");
        }

        assert(reg < REG_COUNT);
        return "y" + reg.Name;
    }

    public static string emitZMMregName(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "ZMM register names are only supported on xarch.");
        }

        assert(reg < REG_COUNT);
        return "z" + reg.Name;
    }
}
