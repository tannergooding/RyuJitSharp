// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial struct CallArgs
{
    private CallArg _head;

    private CallArg _lateHead;

#if UNIX_X86_ABI
    /// <summary>Number of stack bytes pushed before we start pushing these arguments.</summary>
    private int _argsStackSize;

    /// <summary>Stack alignment in bytes required before arguments are pushed for this call.</summary>
    /// <remarks>Computed dynamically during codegen, based on m_stkSizeBytes and the current stack level (genStackLevel) when the first stack adjustment is made for this call.</remarks>
    private int _padStkAlign;
#endif

    private Flags _flags;

    public readonly bool AreArgsComplete => (_flags & Flags.ArgsComplete) != 0;

    public readonly CallArgList Args => new CallArgList(_head);

    public readonly CallEarlyArgList EarlyArgs => new CallEarlyArgList(_head);

    /// <summary>true if we have one or more register arguments.</summary>
    public readonly bool HasRegArgs => (_flags & Flags.HasRegArgs) != 0;

    [MemberNotNullWhen(true, nameof(RetBufferArg))]
    public readonly bool HasRetBuffer => (_flags & Flags.HasRetBuffer) != 0;

    /// <summary>true if we have one or more stack arguments.</summary>
    public readonly bool HasStackArgs => (_flags & Flags.HasStackArgs) != 0;

    [MemberNotNullWhen(true, nameof(ThisArg))]
    public readonly bool HasThisPointer => (_flags & Flags.HasThisPointer) != 0;

    public readonly CallArg? Head => _head;

    public readonly bool IsAbiInformationDetermined => (_flags & Flags.AbiInformationDetermined) != 0;

    public readonly bool IsEmpty => _head is null;

#if UNIX_X86_ABI
    public bool IsStkAlignmentDone
    {
        readonly get
        {
            return (_flags & Flags.AlignmentDone) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.AlignmentDone) | (value ? Flags.AlignmentDone : Flags.None);
        }
    }
#endif

    public bool IsVarArgs
    {
        readonly get
        {
            return (_flags & Flags.IsVarArgs) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsVarArgs) | (value ? Flags.IsVarArgs : Flags.None);
        }
    }

    public readonly CallLateArgList LateArgs => new CallLateArgList(_lateHead);

    public readonly CallArg? LateHead => _lateHead;

    /// <summary>One or more arguments must be copied to a temp by EvalArgsToTemps.</summary>
    public readonly bool NeedsTemps => (_flags & Flags.NeedsTemps) != 0;

    /// <summary>Get the return buffer arg.</summary>
    /// <remarks>
    ///   <para>This is the actual (per-ABI) return buffer argument.</para>
    ///   <para>On some ABIs this argument has special treatment.</para>
    ///   <para>Notably on standard ARM64 calling convention it is passed in x8 (see `CallArgs::GetCustomRegister` for the exact conditions).</para>
    /// </remarks>
    public readonly CallArg? RetBufferArg
    {
        get
        {
            if (!HasRetBuffer)
            {
                return null;
            }

            var result = FindWellKnownArg(WellKnownArg.RetBuffer);
            assert(result is not null, "Expected to find ret buffer argument");
            return result;
        }
    }

    /// <summary> Get the this-pointer argument.</summary>
    /// <remarks>This is only the managed 'this' arg; we consider the 'this' pointer for unmanaged instance calling conventions as normal (non-this) arguments.</remarks>
    public readonly CallArg? ThisArg
    {
        get
        {
            if (!HasThisPointer)
            {
                return null;
            }

            // For calls that do have 'this' pointer the loop is cheap as this is almost always the first or second argument.
            var result = FindWellKnownArg(WellKnownArg.ThisPointer);
            assert(result is not null, "Expected to find this pointer argument");
            return result;
        }
    }

    public readonly int CountUserArgs()
    {
        var result = 0;

        foreach (var arg in Args)
        {
            if (arg.IsUserArg)
            {
                result++;
            }
        }
        return result;
    }

    public readonly CallArg? FindByNode(GenTree node)
    {
        assert(node is not null);

        foreach (var arg in Args)
        {
            if ((arg.EarlyNode == node) || (arg.LateNode == node))
            {
                return arg;
            }
        }
        return null;
    }

    /// <summary>Find a specific well-known argument.</summary>
    /// <param name="arg">The type of well-known argument.</param>
    /// <returns>The found CallArg, or null if it was not found.</returns>
    /// <remarks> For the 'this' arg or the return buffer arg there are more efficient alternatives available in `ThisArg` and `RetBufferArg`.</remarks>
    public readonly CallArg? FindWellKnownArg(WellKnownArg arg)
    {
        assert(arg is not WellKnownArg.None);

        foreach (var callArg in Args)
        {
            if (callArg.WellKnownArg == arg)
            {
                return callArg;
            }
        }
        return null;
    }

    /// <summary>Get an argument with the specified index.</summary>
    /// <param name="index">The index of the argument to find.</param>
    /// <returns>The argument.</returns>
    /// <remarks>This function assumes enough arguments exist.</remarks>
    public readonly CallArg? GetArgByIndex(int index)
    {
        var cur = _head;

        for (var i = 0; i < index; i++)
        {
            assert((cur is not null), "Not enough arguments in GetArgByIndex");
            cur = cur.Next;
        }

        return cur;
    }

    public readonly int GetIndex(CallArg arg)
    {
        var i = 0;

        foreach (var entry in Args)
        {
            if (entry == arg)
            {
                return i;
            }
            i++;
        }

        NO_WAY("Could not find argument in arg list");
        return -1;
    }

    /// <summary>Get a user argument with the specified index.</summary>
    /// <param name="index">The index of the user argument to find.</param>
    /// <returns>The argument</returns>
    /// <remarks>
    ///   <para>Unlike GetArgByIndex, this function ignores non-user args like r2r cells.</para>
    ///   <para>This function assumes enough arguments exist. Also, see IsUserArg's comments</para>
    /// </remarks>
    public readonly CallArg? GetUserArgByIndex(int index)
    {
        var cur = _head;
        assert((cur is not null), "Not enough user arguments in GetUserArgByIndex");

        for (var i = 0; (i < index) || !cur.IsUserArg;)
        {
            if (cur.IsUserArg)
            {
                i++;
            }
            cur = cur.Next;
            assert((cur is not null), "Not enough user arguments in GetUserArgByIndex");
        }
        return cur;
    }

    public readonly int GetUserIndex(CallArg arg)
    {
        var i = 0;

        foreach (var entry in Args)
        {
            if (!entry.IsUserArg)
            {
                continue;
            }

            if (entry == arg)
            {
                return i;
            }
            i++;
        }

        NO_WAY("Could not find argument in arg list");
        return -1;
    }

    /// <summary>Create a new argument after another argument.</summary>
    /// <param name="after">The existing argument to insert the new argument after.</param>
    /// <param name="arg">The builder for the new arg.</param>
    /// <returns>The created representative for the argument.</returns>
    public CallArg InsertAfter(CallArg after, in NewCallArg arg)
    {
#if DEBUG
        var found = false;

        foreach (var entry in Args)
        {
            if (entry == after)
            {
                found = true;
                break;
            }
        }

        assert(found, "Could not find arg to insert after in argument list");
#endif

        return InsertAfterUnchecked(after, arg);
    }

    /// <summary>Insert an argument after 'this' if the call has a 'this' argument, or otherwise first.</summary>
    /// <param name="arg">The builder for the new arg.</param>
    /// <returns>The created representative for the argument.</returns>
    public CallArg InsertAfterThisOrFirst(in NewCallArg arg)
    {
        var thisArg = ThisArg;

        if (thisArg is not null)
        {
            return InsertAfter(thisArg, arg);
        }
        else
        {
            return PushFront(arg);
        }
    }

    /// <summary>Create a new argument after another argument, without debug checks.</summary>
    /// <param name="after">The existing argument to insert the new argument after.</param>
    /// <param name="arg">The builder for the new arg.</param>
    /// <returns>The created representative for the argument.</returns>
    public CallArg InsertAfterUnchecked(CallArg after, in NewCallArg arg)
    {
        var newArg = new CallArg(arg) {
            Next = after.Next
        };
        after.Next = newArg;

        AddedWellKnownArg(arg.WellKnownArg);
        return newArg;
    }

    /// <summary>Create a new argument at the back of the argument list.</summary>
    /// <param name="arg">The argument to add.</param>
    /// <returns>The created representative for the argument.</returns>
    public CallArg PushBack(in NewCallArg arg)
    {
        ref var slot = ref _head;

        while (slot is not null)
        {
            slot = ref slot.NextRef;
        }

        slot = new CallArg(arg);
        AddedWellKnownArg(arg.WellKnownArg);
        return slot;
    }

    /// <summary>Create a new argument at the front of the argument list.</summary>
    /// <param name="arg">The argument to add.</param>
    /// <returns>The created representative for the argument.</returns>
    public CallArg PushFront(in NewCallArg arg)
    {
        var callArg = new CallArg(arg) {
            Next = _head
        };
        _head = callArg;

        AddedWellKnownArg(arg.WellKnownArg);
        return callArg;
    }

    /// <summary>Copy all information from the specified `CallArgs`, making these argument lists equivalent. Nodes are cloned.</summary>
    /// <param name="compiler"></param>
    /// <param name="other"></param>
    internal void InternalCopyFrom(Compiler compiler, in CallArgs other)
    {
        assert((_head is null) && (_lateHead is null));

#if UNIX_X86_ABI
        // Unix x86 info related to stack alignment intentionally not copied as they depend on where the call will be inserted.
        _flags = other._flags & ~Flags.AlignmentDone;
        _argsStackSize = other._argsStackSize;
#else
        _flags = other._flags;
#endif

#nullable disable
        ref var tail = ref _head;
#nullable restore

        foreach (var arg in other.Args)
        {
            var argCopy = new CallArg(compiler, arg);
            tail = argCopy;
            tail = ref argCopy.NextRef;
        }

#nullable disable
        // Now copy late pointers. Note that these may not come in order.
        tail = ref _lateHead;
#nullable restore

        foreach (var lateArg in other.LateArgs)
        {
            var arg = _head;
            var otherArg = other._head;

            while (otherArg != lateArg)
            {
                assert((arg is not null) && (otherArg is not null));
                arg = arg.Next;
                otherArg = otherArg.Next;
            }
            assert(arg is not null);

            tail = arg;
            tail = ref arg.LateNextRef;
        }
    }

    /// <summary>Record details when a well known arg was added.</summary>
    /// <param name="arg">The type of well-known arg that was just added.</param>
    /// <remarks>This is used to improve performance of some common argument lookups.</remarks>
    private void AddedWellKnownArg(WellKnownArg arg)
    {
        switch (arg)
        {
            case WellKnownArg.ThisPointer:
            {
                _flags |= Flags.HasThisPointer;
                break;
            }

            case WellKnownArg.RetBuffer:
            {
                _flags |= Flags.HasRetBuffer;
                break;
            }

            default:
            {
                break;
            }
        }
    }
}
