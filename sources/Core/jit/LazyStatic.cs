// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Threading;

namespace RyuJitSharp;

internal static class LazyStatic
{
    internal static T GetOrCreate<T>(ref T? value)
        where T : class, new()
    {
        var instance = Volatile.Read(ref value);
        if (instance is null)
        {
            var created = new T();
            instance = Interlocked.CompareExchange(ref value, created, null) ?? created;
        }

        return instance;
    }
}
