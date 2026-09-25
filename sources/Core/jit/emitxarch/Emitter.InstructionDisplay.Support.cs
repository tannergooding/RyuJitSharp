// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitDispInsAddr(byte* code)
    {
#if DEBUG
        if (_compiler?.opts.disAddr == true)
        {
            jitprintf(FMT_PTR((void*)dspPtr(code)));
        }
#endif
    }

    private void emitDispInsOffs(uint offset, bool display)
    {
        jitprintf(display ? $"{offset:X6}" : "      ");
    }

    private void emitDispFrameRef(int variable, int displacement, uint ilOffset, bool assembly)
    {
        var compiler = _compiler ?? throw new FatalJitException("Frame reference display requires an active compiler.");
        jitprintf("[");
        if (!assembly || compiler.lvaDoneFrameLayout == Compiler.NO_FRAME_LAYOUT)
        {
            jitprintf(variable < 0 ? $"TEMP_{-variable:D2}" : $"V{variable:D2}");
            if (displacement < 0)
            {
                jitprintf($"-0x{unchecked((uint)-displacement):X}");
            }
            else if (displacement > 0)
            {
                jitprintf($"+0x{displacement:X}");
            }
        }

        if (compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT)
        {
            if (!assembly)
            {
                jitprintf(" ");
            }
            var address = compiler.lvaFrameAddress(variable, out var fpBased) + displacement;
            jitprintf(fpBased ? "rbp" : "rsp");
            if (address < 0)
            {
                jitprintf($"-0x{-address:X2}");
            }
            else if (address > 0)
            {
                jitprintf($"+0x{address:X2}");
            }
        }
        jitprintf("]");

#if DEBUG
        if (variable >= 0 && compiler.opts.varNames && ilOffset != unchecked((uint)BAD_IL_OFFSET))
        {
            for (var i = 0; i < compiler.info.compVarScopesCount; i++)
            {
                ref var scope = ref compiler.info.compVarScopes[i];
                if (scope.vsdVarNum == variable && ilOffset >= unchecked((uint)scope.vsdLifeBeg)
                    && ilOffset < unchecked((uint)scope.vsdLifeEnd))
                {
                    if (scope.vsdName is not null)
                    {
                        jitprintf($"'{scope.vsdName}");
                        if (displacement != 0)
                        {
                            jitprintf($"{displacement:+0;-0}");
                        }
                        jitprintf("'");
                    }
                    break;
                }
            }
        }
#endif
    }

    private unsafe void emitDispClsVar(CORINFO_FIELD_HANDLE field, nint offset, bool reloc)
    {
        var compiler = _compiler ?? throw new FatalJitException("Static reference display requires an active compiler.");
        if (compiler.opts.disDiffable && ((offset >> 20) is not 0 and not -1))
        {
            offset = unchecked((nint)0xD1FFAB1E);
        }
        if (field == FLD_GLOBAL_FS)
        {
            jitprintf($"FS:[0x{unchecked((uint)offset):X4}]");
            return;
        }
        if (field == FLD_GLOBAL_GS)
        {
            jitprintf($"GS:[0x{unchecked((uint)offset):X4}]");
            return;
        }
        if (field == FLD_GLOBAL_DS)
        {
            jitprintf($"[0x{unchecked((uint)offset):X4}]");
            return;
        }

        var dataOffset = Compiler.eeGetJitDataOffs(field);
        jitprintf("[");
        if (reloc)
        {
            jitprintf("reloc ");
        }
        if (dataOffset >= 0)
        {
            jitprintf((dataOffset & 1) != 0 ? $"@CNS{dataOffset - 1:D2}" : $"@RWD{dataOffset:D2}");
        }
        else
        {
            jitprintf($"classVar[{FMT_PTR((void*)compiler.dspPtr(field))}]");
        }
        if (offset != 0)
        {
            jitprintf($"{offset:+0;-0}");
        }
        jitprintf("]");
#if DEBUG
        if (compiler.opts.varNames && offset < 0)
        {
            jitprintf($"'{compiler.eeGetFieldName(field, true)}{(offset == 0 ? "" : offset.ToString("+0;-0", CultureInfo.InvariantCulture))}'");
        }
#endif
    }

    private unsafe void emitDispCommentForHandle(nint handle, nint cookie, GenTreeFlags flags)
    {
        var compiler = _compiler ?? throw new FatalJitException("Handle display requires an active compiler.");
        var kind = flags & GTF_ICON_HDL_MASK;
        const string prefix = "      ;";
        if (cookie != 0)
        {
            if (kind == GTF_ICON_FTN_ADDR)
            {
                jitprintf($"{prefix} code for {compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)cookie)}");
                return;
            }
            if (kind is GTF_ICON_STATIC_HDL or GTF_ICON_STATIC_BOX_PTR)
            {
                var what = kind == GTF_ICON_STATIC_HDL ? "data" : "box";
                jitprintf($"{prefix} {what} for {compiler.eeGetFieldName((CORINFO_FIELD_HANDLE)cookie, true)}");
                return;
            }
            if (kind == GTF_ICON_STATIC_ADDR_PTR)
            {
                jitprintf($"{prefix} static base addr cell");
                return;
            }
        }
        if (handle == 0)
        {
            return;
        }

        var description = kind switch
        {
            GTF_ICON_STR_HDL => "string handle",
            GTF_ICON_CONST_PTR => "const ptr",
            GTF_ICON_GLOBAL_PTR => "global ptr",
            GTF_ICON_STATIC_HDL => "static handle",
            GTF_ICON_FTN_ADDR => "function address",
            GTF_ICON_TOKEN_HDL => "token handle",
            GTF_ICON_CLASS_HDL => compiler.eeGetClassName((CORINFO_CLASS_HANDLE)handle),
            GTF_ICON_FIELD_HDL => compiler.eeGetFieldName((CORINFO_FIELD_HANDLE)handle, true),
            GTF_ICON_METHOD_HDL => compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)handle),
#if !DEBUG
            GTF_ICON_OBJ_HDL => "frozen object handle",
#endif
            _ => null,
        };
#if DEBUG
        if (kind == GTF_ICON_OBJ_HDL)
        {
            compiler.eePrintObjectDescription(prefix, (CORINFO_OBJECT_HANDLE)handle);
            return;
        }
#endif
        if (description is not null)
        {
            jitprintf($"{prefix} {description}");
        }
    }
}
#endif
