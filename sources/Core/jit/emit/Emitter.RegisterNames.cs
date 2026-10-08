// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    public string emitRegName(regNumber reg, emitAttr attr = EA_PTRSIZE, bool varName = true)
    {
#if TARGET_XARCH
        var name = codeGen.Compiler.compRegVarName(reg, varName);
        if (reg.IsMskReg)
        {
            return name;
        }
#if TARGET_X86
        assert(name.Length >= 3);
#endif
#if TARGET_AMD64
        if (reg.IsIntReg && (reg > REG_RDI) &&
            ((attr & EA_SIZE_MASK) is EA_1BYTE or EA_2BYTE or EA_4BYTE))
        {
            assert(name.Length is 2 or 3);
        }
#endif
        switch (attr & EA_SIZE_MASK)
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
#if TARGET_AMD64
                name = reg > REG_RDI ? name + "d" : string.Concat("e", name.AsSpan(1, 2));
#endif
                break;
            }

            case EA_2BYTE:
            {
                if (IsXMMReg(reg))
                {
                    return emitXMMregName(reg);
                }
#if TARGET_AMD64
                if (reg > REG_RDI)
                {
                    name += "w";
                    break;
                }
#endif
                name = name[1..];
                break;
            }

            case EA_1BYTE:
            {
#if TARGET_AMD64
                name = reg > REG_RDI ? name + "b" :
                    (int)reg < 4 ? string.Concat(name.AsSpan(1, 1), "l") : string.Concat(name.AsSpan(1, 2), "l");
#else
                name = string.Concat(name.AsSpan(1, 1), "l", name.AsSpan(3));
#endif
                break;
            }
        }

        return name;
#elif TARGET_ARM
        assert((uint)reg < (uint)REG_COUNT);
        var compiler = _compiler ?? throw new FatalJitException("Register display requires an active compiler.");
        var name = compiler.compRegVarName(reg, varName, false);
        assert(name.Length >= 1);
        return name;
#elif TARGET_ARM64
        assert((uint)reg < (uint)REG_COUNT);
        var size = EA_SIZE(attr);

        if (reg == REG_SP)
        {
            return size switch
            {
                EA_8BYTE => "sp",
                EA_4BYTE => "wsp",
                _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Invalid ARM64 stack register size {size}."),
            };
        }

        if ((reg >= REG_R0) && (reg <= REG_ZR))
        {
            if (reg == REG_ZR)
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

        var vectorIndex = ((int)reg - (int)REG_V0).ToString(CultureInfo.InvariantCulture);
        return size switch
        {
            EA_8BYTE => string.Concat("d", vectorIndex),
            EA_4BYTE => string.Concat("s", vectorIndex),
            EA_16BYTE => string.Concat("q", vectorIndex),
            EA_2BYTE => string.Concat("h", vectorIndex),
            EA_1BYTE => string.Concat("b", vectorIndex),
            EA_SCALABLE => string.Concat("z", vectorIndex),
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Invalid ARM64 vector register size {size}."),
        };
#elif TARGET_RISCV64 && DEBUG
        return emitRegNameRiscV64(reg, attr, varName);
#elif TARGET_LOONGARCH64
        assert((uint)reg < (uint)REG_COUNT);
        return reg.Name;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Emitter register names outside xarch are not implemented.");
#endif
    }

#if TARGET_ARM64
    public string emitVectorRegName(regNumber reg)
    {
        assert((reg >= REG_V0) && (reg <= REG_V31));
        var index = ((int)reg - (int)REG_V0).ToString(CultureInfo.InvariantCulture);
        return string.Concat("v", index);
    }
#endif

#if TARGET_ARM
    private string emitFloatRegName(regNumber reg, emitAttr attr = EA_PTRSIZE, bool varName = true)
    {
        assert((uint)reg < (uint)REG_COUNT);
        var compiler = _compiler ?? throw new FatalJitException("Floating register display requires an active compiler.");
        var name = compiler.compRegVarName(reg, varName, true);
        assert(name.Length >= 1);
        return name;
    }
#endif

#if TARGET_XARCH
    public static string emitXMMregName(regNumber reg)
    {
        assert(reg < REG_COUNT);
        return "x" + reg.Name;
    }

    public static string emitYMMregName(regNumber reg)
    {
        assert(reg < REG_COUNT);
        return "y" + reg.Name;
    }

    public static string emitZMMregName(regNumber reg)
    {
        assert(reg < REG_COUNT);
        return "z" + reg.Name;
    }
#endif
}
