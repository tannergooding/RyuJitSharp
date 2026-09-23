// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

// Random polymorphism is retained for existing JIT consumers, but seeded draws use CLRRandom's own state.
public sealed partial class CLRRandom : Random
{
    private const int MBIG = int.MaxValue;
    private const int MSEED = 161803398;

    private readonly int[] SeedArray = new int[56];

    private int inext;
    private int inextp;
    private bool initialized;

    public CLRRandom()
        : base(0)
    {
    }

    public CLRRandom(int seed)
        : base(0)
    {
        Init(seed);
    }

    public void Init()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unseeded CLRRandom.Init requires the Windows native thread ID.");
        }

        Init(unchecked((int)Stopwatch.GetTimestamp() ^ (int)GetCurrentThreadId() ^ Environment.ProcessId));
    }

    public void Init(int seed)
    {
        // MSVC's abs(INT_MIN) returns INT_MIN; System.Random instead treats it as INT_MAX.
        var mj = unchecked(MSEED - ((seed == int.MinValue) ? seed : Math.Abs(seed)));
        SeedArray[55] = mj;
        var mk = 1;

        for (var i = 1; i < 55; i++)
        {
            var ii = (21 * i) % 55;
            SeedArray[ii] = mk;
            mk = unchecked(mj - mk);
            if (mk < 0)
            {
                mk = unchecked(mk + MBIG);
            }
            mj = SeedArray[ii];
        }

        for (var k = 1; k < 5; k++)
        {
            for (var i = 1; i < 56; i++)
            {
                SeedArray[i] = unchecked(SeedArray[i] - SeedArray[1 + ((i + 30) % 55)]);
                if (SeedArray[i] < 0)
                {
                    SeedArray[i] = unchecked(SeedArray[i] + MBIG);
                }
            }
        }

        inext = 0;
        inextp = 21;
        initialized = true;
    }

    public bool IsInitialized() => initialized;

    protected override double Sample() => InternalSample() * (1.0 / MBIG);

    private int InternalSample()
    {
        if (!initialized)
        {
            throw new InvalidOperationException("CLRRandom must be initialized before drawing values.");
        }

        var locINext = inext;
        var locINextp = inextp;

        if (++locINext >= 56)
        {
            locINext = 1;
        }
        if (++locINextp >= 56)
        {
            locINextp = 1;
        }

        var retVal = unchecked(SeedArray[locINext] - SeedArray[locINextp]);

        if (retVal == MBIG)
        {
            retVal--;
        }
        if (retVal < 0)
        {
            retVal = unchecked(retVal + MBIG);
        }

        SeedArray[locINext] = retVal;
        inext = locINext;
        inextp = locINextp;

        return retVal;
    }

    private double GetSampleForLargeRange()
    {
        var result = InternalSample();
        var negative = (InternalSample() % 2) == 0;

        if (negative)
        {
            result = -result;
        }

        var value = (double)result;
        value += MBIG - 1;
        value /= (2U * (uint)MBIG) - 1U;

        return value;
    }

    public override int Next() => InternalSample();

    public override int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minValue, maxValue);

        var range = (long)maxValue - minValue;
        var result = ((range <= MBIG) ? Sample() : GetSampleForLargeRange()) * range + minValue;
        assert((result >= minValue) && (result < maxValue));

        return (int)result;
    }

    public override int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxValue);

        var result = Sample() * maxValue;
        assert((result >= 0) && (result < maxValue));

        return (int)result;
    }

    public override double NextDouble()
    {
        var result = Sample();
        assert((result >= 0) && (result < 1));

        return result;
    }

    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        NextBytes(buffer.AsSpan());
    }

    public void NextBytes(byte[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        NextBytes(buffer.AsSpan(0, length));
    }

    public override void NextBytes(Span<byte> buffer)
    {
        if (!initialized)
        {
            throw new InvalidOperationException("CLRRandom must be initialized before drawing values.");
        }

        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(InternalSample() % 256);
        }
    }

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint GetCurrentThreadId();
}
