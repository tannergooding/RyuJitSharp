// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RyuJitSharp;

#pragma warning disable CA2007 // The await context is part of the runtime-async behavior being exercised.
internal static class AsyncCases
{
    [RuntimeAsyncMethodGeneration(true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Suspend(Task<int> pending)
    {
        var value = 17;
        value += await pending;
        return value;
    }

    [RuntimeAsyncMethodGeneration(true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> TailAwait(Task<int> pending)
    {
        return await TailVersion(pending);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task<int> TailVersion(Task<int> pending)
    {
        return Suspend(pending);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<int> InlineTailVersion(Task<int> pending)
    {
        return Suspend(pending);
    }

    [RuntimeAsyncMethodGeneration(true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> InlinedTailAwait(Task<int> pending)
    {
        return await InlineTailVersion(pending);
    }

    [RuntimeAsyncMethodGeneration(true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Catch(Task<int> pending)
    {
        try
        {
            return await pending;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    [RuntimeAsyncMethodGeneration(true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Context(Task<int> pending, AsyncLocal<int> local)
    {
        var initial = local.Value;
        var result = await pending;
        local.Value = 19;
        return initial + result;
    }
}
#pragma warning restore CA2007

internal static class Program
{
    public static int Main()
    {
        var normalGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var normal = AsyncCases.Suspend(normalGate.Task);
        if (normal.IsCompleted)
        {
            return 1;
        }

        normalGate.SetResult(25);
        if (normal.GetAwaiter().GetResult() != 42)
        {
            return 2;
        }

        var tailGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tail = AsyncCases.TailAwait(tailGate.Task);
        if (tail.IsCompleted)
        {
            return 3;
        }

        tailGate.SetResult(14);
        if (tail.GetAwaiter().GetResult() != 31)
        {
            return 4;
        }

        var exceptionGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var caught = AsyncCases.Catch(exceptionGate.Task);
        if (caught.IsCompleted)
        {
            return 5;
        }

        exceptionGate.SetException(new InvalidOperationException("Expected exception"));
        if (caught.GetAwaiter().GetResult() != -1)
        {
            return 6;
        }

        var context = new AsyncLocal<int> { Value = 7 };
        var contextGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var flowed = AsyncCases.Context(contextGate.Task, context);
        if (flowed.IsCompleted)
        {
            return 7;
        }

        contextGate.SetResult(5);
        if (flowed.GetAwaiter().GetResult() != 12 || context.Value != 7)
        {
            return 8;
        }

        var inlinedTailGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inlinedTail = AsyncCases.InlinedTailAwait(inlinedTailGate.Task);
        if (inlinedTail.IsCompleted)
        {
            return 9;
        }

        inlinedTailGate.SetResult(23);
        if (inlinedTail.GetAwaiter().GetResult() != 40)
        {
            return 10;
        }

#pragma warning disable CA1303 // Fixed output identifies the verified corpus.
        Console.WriteLine("Native async corpus: suspension, tail await, exception, and context verified");
#pragma warning restore CA1303
        return 0;
    }
}
