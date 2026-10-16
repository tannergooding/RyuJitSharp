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
        public InlinePolicy? Policy;

        public int TreeId;

        public BitArray? ILInstsSet;
    }

    private static class DebugDataStorage
    {
        private static readonly ConditionalWeakTable<InlineContext, DebugData> s_data = [];

        public static DebugData Get(InlineContext context)
        {
            return s_data.GetValue(context, static _ => new DebugData());
        }
    }

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("Inline context diagnostic state is only available in Debug builds.");
        }

        return DebugDataStorage.Get(this);
    }
}
