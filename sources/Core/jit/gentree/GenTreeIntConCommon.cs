// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public abstract class GenTreeIntConCommon : GenTree
{
    protected Value _value;

    protected GenTreeIntConCommon(genTreeOps oper, var_types type)
        : base(oper, type)
    {
        assert(oper.IsIntegralConst);
    }

#if TARGET_64BIT
    public bool FitsInI32 => Globals.FitsInI32(_value.Icon);
#else
    public bool FitsInI32 => Oper is GT_CNS_INT || Globals.FitsInI32(_value.Lcon);
#endif

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
#if TARGET_32BIT
            if (Oper is GT_CNS_LNG)
            {
                return _value.Lcon
            }
#endif

            assert(Debugger.IsAttached || Oper.IsCnsIntOrI);
            return _value.Icon;
        }

        set
        {
#if TARGET_32BIT
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

    public ulong UnsignedIntegralValue
    {
        get
        {
            var mask = ulong.MaxValue >> (64 - (Type.Size * 8));
            return unchecked((ulong)(IntegralValue)) & mask;
        }
    }

    public long LngValue
    {
        get
        {
#if TARGET_32BIT
            assert(Debugger.IsAttached || Oper.IsLong);
            return _value.Lcon;
#else
            assert(Debugger.IsAttached || Oper.IsCnsIntOrI);
            return _value.Icon;
#endif
        }

        set
        {
#if TARGET_32BIT
            assert(Oper.IsLong);
            _value.Lcon = value;
#else
            assert(Oper.IsCnsIntOrI);
            _value.Icon = (nint)(value);
#endif
        }
    }

    /// <summary>can this immediate value be folded for op?</summary>
    /// <param name="comp">Compiler instance</param>
    /// <param name="op">Tree operator</param>
    /// <returns>True if this immediate value can be folded for op; false otherwise.</returns>
    /// <remarks>In general, immediate values that need relocations can't be folded. There are cases where we do want to allow folding of handle comparisons (e.g., typeof(T) == typeof(int)).</remarks>
    public bool ImmedValCanBeFolded(Compiler comp, genTreeOps op) => !ImmedValNeedsReloc(comp) || (op is GT_EQ or GT_NE);

    /// <summary>does this immediate value needs recording a relocation with the VM?</summary>
    /// <param name="comp">Compiler instance</param>
    /// <returns>True if this immediate value requires us to record a relocation for it; false otherwise.</returns>
    public bool ImmedValNeedsReloc(Compiler comp) => comp.opts.compReloc && IsIconHandle();

    public new bool IsIntegralConst(nint value)
    {
#if TARGET_32BIT
        if (Oper.IsCnsIntOrI)
        {
            return _value.Icon == value;
        }
        return _value.Lcon == value;
#else
        return _value.Icon == value;
#endif
    }

    protected struct Value
    {
        public long Lcon;

        // This is the GT_CNS_INT struct definition.
        // It's used to hold for both int constants and pointer handle constants.
        // For the 64-bit targets we will only use GT_CNS_INT as it used to represent all the possible sizes
        // For the 32-bit targets we use a GT_CNS_LNG to hold a 64-bit integer constant and GT_CNS_INT for all others.
        // In the future when we retarget the JIT for x86 we should consider eliminating GT_CNS_LNG
        [UnscopedRef]
        public ref nint Icon => ref Unsafe.As<long, nint>(ref Lcon);
    }
}
