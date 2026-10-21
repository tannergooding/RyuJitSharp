// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class InlineContext
{
    private sealed class DebugData
    {
        private static ConditionalWeakTable<InlineContext, DebugData>? s_instances;

        public InlinePolicy? Policy;

        public int TreeId;

        public BitArray? ILInstsSet;

        internal static DebugData Get(InlineContext context)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instances);
            return instances.GetValue(context, static _ => new DebugData());
        }
    }

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("Inline context diagnostic state is only available in Debug builds.");
        }

        return DebugData.Get(this);
    }
}
