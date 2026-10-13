// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class GenTreePutArgStk : GenTreeUnOp
{
    private GenTreeCall? _call;

    private int _argOffset;
    private int _stackByteSize;

    private int _argPadding;

    private bool _putInIncomingArgArea;

    internal Kind _kind;

    private byte _argLoadSizeDelta;

    public GenTreePutArgStk(var_types type, GenTree op1, GenTreeCall? call, int argOffset, int stackByteSize, bool putInIncomingArgArea)
        : base(GT_PUTARG_STK, type, op1)
    {
#if DEBUG
        _call = call;
#else
        if (Configuration.UNIX_X86_ABI)
        {
            _call = call;
        }
#endif

        _argOffset = argOffset;
        _stackByteSize = stackByteSize;

        if (Configuration.FEATURE_FASTTAILCALL || Configuration.TARGET_WASM)
        {
            _putInIncomingArgArea = putInIncomingArgArea;
        }
        else
        {
            assert(!putInIncomingArgArea);
        }

        _argLoadSizeDelta = byte.MaxValue;
    }

    /// <summary>Get or set the optimal number of bytes to load for this argument.</summary>
    public int ArgLoadSize
    {
        get
        {
            EnsureXarchArgLoadSize();
            assert(Debugger.IsAttached || (_argLoadSizeDelta is not byte.MaxValue));
            return _stackByteSize - _argLoadSizeDelta;
        }

        set
        {
            EnsureXarchArgLoadSize();
            // On XARCH, it is profitable to use wider loads when our source is a local
            // variable. To not duplicate the logic between lowering, LSRA and codegen,
            // we do the legality check once, in lowering, and save the result here, as
            // a negative delta relative to the size of the argument with padding.

            var stackByteSize = _stackByteSize;
            assert(roundUp(value, Configuration.TARGET_POINTER_SIZE) == stackByteSize);
            _argLoadSizeDelta = (byte)(stackByteSize - value);
        }
    }

    public int ArgOffset => _argOffset;

    public int ArgPadding
    {
        get
        {
            EnsureUnixX86Abi();
            return _argPadding;
        }

        set
        {
            EnsureUnixX86Abi();
            _argPadding = value;
        }
    }

    public GenTreeCall? Call
    {
        get
        {
#if DEBUG
            return _call;
#else
            return Configuration.UNIX_X86_ABI ? _call : null;
#endif
        }
    }

    public new GenTree Data => Op1;

    public bool IsPush => _kind == Kind.Push;

    // This is needed because such values are re-typed to simd16, and the type of PutArgStk is VOID.
    public bool IsSimd12 => varTypeIsSimd(Data.Type) && (StackByteSize is 12);

    /// <summary>Whether this arg needs to be placed in incoming arg area.</summary>
    /// <remarks>
    ///   <para>By default this is false and will be placed in out-going arg area.</para>
    ///   <para>Fast tail calls set this to true.</para>
    /// </remarks>
    public bool PutInIncomingArgArea =>
        (Configuration.FEATURE_FASTTAILCALL || Configuration.TARGET_WASM) && _putInIncomingArgArea;

    public int StackByteSize => _stackByteSize;

    private static void EnsureXarchArgLoadSize()
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException("Argument load sizes are only supported on xarch targets.");
        }
    }

    private static void EnsureUnixX86Abi()
    {
        if (!Configuration.UNIX_X86_ABI)
        {
            throw new FatalJitException("Argument padding is only supported on the Unix x86 ABI.");
        }
    }
}
