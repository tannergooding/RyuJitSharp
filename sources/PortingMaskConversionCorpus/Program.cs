// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RyuJitSharp;

internal static class Program
{
    public static int Main()
    {
        Console.WriteLine($"AVX512F={Avx512F.IsSupported}; AVX10v1={Avx10v1.IsSupported}");

        if (!Avx512F.IsSupported)
        {
#pragma warning disable CA1303 // Fixed output identifies the ISA gate and verified execution.
            Console.WriteLine("Mask conversion corpus: unsupported ISA; no intrinsic bodies executed");
#pragma warning restore CA1303
            return 0;
        }

        if (MaskConversionCases.MaskRoundTrip(3) != 14 || MaskConversionCases.MaskRoundTrip(4) != 0)
        {
            return 1;
        }

        if (MaskConversionControls.MaskWithVectorUse(3) != 8 || MaskConversionControls.MaskWithVectorUse(4) != 0)
        {
            return 2;
        }

        if (MaskConversionControls.VectorMaskInput(3) != 7 || MaskConversionControls.VectorMaskInput(4) != 0)
        {
            return 3;
        }

#pragma warning disable CA1303 // Fixed output identifies the ISA gate and verified execution.
        Console.WriteLine("Mask conversion corpus: round-trip, vector-use, and vector-input results verified");
#pragma warning restore CA1303
        return 0;
    }
}

internal static class MaskConversionCases
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int MaskRoundTrip(int value)
    {
        var mask = Avx512F.CompareEqual(Vector512.Create(value), Vector512.Create(3));
        var compressed = Avx512F.Compress(Vector512<int>.Zero, mask, Vector512.Create(7));
        var expanded = Avx512F.Expand(Vector512<int>.Zero, mask, compressed);
        return compressed.GetElement(0) + expanded.GetElement(0);
    }
}

internal static class MaskConversionControls
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int MaskWithVectorUse(int value)
    {
        var mask = Avx512F.CompareEqual(Vector512.Create(value), Vector512.Create(3));
        var compressed = Avx512F.Compress(Vector512<int>.Zero, mask, Vector512.Create(7));
        return compressed.GetElement(0) + (mask.GetElement(0) == -1 ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int VectorMaskInput(int value)
    {
        var mask = Vector512.Create(value == 3 ? -1 : 0);
        var compressed = Avx512F.Compress(Vector512<int>.Zero, mask, Vector512.Create(7));
        return compressed.GetElement(0);
    }
}
