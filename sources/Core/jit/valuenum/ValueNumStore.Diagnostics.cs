// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNum VNForZeroObj(ClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        // Managed layouts use compiler-owned identity numbers instead of native object addresses.
        var layoutVN = VNForIntPtrCon(_compiler.typGetLayoutNum(layout));
        return VNForFunc(TYP_STRUCT, VNF_ZeroObj, layoutVN);
    }

#if DEBUG
    public unsafe void vnDump(Compiler compiler, ValueNum vn, bool isPtr = false)
    {
        jitprintf(" {");
        VNPhiDef phiDef = default;
        VNMemoryPhiDef memoryPhiDef = default;
        if (vn == NoVN)
        {
            jitprintf("NoVN");
        }
        else if (IsVNHandle(vn, GTF_ICON_FIELD_SEQ))
        {
            compiler.gtDispFieldSeq(FieldSeqVNToFieldSeq(vn), 0);
            jitprintf(" ");
        }
        else if (IsVNHandle(vn))
        {
            var value = ConstantValue<nint>(vn);
            var flags = GetHandleFlags(vn);
            jitprintf($"Hnd const: 0x{unchecked((nuint)compiler.dspPtr((void*)value)):x} {HandleKindName(flags)}");
            if (!compiler.IsAot)
            {
                switch (flags & GTF_ICON_HDL_MASK)
                {
                    case GTF_ICON_CLASS_HDL:
                    {
                        jitprintf($" {compiler.eeGetClassName((CORINFO_CLASS_HANDLE)value)}");
                        break;
                    }

                    case GTF_ICON_METHOD_HDL:
                    {
                        jitprintf($" {compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)value)}");
                        break;
                    }

                    case GTF_ICON_FIELD_HDL:
                    {
                        jitprintf($" {compiler.eeGetFieldName((CORINFO_FIELD_HANDLE)value, true)}");
                        break;
                    }
                }
            }
        }
        else if (IsVNConstant(vn))
        {
            DumpConstant(compiler, vn, isPtr);
        }
        else if (IsVNFunc(vn))
        {
            VNFuncApp funcApp = default;
            assert(GetVNFunc(vn, ref funcApp));
            switch (funcApp.Func)
            {
                case VNF_MapSelect:
                {
                    DumpMapSelect(compiler, funcApp);
                    break;
                }

                case VNF_MapStore:
                {
                    DumpMapStore(compiler, funcApp);
                    break;
                }

                case VNF_MapPhysicalStore:
                {
                    DumpMapPhysicalStore(compiler, funcApp);
                    break;
                }

                case VNF_ValWithExc:
                {
                    DumpValWithExc(compiler, funcApp);
                    break;
                }

                case VNF_MemOpaque:
                {
                    DumpMemOpaque(funcApp);
                    break;
                }

#if FEATURE_SIMD
                case VNF_SimdType:
                {
                    DumpSimdType(funcApp);
                    break;
                }
#endif

                case VNF_Cast:
                case VNF_CastOvf:
                {
                    DumpCast(compiler, vn, funcApp);
                    break;
                }

                case VNF_BitCast:
                {
                    DumpBitCast(compiler, funcApp);
                    break;
                }

                case VNF_ZeroObj:
                {
                    DumpZeroObj(compiler, funcApp);
                    break;
                }

                default:
                {
                    jitprintf($"{FuncName(funcApp.Func)}(");
                    for (var index = 0; index < funcApp.Arity; index++)
                    {
                        if (index > 0)
                        {
                            jitprintf(", ");
                        }

                        var arg = funcApp.GetArg(index);
                        jitprintf($"${arg:x}");
#if FEATURE_VN_DUMP_FUNC_ARGS
                        jitprintf("=");
                        vnDump(compiler, arg);
#endif
                    }
                    jitprintf(")");
                    break;
                }
            }
        }
        else if (GetPhiDef(vn, ref phiDef))
        {
            jitprintf($"PhiDef(V{phiDef.LclNum:D2} d:{phiDef.SsaDef}");
            foreach (var arg in phiDef.SsaArgs.Span)
            {
                jitprintf($", u:{arg}");
            }
            jitprintf(")");
        }
        else if (GetMemoryPhiDef(vn, ref memoryPhiDef))
        {
            jitprintf($"MemoryPhiDef({FMT_BB(memoryPhiDef.Block.bbNum)}");
            foreach (var arg in memoryPhiDef.SsaArgs.Span)
            {
                jitprintf($", m:{arg}");
            }
            jitprintf(")");
        }
        else
        {
            jitprintf($"{vn:x}");
        }
        jitprintf("}");
    }

    private unsafe void DumpConstant(Compiler compiler, ValueNum vn, bool isPtr)
    {
        var type = TypeOfVN(vn);
        switch (type)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            case TYP_SHORT:
            case TYP_USHORT:
            case TYP_INT:
            case TYP_UINT:
            {
                var value = ConstantValue<int>(vn);
                if (isPtr)
                {
                    jitprintf($"PtrCns[0x{unchecked((uint)compiler.dspPtr((void*)(nint)value)):x}]");
                }
                else
                {
                    jitprintf((value > -1000) && (value < 1000)
                        ? $"IntCns {value}"
                        : $"IntCns 0x{unchecked((uint)value):X}");
                }
                break;
            }

            case TYP_LONG:
            case TYP_ULONG:
            {
                var value = ConstantValue<long>(vn);
                if (isPtr)
                {
                    jitprintf($"LngPtrCns: 0x{unchecked((ulong)compiler.dspPtr((void*)(nint)value)):x}");
                }
                else if ((value > -1000) && (value < 1000))
                {
                    jitprintf($"LngCns {value}");
                }
                else
                {
                    jitprintf((value & unchecked((long)0xFFFFFFFF00000000)) == 0
                        ? $"LngCns 0x{unchecked((ulong)value):X}"
                        : $"LngCns 0x{unchecked((ulong)value):x}");
                }
                break;
            }

            case TYP_FLOAT:
            {
                jitprintf($"FltCns[{formatFloat(ConstantValue<float>(vn), "F6")}]");
                break;
            }

            case TYP_DOUBLE:
            {
                jitprintf($"DblCns[{formatFloat(ConstantValue<double>(vn), "F6")}]");
                break;
            }

            case TYP_REF:
            {
                if (vn == VNForNull())
                {
                    jitprintf("null");
                }
                else if (vn == VNForVoid())
                {
                    jitprintf("void");
                }
                break;
            }

            case TYP_BYREF:
            {
                jitprintf("byrefVal");
                break;
            }

            case TYP_STRUCT:
            {
                jitprintf("structVal(zero)");
                break;
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            {
                var value = GetConstantSimd8(vn);
                jitprintf($"Simd8Cns[0x{value.u32[0]:x8}, 0x{value.u32[1]:x8}]");
                break;
            }

            case TYP_SIMD12:
            {
                var value = GetConstantSimd12(vn);
                jitprintf($"Simd12Cns[0x{value.u32[0]:x8}, 0x{value.u32[1]:x8}, 0x{value.u32[2]:x8}]");
                break;
            }

            case TYP_SIMD16:
            {
                var value = GetConstantSimd16(vn);
                jitprintf($"Simd16Cns[0x{value.u32[0]:x8}, 0x{value.u32[1]:x8}, 0x{value.u32[2]:x8}, 0x{value.u32[3]:x8}]");
                break;
            }

#if TARGET_XARCH
            case TYP_SIMD32:
            {
                var value = GetConstantSimd32(vn);
                jitprintf($"Simd32Cns[0x{value.u64[0]:x16}, 0x{value.u64[1]:x16}, " +
                    $"0x{value.u64[2]:x16}, 0x{value.u64[3]:x16}]");
                break;
            }

            case TYP_SIMD64:
            {
                var value = GetConstantSimd64(vn);
                jitprintf($"Simd64Cns[0x{value.u64[0]:x16}, 0x{value.u64[1]:x16}, " +
                    $"0x{value.u64[2]:x16}, 0x{value.u64[3]:x16}, 0x{value.u64[4]:x16}, " +
                    $"0x{value.u64[5]:x16}, 0x{value.u64[6]:x16}, 0x{value.u64[7]:x16}]");
                break;
            }
#elif TARGET_ARM64
            case TYP_SIMD:
            {
                throw new NotImplementedException("ARM64 scalable SIMD VN diagnostics require scalable VN storage.");
            }
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            case TYP_MASK:
            {
#if TARGET_ARM64
                throw new NotImplementedException("ARM64 scalable mask VN diagnostics require scalable VN storage.");
#else
                var value = GetConstantSimdMask(vn);
                jitprintf($"SimdMaskCns[0x{value.u32[0]:x8}, 0x{value.u32[1]:x8}]");
                break;
#endif
            }
#endif
#endif

            default:
            {
                throw new FatalJitException("Unexpected VN constant type.");
            }
        }
    }

    private void DumpValWithExc(Compiler compiler, VNFuncApp app)
    {
        assert(app.FuncIs(VNF_ValWithExc));
        var normal = app.GetArg(0);
        var exceptions = app.GetArg(1);
        assert(IsVNFunc(exceptions));
        VNFuncApp exceptionSequence = default;
        assert(GetVNFunc(exceptions, ref exceptionSequence));

        jitprintf("norm=");
        compiler.vnPrint(normal, 1);
        jitprintf($", exc=${exceptions:x}");
        DumpExcSequence(compiler, exceptionSequence, isHead: true);
    }

    public void vnDumpExc(Compiler compiler, ValueNum vn)
    {
        if (vn == VNForEmptyExcSet())
        {
            jitprintf("EmptyExcSet");
        }
        else
        {
            VNFuncApp app = default;
            assert(GetVNFunc(vn, ref app) && app.FuncIs(VNF_ExcSetCons));
            DumpExcSequence(compiler, app, isHead: true);
        }
    }

    private void DumpExcSequence(Compiler compiler, VNFuncApp app, bool isHead)
    {
        assert(app.FuncIs(VNF_ExcSetCons));
        var exception = app.GetArg(0);
        var tail = app.GetArg(1);
        var hasTail = tail != VNForEmptyExcSet();
        if (isHead && hasTail)
        {
            jitprintf("(");
        }

        vnDump(compiler, exception);
        if (hasTail)
        {
            jitprintf(", ");
            VNFuncApp next = default;
            assert(GetVNFunc(tail, ref next));
            DumpExcSequence(compiler, next, isHead: false);
        }

        if (isHead && hasTail)
        {
            jitprintf(")");
        }
    }

    private static void DumpMapSelect(Compiler compiler, VNFuncApp app)
    {
        assert(app.FuncIs(VNF_MapSelect));
        compiler.vnPrint(app.GetArg(0), 0);
        jitprintf("[");
        compiler.vnPrint(app.GetArg(1), 0);
        jitprintf("]");
    }

    private static void DumpMapStore(Compiler compiler, VNFuncApp app)
    {
        assert(app.FuncIs(VNF_MapStore));
        compiler.vnPrint(app.GetArg(0), 0);
        jitprintf("[");
        compiler.vnPrint(app.GetArg(1), 0);
        jitprintf(" := ");
        compiler.vnPrint(app.GetArg(2), 0);
        jitprintf("]");
        if (app.GetArg(3) != NoLoop)
        {
            jitprintf($"@L{app.GetArg(3):D2}");
        }
    }

    private void DumpMapPhysicalStore(Compiler compiler, VNFuncApp app)
    {
        compiler.vnPrint(app.GetArg(0), 0);
        DumpPhysicalSelector(app.GetArg(1));
        jitprintf(" := ");
        compiler.vnPrint(app.GetArg(2), 0);
        jitprintf("]");
    }

    private static void DumpMemOpaque(VNFuncApp app)
    {
        assert(app.FuncIs(VNF_MemOpaque));
        var loop = app.GetArg(0);
        jitprintf(loop switch
        {
            NoLoop => "MemOpaque:NotInLoop",
            UnknownLoop => "MemOpaque:Indeterminate",
            _ => $"MemOpaque:L{loop:D2}",
        });
    }

#if FEATURE_SIMD
    private void DumpSimdType(VNFuncApp app)
    {
        assert(app.FuncIs(VNF_SimdType) && (app.Arity == 2));
        var size = ConstantValue<int>(app.GetArg(0));
        var baseType = (var_types)ConstantValue<int>(app.GetArg(1));
        jitprintf($"{FuncName(app.Func)}(simd{size}, {NativeTypeName(baseType)})");
    }
#endif

    private void DumpCast(Compiler compiler, ValueNum vn, VNFuncApp app)
    {
        assert(app.FuncIs(VNF_Cast, VNF_CastOvf));
        GetCastOperFromVN(app.GetArg(1), out var castToType, out var sourceIsUnsigned);
        var fromType = TypeOfVN(app.GetArg(0));
        var resultType = TypeOfVN(vn);
        if (sourceIsUnsigned)
        {
            fromType = varTypeToUnsigned(fromType);
        }

        compiler.vnPrint(app.GetArg(0), 0);
        jitprintf(", ");
        jitprintf((resultType != castToType) && (castToType != fromType)
            ? $"{NativeTypeName(resultType)} <- {NativeTypeName(castToType)} <- {NativeTypeName(fromType)}"
            : $"{NativeTypeName(resultType)} <- {NativeTypeName(fromType)}");
    }

    private void DumpBitCast(Compiler compiler, VNFuncApp app)
    {
        var fromType = TypeOfVN(app.GetArg(0));
        var encoded = unchecked((uint)ConstantValue<int>(app.GetArg(1)));
        var toType = encoded < (uint)TYP_COUNT ? (var_types)encoded : TYP_STRUCT;
        var size = encoded < (uint)TYP_COUNT ? 0 : encoded - (uint)TYP_COUNT;
        jitprintf($"BitCast<{NativeTypeName(toType)}");
        if (toType is TYP_STRUCT)
        {
            jitprintf($"<{size}>");
        }
        jitprintf($" <- {NativeTypeName(fromType)}>(");
        compiler.vnPrint(app.GetArg(0), 0);
        jitprintf(")");
    }

    private void DumpZeroObj(Compiler compiler, VNFuncApp app)
    {
        jitprintf("ZeroObj(");
        compiler.vnPrint(app.GetArg(0), 0);
        var layoutNumber = checked((int)ConstantValue<nint>(app.GetArg(0)));
        jitprintf($": {compiler.typGetLayoutByNum(layoutNumber).ClassName})");
    }

    private static string NativeTypeName(var_types type) => type switch
    {
        TYP_UNDEF => "<UNDEF>",
        TYP_UNKNOWN => "unknown",
        _ => VNMapTypeName(type),
    };

    private static string FuncName(VNFunc func)
    {
        if (func < VNF_Boundary)
        {
            return ((genTreeOps)func).Name;
        }

        var name = func.ToString();
        return name.StartsWith("VNF_", StringComparison.Ordinal) ? name[4..] : name;
    }

    private static string HandleKindName(GenTreeFlags flags) => (flags & GTF_ICON_HDL_MASK) switch
    {
        GTF_EMPTY => "",
        GTF_ICON_SCOPE_HDL => nameof(GTF_ICON_SCOPE_HDL),
        GTF_ICON_CLASS_HDL => nameof(GTF_ICON_CLASS_HDL),
        GTF_ICON_METHOD_HDL => nameof(GTF_ICON_METHOD_HDL),
        GTF_ICON_FIELD_HDL => nameof(GTF_ICON_FIELD_HDL),
        GTF_ICON_STATIC_HDL => nameof(GTF_ICON_STATIC_HDL),
        GTF_ICON_STR_HDL => nameof(GTF_ICON_STR_HDL),
        GTF_ICON_OBJ_HDL => nameof(GTF_ICON_OBJ_HDL),
        GTF_ICON_CONST_PTR => nameof(GTF_ICON_CONST_PTR),
        GTF_ICON_GLOBAL_PTR => nameof(GTF_ICON_GLOBAL_PTR),
        GTF_ICON_VARG_HDL => nameof(GTF_ICON_VARG_HDL),
        GTF_ICON_TOKEN_HDL => nameof(GTF_ICON_TOKEN_HDL),
        GTF_ICON_TLS_HDL => nameof(GTF_ICON_TLS_HDL),
        GTF_ICON_FTN_ADDR => nameof(GTF_ICON_FTN_ADDR),
        GTF_ICON_CIDMID_HDL => nameof(GTF_ICON_CIDMID_HDL),
        GTF_ICON_BBC_PTR => nameof(GTF_ICON_BBC_PTR),
        GTF_ICON_STATIC_BOX_PTR => nameof(GTF_ICON_STATIC_BOX_PTR),
        GTF_ICON_FIELD_SEQ => nameof(GTF_ICON_FIELD_SEQ),
        GTF_ICON_STATIC_ADDR_PTR => nameof(GTF_ICON_STATIC_ADDR_PTR),
        GTF_ICON_SECREL_OFFSET => nameof(GTF_ICON_SECREL_OFFSET),
        GTF_ICON_TLSGD_OFFSET => nameof(GTF_ICON_TLSGD_OFFSET),
        _ => "ILLEGAL!",
    };
#endif
}
