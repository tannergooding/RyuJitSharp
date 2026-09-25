// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

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
                if (reg.IsFltReg)
                {
                    return emitZMMregName(reg);
                }
                break;
            }

            case EA_32BYTE:
            {
                if (reg.IsFltReg)
                {
                    return emitYMMregName(reg);
                }
                break;
            }

            case EA_16BYTE:
            case EA_8BYTE:
            {
                if (reg.IsFltReg)
                {
                    return emitXMMregName(reg);
                }
                break;
            }

            case EA_4BYTE:
            {
                if (reg.IsFltReg)
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
                if (reg.IsFltReg)
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
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Emitter register names outside xarch are not implemented.");
#endif
    }

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
