// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
#if DEBUG
using System.Diagnostics;
#endif

namespace RyuJitSharp;

// Managed arrays provide the compiler-lifetime storage formerly supplied by CompAllocator.
#if DEBUG
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
[DebuggerTypeProxy(typeof(JitExpandArrayDebuggerProxy<>))]
#endif
internal class JitExpandArray<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>
{
#if DEBUG
    private string DebuggerDisplayText => m_size is 0 ? "Empty" : $"size={m_size}";

    internal uint DebuggerSize => m_size;
#endif

    // Native value-initialization invokes explicit value-type constructors; Array.Clear does not.
    private static readonly bool s_hasParameterlessValueTypeConstructor =
        typeof(T).IsValueType && typeof(T).GetConstructor(Type.EmptyTypes) is not null;

    protected T[]? m_members;
    protected uint m_size;
    protected uint m_minSize;

    internal JitExpandArray(uint minSize = 1)
    {
        assert(minSize > 0);
        m_minSize = minSize;
    }

    protected T[] Members => m_members
        ?? throw new InvalidOperationException("The expandable array has not been allocated.");

    private void InitializeRange(uint low, uint high)
    {
        assert(m_members is not null);
        assert((low <= high) && (high <= m_size));
        if (low != high)
        {
            var members = Members;
            if (s_hasParameterlessValueTypeConstructor)
            {
                for (var index = low; index < high; index++)
                {
                    members[checked((int)index)] = Activator.CreateInstance<T>();
                }
            }
            else
            {
                Array.Clear(members, checked((int)low), checked((int)(high - low)));
            }
        }
    }

    internal void Init(uint minSize = 1)
    {
        m_members = null;
        m_size = 0;
        m_minSize = minSize;
    }

    internal void Reset(uint minSize)
    {
        m_minSize = minSize;
        Reset();
    }

    internal void Reset()
    {
        if (m_minSize > m_size)
        {
            EnsureCoversInd(m_minSize - 1);
        }

        InitializeRange(0, m_size);
    }

    internal T Get(uint index)
    {
        EnsureCoversInd(index);
        return Members[checked((int)index)];
    }

    internal ref T GetRef(uint index)
    {
        EnsureCoversInd(index);
        return ref Members[checked((int)index)];
    }

    internal void Set(uint index, T value)
    {
        EnsureCoversInd(index);
        Members[checked((int)index)] = value;
    }

    internal ref T this[uint index] => ref GetRef(index);

    protected void EnsureCoversInd(uint index)
    {
        if (index >= m_size)
        {
            var oldSize = m_size;
            var newSize = checked(index + 1);
            var doubledSize = unchecked(m_size * 2);
            if (m_minSize > newSize)
            {
                newSize = m_minSize;
            }
            if (doubledSize > newSize)
            {
                newSize = doubledSize;
            }

            var members = new T[checked((int)newSize)];
            if (m_members is not null)
            {
                Array.Copy(m_members, members, checked((int)oldSize));
            }

            m_members = members;
            m_size = newSize;
            InitializeRange(oldSize, m_size);
        }
    }
}

#if DEBUG
[DebuggerDisplay("{DebuggerStackDisplayText,nq}")]
[DebuggerTypeProxy(typeof(JitExpandArrayStackDebuggerProxy<>))]
#endif
internal class JitExpandArrayStack<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : JitExpandArray<T>
{
    private uint _used;

#if DEBUG
    private string DebuggerStackDisplayText => m_size is 0 ? "Empty" : $"size={m_size} used={_used}";

    internal uint DebuggerUsed => _used;
#endif

    internal JitExpandArrayStack(uint minSize = 1)
        : base(minSize)
    {
    }

    internal new ref T GetRef(uint index)
    {
#pragma warning disable IDE0007 // Explicit ref type avoids nullable inference for unconstrained T.
        ref T item = ref base.GetRef(index);
#pragma warning restore IDE0007
        _used = Math.Max(unchecked(index + 1), _used);
        return ref item;
    }

    internal new void Set(uint index, T value)
    {
        base.Set(index, value);
        _used = Math.Max(unchecked(index + 1), _used);
    }

    internal new void Reset()
    {
        base.Reset();
        _used = 0;
    }

    internal uint Push(T value)
    {
        var index = _used;
        base.Set(index, value);
        _used = unchecked(_used + 1);
        return index;
    }

    internal T Pop()
    {
        assert(Size() > 0);
        _used = unchecked(_used - 1);
        return Members[checked((int)_used)];
    }

    internal T Top()
    {
        assert(Size() > 0);
        return Members[checked((int)(_used - 1))];
    }

    internal ref T TopRef()
    {
        assert(Size() > 0);
        return ref Members[checked((int)(_used - 1))];
    }

    internal T GetNoExpand(uint index)
    {
        assert(index < _used);
        return Members[checked((int)index)];
    }

    internal ref T GetRefNoExpand(uint index)
    {
        assert(index < _used);
        return ref Members[checked((int)index)];
    }

    internal void Remove(uint index)
    {
        assert(index < _used);
        if (index < _used - 1)
        {
            Array.Copy(Members, checked((int)(index + 1)), Members, checked((int)index),
                checked((int)(_used - index - 1)));
        }

        _used = unchecked(_used - 1);
    }

    internal uint Size() => _used;
}

#if DEBUG
internal sealed class JitExpandArrayDebuggerProxy<T>
{
    private readonly JitExpandArray<T> _array;

    public JitExpandArrayDebuggerProxy(JitExpandArray<T> array)
    {
        _array = array;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items
    {
        get
        {
            var items = new T[checked((int)_array.DebuggerSize)];
            for (var index = 0; index < items.Length; index++)
            {
                items[index] = _array.Get((uint)index);
            }

            return items;
        }
    }
}

internal sealed class JitExpandArrayStackDebuggerProxy<T>
{
    private readonly JitExpandArrayStack<T> _array;

    public JitExpandArrayStackDebuggerProxy(JitExpandArrayStack<T> array)
    {
        _array = array;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items
    {
        get
        {
            var items = new T[checked((int)_array.DebuggerUsed)];
            for (var index = 0; index < items.Length; index++)
            {
                items[index] = _array.GetNoExpand((uint)index);
            }

            return items;
        }
    }
}
#endif
