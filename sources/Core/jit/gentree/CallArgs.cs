// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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

    public readonly bool HasRetBuffer => (_flags & Flags.HasRetBuffer) != 0;

    /// <summary>true if we have one or more stack arguments.</summary>
    public readonly bool HasStackArgs => (_flags & Flags.HasStackArgs) != 0;

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

        assert(false, "Could not find argument in arg list");
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

        assert(false, "Could not find argument in arg list");
        return -1;
    }
}
