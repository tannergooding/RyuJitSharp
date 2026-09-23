// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly struct VNFuncApp
{
    private readonly ReadOnlyMemory<ValueNum> _args;

    public VNFuncApp()
        : this(VNF_COUNT)
    {
    }

    public VNFuncApp(VNFunc func)
    {
        Func = func;
        _args = default;
    }

    internal VNFuncApp(VNFunc func, ReadOnlyMemory<ValueNum> args)
    {
        Func = func;
        _args = args;
    }

    public VNFunc Func { get; }

    public int Arity => _args.Length;

    public ValueNum GetArg(int index)
    {
        assert((uint)index < (uint)Arity);
        return _args.Span[index];
    }

    public bool FuncIs(VNFunc func) => Func == func;

    public bool FuncIs(VNFunc first, params ReadOnlySpan<VNFunc> rest)
    {
        if (FuncIs(first))
        {
            return true;
        }

        foreach (var func in rest)
        {
            if (FuncIs(func))
            {
                return true;
            }
        }

        return false;
    }

    public bool Equals(VNFuncApp other) => (Func == other.Func) && _args.Span.SequenceEqual(other._args.Span);
}
