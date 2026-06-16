// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

/// <summary>keeps track of temporaries allocated in the stack frame during code-generation (after register allocation). These spill-temps are only used if we run out of registers while evaluating a tree.</summary>
/// <remarks>These are different from the more common temps allocated by lvaGrabTemp</remarks>
public sealed class TempDsc
{
#if DEBUG
    // used as a sentinel "bad value" for tdOffs in DEBUG
    private const int BAD_TEMP_OFFSET = unchecked((int)(0xDDDDDDDD));
#endif

    public TempDsc? tdNext;

    private int _tdOffs;

    private int _tdNum;

    private byte _tdSize;

    private var_types _tdType;

    public TempDsc(int tdNum, byte tdSize, var_types tdType)
    {
        // temps must have a negative number (so they have a different number from all local variables)
        assert(tdNum < 0);

#if DEBUG
        _tdOffs = BAD_TEMP_OFFSET;
#endif

        _tdNum = tdNum;
        _tdSize = tdSize;
        _tdType = tdType;
    }

#if DEBUG
    public bool tdLegalOffset => _tdOffs != BAD_TEMP_OFFSET;
#endif

    public int tdTempNum => _tdNum;

    public int tdTempOffs
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || tdLegalOffset);
#endif
            return _tdOffs;
        }

        set
        {
            _tdOffs = value;

#if DEBUG
            assert(tdLegalOffset);
#endif
        }
    }

    public byte tdTempSize => _tdSize;

    public var_types tdTempType => _tdType;

    public void tdAdjustTempOffs(int offs)
    {
#if TARGET_ARM64
        // Cannot adjust temporary offsets on the UnknownSizeFrame.
        assert(!varTypeHasUnknownSize(tdType));
#endif

        tdTempOffs = _tdOffs + offs;

#if DEBUG
        assert(tdLegalOffset);
#endif
    }
}
