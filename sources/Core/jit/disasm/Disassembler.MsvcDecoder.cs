// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM && (TARGET_XARCH || TARGET_ARM64)
using System;
using System.IO;

namespace RyuJitSharp;

public partial struct Disassembler
{
    private const nuint MAX_CLASSNAME_LENGTH = 1024;
#if TARGET_ARM64
    private const int CCH_INDENT = 8;
#elif TARGET_AMD64
    private const int CCH_INDENT = 30;
#else
    private const int CCH_INDENT = 24;
#endif
    private static nuint s_cchBytesMax = nuint.MaxValue;

    private unsafe nuint CbDisassemble(MsDisassembler pdis, nuint offs, ulong addr,
        byte* pb, nuint cbMax, StreamWriter pfile, bool findLabels,
        bool printit = false, bool dispOffs = false, bool dispCodeBytes = false)
    {
        var cb = pdis.Disassemble(addr, pb, cbMax);
        if (cb == 0)
        {
            DumpMsCallback($"CbDisassemble offs {offs} addr {addr}\n");
            pfile.Write($"MSVCDIS can't disassemble instruction @ offset {offs} (0x{offs:x2})!!!\n");
#if TARGET_ARM64
            pfile.Write($"{*(uint*)pb:X8}h\n");

            return 4;
#else
            pfile.Write($"{*pb:X2}h\n");

            return 1;
#endif
        }

#if TARGET_ARM64
        assert(cb == 4);
#endif
        _curOffset = unchecked((nuint)addr);
        _instSize = cb;
        _target = unchecked((nuint)pdis.Target);

        if (findLabels)
        {
            switch (pdis.Termination)
            {
#if TARGET_XARCH
                case MsTermination.CallNear16:
                case MsTermination.CallNear32:
                case MsTermination.CallFar:
#else
                case MsTermination.Call:
                case MsTermination.CallCc:
#endif
                {
                    var absoluteAddr = (nuint)disGetLinearAddr(unchecked((nuint)pdis.AddressAddress(1)));
                    if (GetRelocationMap().ContainsKey(absoluteAddr))
                    {
                        break;
                    }

#if TARGET_XARCH
                    goto case MsTermination.JmpShort;
#else
                    goto case MsTermination.Bra;
#endif
                }

#if TARGET_XARCH
                case MsTermination.JmpShort:
                case MsTermination.JmpNear:
                case MsTermination.JmpFar:
                case MsTermination.JmpCcShort:
                case MsTermination.JmpCcNear:
#else
                case MsTermination.Bra:
                case MsTermination.BraCase:
                case MsTermination.BraCc:
                case MsTermination.BraCcCase:
#endif
                {
                    // Preserve the backend's addrNil sentinel; do not assume it is zero.
                    if ((ulong)_target != pdis.NilAddress && _target < _totalCodeSize)
                    {
                        SetLabel(_target, 1);
                    }
                    break;
                }

                case MsTermination.FallThrough:
                {
#if TARGET_ARM64
                    if (pdis.Decode(out var instruction))
                    {
                        if (instruction.IsAddress)
                        {
                            assert(instruction.OperandCount >= 2);
                            assert(instruction.Operand1IsImmediate);
                            assert(instruction.Operand1IsAddress);
                            _target = unchecked((nuint)instruction.Operand1);
                        }

                        if (_target < _totalCodeSize)
                        {
                            SetLabel(_target, 1);
                        }
                    }
#endif
                    break;
                }

                default:
                {
                    break;
                }
            }

            return cb;
        }

        if (printit && Label(unchecked((nuint)addr)) != 0)
        {
            pfile.Write($"L_{Label(unchecked((nuint)addr)):D2}:\n");
        }

        // Native formats even when printit is false (and callbacks may have side effects).
        var instructionText = pdis.FormatInstruction(MAX_CLASSNAME_LENGTH);
        if (printit)
        {
            if (dispOffs)
            {
                pfile.Write($"{offs:X3}");
            }

            var cchIndent = CCH_INDENT;
            if (dispCodeBytes)
            {
                if (s_cchBytesMax == nuint.MaxValue)
                {
                    s_cchBytesMax = pdis.FormatBytesMax();
                }

                assert(s_cchBytesMax < MAX_CLASSNAME_LENGTH);
                var bytes = pdis.FormatBytes(MAX_CLASSNAME_LENGTH, out var cchBytes);
                if (cchBytes > CCH_INDENT)
                {
                    // The native four-character memcpy includes the terminating null.
                    bytes = string.Concat(bytes.AsSpan(0, CCH_INDENT - 4), "...");
                    cchBytes = CCH_INDENT;
                }

                pfile.Write($"  {bytes}");
                cchIndent = CCH_INDENT - (int)cchBytes;
            }

            // %*c prints at least the character, even at width zero.
            pfile.Write($"{new string(' ', Math.Max(1, cchIndent))} {instructionText}\n");
        }

        return cb;
    }

    private static unsafe nuint CbDisassembleWithBytes(MsDisassembler pdis, ulong addr,
        byte* pb, nuint cbMax, StreamWriter pfile)
    {
        var addressText = pdis.FormatAddress(addr, MAX_CLASSNAME_LENGTH);
        // fprintf returns encoded byte count, not the number of UTF-16 characters.
        var cchIndent = pdis.WriteAddressPrefix(pfile, addressText);

        var cb = pdis.Disassemble(addr, pb, cbMax);
        if (cb == 0)
        {
            pfile.Write($"{*pb:X2}h\n");

            return 1;
        }

        var cchBytesMax = checked((int)nuint.Min(pdis.FormatBytesMax(), 18));
        var bytes = pdis.FormatBytes(64, out _);
        var first = true;
        while (true)
        {
            string line;
            string? next;
            if (bytes.Length <= cchBytesMax)
            {
                line = bytes;
                next = null;
            }
            else
            {
                var split = cchBytesMax;
                if (bytes[split] != ' ')
                {
                    // Native restores the temporarily inserted null before strrchr,
                    // so this deliberately searches the whole remaining string.
                    split = bytes.LastIndexOf(' ', StringComparison.Ordinal);
                    assert(split >= 0);
                }

                line = bytes[..split];
                next = bytes[(split + 1)..];
            }

            if (first)
            {
                var instruction = pdis.FormatInstruction(MAX_CLASSNAME_LENGTH);
                pfile.Write($"{line.PadRight(cchBytesMax)} {instruction}\n");
                first = false;
            }
            else
            {
                pfile.Write($"{new string(' ', Math.Max(1, Math.Abs(cchIndent)))}{line}\n");
            }

            if (next is null)
            {
                break;
            }

            bytes = next;
        }

        return cb;
    }

    private unsafe void DisasmBufferMsvc(StreamWriter pfile, bool printit)
    {
#if TARGET_X86
        var pdis = NewMsDisassembler(MsArchitecture.X86);
#elif TARGET_AMD64
        var pdis = NewMsDisassembler(MsArchitecture.X8664);
#else
        var pdis = NewMsDisassembler(MsArchitecture.Arm64);
#endif
        if (pdis is null)
        {
            assert(false, "out of memory in disassembler?");
            return;
        }

        DisasmBufferMsvc(pdis, pfile, printit);
    }

    private unsafe void DisasmBufferMsvc(MsDisassembler pdis, StreamWriter pfile, bool printit)
    {
#if TARGET_64BIT
        pdis.SetAddress64(true);
#endif
        pdis.SetClient(ref this);

        nuint ibCur = 0;
        ulong addr = 0;
        while (ibCur < _totalCodeSize)
        {
            var cb = CbDisassemble(pdis, ibCur, addr + (ulong)ibCur, disGetLinearAddr(ibCur),
                disGetBufferSize(ibCur), pfile, findLabels: true);
            ibCur = unchecked(ibCur + cb);
        }

        byte label = 0;
        for (uint i = 0; i < _totalCodeSize; i++)
        {
            if (Label(i) != 0)
            {
                label = unchecked((byte)(label + 1));
                SetLabel(i, label);
            }
        }

        ibCur = 0;
        addr = 0;
        if (printit)
        {
            pdis.SetAddressCallback(ref this);
            pdis.SetFixupCallback(ref this);
            pdis.SetRegisterRelativeCallback(ref this);
            pdis.SetRegisterCallback(ref this);
        }

        while (ibCur < _totalCodeSize)
        {
            var cb = CbDisassemble(pdis, ibCur, addr + (ulong)ibCur, disGetLinearAddr(ibCur),
                disGetBufferSize(ibCur), pfile, findLabels: false, printit, dispOffs: !_diffable,
#if DEBUG
                dispCodeBytes: !_diffable
#else
                dispCodeBytes: false
#endif
            );
            ibCur = unchecked(ibCur + (uint)cb);
        }

        pdis.Delete();
    }

    private readonly void SetLabel(nuint offset, byte value)
    {
        assert(_labels is not null);
        _labels[checked((int)offset)] = value;
    }
}
#endif
