// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

internal interface IJitStdScopeAllocation
{
    void release();
}

internal sealed class JitStdArrayAllocation<T> : IJitStdScopeAllocation
{
    internal T[] array { get; private set; }

    internal JitStdArrayAllocation(int size)
    {
        array = new T[size];
    }

    public void release()
    {
        Array.Clear(array);
        array = [];
    }
}

internal sealed class JitStdAllocationScope : IDisposable
{
    private readonly List<IJitStdScopeAllocation> _allocations = [];
    private bool _isDisposed;

    internal nuint allocationCount => (nuint)_allocations.Count;

    internal T[] allocate<T>(nuint count)
    {
        throwIfDisposed();
        if (count > (nuint)Array.MaxLength)
        {
            Globals.NOMEM();
            return [];
        }

        var allocation = new JitStdArrayAllocation<T>(checked((int)count));
        register(allocation);
        return allocation.array;
    }

    internal void register(IJitStdScopeAllocation allocation)
    {
        throwIfDisposed();
        _allocations.Add(allocation);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        foreach (var allocation in _allocations)
        {
            allocation.release();
        }

        _allocations.Clear();
        _isDisposed = true;
    }

    internal void throwIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}

internal interface IJitStdElementOperations<T>
{
    void construct(ref T destination, in T value);

    void assign(ref T destination, in T value);

    void destroy(ref T element);
}

internal readonly struct JitStdAllocatorVoid
{
    internal JitStdAllocationScope scope { get; }

    internal JitStdAllocatorVoid(JitStdAllocationScope scope)
    {
        this.scope = scope;
    }

    internal static JitStdAllocatorVoid from<T>(JitStdAllocator<T> allocator)
    {
        return new JitStdAllocatorVoid(allocator.scope);
    }

    internal JitStdAllocator<T> rebind<T>()
    {
        return new JitStdAllocator<T>(scope);
    }
}

internal readonly struct JitStdAllocator<T>
{
    private readonly IJitStdElementOperations<T>? _operations;

    internal JitStdAllocationScope scope { get; }

    internal JitStdAllocator(JitStdAllocationScope scope, IJitStdElementOperations<T>? operations = null)
    {
        this.scope = scope;
        _operations = operations;
    }

    internal JitStdAllocator(JitStdAllocatorVoid allocator)
        : this(allocator.scope)
    {
    }

    internal static JitStdAllocator<T> from<TSource>(JitStdAllocator<TSource> allocator, IJitStdElementOperations<T>? operations = null)
    {
        return new JitStdAllocator<T>(allocator.scope, operations);
    }

    internal T[] allocate(nuint count)
    {
        return scope.allocate<T>(count);
    }

    internal ref T address(ref T value)
    {
        return ref value;
    }

    internal ref readonly T address_readonly(in T value)
    {
        return ref value;
    }

    internal void construct(ref T destination, in T value)
    {
        if (_operations is null)
        {
            destination = value;
        }
        else
        {
            _operations.construct(ref destination, in value);
        }
    }

    internal void assign(ref T destination, in T value)
    {
        if (_operations is null)
        {
            destination = value;
        }
        else
        {
            _operations.assign(ref destination, in value);
        }
    }

    internal void destroy(ref T element)
    {
        _operations?.destroy(ref element);
        element = default!;
    }

    internal void deallocate(T[] block, nuint count)
    {
        scope.throwIfDisposed();
    }

    internal nuint max_size() => nuint.MaxValue;

    internal JitStdAllocator<TNew> rebind<TNew>()
    {
        return new JitStdAllocator<TNew>(scope);
    }
}
