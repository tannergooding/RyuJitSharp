// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

// Represents a Wasm BLOCK/END or LOOP/END span in the linearized basic-block list.
public sealed class WasmInterval
{
#if TARGET_WASM
    // interval kind
    public enum Kind
    {
        Block,
        Loop,
        Try,
    }

    // _chain refers to the conflict-set member with the lowest start index.
    private WasmInterval _chain;

    // True indices of the interval bounds; the interval ends just before _end.
    private readonly uint _start;
    private readonly uint _end;

    // Largest end index of any interval in the chain.
    private uint _chainEnd;
    private readonly Kind _kind;

    // True when this Block interval is the [exnref]-result wrapper paired with a Try.
    private bool _isExnRefWrapper;

    public WasmInterval(uint start, uint end, Kind kind)
    {
        _chain = this;
        _start = start;
        _end = end;
        _chainEnd = end;
        _kind = kind;
    }

    public uint Start()
    {
        return _start;
    }

    public uint End()
    {
        return _end;
    }

    public uint ChainEnd()
    {
        return _chainEnd;
    }

    // Call while resolving intervals when building chains.
    public WasmInterval FetchAndUpdateChain()
    {
        if (ReferenceEquals(_chain, this))
        {
            return this;
        }

        var chain = _chain.FetchAndUpdateChain();
        _chain = chain;
        return chain;
    }

    // Call after intervals are resolved and chains are fixed.
    public WasmInterval Chain()
    {
        assert(ReferenceEquals(_chain, this) || ReferenceEquals(_chain, _chain.Chain()));
        return _chain;
    }

    public bool IsBlock()
    {
        return _kind == Kind.Block;
    }

    public bool IsLoop()
    {
        return _kind == Kind.Loop;
    }

    public bool IsTry()
    {
        return _kind == Kind.Try;
    }

    public bool IsExnRefWrapper()
    {
        return _isExnRefWrapper;
    }

    public void SetChain(WasmInterval chain)
    {
        _chain = chain;
        chain._chainEnd = Math.Max(chain._chainEnd, _chainEnd);
    }

    // Managed allocation replaces the native compiler-arena placement for these intervals.
    public static WasmInterval NewBlock(BasicBlock start, BasicBlock end)
    {
        return new WasmInterval(unchecked((uint)start.bbPreorderNum), unchecked((uint)end.bbPreorderNum), Kind.Block);
    }

    public static WasmInterval NewLoop(BasicBlock start, BasicBlock end)
    {
        return new WasmInterval(unchecked((uint)start.bbPreorderNum), unchecked((uint)end.bbPreorderNum), Kind.Loop);
    }

    public static WasmInterval NewTry(BasicBlock start, BasicBlock end)
    {
        return new WasmInterval(unchecked((uint)start.bbPreorderNum), unchecked((uint)end.bbPreorderNum), Kind.Try);
    }

    // Construct the [exnref]-result wrapper paired with a Try interval.
    public static WasmInterval NewExnRefWrapper(BasicBlock start, BasicBlock end)
    {
        return new WasmInterval(
            unchecked((uint)start.bbPreorderNum), unchecked((uint)end.bbPreorderNum), Kind.Block)
        {
            _isExnRefWrapper = true,
        };
    }

#if DEBUG
    public void Dump(bool chainExtent = false)
    {
        jitprintf($"[{_start:D3},{(chainExtent ? _chainEnd : _end):D3}]");

        if (!chainExtent)
        {
            if (_kind == Kind.Loop)
            {
                jitprintf("L");
            }
            else if (_kind == Kind.Try)
            {
                jitprintf("T");
            }
            else if (_isExnRefWrapper)
            {
                jitprintf("X");
            }
        }

        if (!ReferenceEquals(_chain, this))
        {
            jitprintf(" --> ");
            _chain.Dump(chainExtent: true);
        }
        else
        {
            jitprintf("\n");
        }
    }

    public string KindString()
    {
        return _kind switch {
            Kind.Block => _isExnRefWrapper ? "ExnRefWrapper" : "Block",
            Kind.Loop => "Loop",
            Kind.Try => "Try",
            _ => "??",
        };
    }
#endif
#endif
}
