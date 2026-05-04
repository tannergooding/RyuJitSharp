// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

/// <summary>Indir is just an op, no additional data, but some additional abstractions</summary>
public class GenTreeIndir : GenTreeOp
{
    public GenTreeIndir(genTreeOps oper, var_types type, GenTree addr, GenTree? data)
        : base(oper, type, addr, data)
    {
    }

    /// <summary>The address for the indirection.</summary>
    public GenTree Addr
    {
        get
        {
            assert(Op1 is not null);
            assert(varTypeIsI(Op1.Type));
            return Op1;
        }

        set
        {
            assert(varTypeIsI(value.Type));
            Op1 = value;
        }
    }

#nullable disable
    public ref GenTree AddrRef => ref Op1Ref;
#nullable restore

    public GenTree? Base
    {
        get
        {
            var addr = Addr;

            if (IsIndirAddrMode)
            {
                var result = addr.AsAddrMode().Base;

                if (result is not null)
                {
                    result = result.EffectiveVal;
                }
                return result;
            }
            else
            {
                // TODO: why do we return 'addr' here, but we return 'null' in the equivalent Index() case?
                return addr;
            }
        }
    }

    public GenTree? Data
    {
        get
        {
            assert(Debugger.IsAttached || (Oper is GT_STOREIND) || Oper.IsStoreBlk || Oper.IsAtomic);
            return Op2;
        }

        set
        {
            assert(Debugger.IsAttached || (Oper is GT_STOREIND) || Oper.IsStoreBlk || Oper.IsAtomic);
            Op2 = value;
        }
    }

#nullable disable
    public ref GenTree DataRef => ref Op2Ref;
#nullable restore

    [MemberNotNullWhen(true, nameof(Base))]
    public bool HasBase => Base is not null;

    [MemberNotNullWhen(true, nameof(Index))]
    public bool HasIndex => Index is not null;

    public GenTree? Index
    {
        get
        {
            if (IsIndirAddrMode)
            {
                var result = Addr.AsAddrMode().Index;

                if (result is not null)
                {
                    result = result.EffectiveVal;
                }

                return result;
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>True if this indirection is invariant.</summary>
    public bool IsInvariantLoad
    {
        get
        {
            var isInvariant = ((Flags & GTF_IND_INVARIANT) is not 0);
            assert(Debugger.IsAttached || !isInvariant || Oper.IsLoad);
            return isInvariant;
        }
    }

    /// <summary>True if this indirection is an unaligned memory operation.</summary>
    public bool IsUnaligned => (Flags & GTF_IND_UNALIGNED) is not 0;

    /// <summary>True if this indirection is a volatile memory operation.</summary>
    public bool IsVolatile => (Flags & GTF_IND_VOLATILE) is not 0;

    public nint Offset
    {
        get
        {
            var addr = Addr;

            if (IsIndirAddrMode)
            {
                return addr.AsAddrMode().Offset;
            }
            else if (addr.Oper.IsCnsIntOrI && addr.IsContained)
            {
                return addr.AsIntConCommon().IconValue;
            }
            else
            {
                return 0;
            }
        }
    }

    public byte Scale
    {
        get
        {
            if (HasIndex)
            {
                return Addr.AsAddrMode().Scale;
            }
            else
            {
                return 1;
            }
        }
    }

    public uint Size => ValueSize.ExactSize;

    public ValueSize ValueSize
    {
        get
        {
            assert(Debugger.IsAttached || Oper.IsTrueIndir || Oper.IsBlk);
            return Oper.IsBlk ? new ValueSize(AsBlk().Size) : ValueSize.FromJitType(Type);
        }
    }

    public bool IsAddressNotOnHeap(Compiler compiler)
    {
        var oper = Oper;
        var operIsStoreBlk = oper.IsStoreBlk;

        if ((operIsStoreBlk || (oper is GT_STOREIND)) && ((Flags & GTF_IND_TGT_NOT_HEAP) is not 0))
        {
            return true;
        }

        var @base = Base;

        if ((@base is not null) && !compiler.fgAddrCouldBeHeap(@base.SkipCopyOrReload))
        {
            return true;
        }

        if (operIsStoreBlk && AsBlk().Layout.IsStackOnly(compiler))
        {
            return true;
        }

        return false;
    }
}
