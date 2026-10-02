// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LclVarDscVectorPerElementMaskTests
{
    private const long Marker = 1L << 51;
    private const int ElementSizeShift = 52;
    private const long ElementSizeMask = 3L << ElementSizeShift;

    private static readonly FieldInfo s_flags = typeof(LclVarDsc).GetField("_flags", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new AssertionException("Missing local descriptor flags.");

    private static readonly (var_types Type, int SizeLog2)[] s_elementTypes = [
        (TYP_BYTE, 0), (TYP_UBYTE, 0), (TYP_SHORT, 1), (TYP_USHORT, 1),
        (TYP_INT, 2), (TYP_UINT, 2), (TYP_FLOAT, 2),
        (TYP_LONG, 3), (TYP_ULONG, 3), (TYP_DOUBLE, 3),
    ];

    [TestCase(TYP_BYTE, 0)]
    [TestCase(TYP_UBYTE, 0)]
    [TestCase(TYP_SHORT, 1)]
    [TestCase(TYP_USHORT, 1)]
    [TestCase(TYP_INT, 2)]
    [TestCase(TYP_UINT, 2)]
    [TestCase(TYP_FLOAT, 2)]
    [TestCase(TYP_LONG, 3)]
    [TestCase(TYP_ULONG, 3)]
    [TestCase(TYP_DOUBLE, 3)]
    public static void MaskCompatibilityUsesTheNativeElementWidthAndPreservesOtherFlags(var_types storedType, int sizeLog2)
    {
        LclVarDsc descriptor = new() { Type = TYP_STRUCT, IsSpan = true, lvTracked = true, lvSingleDef = true };
        var originalFlags = GetFlags(descriptor);
        foreach (var (type, _) in s_elementTypes)
        {
            Assert.That(descriptor.IsVectorPerElementMask(type), Is.False);
        }

        descriptor.SetIsVectorPerElementMask(storedType);

        var flags = GetFlags(descriptor);
        Assert.That(flags & Marker, Is.EqualTo(Marker));
        Assert.That((flags & ElementSizeMask) >> ElementSizeShift, Is.EqualTo(sizeLog2));
        Assert.That(flags & ~(Marker | ElementSizeMask), Is.EqualTo(originalFlags));
        foreach (var (type, querySizeLog2) in s_elementTypes)
        {
            Assert.That(descriptor.IsVectorPerElementMask(type), Is.EqualTo(querySizeLog2 <= sizeLog2),
                $"Stored {storedType}, queried {type}");
        }
    }

    [TestCase(TYP_BYTE, TYP_UBYTE, 0)]
    [TestCase(TYP_SHORT, TYP_BYTE, 1)]
    [TestCase(TYP_INT, TYP_SHORT, 2)]
    [TestCase(TYP_DOUBLE, TYP_UINT, 3)]
    public static void RepeatedAnnotationsRetainTheMonotonicTwoBitMaximum(var_types largerType, var_types smallerType, int sizeLog2)
    {
        LclVarDsc descriptor = default;
        descriptor.SetIsVectorPerElementMask(smallerType);
        descriptor.SetIsVectorPerElementMask(largerType);
        var largerFlags = GetFlags(descriptor);

        descriptor.SetIsVectorPerElementMask(smallerType);
        descriptor.SetIsVectorPerElementMask(largerType);

        Assert.That(GetFlags(descriptor), Is.EqualTo(largerFlags));
        Assert.That((largerFlags & ElementSizeMask) >> ElementSizeShift, Is.EqualTo(sizeLog2));
        Assert.That(descriptor.IsVectorPerElementMask(largerType), Is.True);
        Assert.That(descriptor.IsVectorPerElementMask(smallerType), Is.True);
    }

    [TestCase(TYP_UNDEF)]
    [TestCase(TYP_VOID)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_STRUCT)]
    [TestCase(TYP_UNKNOWN)]
    [TestCase(TYP_COUNT)]
#if FEATURE_SIMD
    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
#if TARGET_XARCH
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
#elif TARGET_ARM64
    [TestCase(TYP_SIMD)]
#endif
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_MASK)]
#endif
#endif
    public static void InvalidBaseTypesShortCircuitOnlyWithoutTheMarkerAndOtherwiseUseUnreached(var_types invalidType)
    {
        var config = JitConfig;
#if MEASURE_FATAL
        var fatalCount = s_fatalNoWayAssertBodyCount;
#if DEBUG
        var fatalArgsCount = s_fatalNoWayAssertBodyArgsCount;
#endif
#endif
        JitConfig = new JitConfigValues();
        try
        {
            LclVarDsc descriptor = new() { IsSpan = true };
            var originalFlags = GetFlags(descriptor);
            Assert.That(descriptor.IsVectorPerElementMask(invalidType), Is.False);
            Assert.That(GetFlags(descriptor), Is.EqualTo(originalFlags));

            var setterException = Assert.Throws<FatalJitException>(() => descriptor.SetIsVectorPerElementMask(invalidType));
            Assert.That(setterException?.Result, Is.EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
            Assert.That(GetFlags(descriptor), Is.EqualTo(originalFlags));

            descriptor.SetIsVectorPerElementMask(TYP_INT);
            var markedFlags = GetFlags(descriptor);
            var queryException = Assert.Throws<FatalJitException>(() => descriptor.IsVectorPerElementMask(invalidType));
            Assert.That(queryException?.Result, Is.EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
            Assert.That(GetFlags(descriptor), Is.EqualTo(markedFlags));
#if MEASURE_FATAL
            Assert.That(s_fatalNoWayAssertBodyCount, Is.EqualTo(unchecked(fatalCount + 2)));
#if DEBUG
            Assert.That(s_fatalNoWayAssertBodyArgsCount, Is.EqualTo(unchecked(fatalArgsCount + 2)));
#endif
#endif
        }
        finally
        {
            JitConfig = config;
#if MEASURE_FATAL
            s_fatalNoWayAssertBodyCount = fatalCount;
#if DEBUG
            s_fatalNoWayAssertBodyArgsCount = fatalArgsCount;
#endif
#endif
        }
    }

    private static long GetFlags(LclVarDsc descriptor)
    {
        return Convert.ToInt64(s_flags.GetValue(descriptor), CultureInfo.InvariantCulture);
    }
}
#endif
