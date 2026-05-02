// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public abstract class GenTreeIntConCommon : GenTree
{
    protected _Value_e__Union _value;

    protected GenTreeIntConCommon(genTreeOps oper, var_types type)
        : base(oper, type)
    {
    }

    public nint IconValue
    {
        get
        {
            assert(Debugger.IsAttached || Oper.IsCnsIntOrI);
            return _value.Icon;
        }

        set
        {
            assert(Oper.IsCnsIntOrI);
            _value.Icon = value;
        }
    }

    public long IntegralValue
    {
        get
        {
#if !TARGET_64BIT
            if (OperIs(GT_CNS_LNG))
            {
                return _value.Lcon
            }
#endif

            assert(Debugger.IsAttached || Oper.IsCnsIntOrI);
            return _value.Icon;
        }

        set
        {
#if !TARGET_64BIT
            if (OperIs(GT_CNS_LNG))
            {
                _value.Lcon = value;
                return;
            }

            assert((nint)(value) == value));
#endif

            assert(Oper.IsCnsIntOrI);
            _value.Icon = (nint)(value);
        }
    }

    public long LngValue
    {
        get
        {
#if !TARGET_64BIT
            assert(Debugger.IsAttached || Oper.IsLong);
            return _value.Lcon;
#else
            assert(Debugger.IsAttached || Oper.IsCnsIntOrI);
            return _value.Icon;
#endif
        }

        set
        {
#if !TARGET_64BIT
            assert(Oper.IsLong);
            _value.Lcon = value;
#else
            assert(Oper.IsCnsIntOrI);
            _value.Icon = (nint)(value);
#endif
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    protected struct _Value_e__Union
    {
        // This is the GT_CNS_INT struct definition.
        // It's used to hold for both int constants and pointer handle constants.
        // For the 64-bit targets we will only use GT_CNS_INT as it used to represent all the possible sizes
        // For the 32-bit targets we use a GT_CNS_LNG to hold a 64-bit integer constant and GT_CNS_INT for all others.
        // In the future when we retarget the JIT for x86 we should consider eliminating GT_CNS_LNG
        [FieldOffset(0)]
        public nint Icon;

        [FieldOffset(0)]
        public long Lcon;
    }
}
