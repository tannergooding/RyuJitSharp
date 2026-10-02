// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class FloatingPointUtils
{
    // Keep both overloads aligned with the pinned Math.Round/MathF.Round algorithms.
    public static double round(double x)
    {
        // At 2^52, adjacent doubles are whole integers. Adding this signed offset
        // rounds the fractional part to even under the default rounding mode.
        const double IntegerBoundary = 4503599627370496.0;

        if (Math.Abs(x) >= IntegerBoundary)
        {
            return x;
        }

        var temp = Math.CopySign(IntegerBoundary, x);

        // Subtraction can lose the sign of zero; restore the original sign.
        return Math.CopySign((x + temp) - temp, x);
    }

    public static float round(float x)
    {
        // Based on nearbyint from amd/aocl-libm-ose, under the following BSD license:
        // Copyright (C) 2008-2022 Advanced Micro Devices, Inc. All rights reserved.
        //
        // Redistribution and use in source and binary forms, with or without modification,
        // are permitted provided that the following conditions are met:
        // 1. Redistributions of source code must retain the above copyright notice,
        //    this list of conditions and the following disclaimer.
        // 2. Redistributions in binary form must reproduce the above copyright notice,
        //    this list of conditions and the following disclaimer in the documentation
        //    and/or other materials provided with the distribution.
        // 3. Neither the name of the copyright holder nor the names of its contributors
        //    may be used to endorse or promote products derived from this software without
        //    specific prior written permission.
        //
        // THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
        // ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
        // WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
        // IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
        // INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
        // BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
        // OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
        // WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
        // ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
        // POSSIBILITY OF SUCH DAMAGE.

        // The same algorithm uses the single-precision integer boundary, 2^23.
        const float IntegerBoundary = 8388608.0f;

        if (MathF.Abs(x) >= IntegerBoundary)
        {
            return x;
        }

        var temp = MathF.CopySign(IntegerBoundary, x);

        return MathF.CopySign((x + temp) - temp, x);
    }

    public static double infinite_double()
    {
        return UInt64BitsToDouble(0x7FF0000000000000UL);
    }

    public static float infinite_float()
    {
        return UInt32BitsToSingle(0x7F800000U);
    }

    public static bool isAllBitsSet(float val)
    {
        var bits = SingleToUInt32Bits(val);

        return bits == 0xFFFFFFFFU;
    }

    public static bool isAllBitsSet(double val)
    {
        var bits = DoubleToUInt64Bits(val);

        return bits == 0xFFFFFFFFFFFFFFFFUL;
    }
}
