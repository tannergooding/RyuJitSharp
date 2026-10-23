// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private static class Arm64Data
    {
        private sealed class SimdScalableValueMapData
        {
            internal Dictionary<simdscalable_t, ValueNum>? CnsMap;
        }

        private static ConditionalWeakTable<ValueNumStore, SimdScalableValueMapData>? s_simdScalableValueMaps;

        internal static Dictionary<simdscalable_t, ValueNum> GetSimdScalableCnsMap(ValueNumStore store)
        {
            var instances = LazyStatic.GetOrCreate(ref s_simdScalableValueMaps);
            var data = instances.GetValue(store, static _ => new SimdScalableValueMapData());

            return data.CnsMap ??= [];
        }
    }
}
