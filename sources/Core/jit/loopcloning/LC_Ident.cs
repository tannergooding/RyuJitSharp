// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.

using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public unsafe struct LC_Ident
{
    public enum IdentType
    {
        Invalid,
        Const,
        Var,
        ArrAccess,
        SpanAccess,
        Null,
        ClassHandle,
        IndirOfLocal,
        MethodAddr,
        IndirOfMethodAddrSlot,
    }

    public IdentType Type;
    public var_types LclType;
    public int Offset;
    public uint Constant;
    public int LclNum;
    public uint IndirOffs;
    public LC_Array ArrAccess;
    public LC_Span SpanAccess;
    public CORINFO_CLASS_HANDLE ClsHnd;
    public void* MethAddr;
#if DEBUG
    public CORINFO_METHOD_HANDLE TargetMethHnd;
#endif

    public readonly bool Matches(in LC_Ident that)
    {
        if (Type != that.Type)
        {
            return false;
        }
        return Type switch
        {
            IdentType.Const => Constant == that.Constant,
            IdentType.ClassHandle => ClsHnd == that.ClsHnd,
            IdentType.Var => (LclNum == that.LclNum) && (LclType == that.LclType) && (Offset == that.Offset),
            IdentType.IndirOfLocal => (LclNum == that.LclNum) && (IndirOffs == that.IndirOffs) &&
                (LclType == that.LclType),
            IdentType.ArrAccess => ArrAccess.Matches(that.ArrAccess) && (Offset == that.Offset),
            IdentType.SpanAccess => SpanAccess.Matches(that.SpanAccess),
            IdentType.Null => true,
            IdentType.MethodAddr or IdentType.IndirOfMethodAddrSlot => MethAddr == that.MethAddr,
            _ => throw new FatalJitException("Unknown loop-cloning identifier type."),
        };
    }

#if DEBUG
    public readonly void Print()
    {
        switch (Type)
        {
            case IdentType.Const:
            {
                jitprintf($"{Constant}");
                break;
            }
            case IdentType.Var:
            {
                jitprintf($"V{LclNum:D2}");
                PrintOffset();
                break;
            }
            case IdentType.IndirOfLocal:
            {
                jitprintf((IndirOffs != 0) ? $"*(V{LclNum:D2} + {IndirOffs})" : $"*V{LclNum:D2}");
                break;
            }
            case IdentType.ClassHandle:
            {
                jitprintf($"0x{(nuint)ClsHnd:x}");
                break;
            }
            case IdentType.ArrAccess:
            {
                ArrAccess.Print();
                PrintOffset();
                break;
            }
            case IdentType.SpanAccess:
            {
                SpanAccess.Print();
                break;
            }
            case IdentType.Null:
            {
                jitprintf("null");
                break;
            }
            case IdentType.MethodAddr:
            case IdentType.IndirOfMethodAddrSlot:
            {
                if (Type is IdentType.IndirOfMethodAddrSlot)
                {
                    jitprintf("[");
                }
                jitprintf($"0x{(nuint)MethAddr:x}");
                if (Type is IdentType.IndirOfMethodAddrSlot)
                {
                    jitprintf("]");
                }
                break;
            }
            default:
            {
                jitprintf("INVALID");
                break;
            }
        }
    }

    private readonly void PrintOffset()
    {
        if (Offset > 0)
        {
            jitprintf($"+{Offset}");
        }
        else if (Offset < 0)
        {
            jitprintf($"{Offset}");
        }
    }
#endif

    public readonly GenTree ToGenTree(Compiler compiler, BasicBlock block)
    {
        switch (Type)
        {
            case IdentType.Const:
            {
                assert(Constant <= int.MaxValue);
                return compiler.gtNewIconNode(TYP_INT, (nint)Constant);
            }

            case IdentType.Var:
            {
                GenTree node = compiler.gtNewLclvNode(compiler.lvaGetDesc(LclNum).Type, LclNum);
                if (Offset != 0)
                {
                    node = compiler.gtNewBinaryNode(GT_ADD, node.Type.ActualType, node, compiler.gtNewIconNode(TYP_INT, Offset));
                }
                return node;
            }

            case IdentType.ArrAccess:
            {
                GenTree node = ArrAccess.ToGenTree(compiler, block);
                if (Offset != 0)
                {
                    node = compiler.gtNewBinaryNode(GT_ADD, node.Type, node, compiler.gtNewIconNode(TYP_INT, Offset));
                }
                return node;
            }

            case IdentType.SpanAccess:
            {
                return SpanAccess.ToGenTree(compiler);
            }

            case IdentType.Null:
            {
                return compiler.gtNewIconNode(LclType, 0);
            }

            case IdentType.ClassHandle:
            {
                return compiler.gtNewIconHandleNode((nint)ClsHnd, GTF_ICON_CLASS_HDL);
            }

            case IdentType.IndirOfLocal:
            {
                GenTree addr = compiler.gtNewLclvNode(compiler.lvaGetDesc(LclNum).Type, LclNum);
                if (IndirOffs == 0)
                {
                    return compiler.gtNewMethodTableLookup(addr);
                }
                addr = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, addr,
                    compiler.gtNewIconNode(TYP_I_IMPL, (nint)IndirOffs));
                return compiler.gtNewIndir(TYP_I_IMPL, addr, GTF_IND_INVARIANT);
            }

            case IdentType.MethodAddr:
            case IdentType.IndirOfMethodAddrSlot:
            {
                var handle = compiler.gtNewIconHandleNode((nint)MethAddr, GTF_ICON_FTN_ADDR);
#if DEBUG
                handle.TargetHandle = (nint)TargetMethHnd;
#endif
                return Type is IdentType.MethodAddr ? handle :
                    compiler.gtNewIndir(TYP_I_IMPL, handle, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
            }

            default:
            {
                throw new FatalJitException("Cannot materialize an invalid loop-cloning identifier.");
            }
        }
    }

    public static LC_Ident CreateVar(int lclNum, var_types lclType, int offset = 0)
        => new() { Type = IdentType.Var, LclNum = lclNum, LclType = lclType, Offset = offset };

    public static LC_Ident CreateIndirOfLocal(int lclNum, uint offs, var_types lclType)
        => new() { Type = IdentType.IndirOfLocal, LclNum = lclNum, IndirOffs = offs, LclType = lclType };

    public static LC_Ident CreateConst(uint value)
        => new() { Type = IdentType.Const, Constant = value };

    public static LC_Ident CreateArrAccess(LC_Array arrLen, int offset = 0)
        => new() { Type = IdentType.ArrAccess, ArrAccess = arrLen, Offset = offset };

    public static LC_Ident CreateSpanAccess(LC_Span spanLen)
        => new() { Type = IdentType.SpanAccess, SpanAccess = spanLen };

    public static LC_Ident CreateNull(var_types nullType = TYP_REF)
        => new() { Type = IdentType.Null, LclType = nullType };

    public static LC_Ident CreateClassHandle(CORINFO_CLASS_HANDLE clsHnd)
        => new() { Type = IdentType.ClassHandle, ClsHnd = clsHnd };

    public static LC_Ident CreateMethodAddr(void* methAddr
#if DEBUG
        , CORINFO_METHOD_HANDLE targetMethHnd
#endif
        ) => new()
        {
            Type = IdentType.MethodAddr,
            MethAddr = methAddr,
#if DEBUG
            TargetMethHnd = targetMethHnd,
#endif
        };

    public static LC_Ident CreateIndirMethodAddrSlot(void* methAddrSlot
#if DEBUG
        , CORINFO_METHOD_HANDLE targetMethHnd
#endif
        ) => new()
        {
            Type = IdentType.IndirOfMethodAddrSlot,
            MethAddr = methAddrSlot,
#if DEBUG
            TargetMethHnd = targetMethHnd,
#endif
        };
}
