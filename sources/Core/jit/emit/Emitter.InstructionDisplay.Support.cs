// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitDispCommentForHandle(nint handle, nint cookie, GenTreeFlags flags)
    {
        var compiler = _compiler ?? throw new FatalJitException("Handle display requires an active compiler.");
#if TARGET_XARCH
        const string prefix = "      ;";
#elif TARGET_WASM
        const string prefix = "      ;;";
#else
        const string prefix = "      //";
#endif
        var kind = flags & GTF_ICON_HDL_MASK;
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
