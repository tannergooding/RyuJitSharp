// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct PhasedVar<T>
{
    private T _value;

#if DEBUG
    /// <summary>true once the variable has been initialized, that is, written once.</summary>
    private bool _initialized;

    /// <summary>false if we are in the (initial) "write" phase.</summary>
    /// <remarks>Once the value is read, this changes to true, and can't be changed back.</remarks>
    private bool _readPhase;
#endif

    public T Value
    {
        get
        {
#if DEBUG
            assert(_initialized);
            _readPhase = true;
#endif

            return _value;
        }

        set
        {
#if DEBUG
            assert(!_readPhase);
            _initialized = true;
#endif

            _value = value;
        }
    }
}
