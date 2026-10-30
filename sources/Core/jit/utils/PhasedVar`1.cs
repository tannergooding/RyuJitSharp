// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public struct PhasedVar<T>
{
    private T _value;

    /// <summary>true once the variable has been initialized, that is, written once.</summary>
    private bool _initialized;

    /// <summary>false if we are in the (initial) "write" phase.</summary>
    /// <remarks>Reading the value enters this phase until explicitly reset.</remarks>
    private bool _readPhase;

    public PhasedVar(T value)
    {
        _value = value;
        _initialized = false;
        _readPhase = false;

        if (Configuration.DEBUG)
        {
            _initialized = true;
            _readPhase = false;
        }
    }

    public readonly bool HasFinalValue => !Configuration.DEBUG || _readPhase;

    public T Value
    {
        get
        {
            if (Configuration.DEBUG)
            {
                if (_initialized)
                {
                    _readPhase = true;
                }
                else
                {
                    assert(Debugger.IsAttached);
                }
            }

            return _value;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                assert(!_readPhase);
                _initialized = true;
            }

            _value = value;
        }
    }

    public void MarkAsReadOnly()
    {
        if (Configuration.DEBUG)
        {
            assert(_initialized);
            _readPhase = true;
        }
    }

    public void ResetWritePhase()
    {
        if (Configuration.DEBUG)
        {
            _readPhase = false;
        }
    }
}
