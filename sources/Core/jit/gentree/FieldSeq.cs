// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class FieldSeq
{
    // GT_FIELD_ADDR nodes will be lowered into more "code-gen-able" representations, like ADD's of addresses.
    // For value numbering, we would like to preserve the aliasing information for class and static fields,
    // and so will annotate such lowered addresses with "field sequences", representing the "base" static or
    // class field and any additional struct fields. We only need to preserve the handle for the first field,
    // so any struct fields will be represented implicitly (via offsets). See also "IsFieldAddr".

    private const nuint FIELD_KIND_MASK = 0b11;

    private nuint _fieldHandleAndKind;
    private nint _offset;

    public unsafe FieldSeq(CORINFO_FIELD_HANDLE fieldHnd, nint offset, FieldKind fieldKind)
    {
        _offset = offset;

        assert(fieldHnd != NO_FIELD_HANDLE);

        var handleValue = (nuint)(fieldHnd);

        assert((handleValue & FIELD_KIND_MASK) == 0);
        _fieldHandleAndKind = handleValue | (byte)(fieldKind);

        assert((JitTls.Compiler is Compiler compiler) && (compiler.eeIsFieldStatic(fieldHnd) == IsStaticField));

        if (fieldKind == FieldKind.Instance)
        {
            // TODO: enable this assert. At the time of writing, crossgen2 had a bug where the value "getFieldOffset"
            // would return for fields with an offset unknown at compile time was incorrect (not zero).
            // assert(static_cast<ssize_t>(JitTls::GetCompiler()->info.compCompHnd->getFieldOffset(fieldHnd)) == offset);
        }
    }

    public unsafe CORINFO_FIELD_HANDLE FieldHandle => (CORINFO_FIELD_HANDLE)(_fieldHandleAndKind & ~FIELD_KIND_MASK);

    public bool IsStaticField => Kind is not FieldKind.Instance;

    public bool IsSharedStaticField => Kind is FieldKind.SharedStatic;

    public FieldKind Kind => unchecked((FieldKind)(_fieldHandleAndKind & FIELD_KIND_MASK));

    /// <summary>Retrieve "the offset" for the field this node represents.</summary>
    /// <remarks>
    ///   <para>For statics with a known (constant) address it will be the value of that address.</para>
    ///   <para>For boxed statics, it will be TARGET_POINTER_SIZE (the method table pointer size).</para>
    ///   <para>For other fields, it will be equal to the value "getFieldOffset" would return.</para> 
    /// </remarks>
    public nint Offset => _offset;
}
