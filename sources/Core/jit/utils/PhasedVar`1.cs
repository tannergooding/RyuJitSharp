// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public struct PhasedVar<T>
{
    private T _value;

#if DEBUG
    /// <summary>true once the variable has been initialized, that is, written once.</summary>
    private bool _initialized;

    /// <summary>false if we are in the (initial) "write" phase.</summary>
    /// <remarks>Reading the value enters this phase until explicitly reset.</remarks>
    private bool _readPhase;
#endif

#if DEBUG
    public readonly bool HasFinalValue => _readPhase == true;
#else
    public readonly bool HasFinalValue => true;
#endif

    public T Value
    {
#if DEBUG
        get
        {
            if (_initialized)
            {
                _readPhase = true;
            }
            else
            {
                assert(Debugger.IsAttached);
            }
            return _value;
        }
#else
        readonly get
        {
            return _value;
        }
#endif

        set
        {
#if DEBUG
            assert(!_readPhase);
            _initialized = true;
#endif

            _value = value;
        }
    }

#if DEBUG
    public void MarkAsReadOnly()
#else
    public readonly void MarkAsReadOnly()
#endif
    {
#if DEBUG
        assert(_initialized);
        _readPhase = true;
#endif
    }

#if DEBUG
    public void ResetWritePhase()
#else
    public readonly void ResetWritePhase()
#endif
    {
#if DEBUG
        _readPhase = false;
#endif
    }
}
