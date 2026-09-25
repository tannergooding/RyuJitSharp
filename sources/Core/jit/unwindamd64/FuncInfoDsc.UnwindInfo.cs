// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct FuncInfoDsc
{
    public emitLocation? startLoc;
    public emitLocation? endLoc;
    public emitLocation? coldStartLoc;
    public emitLocation? coldEndLoc;

    public Amd64UnwindHeader unwindHeader;

    // offsetof(UNWIND_INFO, UnwindCode) + 255 * sizeof(UNWIND_CODE).
    public byte[]? unwindCodes;
    public uint unwindCodeSlot;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Amd64UnwindHeader
{
    private byte _versionAndFlags;
    public byte SizeOfProlog;
    public byte CountOfUnwindCodes;
    private byte _frameRegisterAndOffset;

    public byte Version
    {
        readonly get
        {
            return (byte)(_versionAndFlags & 7);
        }
        set
        {
            _versionAndFlags = (byte)((_versionAndFlags & 0xF8) | (value & 7));
        }
    }

    public byte Flags
    {
        readonly get
        {
            return (byte)(_versionAndFlags >> 3);
        }
        set
        {
            _versionAndFlags = (byte)((_versionAndFlags & 7) | ((value & 0x1F) << 3));
        }
    }

    public byte FrameRegister
    {
        readonly get
        {
            return (byte)(_frameRegisterAndOffset & 15);
        }
        set
        {
            _frameRegisterAndOffset = (byte)((_frameRegisterAndOffset & 0xF0) | (value & 15));
        }
    }

    public byte FrameOffset
    {
        readonly get
        {
            return (byte)(_frameRegisterAndOffset >> 4);
        }
        set
        {
            _frameRegisterAndOffset = (byte)((_frameRegisterAndOffset & 15) | ((value & 15) << 4));
        }
    }
}
