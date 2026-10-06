// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct JitPrimeInfo
{
    public uint prime;
    public uint magic;
    public uint shift;

    public JitPrimeInfo()
    {
        prime = 0;
        magic = 0;
        shift = 0;
    }

    public JitPrimeInfo(uint p, uint m, uint s)
    {
        prime = p;
        magic = m;
        shift = s;
    }

    public readonly uint magicNumberDivide(uint numerator)
    {
        ulong num = numerator;
        ulong mag = magic;
        ulong product = (num * mag) >> unchecked((int)(32 + shift));

        return (uint)product;
    }

    public readonly uint magicNumberRem(uint numerator)
    {
        uint div = magicNumberDivide(numerator);
        uint result = unchecked(numerator - (div * prime));
        assert(result == numerator % prime);

        return result;
    }
}
