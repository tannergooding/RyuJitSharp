// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class GenTreePutArgStk : GenTreeUnOp
{
#if DEBUG || UNIX_X86_ABI
    private GenTreeCall? _call;
#endif

    private int _argOffset;
    private int _stackByteSize;

#if UNIX_X86_ABI
    private int _argPadding;
#endif

#if FEATURE_FASTTAILCALL
    private bool _putInIncomingArgArea;
#endif

    private Kind _kind;

#if TARGET_XARCH
    private byte _argLoadSizeDelta;
#endif

    public GenTreePutArgStk(var_types type, GenTree op1, GenTreeCall? call, int argOffset, int stackByteSize, bool putInIncomingArgArea)
        : base(GT_PUTARG_STK, type, op1)
    {
#if DEBUG || UNIX_X86_ABI
        _call = call;
#endif

        _argOffset = argOffset;
        _stackByteSize = stackByteSize;

#if FEATURE_FASTTAILCALL
        _putInIncomingArgArea = putInIncomingArgArea;
#else
        assert(!putInIncomingArgArea);
#endif

#if TARGET_XARCH
        _argLoadSizeDelta = byte.MaxValue;
#endif
    }

#if TARGET_XARCH
    /// <summary>Get or set the optimal number of bytes to load for this argument.</summary>
    public int ArgLoadSize
    {
        get
        {
            assert(Debugger.IsAttached || (_argLoadSizeDelta is not byte.MaxValue));
            return _argLoadSizeDelta;
        }

        set
        {
            // On XARCH, it is profitable to use wider loads when our source is a local
            // variable. To not duplicate the logic between lowering, LSRA and codegen,
            // we do the legality check once, in lowering, and save the result here, as
            // a negative delta relative to the size of the argument with padding.

            var stackByteSize = _stackByteSize;
            assert(roundUp(value, TARGET_POINTER_SIZE) == stackByteSize);
            _argLoadSizeDelta = (byte)(stackByteSize - value);
        }
    }
#endif

    public int ArgOffset => _argOffset;

#if UNIX_X86_ABI
    public int ArgPadding
    {
        get
        {
            return _argPadding;
        }

        set
        {
            _argPadding = value;
        }
    }
#endif

#if DEBUG || UNIX_X86_ABI
    public GenTreeCall? Call => _call;
#else
    public GenTreeCall? Call => null;
#endif

    public new GenTree Data => Op1;

    public bool IsPush => _kind == Kind.Push;

    // This is needed because such values are re-typed to simd16, and the type of PutArgStk is VOID.
    public bool IsSimd12 => varTypeIsSimd(Data.Type) && (StackByteSize is 12);

    /// <summary>Whether this arg needs to be placed in incoming arg area.</summary>
    /// <remarks>
    ///   <para>By default this is false and will be placed in out-going arg area.</para>
    ///   <para>Fast tail calls set this to true.</para>
    /// </remarks>
#if FEATURE_FASTTAILCALL
    public bool PutInIncomingArgArea => _putInIncomingArgArea;
#else
    public bool PutInIncomingArgArea => false;
#endif

    public int StackByteSize => _stackByteSize;

}
