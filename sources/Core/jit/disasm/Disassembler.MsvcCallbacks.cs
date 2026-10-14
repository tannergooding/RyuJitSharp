// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;

namespace RyuJitSharp;

public partial struct Disassembler
{
    private nuint _curOffset;
    private nuint _instSize;
    private nuint _target;

    private unsafe nuint disCchAddrMember(MsDisassembler pdis, ulong addr, char* wz,
        nuint cchMax, ulong* pdwDisp)
    {
        nuint retval = 0;
        var terminationType = pdis.Termination;
        DumpMsCallback($"AddrMember {FormatRawPointer((nuint)addr)} " +
            $"({FormatRawPointer((nuint)disGetLinearAddr((nuint)addr))}), termType {unchecked((uint)pdis.NativeTermination)}\n");

        switch (terminationType)
        {
            case MsTermination.JmpShort:
            case MsTermination.JmpCcShort:
            case MsTermination.JmpNear:
            case MsTermination.JmpCcNear:
            case MsTermination.Bra:
            case MsTermination.BraCase:
            case MsTermination.BraCc:
            case MsTermination.BraCcCase:
            case MsTermination.BraCcInd:
            case MsTermination.BraInd:
            {
                if (_target < _totalCodeSize)
                {
                    var prefix = Configuration.TARGET_XARCH &&
                        terminationType is MsTermination.JmpShort or MsTermination.JmpCcShort
                            ? "short "
                            : string.Empty;
                    pdis.WriteCallbackText(wz, cchMax, $"{prefix}L_{Label(_target):D2}");
                    retval = 1;
                }
                break;
            }

            case MsTermination.CallNear16:
            case MsTermination.CallNear32:
            case MsTermination.Call:
            case MsTermination.CallCc:
            case MsTermination.CallCcInd:
            case MsTermination.CallInd:
            {
                if (!Configuration.TARGET_ARM64 &&
                    terminationType is MsTermination.CallCcInd or MsTermination.CallInd)
                {
                    goto default;
                }

                if (_target < _totalCodeSize)
                {
                    var prefix = Configuration.TARGET_XARCH &&
                        terminationType is MsTermination.CallNear16 or MsTermination.CallNear32
                            ? "short "
                            : string.Empty;
                    pdis.WriteCallbackText(wz, cchMax, $"{prefix}L_{Label(_target):D2}");
                    retval = 1;
                    break;
                }

                var absoluteTarget = (nuint)disGetLinearAddr(_target);
                var name = disGetMethodFullName(absoluteTarget);
                if (name is not null)
                {
                    pdis.WriteCallbackText(wz, cchMax, $"{dspAddr(absoluteTarget):x} {name}");
                    retval = 1;
                }
                break;
            }

            case MsTermination.FallThrough:
            {
                if (!Configuration.TARGET_AMD64 && !Configuration.TARGET_ARM64)
                {
                    goto default;
                }

                if (Configuration.TARGET_ARM64 && pdis.Decode(out var instruction) &&
                    instruction.IsAddress && addr < (ulong)_totalCodeSize)
                {
                    pdis.WriteCallbackText(wz, cchMax, $"L_{Label((nuint)addr):D2}");
                    retval = 1;
                }

                break;
            }

            default:
            {
                jitprintf($"Termination type is {pdis.NativeTermination}\n");
                assert(false, "treat this case");
                break;
            }
        }

        if (retval == 0)
        {
            if (_diffable)
            {
                pdis.WriteCallbackText(wz, cchMax,
                    dspAddr(1).ToString($"X{sizeof(nuint) * 2}", CultureInfo.InvariantCulture));
            }
        }
        else
        {
            *pdwDisp = 0;
        }

        return retval;
    }

    private unsafe nuint disCchFixupMember(MsDisassembler pdis, ulong addr, nuint size,
        char* wz, nuint cchMax, ulong* pdwDisp)
    {
        var terminationType = pdis.Termination;
        DumpMsCallback($"FixupMember {addr:X16} ({(nuint)disGetLinearAddr((nuint)addr):X8}), " +
            $"size {unchecked((int)size)}, termType {unchecked((uint)pdis.NativeTermination)}\n");
        var absoluteAddr = (nuint)disGetLinearAddr((nuint)addr);
        var anyReloc = GetRelocationMap().TryGetValue(absoluteAddr, out var targetAddr);

        switch (terminationType)
        {
            case MsTermination.Unknown:
            {
                if (Configuration.TARGET_ARM64)
                {
                    return 0;
                }

                goto default;
            }
            case MsTermination.FallThrough:
            {
                if (Configuration.TARGET_XARCH)
                {
                    assert(addr > pdis.Address);
                }

                if (anyReloc)
                {
                    if (Configuration.TARGET_ARM64)
                    {
                        assert(addr > pdis.Address);
                    }

                    pdis.WriteCallbackText(wz, cchMax, $"{dspAddr(targetAddr):X}h");
                    break;
                }

                return 0;
            }

            case MsTermination.JmpInd:
            case MsTermination.JmpShort:
            case MsTermination.JmpCcShort:
            case MsTermination.JmpNear:
            case MsTermination.JmpCcNear:
            case MsTermination.BraInd:
            case MsTermination.BraCcInd:
            case MsTermination.Bra:
            case MsTermination.BraCase:
            case MsTermination.BraCc:
            case MsTermination.BraCcCase:
            case MsTermination.Trap:
            case MsTermination.TrapCc:
            {
                return 0;
            }

            case MsTermination.CallNear16:
            case MsTermination.CallNear32:
            case MsTermination.Call:
            case MsTermination.CallCc:
            {
                if (anyReloc)
                {
                    var name = disGetMethodFullName(targetAddr);
                    if (name is not null)
                    {
                        pdis.WriteCallbackText(wz, cchMax, $"{dspAddr(targetAddr):x} {name}");
                        break;
                    }
                }

                return 0;
            }

            case MsTermination.CallInd:
            case MsTermination.CallCcInd:
            {
                if (terminationType == MsTermination.CallCcInd && !Configuration.TARGET_ARM64)
                {
                    goto default;
                }

                assert(addr > pdis.Address);
                _ = addr - pdis.Address;

                return 0;
            }

            default:
            {
                jitprintf($"Termination type is {pdis.NativeTermination}\n");
                assert(false, "treat this case");
                break;
            }
        }

        *pdwDisp = 0;

        return 1;
    }

    private unsafe nuint disCchRegRelMember(MsDisassembler pdis, uint reg, uint disp,
        char* wz, nuint cchMax, uint* pdwDisp)
    {
        var terminationType = pdis.Termination;
        DumpMsCallback($"RegRelMember reg {reg}, disp {disp}, termType {unchecked((uint)pdis.NativeTermination)}\n");

        switch (terminationType)
        {
            case MsTermination.FallThrough:
            case MsTermination.Trap:
            case MsTermination.TrapCc:
            {
                assert(_compiler is not null);
                assert(_compiler.codeGen is not null);
                var variable = _compiler.codeGen.siStackVarName(
                    unchecked((nuint)(pdis.Address - (ulong)_startAddr)), pdis.InstructionSize, reg, disp);
                if (variable is not null)
                {
                    pdis.WriteCallbackText(wz, cchMax, $"{pdis.RegisterName(reg)}+{disp:X}h '{variable}'");
                    *pdwDisp = 0;

                    return 1;
                }

                _ = Configuration.TARGET_XARCH && *disGetLinearAddr(_curOffset) == 0x66 ? 3 : 2;
                return 0;
            }

            case MsTermination.CallNear16:
            case MsTermination.CallNear32:
            case MsTermination.JmpInd:
            case MsTermination.Call:
            case MsTermination.CallCc:
            case MsTermination.BraInd:
            case MsTermination.BraCcInd:
            {
                break;
            }

            case MsTermination.CallInd:
            case MsTermination.CallCcInd:
            {
                if (terminationType == MsTermination.CallCcInd && !Configuration.TARGET_ARM64)
                {
                    goto default;
                }

                if (unchecked((sbyte)disp) == unchecked((int)disp))
                {
                    _ = 2;

                    return 0;
                }

                if (_hasName)
                {
                    // The pinned callbacks never populate disFuncTempBuf. Preserve that raw
                    // storage at the native client boundary rather than inventing a name.
                    var name = pdis.ReadDeferredFunctionName(ref this);
                    pdis.WriteCallbackText(wz, cchMax, $"{pdis.RegisterName(reg)}+{disp} '{name}'");
                    *pdwDisp = 0;
                    _hasName = false;

                    return 1;
                }

                return 0;
            }

            default:
            {
                jitprintf($"Termination type is {pdis.NativeTermination}\n");
                assert(false, "treat this case");
                break;
            }
        }

        *pdwDisp = disp;

        return 1;
    }

    private readonly byte Label(nuint offset)
    {
        assert(_labels is not null);

        return _labels[checked((int)offset)];
    }

    private readonly void DumpMsCallback(string text)
    {
        if (Configuration.DISASM_DEBUG)
        {
            if (Configuration.DEBUG)
            {
                assert(_compiler is not null);
            }

            if (!Configuration.DEBUG || _compiler.verbose)
            {
                jitprintf(text);
            }
        }
    }

    private static unsafe string FormatRawPointer(nuint address)
    {
        return address.ToString($"X{sizeof(nuint) * 2}", CultureInfo.InvariantCulture);
    }
}
