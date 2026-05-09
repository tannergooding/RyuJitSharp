// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    private bool _contains256BitOrMoreAvxInstruction;
    private bool _containsAvxInstruction;
    private bool _containsCallNeedingVzeroupper;

    private bool _useEvexEncodings;
    private bool _usePromotedEvexEncodings;
    private bool _useRex2Encodings;
    private bool _useVexEncodings;

    public bool Contains256BitOrMoreAvxInstruction
    {
        get
        {
            return _contains256BitOrMoreAvxInstruction;
        }

        set
        {
            _contains256BitOrMoreAvxInstruction = value;
        }
    }

    public bool ContainsAvxInstruction
    {
        get
        {
            return _containsAvxInstruction;
        }

        set
        {
            _containsAvxInstruction = value;
        }
    }

    public bool ContainsCallNeedingVzeroupper
    {
        get
        {
            return _containsCallNeedingVzeroupper;
        }

        set
        {
            _containsCallNeedingVzeroupper = value;
        }
    }

    public bool UseEvexEncodings
    {
        get
        {
            return _useEvexEncodings;
        }

        set
        {
            assert(!value || UseVexEncodings);
            _useEvexEncodings = value;
        }
    }

    public bool UsePromotedEvexEncodings
    {
        get
        {
            return _usePromotedEvexEncodings;
        }

        set
        {
            assert(!value || UseEvexEncodings);
            _usePromotedEvexEncodings = value;
        }
    }

    public bool UseRex2Encodings
    {
        get
        {
            return _useRex2Encodings;
        }
        set
        {
            _useRex2Encodings = value;
        }
    }

    public bool UseVexEncodings
    {
        get
        {
            return _useVexEncodings;
        }

        set
        {
            _useVexEncodings = value;
        }
    }
}
#endif
