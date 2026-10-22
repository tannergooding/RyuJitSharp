// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public readonly struct ContinuationMember
{
    private readonly ClassLayout? _customAwaiterLayout;
    private readonly int _inlineDepth;

    private ContinuationMember(ContinuationMemberType type, ClassLayout? layout, int inlineDepth)
    {
        Type = type;
        _customAwaiterLayout = layout;
        _inlineDepth = inlineDepth;
    }

    public ContinuationMemberType Type { get; }

    public bool IsInlineFrameMember => Type is ContinuationMemberType.InlineFrameExecutionContext
        or ContinuationMemberType.InlineFrameContinuationContext or ContinuationMemberType.InlineFrameFlags;

    public ClassLayout CustomAwaiterLayout
    {
        get
        {
            assert(Type is ContinuationMemberType.CustomAwaiterOfLayout);
            assert(_customAwaiterLayout is not null);
            return _customAwaiterLayout;
        }
    }

    public int InlineDepth
    {
        get
        {
            assert(IsInlineFrameMember);
            return _inlineDepth;
        }
    }

    public var_types GetStorageType(out ClassLayout? layout)
    {
        layout = Type is ContinuationMemberType.CustomAwaiterOfLayout ? _customAwaiterLayout : null;

        switch (Type)
        {
            case ContinuationMemberType.CustomAwaiterOfLayout:
                return TYP_STRUCT;
            case ContinuationMemberType.InlineFrameExecutionContext or ContinuationMemberType.InlineFrameContinuationContext:
                return TYP_REF;
            case ContinuationMemberType.InlineFrameFlags:
                return TYP_INT;
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
        }
    }

    public static ContinuationMember CustomAwaiterOfLayout(ClassLayout layout)
        => new(ContinuationMemberType.CustomAwaiterOfLayout, layout, 0);

    public static ContinuationMember InlineFrameExecutionContext(int inlineDepth)
        => new(ContinuationMemberType.InlineFrameExecutionContext, null, inlineDepth);

    public static ContinuationMember InlineFrameContinuationContext(int inlineDepth)
        => new(ContinuationMemberType.InlineFrameContinuationContext, null, inlineDepth);

    public static ContinuationMember InlineFrameFlags(int inlineDepth)
        => new(ContinuationMemberType.InlineFrameFlags, null, inlineDepth);

    public static bool AreCompatible(in ContinuationMember a, in ContinuationMember b)
    {
        if (a.Type != b.Type)
        {
            return false;
        }

        // Frames at one depth cannot overlap: a frame's first suspension writes
        // the value that its own post-inline IR consumes before another can enter.
        switch (a.Type)
        {
            case ContinuationMemberType.CustomAwaiterOfLayout:
                return ClassLayout.AreCompatible(a._customAwaiterLayout, b._customAwaiterLayout);
            case ContinuationMemberType.InlineFrameExecutionContext or ContinuationMemberType.InlineFrameContinuationContext or ContinuationMemberType.InlineFrameFlags:
                return a._inlineDepth == b._inlineDepth;
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
        }
    }

    [Conditional("DEBUG")]
    public void Print()
    {
        string __jitExceptionResult27;
        switch (Type)
        {
            case ContinuationMemberType.CustomAwaiterOfLayout:
                __jitExceptionResult27 = $"CustomAwaiter<{CustomAwaiterLayout.ClassName}>";
                break;
            case ContinuationMemberType.InlineFrameExecutionContext:
                __jitExceptionResult27 = $"ExecutionContext for inline depth {_inlineDepth}";
                break;
            case ContinuationMemberType.InlineFrameContinuationContext:
                __jitExceptionResult27 = $"Continuation context for inline depth {_inlineDepth}";
                break;
            case ContinuationMemberType.InlineFrameFlags:
                __jitExceptionResult27 = $"Continuation flags for inline depth {_inlineDepth}";
                break;
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                __jitExceptionResult27 = default;
                break;
        }
        var description = __jitExceptionResult27;
        jitprintf(description);
    }
}
