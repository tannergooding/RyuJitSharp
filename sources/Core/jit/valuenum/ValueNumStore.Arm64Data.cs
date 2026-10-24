// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private static class Arm64Data
    {
        private sealed class SimdScalableValueMapData
        {
            internal Dictionary<simdscalable_t, ValueNum>? CnsMap;
        }

        private sealed class SimdMaskValueMapData
        {
            internal Dictionary<simdmaskvalue_t, ValueNum>? CnsMap;
        }

        private static ConditionalWeakTable<ValueNumStore, SimdScalableValueMapData>? s_simdScalableValueMaps;
        private static ConditionalWeakTable<ValueNumStore, SimdMaskValueMapData>? s_simdMaskValueMaps;

        internal static Dictionary<simdscalable_t, ValueNum> GetSimdScalableCnsMap(ValueNumStore store)
        {
            var instances = LazyStatic.GetOrCreate(ref s_simdScalableValueMaps);
            var data = instances.GetValue(store, static _ => new SimdScalableValueMapData());

            return data.CnsMap ??= [];
        }

        internal static ValueNum VNForSimdMaskCon(ValueNumStore store, simdmaskvalue_t value)
        {
            var instances = LazyStatic.GetOrCreate(ref s_simdMaskValueMaps);
            var data = instances.GetValue(store, static _ => new SimdMaskValueMapData());
            var constants = data.CnsMap ??= [];

            return store.VnForConst(value, value, constants, TYP_MASK);
        }

        internal static bool IsScalableSimdMask(ValueNumStore store, ValueNum vn)
            => GetSimdMaskValue(store, vn).IsScalable;

        internal static simdmask_t GetFixedSimdMask(ValueNumStore store, ValueNum vn)
        {
            var value = GetSimdMaskValue(store, vn);
            noway_assert(!value.IsScalable);
            return value.Fixed;
        }

        internal static simdmaskscalable_t GetScalableSimdMask(ValueNumStore store, ValueNum vn)
        {
            var value = GetSimdMaskValue(store, vn);
            noway_assert(value.IsScalable);
            return value.Scalable;
        }

        internal static bool TryGetScalableSimdMask(
            ValueNumStore store,
            ValueNum vn,
            out simdmaskscalable_t scalableValue)
        {
            var value = GetSimdMaskValue(store, vn);
            scalableValue = value.Scalable;
            return value.IsScalable;
        }

        private static simdmaskvalue_t GetSimdMaskValue(ValueNumStore store, ValueNum vn)
            => store.GetVectorConstant<simdmaskvalue_t>(vn, TYP_MASK);
    }
}
